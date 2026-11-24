#!/usr/bin/env bash
# Executable acceptance check for the build rules (issue #3, docs/CSHARP_SETUP_TODO.md sections 1-3).
# Builds small throwaway projects inside src/ and tests/, so the root Directory.Build.* files apply,
# and checks that each rule fails the build, and that clean code still builds.
# Usage: tools/ci/check-build-rules.sh   (exit code 0 = every rule works)
set -uo pipefail

root=$(cd "$(dirname "$0")/../.." && pwd)
a="$root/src/BuildCheckA"
b="$root/src/BuildCheckB"
t="$root/tests/BuildCheck.Tests"
trap 'rm -rf "$a" "$b" "$t"' EXIT
failures=0

# write_project <dir> [extra csproj lines]
write_project() {
  rm -rf "$1"
  mkdir -p "$1"
  cat > "$1/$(basename "$1").csproj" <<EOF
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
  </PropertyGroup>
  ${2:-}
</Project>
EOF
}

# write_code <dir> <type name>   (C# source on stdin; the file is named after the type, MA0048)
write_code() {
  cat > "$1/$2.cs"
}

build() {
  dotnet build "$1" -v q -clp:NoSummary -tl:off 2>&1
}

# expect_pass <description> <dir>
expect_pass() {
  local out
  if out=$(build "$2"); then
    echo "PASS  $1"
  else
    echo "FAIL  $1: the build should succeed"
    grep -E 'error' <<<"$out" | head -5
    failures=$((failures + 1))
  fi
}

# expect_error <description> <dir> <diagnostic id>
expect_error() {
  local out status
  out=$(build "$2")
  status=$?
  if [ "$status" -ne 0 ] && grep -q "$3" <<<"$out"; then
    echo "PASS  $1"
  else
    echo "FAIL  $1: expected build error $3"
    grep -E 'error|warning' <<<"$out" | head -5
    failures=$((failures + 1))
  fi
}

clean_code() {
  write_code "$1" Calculator <<'EOF'
namespace BuildCheck;

/// <summary>Adds numbers.</summary>
public static class Calculator
{
    /// <summary>Returns the sum of two numbers.</summary>
    /// <param name="left">The first number.</param>
    /// <param name="right">The second number.</param>
    /// <returns>The sum.</returns>
    public static int Add(int left, int right)
    {
        return left + right;
    }
}
EOF
}

undocumented_code() {
  write_code "$1" Calculator <<'EOF'
namespace BuildCheck;

public static class Calculator
{
    public static int Add(int left, int right)
    {
        return left + right;
    }
}
EOF
}

# 1. Clean code builds (latest-all plus the disable list is not too noisy)
write_project "$a"
clean_code "$a"
expect_pass "clean, documented code builds" "$a"

# 2. Course rule #4: XML comments on public code
write_project "$a"
undocumented_code "$a"
expect_error "missing XML comment fails" "$a" "CS1591"

# 3. NoWarn in a .csproj has no effect
write_project "$a" "<PropertyGroup><NoWarn>CS1591</NoWarn></PropertyGroup>"
undocumented_code "$a"
expect_error "NoWarn in a .csproj is overridden" "$a" "CS1591"

# 4. TreatWarningsAsErrors can't be switched off per project
write_project "$a" "<PropertyGroup><TreatWarningsAsErrors>false</TreatWarningsAsErrors></PropertyGroup>"
undocumented_code "$a"
expect_error "TreatWarningsAsErrors=false in a .csproj is overridden" "$a" "CS1591"

# 5. XML documentation can't be switched off per project
write_project "$a" "<PropertyGroup><GenerateDocumentationFile>false</GenerateDocumentationFile></PropertyGroup>"
undocumented_code "$a"
expect_error "GenerateDocumentationFile=false in a .csproj is overridden" "$a" "CS1591"

# 5b. dotnet publish works and ships the XML documentation file (issue #64)
write_project "$a"
clean_code "$a"
if out=$(dotnet publish "$a" -c Release -o "$a/publish" -v q -clp:NoSummary -tl:off 2>&1) && [ -f "$a/publish/BuildCheckA.xml" ]; then
  echo "PASS  dotnet publish works and ships the XML documentation file"
else
  echo "FAIL  dotnet publish works and ships the XML documentation file"
  grep -E 'error' <<<"$out" | head -5
  failures=$((failures + 1))
fi

# 6. Nullable can't be switched off per project
write_project "$a" "<PropertyGroup><Nullable>disable</Nullable></PropertyGroup>"
write_code "$a" Names <<'EOF'
namespace BuildCheck;

/// <summary>Names.</summary>
public static class Names
{
    /// <summary>Returns no name.</summary>
    /// <returns>Nothing.</returns>
    public static string Get()
    {
        return null;
    }
}
EOF
expect_error "Nullable=disable in a .csproj is overridden" "$a" "CS8603"

# 7. Banned APIs (BannedSymbols.txt)
write_project "$a"
write_code "$a" Clock <<'EOF'
namespace BuildCheck;

/// <summary>Clock.</summary>
public static class Clock
{
    /// <summary>Returns the time.</summary>
    /// <returns>The time.</returns>
    public static DateTime Now()
    {
        return DateTime.Now;
    }
}
EOF
expect_error "DateTime.Now is banned" "$a" "RS0030"

# 8. Null-forgiving operator without justification (Nullable.Extended.Analyzer)
write_project "$a"
write_code "$a" Names <<'EOF'
namespace BuildCheck;

/// <summary>Names.</summary>
public static class Names
{
    /// <summary>Returns no name.</summary>
    /// <returns>Nothing.</returns>
    public static string Get()
    {
        return null!;
    }
}
EOF
expect_error "null! without justification fails" "$a" "NX000"

# 9. Sonar limits from SonarLint.xml: at most 5 parameters (Sonar's default is 7)
write_project "$a"
write_code "$a" Calculator <<'EOF'
namespace BuildCheck;

/// <summary>Adds numbers.</summary>
public static class Calculator
{
    /// <summary>Returns the sum of six numbers.</summary>
    /// <param name="a">A number.</param>
    /// <param name="b">A number.</param>
    /// <param name="c">A number.</param>
    /// <param name="d">A number.</param>
    /// <param name="e">A number.</param>
    /// <param name="f">A number.</param>
    /// <returns>The sum.</returns>
    public static int Add(int a, int b, int c, int d, int e, int f)
    {
        return a + b + c + d + e + f;
    }
}
EOF
expect_error "6 parameters fails (S107, max 5)" "$a" "S107"

# 10. Central Package Management: no versions in a .csproj
write_project "$a" '<ItemGroup><PackageReference Include="Newtonsoft.Json" Version="13.0.3" /></ItemGroup>'
clean_code "$a"
expect_error "Version= on a PackageReference fails" "$a" "NU1008"

# 11. No references between services
write_project "$b"
clean_code "$b"
write_project "$a" '<ItemGroup><ProjectReference Include="../BuildCheckB/BuildCheckB.csproj" /></ItemGroup>'
clean_code "$a"
expect_error "ProjectReference into another service fails" "$a" "FITLIFE001"
rm -rf "$b"

# 12. Test projects: no XML comments needed, underscores allowed in test names
write_project "$t"
write_code "$t" CalculatorTests <<'EOF'
namespace BuildCheck.Tests;

public static class CalculatorTests
{
    public static void Add_TwoNumbers_ReturnsSum()
    {
        // Intentionally empty: only the naming and documentation rules are checked here.
    }
}
EOF
expect_pass "test project builds without XML comments and with underscores" "$t"

echo
if [ "$failures" -eq 0 ]; then
  echo "All build rules work."
else
  echo "$failures check(s) failed."
fi
exit "$failures"

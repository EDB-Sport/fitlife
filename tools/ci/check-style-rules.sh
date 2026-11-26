#!/usr/bin/env bash
# Executable acceptance check for the code style rules (issue #12, docs/CSHARP_SETUP_TODO.md section 5).
# Builds small throwaway projects inside src/, so the root Directory.Build.* files, .editorconfig and
# .globalconfig apply, and checks that each style rule fails the build, and that clean code still builds.
# Also checks the repo-level criteria: dotnet format is clean, README and CODEOWNERS are updated.
# Usage: tools/ci/check-style-rules.sh   (exit code 0 = every style rule works)
set -uo pipefail

root=$(cd "$(dirname "$0")/../.." && pwd)
a="$root/src/StyleCheck"
trap 'rm -rf "$a"' EXIT
failures=0

write_project() {
  rm -rf "$a"
  mkdir -p "$a"
  cat > "$a/StyleCheck.csproj" <<EOF
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
  </PropertyGroup>
</Project>
EOF
}

# expect_pass <description> <file content on stdin>
expect_pass() {
  local out
  write_project
  cat > "$a/Sample.cs"
  if out=$(dotnet build "$a" -v q -clp:NoSummary -tl:off 2>&1); then
    echo "PASS  $1"
  else
    echo "FAIL  $1: the build should succeed"
    grep -E 'error' <<<"$out" | head -5
    failures=$((failures + 1))
  fi
}

# expect_error <description> <diagnostic id> <file content on stdin>
expect_error() {
  local out status
  write_project
  cat > "$a/Sample.cs"
  out=$(dotnet build "$a" -v q -clp:NoSummary -tl:off 2>&1)
  status=$?
  if [ "$status" -ne 0 ] && grep -q "error $2" <<<"$out"; then
    echo "PASS  $1"
  else
    echo "FAIL  $1: expected build error $2"
    grep -E 'error|warning' <<<"$out" | head -5
    failures=$((failures + 1))
  fi
}

# expect_text <description> <file> <pattern>
expect_text() {
  if grep -qE -- "$3" "$root/$2"; then
    echo "PASS  $1"
  else
    echo "FAIL  $1: $2 should match $3"
    failures=$((failures + 1))
  fi
}

# 1. Clean code in the agreed style builds
expect_pass "clean code in the agreed style builds" <<'EOF'
namespace StyleCheck;

/// <summary>Adds numbers.</summary>
public static class Sample
{
    private const string Prefix = "n";

    private static readonly string[] Names = [Prefix];

    /// <summary>Returns the larger of two numbers.</summary>
    /// <param name="left">The first number.</param>
    /// <param name="right">The second number.</param>
    /// <returns>The larger number.</returns>
    public static int Larger(int left, int right)
    {
        if (left > right)
        {
            return left;
        }

        return right;
    }

    /// <summary>Counts the names.</summary>
    /// <returns>The count.</returns>
    public static int Count()
    {
        var names = new List<string>(Names);
        return names.Count;
    }
}
EOF

# 2. var only when the type is apparent
expect_error "var for a built-in type fails (IDE0008)" IDE0008 <<'EOF'
namespace StyleCheck;

/// <summary>Sample.</summary>
public static class Sample
{
    /// <summary>Returns a number.</summary>
    /// <returns>A number.</returns>
    public static int Get()
    {
        var number = 5;
        return number;
    }
}
EOF

expect_error "var where the type is not apparent fails (IDE0008)" IDE0008 <<'EOF'
namespace StyleCheck;

/// <summary>Sample.</summary>
public static class Sample
{
    private static List<string> Names()
    {
        return new List<string>();
    }

    /// <summary>Returns a count.</summary>
    /// <returns>A count.</returns>
    public static int Count()
    {
        var names = Names();
        return names.Count;
    }
}
EOF

expect_error "explicit type where the type is apparent fails (IDE0007)" IDE0007 <<'EOF'
namespace StyleCheck;

/// <summary>Sample.</summary>
public static class Sample
{
    /// <summary>Returns a count.</summary>
    /// <returns>A count.</returns>
    public static int Count()
    {
        List<string> names = new List<string>();
        return names.Count;
    }
}
EOF

# 3. Braces always
expect_error "if without braces fails (IDE0011)" IDE0011 <<'EOF'
namespace StyleCheck;

/// <summary>Sample.</summary>
public static class Sample
{
    /// <summary>Returns the number, at least zero.</summary>
    /// <param name="number">A number.</param>
    /// <returns>The number, at least zero.</returns>
    public static int AtLeastZero(int number)
    {
        if (number < 0)
            return 0;

        return number;
    }
}
EOF

# 4. Block-bodied methods
expect_error "expression-bodied method fails (IDE0022)" IDE0022 <<'EOF'
namespace StyleCheck;

/// <summary>Sample.</summary>
public static class Sample
{
    /// <summary>Returns the sum.</summary>
    /// <param name="left">The first number.</param>
    /// <param name="right">The second number.</param>
    /// <returns>The sum.</returns>
    public static int Add(int left, int right) => left + right;
}
EOF

# 5. File-scoped namespaces
expect_error "block-scoped namespace fails (IDE0161)" IDE0161 <<'EOF'
namespace StyleCheck
{
    /// <summary>Sample.</summary>
    public static class Sample
    {
    }
}
EOF

# 6. Naming rules
expect_error "lower-case public method fails (IDE1006)" IDE1006 <<'EOF'
namespace StyleCheck;

/// <summary>Sample.</summary>
public static class Sample
{
    /// <summary>Does nothing.</summary>
    public static void doNothing()
    {
        // Intentionally empty: only the naming rule is checked here.
    }
}
EOF

expect_error "private field without underscore fails (IDE1006)" IDE1006 <<'EOF'
namespace StyleCheck;

/// <summary>Sample.</summary>
public sealed class Sample
{
    private int count = 1;

    /// <summary>Returns the count.</summary>
    /// <returns>The count.</returns>
    public int Count()
    {
        return count;
    }
}
EOF

expect_error "static field in camelCase fails (IDE1006)" IDE1006 <<'EOF2'
namespace StyleCheck;

/// <summary>Sample.</summary>
public static class Sample
{
    private static int count = 1;

    /// <summary>Returns the count.</summary>
    /// <returns>The count.</returns>
    public static int Count()
    {
        return count;
    }
}
EOF2

expect_error "interface without I prefix fails (IDE1006)" IDE1006 <<'EOF'
namespace StyleCheck;

/// <summary>Sample.</summary>
public interface Sample
{
    /// <summary>Does nothing.</summary>
    void Run();
}
EOF

# 6b. The same rules in a .razor @code block (#397): Razor's C# output counts as hand-written (.editorconfig)
write_razor_project() {
  rm -rf "$a"
  mkdir -p "$a"
  cat > "$a/StyleCheck.csproj" <<EOF
<Project Sdk="Microsoft.NET.Sdk.Razor">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
  </PropertyGroup>
  <ItemGroup>
    <FrameworkReference Include="Microsoft.AspNetCore.App" />
  </ItemGroup>
</Project>
EOF
  # The SDK finds .editorconfig files from the folders of the C# files, so a project needs one, as every real one has
  cat > "$a/Names.cs" <<'EOF'
namespace StyleCheck;

/// <summary>A name for the sample component.</summary>
public static class Names
{
    /// <summary>Gets the name.</summary>
    public static string Sample { get; } = "sample";
}
EOF
}

# expect_razor <0 = builds | diagnostic id> <description> <Sample.razor content on stdin>
expect_razor() {
  local out status
  write_razor_project
  cat > "$a/Sample.razor"
  out=$(dotnet build "$a" -v q -clp:NoSummary -tl:off 2>&1)
  status=$?
  if [ "$1" = 0 ] && [ "$status" -eq 0 ]; then
    echo "PASS  $2"
  elif [ "$1" != 0 ] && [ "$status" -ne 0 ] && grep -q "Sample.razor.*error $1" <<<"$out"; then
    echo "PASS  $2"
  else
    echo "FAIL  $2 (wanted ${1/#0/a clean build})"
    grep -E 'error|warning' <<<"$out" | head -5
    failures=$((failures + 1))
  fi
}

expect_razor 0 "a .razor component in the agreed style builds (the generator's own #nullable/#pragma lines aren't TEAM004)" <<'EOF'
<p>@Text(1)</p>

@code {
    private static string Text(int number)
    {
        return number.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }
}
EOF

expect_razor IDE0022 "an expression-bodied method in a .razor @code block fails (IDE0022)" <<'EOF'
<p>@Text(1)</p>

@code {
    private static string Text(int number) => number.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
EOF

# 6c. A .globalconfig above the checkout doesn't switch the rules off (#434). In a worktree inside the main checkout,
# the SDK finds both .globalconfig files; every key they share is then unset, IDE0022 included. The project below
# adds such a file by hand, so the case works wherever this checkout sits.
outer=$(mktemp -d)
cp "$root/.globalconfig" "$outer/.globalconfig"
# Git Bash on Windows: MSBuild needs the Windows form of the temp path
outer_for_msbuild=$(cygpath -m "$outer" 2>/dev/null || echo "$outer")
rm -rf "$a"
mkdir -p "$a"
cat > "$a/StyleCheck.csproj" <<EOF
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
  </PropertyGroup>
  <ItemGroup>
    <EditorConfigFiles Include="$outer_for_msbuild/.globalconfig" />
  </ItemGroup>
</Project>
EOF
cat > "$a/Sample.cs" <<'EOF'
namespace StyleCheck;

/// <summary>Sample.</summary>
public static class Sample
{
    /// <summary>Doubles a number.</summary>
    /// <param name="number">The number.</param>
    /// <returns>Twice the number.</returns>
    public static int Twice(int number) => number * 2;
}
EOF
out=$(dotnet build "$a" -v q -clp:NoSummary -tl:off 2>&1)
if grep -q "error IDE0022" <<<"$out" && ! grep -q "MultipleGlobalAnalyzerKeys" <<<"$out"; then
  echo "PASS  a .globalconfig outside the repo is ignored, so IDE0022 still fails the build (#434)"
else
  echo "FAIL  a .globalconfig outside the repo is ignored, so IDE0022 still fails the build (#434)"
  grep -E 'error|MultipleGlobalAnalyzerKeys' <<<"$out" | head -3
  failures=$((failures + 1))
fi
rm -rf "$outer"

# 7. Repo-level criteria
expect_text ".editorconfig has an empty Blazor section" .editorconfig '^\[src/\*\.Web/\*\*\.cs\]'
expect_text ".editorconfig is code-owned" .github/CODEOWNERS '^/\.editorconfig[[:space:]]+@EDB-Sport/edb-team'
expect_text "README names the format fixer" README.md 'dotnet format style --diagnostics'
expect_text "README names the analyzer fixer" README.md 'dotnet format analyzers --diagnostics'

rm -rf "$a"
if (cd "$root" && dotnet format --verify-no-changes --no-restore >/dev/null 2>&1); then
  echo "PASS  dotnet format --verify-no-changes passes"
else
  echo "FAIL  dotnet format --verify-no-changes should pass"
  failures=$((failures + 1))
fi

echo
if [ "$failures" -eq 0 ]; then
  echo "All style rules work."
else
  echo "$failures check(s) failed."
fi
exit "$failures"

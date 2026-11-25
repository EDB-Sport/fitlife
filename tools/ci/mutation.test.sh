#!/usr/bin/env bash
# Tests for tools/ci/mutation.sh (issue #53). Each case copies the repo into a throwaway git repo,
# so the real Directory.Build.*, dotnet-tools.json and stryker-config.json (break threshold) apply.
# Needs the .NET SDK. Usage: tools/ci/mutation.test.sh   (exit code 0 = every case passes)
set -uo pipefail

root=$(cd "$(dirname "$0")/../.." && pwd)
script="$root/tools/ci/mutation.sh"
work=$(mktemp -d)
trap 'rm -rf "$work"' EXIT
failures=0

# A copy of the tracked files as they are now, with a library and its test project added, committed as the base.
setup_repo() {
  repo="$work/repo"
  rm -rf "$repo"
  mkdir -p "$repo"
  (cd "$root" && git ls-files -z | xargs -0 tar -cf - 2>/dev/null) | tar -xf - -C "$repo"
  cd "$repo" || exit 1
  mkdir -p src/MutationCheck tests/MutationCheck.Tests
  cat > src/MutationCheck/MutationCheck.csproj <<'EOF'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
  </PropertyGroup>
</Project>
EOF
  cat > tests/MutationCheck.Tests/MutationCheck.Tests.csproj <<'EOF'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="../../src/MutationCheck/MutationCheck.csproj" />
  </ItemGroup>
</Project>
EOF
  write_calculator ""
  write_tests ""
  dotnet sln FitLife.slnx add src/MutationCheck/MutationCheck.csproj tests/MutationCheck.Tests/MutationCheck.Tests.csproj >/dev/null
  # The lock files are committed, as in the repo. Otherwise Stryker sees changed files in a test project and tests every mutant.
  dotnet restore FitLife.slnx >/dev/null
  git init -q -b develop
  git -c user.name=test -c user.email=test@example.com add -A
  git -c user.name=test -c user.email=test@example.com commit -q -m base
  base=$(git rev-parse HEAD)
}

# write_calculator <extra members>
write_calculator() {
  cat > src/MutationCheck/Calculator.cs <<EOF
namespace MutationCheck;

/// <summary>Adds and subtracts numbers.</summary>
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
$1
}
EOF
}

# write_tests <extra tests>
write_tests() {
  cat > tests/MutationCheck.Tests/CalculatorTests.cs <<EOF
namespace MutationCheck.Tests;

public class CalculatorTests
{
    [Test]
    public void Add_TwoNumbers_ReturnsSum()
    {
        Assert.That(Calculator.Add(2, 3), Is.EqualTo(5));
    }
$1
}
EOF
}

subtract='
    /// <summary>Returns the difference of two numbers.</summary>
    /// <param name="left">The first number.</param>
    /// <param name="right">The number to subtract.</param>
    /// <returns>The difference.</returns>
    public static int Subtract(int left, int right)
    {
        return left - right;
    }'

subtract_test='
    [Test]
    public void Subtract_TwoNumbers_ReturnsDifference()
    {
        Assert.That(Calculator.Subtract(5, 3), Is.EqualTo(2));
    }'

commit() {
  git -c user.name=test -c user.email=test@example.com commit -q -am "$1"
}

# expect <pass|fail> <description> [text the output must contain]
expect() {
  local out status
  out=$(SINCE="$base" "$script" 2>&1)
  status=$?
  if { { [ "$1" = pass ] && [ "$status" -eq 0 ]; } || { [ "$1" = fail ] && [ "$status" -ne 0 ]; }; } &&
     grep -qi "${3:-}" <<<"$out"; then
    echo "PASS  $2"
  else
    echo "FAIL  $2: expected $1, got exit $status"
    tail -20 <<<"$out" | sed 's/^/      /'
    failures=$((failures + 1))
  fi
}

# 1. An untested line in the change leaves mutants alive, so the score is below the break threshold
setup_repo
write_calculator "$subtract"
commit "untested Subtract"
expect fail "a change with an untested line fails" "break threshold"

# 2. The same change with a test for it passes
setup_repo
write_calculator "$subtract"
write_tests "$subtract_test"
commit "Subtract with its test"
expect pass "a change with a test for it passes" "final mutation score is 100"

# 3. Only the changed files are mutated, and a change without C# files has nothing to mutate
setup_repo
echo "Docs only." >> README.md
commit "docs only"
expect pass "a change without C# files passes" "nothing to mutate"

# 3b. ConfigureAwait(false) -> true is an equivalent mutant: ASP.NET Core has no SynchronizationContext, so no
#     test can kill it (issue #270). A tested async method must score 100, not lose a point to it.
add_async='
    /// <summary>Returns the sum of two numbers, asynchronously.</summary>
    /// <param name="left">The first number.</param>
    /// <param name="right">The second number.</param>
    /// <returns>The sum.</returns>
    public static async Task<int> AddAsync(int left, int right)
    {
        return await Task.FromResult(left + right).ConfigureAwait(false);
    }'
add_async_test='
    [Test]
    public async Task AddAsync_TwoNumbers_ReturnsSum()
    {
        Assert.That(await Calculator.AddAsync(2, 3), Is.EqualTo(5));
    }'
setup_repo
write_calculator "$add_async"
write_tests "$add_async_test"
commit "AddAsync with its test"
expect pass "a ConfigureAwait(false) mutant isn't counted" "final mutation score is 100"

# 4. Without a base to compare with, the script refuses to run
cd "$work" || exit 1
if out=$(env -u SINCE "$script" 2>&1); then
  echo "FAIL  missing SINCE fails: it passed"
  failures=$((failures + 1))
else
  echo "PASS  missing SINCE fails"
fi

echo
if [ "$failures" -eq 0 ]; then
  echo "All mutation.sh cases pass."
else
  echo "$failures case(s) failed."
fi
exit "$failures"

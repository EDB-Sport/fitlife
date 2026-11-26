#!/usr/bin/env bash
# Tests for tools/ci/check-dockerfile-restore-order.sh (#357). Fixture Dockerfiles cover the good and the bad
# shapes; the last case runs the check on the repo's own service Dockerfiles.
# Usage: tools/ci/check-dockerfile-restore-order.test.sh   (exit code 0 = every case passes)
. "$(dirname "${BASH_SOURCE[0]}")/test-report.sh"
set -uo pipefail

here=$(cd "$(dirname "$0")" && pwd)
root=$(cd "$here/../.." && pwd)
script="$here/check-dockerfile-restore-order.sh"
work=$(mktemp -d)
trap 'rm -rf "$work"' EXIT
failures=0

# expect <exit status> <case name> <Dockerfile...>
expect() {
  local want=$1 name=$2
  shift 2
  local out status
  out=$("$script" "$@" 2>&1)
  status=$?
  if [ "$status" = "$want" ]; then
    echo "PASS  $name"
  else
    echo "FAIL  $name (exit $status, want $want)"
    echo "$out" | sed 's/^/      /'
    failures=$((failures + 1))
  fi
}

# A Dockerfile with the restore inputs copied first, then the source after the restore
good="$work/good.Dockerfile"
cat > "$good" <<'EOF'
FROM sdk AS build
WORKDIR /app
COPY global.json Directory.Build.props Directory.Build.targets Directory.Packages.props ./
COPY tools/Analyzers/Team.Analyzers/Team.Analyzers.csproj tools/Analyzers/Team.Analyzers/packages.lock.json tools/Analyzers/Team.Analyzers/
COPY libs/FitLife.Correlation/FitLife.Correlation.csproj libs/FitLife.Correlation/packages.lock.json libs/FitLife.Correlation/
COPY src/Svc/Svc.csproj src/Svc/packages.lock.json src/Svc/
RUN dotnet restore src/Svc/Svc.csproj --locked-mode
COPY .editorconfig BannedSymbols.txt GLOSSARY.md ./
COPY libs/ libs/
COPY src/Svc/ src/Svc/
RUN dotnet publish src/Svc/Svc.csproj -c Release -o /app/publish --no-restore
EOF

# The source directory is copied before the restore: the restore layer is invalidated by every code change
source_first="$work/source-first.Dockerfile"
cat > "$source_first" <<'EOF'
FROM sdk AS build
WORKDIR /app
COPY global.json Directory.Build.props ./
COPY src/Svc/ src/Svc/
RUN dotnet restore src/Svc/Svc.csproj --locked-mode
RUN dotnet publish src/Svc/Svc.csproj -c Release -o /app/publish --no-restore
EOF

# The source is copied before the restore, which is allowed when the restore has a BuildKit cache mount: the
# packages then survive a code change (MemberPortal.Web, #374). Without the mount it is the bad shape above.
cache_mount="$work/cache-mount.Dockerfile"
cat > "$cache_mount" <<'EOF'
FROM sdk AS build
WORKDIR /app
COPY global.json Directory.Build.props ./
COPY src/Svc/ src/Svc/
RUN --mount=type=cache,target=/root/.nuget/packages dotnet restore src/Svc/Svc.csproj --locked-mode
RUN dotnet publish src/Svc/Svc.csproj -c Release -o /app/publish --no-restore
EOF

# The analyzer source directory is source too, even though restore reads part of it
analyzer_dir="$work/analyzer-dir.Dockerfile"
cat > "$analyzer_dir" <<'EOF'
FROM sdk AS build
WORKDIR /app
COPY global.json Directory.Build.props ./
COPY tools/Analyzers/ tools/Analyzers/
COPY src/Svc/Svc.csproj src/Svc/packages.lock.json src/Svc/
RUN dotnet restore src/Svc/Svc.csproj --locked-mode
EOF

# A source file hidden among the restore inputs in a multi-source COPY
mixed="$work/mixed.Dockerfile"
cat > "$mixed" <<'EOF'
FROM sdk AS build
WORKDIR /app
COPY src/Svc/Svc.csproj src/Svc/Program.cs src/Svc/
RUN dotnet restore src/Svc/Svc.csproj --locked-mode
EOF

# No restore at all: nothing to check the order against
no_restore="$work/no-restore.Dockerfile"
cat > "$no_restore" <<'EOF'
FROM sdk AS build
WORKDIR /app
COPY . .
RUN dotnet publish src/Svc/Svc.csproj -c Release -o /app/publish
EOF

# A small repo for the reference cases (#420): Svc references libs/A, and A references libs/B. Every project also
# gets the analyzer project through Directory.Build.props, as in the real repo.
repo="$work/repo"
mkdir -p "$repo/src/Svc" "$repo/libs/A" "$repo/libs/B" "$repo/tools/Analyzers/Team.Analyzers"
cat > "$repo/Directory.Build.props" <<'EOF'
<Project>
  <ItemGroup Condition="'$(MSBuildProjectName)' != 'Team.Analyzers'">
    <ProjectReference Include="$(MSBuildThisFileDirectory)tools/Analyzers/Team.Analyzers/Team.Analyzers.csproj" />
  </ItemGroup>
</Project>
EOF
printf '<Project Sdk="Microsoft.NET.Sdk" />\n' > "$repo/tools/Analyzers/Team.Analyzers/Team.Analyzers.csproj"
printf '<Project Sdk="Microsoft.NET.Sdk" />\n' > "$repo/libs/B/B.csproj"
printf '<Project Sdk="Microsoft.NET.Sdk">\n  <ItemGroup>\n    <ProjectReference Include="..\\B\\B.csproj" />\n  </ItemGroup>\n</Project>\n' > "$repo/libs/A/A.csproj"
printf '<Project Sdk="Microsoft.NET.Sdk">\n  <ItemGroup>\n    <ProjectReference Include="$(RepoRoot)libs/A/A.csproj" />\n  </ItemGroup>\n</Project>\n' > "$repo/src/Svc/Svc.csproj"
restore_layer() {
  printf 'FROM sdk AS build\nWORKDIR /app\nCOPY global.json Directory.Build.props ./\n'
  printf 'COPY tools/Analyzers/Team.Analyzers/Team.Analyzers.csproj tools/Analyzers/Team.Analyzers/packages.lock.json tools/Analyzers/Team.Analyzers/\n'
  for lib in "$@"; do printf 'COPY libs/%s/%s.csproj libs/%s/packages.lock.json libs/%s/\n' "$lib" "$lib" "$lib" "$lib"; done
  printf 'COPY src/Svc/Svc.csproj src/Svc/packages.lock.json src/Svc/\nRUN dotnet restore src/Svc/Svc.csproj --locked-mode\n'
}
all_libs="$work/all-libs.Dockerfile"
restore_layer A B > "$all_libs"
missing_lib="$work/missing-lib.Dockerfile"
restore_layer A > "$missing_lib"

# expect_message <case name> <pattern> <Dockerfile>: fails, and the output matches the pattern
expect_message() {
  local out
  out=$(REPO_ROOT="$repo" "$script" "$3" 2>&1)
  if [ $? -ne 0 ] && grep -qE -- "$2" <<<"$out"; then
    echo "PASS  $1"
  else
    echo "FAIL  $1 (want a failure matching: $2)"
    echo "$out" | sed 's/^/      /'
    failures=$((failures + 1))
  fi
}

if REPO_ROOT="$repo" "$script" "$all_libs" >/dev/null 2>&1; then
  echo "PASS  every referenced project's csproj and lock file before the restore: passes"
else
  echo "FAIL  every referenced project's csproj and lock file before the restore: passes"
  REPO_ROOT="$repo" "$script" "$all_libs" 2>&1 | sed 's/^/      /'
  failures=$((failures + 1))
fi
expect_message "a lib referenced through another lib, not copied before the restore: fails, naming it and the fix (#420)" \
  'missing-lib\.Dockerfile.*libs/B/B\.csproj.*COPY libs/B/B\.csproj libs/B/packages\.lock\.json libs/B/' "$missing_lib"

expect 0 "restore inputs first, then source: passes" "$good"
expect 1 "source directory copied before the restore: fails" "$source_first"
expect 0 "source copied before a restore with a cache mount: passes" "$cache_mount"
expect 1 "analyzer source directory copied before the restore: fails" "$analyzer_dir"
expect 1 "a .cs file in a multi-source COPY before the restore: fails" "$mixed"
expect 1 "no dotnet restore in the Dockerfile: fails" "$no_restore"
expect 0 "every service Dockerfile in the repo restores before it copies source" "$root"/src/*/Dockerfile

[ "$failures" -eq 0 ] || exit 1

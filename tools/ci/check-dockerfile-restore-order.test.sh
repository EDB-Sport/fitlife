#!/usr/bin/env bash
# Tests for tools/ci/check-dockerfile-restore-order.sh (#357). Fixture Dockerfiles cover the good and the bad
# shapes; the last case runs the check on the repo's own service Dockerfiles.
# Usage: tools/ci/check-dockerfile-restore-order.test.sh   (exit code 0 = every case passes)
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

expect 0 "restore inputs first, then source: passes" "$good"
expect 1 "source directory copied before the restore: fails" "$source_first"
expect 1 "analyzer source directory copied before the restore: fails" "$analyzer_dir"
expect 1 "a .cs file in a multi-source COPY before the restore: fails" "$mixed"
expect 1 "no dotnet restore in the Dockerfile: fails" "$no_restore"
expect 0 "every service Dockerfile in the repo restores before it copies source" "$root"/src/*/Dockerfile

echo
if [ "$failures" -eq 0 ]; then
  echo "All check-dockerfile-restore-order.sh cases pass."
else
  echo "$failures case(s) failed."
  exit 1
fi

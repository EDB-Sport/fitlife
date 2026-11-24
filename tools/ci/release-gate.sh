#!/usr/bin/env bash
# The release gate on PRs into main (issue #94, docs/CSHARP_SETUP_TODO.md section 9, METHODOLOGY section 2.1).
# For each service src/<Service>/<Service>.csproj, compared with its newest <prefix>/vX.Y.Z tag (tools/ci/services.sh):
#   - changed since the tag  -> the <Version> in the .csproj is higher than the tag. A change counts when it is in
#                               src/<Service>/, in a libs/ project the service references (directly or through
#                               another libs/ project), in a libs/ file the .csproj links in (an item other than a
#                               ProjectReference whose Include is $(RepoRoot)libs/..., e.g. libs/nlog.config as
#                               Content, None or Compile; issue #182), or in a root build file (issue #112)
#   - a breaking API change  -> the MAJOR is raised (tools/ci/oasdiff-breaking.sh)
#   - a hotfix branch        -> only one service changed, and only its PATCH is raised
#   - no tag yet             -> passes: that's the service's first release
# Usage: HEAD_REF=<PR branch> tools/ci/release-gate.sh   (from the repo root, with the tags fetched)
set -uo pipefail

head_ref=${HEAD_REF:-$(git rev-parse --abbrev-ref HEAD)}
failures=0
changed=()

fail() {
  echo "::error title=Release gate::$1"
  failures=$((failures + 1))
}

# higher <a> <b>: true when version a is higher than version b
higher() {
  [ "$1" != "$2" ] && [ "$(printf '%s\n%s\n' "$1" "$2" | sort -V | tail -1)" = "$1" ]
}

# The root files that go into every service's build (issue #112)
root_files=(Directory.Packages.props Directory.Build.props Directory.Build.targets global.json)
root=$(pwd -P)

# lib_dirs <csproj>: the libs/ project folders the project references, directly or through another libs/ project.
# A ProjectReference is either $(RepoRoot)libs/... or relative to the project, with / or \ separators.
lib_dirs() {
  local csproj=$1 include target
  sed -n 's:.*<ProjectReference[^>]*Include="\([^"]*\)".*:\1:p' "$csproj" | while read -r include; do
    include=${include//\\//}
    include=${include//'$(RepoRoot)'/$root/}
    [[ $include == /* ]] || include="$(dirname "$csproj")/$include"
    target=$(cd "$(dirname "$include")" 2>/dev/null && pwd -P) || continue
    target=${target#"$root"/}
    if [[ $target == libs/* ]]; then
      echo "$target"
      lib_dirs "$target/$(basename "$include")"
    fi
  done
}

# linked_files <csproj>: the libs/ files the project links in, an Include="$(RepoRoot)libs/..." on any item
# but a ProjectReference (issue #182)
linked_files() {
  sed -n '/<ProjectReference/!s:.*<[A-Za-z]* [^>]*Include="\$(RepoRoot)\(libs/[^"]*\)".*:\1:p' "$1"
}

# shared_changes <tag> <csproj>: the shared paths (libs/ folders and linked files, root files) that go into the service
# and changed since the tag, separated by spaces
shared_changes() {
  local path
  for path in "${root_files[@]}" $( { lib_dirs "$2"; linked_files "$2"; } | sort -u); do
    git diff --quiet "$1" HEAD -- "$path" || printf '%s ' "$path"
  done
}

# shellcheck source=tools/ci/services.sh
. "$(dirname "$0")/services.sh"

while read -r name service version; do
  csproj="src/$name/$name.csproj"
  if [ -z "$version" ]; then
    fail "$csproj has no <Version>. Every service has its own version (METHODOLOGY section 2.1)."
    continue
  fi
  tag=$(git tag --list "$service/v*" --sort=-v:refname | head -1)

  if [ -z "$tag" ]; then
    echo "$name $version: no $service/v* tag yet, first release"
    continue
  fi
  released=${tag#"$service/v"}
  shared=$(shared_changes "$tag" "$csproj")
  if git diff --quiet "$tag" HEAD -- "src/$name/" && [ -z "$shared" ]; then
    echo "$name: unchanged since $tag"
    continue
  fi
  changed+=("$name")

  if ! higher "$version" "$released"; then
    because=""
    [ -z "$shared" ] || because=" Shared code it builds with changed: ${shared% }."
    fail "$name changed since $tag, but its version is still $version.$because Raise <Version> in $csproj."
    continue
  fi
  if [ -f "contracts/openapi/$service.json" ] && ! tools/ci/oasdiff-breaking.sh "$service" >/dev/null; then
    if [ "${version%%.*}" -le "${released%%.*}" ]; then
      fail "$name has a breaking API change since $tag, so its release must be a new MAJOR, not $version (tools/ci/oasdiff-breaking.sh $service shows the change)."
      continue
    fi
  fi
  if [[ $head_ref == hotfix/* ]] && [ "${version%.*}" != "${released%.*}" ]; then
    fail "A hotfix raises only PATCH: $name goes from $released to $version."
    continue
  fi
  echo "$name: $released -> $version"
done < <(services)

if [[ $head_ref == hotfix/* ]] && [ "${#changed[@]}" -gt 1 ]; then
  fail "A hotfix changes only one service, but this one changes ${changed[*]}."
fi

if [ "$failures" -eq 0 ]; then
  echo "The release gate passes."
fi
exit $((failures > 0))

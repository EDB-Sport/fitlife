#!/usr/bin/env bash
# The change rules shared by release-gate.sh (#94) and develop-plan.sh (#340): which shared paths go into a service's
# build, and whether they changed since a commit. Source it from the repo root:  . tools/ci/changes.sh
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
# but a ProjectReference (issue #182); it sees one item per line, in the
# $(RepoRoot)libs/ form only
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

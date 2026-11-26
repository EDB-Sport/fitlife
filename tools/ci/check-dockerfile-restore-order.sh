#!/usr/bin/env bash
# Checks that a service Dockerfile keeps the NuGet restore layer apart from the source (#357). Before `dotnet restore`
# only the restore inputs may be copied: project files, packages.lock.json, global.json and the Directory.* build props
# and targets. A change to a .cs file then leaves the restore layer cached, so a rebuild doesn't download NuGet packages.
# A restore that can't run first (MemberPortal.Web: its lock file needs the whole project folder, #374) is allowed
# when it has a BuildKit cache mount: the packages then survive a source change, so nothing is downloaded again.
# It also checks that the restore layer has all it needs (#420): the .csproj and packages.lock.json of every project
# the restored project references, directly, through another project, or through Directory.Build.props (the
# analyzer project). A missing one only showed up later as NETSDK1004 in api-tests (#418).
# Usage: tools/ci/check-dockerfile-restore-order.sh [Dockerfile...]   (default: src/*/Dockerfile)
#        REPO_ROOT=<dir> reads the project files from another tree (the tests' fixture repo)
# Tests: tools/ci/check-dockerfile-restore-order.test.sh
set -uo pipefail

root=$(cd "$(dirname "$0")/../.." && pwd)
projects_root=${REPO_ROOT:-$root}

# normalize <path>: a/b/../c -> a/c, and drops ./
normalize() {
  local part out=()
  local IFS=/
  for part in $1; do
    case $part in
      '' | .) ;;
      ..) [ ${#out[@]} -gt 0 ] && out=("${out[@]:0:${#out[@]}-1}") ;;
      *) out+=("$part") ;;
    esac
  done
  echo "${out[*]}"
}

# references <file, repo-relative>: the projects its ProjectReferences name, repo-relative
references() {
  local dir inc
  dir=$(dirname "$1")
  { grep -oE '<ProjectReference[^>]*Include="[^"]+"' "$projects_root/$1" 2>/dev/null || true; } |
    sed -E 's/.*Include="([^"]+)"/\1/' | while IFS= read -r inc; do
      inc=${inc//\\//}
      case $inc in
        '$(RepoRoot)'*) inc=${inc#'$(RepoRoot)'} ;;
        '$(MSBuildThisFileDirectory)'*) inc="$dir/${inc#'$(MSBuildThisFileDirectory)'}" ;;
        *) inc="$dir/$inc" ;;
      esac
      normalize "$inc"
    done
}

# closure <project, repo-relative>: the project and everything it needs restored, one per line
closure() {
  local queue=("$1") seen=" $1 " project ref implicit
  implicit=$(references Directory.Build.props)
  while [ ${#queue[@]} -gt 0 ]; do
    project=${queue[0]}
    queue=("${queue[@]:1}")
    echo "$project"
    for ref in $(references "$project") $implicit; do
      [ "$ref" = "$project" ] && continue
      [[ $seen == *" $ref "* ]] && continue
      seen+="$ref "
      queue+=("$ref")
    done
  done
}
shopt -s nullglob
if [ $# -eq 0 ]; then
  set -- "$root"/src/*/Dockerfile
fi
if [ $# -eq 0 ]; then
  echo "FAIL  no Dockerfiles found under src/*/"
  exit 1
fi

restore_input='(\.csproj|packages\.lock\.json|global\.json|Directory\.(Build|Packages)\.(props|targets))$'
restore_run='^[[:space:]]*RUN[[:space:]].*dotnet[[:space:]]+restore([[:space:]]|$)'
copy_line='^[[:space:]]*COPY[[:space:]]'
status=0

for file in "$@"; do
  restore_text=$(grep -E -m1 "$restore_run" "$file")
  if [ -z "$restore_text" ]; then
    echo "FAIL  $file has no dotnet restore, so the check can't see the restore layer (#357)"
    status=1
    continue
  fi
  if [[ "$restore_text" == *--mount=type=cache* ]]; then
    echo "PASS  $file restores with a BuildKit cache mount (#374)"
    continue
  fi

  file_failed=0
  line_no=0
  copied=" "
  while IFS= read -r line || [ -n "$line" ]; do
    line_no=$((line_no + 1))
    [[ "$line" =~ $restore_run ]] && break
    if [[ "$line" =~ $copy_line ]]; then
      read -r -a words <<< "$line"
      # words[0] is COPY, then flags (--chown=…), then sources, then the destination
      sources=()
      for ((i = 1; i < ${#words[@]} - 1; i++)); do
        [[ "${words[i]}" == --* ]] && continue
        sources+=("${words[i]}")
      done
      for src in "${sources[@]}"; do
        copied+="$(normalize "$src") "
        if [[ ! "$src" =~ $restore_input ]]; then
          echo "FAIL  $file:$line_no copies $src before dotnet restore. Only project files, packages.lock.json and the Directory.* build files may come first (#357)"
          file_failed=1
          status=1
        fi
      done
    fi
  done < "$file"

  # Every project the restored one needs must have its project file and lock file in the restore layer (#420)
  restored=$(sed -E 's/.*dotnet[[:space:]]+restore[[:space:]]+([^[:space:]]+\.csproj).*/\1/' <<<"$restore_text")
  restored=$(normalize "$restored")
  if [[ "$restored" == *.csproj && -f "$projects_root/$restored" ]]; then
    for project in $(closure "$restored"); do
      dir=$(dirname "$project")
      for needed in "$project" "$dir/packages.lock.json"; do
        if [[ $copied != *" $needed "* ]]; then
          echo "FAIL  $file restores $restored, which needs $needed, but it isn't copied before dotnet restore. Add: COPY $project $dir/packages.lock.json $dir/ (#420)"
          file_failed=1
          status=1
          break
        fi
      done
    done
  fi

  [ "$file_failed" = 0 ] && echo "PASS  $file restores before it copies source"
done

exit "$status"

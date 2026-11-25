#!/usr/bin/env bash
# Checks that a service Dockerfile keeps the NuGet restore layer apart from the source (#357). Before `dotnet restore`
# only the restore inputs may be copied: project files, packages.lock.json, global.json and the Directory.* build props
# and targets. A change to a .cs file then leaves the restore layer cached, so a rebuild doesn't download NuGet packages.
# A restore that can't run first (MemberPortal.Web: its lock file needs the whole project folder, #374) is allowed
# when it has a BuildKit cache mount: the packages then survive a source change, so nothing is downloaded again.
# Usage: tools/ci/check-dockerfile-restore-order.sh [Dockerfile...]   (default: src/*/Dockerfile)
# Tests: tools/ci/check-dockerfile-restore-order.test.sh
set -uo pipefail

root=$(cd "$(dirname "$0")/../.." && pwd)
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
        if [[ ! "$src" =~ $restore_input ]]; then
          echo "FAIL  $file:$line_no copies $src before dotnet restore. Only project files, packages.lock.json and the Directory.* build files may come first (#357)"
          file_failed=1
          status=1
        fi
      done
    fi
  done < "$file"

  [ "$file_failed" = 0 ] && echo "PASS  $file restores before it copies source"
done

exit "$status"

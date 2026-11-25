#!/usr/bin/env bash
# Checks that a service Dockerfile runs `dotnet restore` before it copies any source (#357). Only the restore inputs
# may be copied first: project files, packages.lock.json, global.json and the Directory.* build props and targets.
# A change to a .cs file then leaves the restore layer cached, so a rebuild doesn't download NuGet packages again.
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

# Exempt: MemberPortal.Web's restore needs the whole project folder. Its lock file has the implicit package
# Microsoft.AspNetCore.App.Internal.Assets, which the Web SDK adds only when it sees the project's sources. Restore
# from the project files alone fails: "NU1004 ... The packages lock file is inconsistent" (measured for #357).
# A restore-first layout there is a group decision (for example a BuildKit NuGet cache mount), not made here.
exempt=("src/MemberPortal.Web/Dockerfile")

restore_input='(\.csproj|packages\.lock\.json|global\.json|Directory\.(Build|Packages)\.(props|targets))$'
restore_run='^[[:space:]]*RUN[[:space:]].*dotnet[[:space:]]+restore([[:space:]]|$)'
copy_line='^[[:space:]]*COPY[[:space:]]'
status=0

for file in "$@"; do
  for skip in "${exempt[@]}"; do
    if [[ "$file" == *"$skip" ]]; then
      echo "SKIP  $file: exempt, see the comment above exempt=() (#357)"
      continue 2
    fi
  done
  restored=0
  file_failed=0
  line_no=0
  while IFS= read -r line || [ -n "$line" ]; do
    line_no=$((line_no + 1))
    if [[ "$line" =~ $restore_run ]]; then
      restored=1
      break
    fi
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

  if [ "$restored" = 0 ]; then
    echo "FAIL  $file has no dotnet restore, so the check can't see the restore layer (#357)"
    status=1
  elif [ "$file_failed" = 0 ]; then
    echo "PASS  $file restores before it copies source"
  fi
done

exit "$status"

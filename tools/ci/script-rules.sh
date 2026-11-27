#!/usr/bin/env bash
# Script rules for every git-tracked *.sh under tools/ and deploy/ (#563), for what shellcheck does not flag:
#   1. no bash 4 forms outside comments: declare -A, mapfile, readarray, ${x^^} and ${x,,}, &>> (macOS runs bash 3.2)
#   2. mode 100755, unless the script is a sourced library (the CI runs the scripts directly)
#   3. a set line with pipefail, unless the script is a sourced library or a #!/bin/sh script (pipefail is bash)
# Usage: tools/ci/script-rules.sh [repo root]   (default: this repo; exit 0 = clean, 1 = findings, "path:line: reason" each)
set -uo pipefail

# Sourced libraries: nobody runs them, so they have no executable bit and need no pipefail.
libraries=(
  "tools/ci/code-lines.sh"   # sourced by tdd-check.sh and mutation.sh
  "tools/ci/services.sh"     # sourced by release-gate.sh and release-plan.sh
  "tools/ci/test-report.sh"  # sourced by every tools/ci/*.test.sh
)

is_library() {
  local lib
  for lib in "${libraries[@]}"; do
    [ "$lib" = "$1" ] && return 0
  done
  return 1
}

root=${1:-$(git -C "$(dirname "$0")" rev-parse --show-toplevel)} || exit 2
cd "$root" || exit 2

status=0

# Bash 4 forms outside comments and outside quoted heredoc bodies (literal text that is never run).
# A comment is a line that starts with #, or text after whitespace and #. The messages avoid spelling the forms out.
bash4_rules() {
  awk -v path="$1" '
    heredoc != "" {
      line = $0
      if (dash) sub(/^\t+/, "", line)
      if (line == heredoc) heredoc = ""
      next
    }
    /^[[:space:]]*#/ { next }
    {
      code = $0
      sub(/[[:space:]]+#.*/, "", code)
    }
    code ~ /(declare|typeset|local)[[:space:]]+-[A-Za-z]*A/ {
      print path ":" NR ": associative arrays are bash 4 only (macOS bash 3.2): use plain variables"
    }
    code ~ /(^|[;&|({][[:space:]]*|then[[:space:]]+|do[[:space:]]+)(mapfile|readarray)([[:space:]]|$)/ {
      print path ":" NR ": mapfile and readarray are bash 4 only: read the lines with while read"
    }
    code ~ /\$\{[A-Za-z_][A-Za-z0-9_]*(\^\^?|,,?)/ {
      print path ":" NR ": case-changing expansions (^^ and ,,) are bash 4 only: use tr"
    }
    code ~ /&[>][>]/ {
      print path ":" NR ": the combined redirect is bash 4 only: write >> file 2>&1"
    }
    match($0, /(^|[^<])[<][<]-?[ \t]*[^[:alnum:]_ \t<>-]/) {
      d = substr($0, RSTART + RLENGTH - 1)
      sub(/^[^[:alnum:]_]+/, "", d)
      if (match(d, /^[A-Za-z_][A-Za-z0-9_]*/)) heredoc = substr(d, 1, RLENGTH)
      dash = ($0 ~ /[<][<]-/)
    }
  ' "$1"
}

# Every tracked script: "<mode> <path>" from the index, so the mode is the committed one
while IFS=$'\t' read -r meta path; do
  case $path in *.sh) ;; *) continue ;; esac
  [ -f "$path" ] || continue
  mode=${meta%% *}

  hits=$(bash4_rules "$path")
  if [ -n "$hits" ]; then
    echo "$hits"
    status=1
  fi

  is_library "$path" && continue

  if [ "$mode" != 100755 ]; then
    echo "$path:1: mode $mode, not 100755: the CI runs this script directly (git update-index --chmod=+x)"
    status=1
  fi
  # pipefail is bash: a #!/bin/sh script (POSIX sh, as the Vault scripts are) has no such option
  if [ "$(head -n 1 "$path")" != "#!/bin/sh" ] && ! grep -qE '^[[:space:]]*set[[:space:]].*pipefail' "$path"; then
    echo "$path:1: no set line with pipefail (set -uo pipefail)"
    status=1
  fi
done < <(git ls-files -s -- tools deploy)

if [ "$status" -eq 0 ]; then
  echo "script-rules: ok"
fi
exit "$status"

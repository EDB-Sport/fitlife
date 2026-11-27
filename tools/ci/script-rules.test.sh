#!/usr/bin/env bash
# Tests for tools/ci/script-rules.sh (#563): the bash 4 forms, the executable bit and pipefail that shellcheck does not flag.
# Fixture repos hold one tracked script each; the last case runs the rules on this repo, which must be clean.
# Usage: tools/ci/script-rules.test.sh   (exit 0 = every case passes)
. "$(dirname "${BASH_SOURCE[0]}")/test-report.sh"
set -uo pipefail

here=$(cd "$(dirname "$0")" && pwd)
root=$(cd "$here/../.." && pwd)
script="$here/script-rules.sh"
work=$(mktemp -d)
trap 'rm -rf "$work"' EXIT
failures=0

# new_repo <name>: a git repo with tools/ci and deploy/
new_repo() {
  git init -q "$work/$1"
  mkdir -p "$work/$1/tools/ci" "$work/$1/deploy"
}

# add_script <repo> <path> <mode 755|644>: the script body comes on stdin; it is tracked with that mode
add_script() {
  cat > "$work/$1/$2"
  chmod "$3" "$work/$1/$2"
  git -C "$work/$1" add -- "$2"
}

# expect <case> <want exit> <output pattern> <repo>
expect() {
  local out code
  out=$("$script" "$work/$4" 2>&1)
  code=$?
  if [ "$code" -eq "$2" ] && grep -qE -- "$3" <<<"$out"; then
    echo "PASS  $1"
  else
    echo "FAIL  $1 (want exit $2 and output matching: $3; got exit $code)"
    echo "$out" | sed 's/^/      /'
    failures=$((failures + 1))
  fi
}

clean_body='#!/usr/bin/env bash
set -uo pipefail
echo ok
'

new_repo declare
add_script declare tools/ci/bad.sh 755 <<'S'
#!/usr/bin/env bash
set -uo pipefail
declare -A tag_of
S
expect "an associative array fails with the file and line" 1 "tools/ci/bad.sh:3: " declare

new_repo mapfile
add_script mapfile tools/ci/bad.sh 755 <<'S'
#!/usr/bin/env bash
set -uo pipefail
mapfile -t lines < file.txt
S
expect "mapfile fails with the file and line" 1 "tools/ci/bad.sh:3: " mapfile

new_repo indented-mapfile
add_script indented-mapfile tools/ci/bad.sh 755 <<'S'
#!/usr/bin/env bash
set -uo pipefail
load() {
  mapfile -t a < x
}
S
expect "an indented mapfile in a function fails" 1 "tools/ci/bad.sh:4: " indented-mapfile

new_repo indented-readarray
add_script indented-readarray tools/ci/bad.sh 755 <<'S'
#!/usr/bin/env bash
set -uo pipefail
while true; do
    readarray -t b < y
done
S
expect "an indented readarray in a loop body fails" 1 "tools/ci/bad.sh:4: " indented-readarray

new_repo positional-case
add_script positional-case tools/ci/bad.sh 755 <<'S'
#!/usr/bin/env bash
set -uo pipefail
echo "${1^^}" "${arr[i]^^}"
S
expect "case-changing on a positional or indexed name fails" 1 "tools/ci/bad.sh:3: " positional-case

new_repo readarray
add_script readarray tools/ci/bad.sh 755 <<'S'
#!/usr/bin/env bash
set -uo pipefail
readarray -t lines < file.txt
S
expect "readarray fails with the file and line" 1 "tools/ci/bad.sh:3: " readarray

new_repo case-ops
add_script case-ops tools/ci/bad.sh 755 <<'S'
#!/usr/bin/env bash
set -uo pipefail
name=$1
echo "${name^^}"
S
expect "case-changing ^^ fails with the file and line" 1 "tools/ci/bad.sh:4: " case-ops

new_repo lower-ops
add_script lower-ops tools/ci/bad.sh 755 <<'S'
#!/usr/bin/env bash
set -uo pipefail
name=$1
echo "${name,,}"
S
expect "case-changing ,, fails with the file and line" 1 "tools/ci/bad.sh:4: " lower-ops

new_repo append
add_script append tools/ci/bad.sh 755 <<'S'
#!/usr/bin/env bash
set -uo pipefail
ls missing &>> log.txt
S
expect "the combined redirect fails with the file and line" 1 "tools/ci/bad.sh:3: " append

new_repo clean
add_script clean tools/ci/ok.sh 755 <<<"$clean_body"
expect "a clean script passes" 0 "script-rules: ok" clean

new_repo comment
add_script comment tools/ci/ok.sh 755 <<'S'
#!/usr/bin/env bash
# bash 3.2 has no mapfile or readarray, and no declare -A: say so with a list instead
set -uo pipefail
echo ok
S
expect "a comment that only mentions mapfile passes" 0 "script-rules: ok" comment

new_repo message
add_script message tools/ci/ok.sh 755 <<'S'
#!/usr/bin/env bash
set -uo pipefail
echo "no mapfile or readarray here"
S
expect "mapfile in a message string passes" 0 "script-rules: ok" message

new_repo after-heredoc
add_script after-heredoc tools/ci/bad.sh 755 <<'S'
#!/usr/bin/env bash
set -uo pipefail
cat <<'T'
declare -A inside a quoted heredoc is text
T
declare -A tag_of
S
expect "a bash 4 form after a quoted heredoc still fails" 1 "tools/ci/bad.sh:6: " after-heredoc

new_repo posix-sh
add_script posix-sh deploy/ok.sh 755 <<'S'
#!/bin/sh
set -eu
echo ok
S
expect "a #!/bin/sh script needs no pipefail" 0 "script-rules: ok" posix-sh

new_repo unlisted-mode
add_script unlisted-mode tools/ci/ok.sh 644 <<<"$clean_body"
expect "a 100644 script that is not a sourced library fails" 1 "tools/ci/ok.sh:1: .*100755" unlisted-mode

new_repo no-pipefail
add_script no-pipefail tools/ci/ok.sh 755 <<'S'
#!/usr/bin/env bash
set -u
echo ok
S
expect "a 100755 script without pipefail fails" 1 "tools/ci/ok.sh:1: .*pipefail" no-pipefail

new_repo library
add_script library tools/ci/services.sh 644 <<'S'
#!/usr/bin/env bash
# Sourced by the release scripts.
services() { echo none; }
S
expect "a sourced library needs no executable bit and no pipefail" 0 "script-rules: ok" library

new_repo deploy-script
add_script deploy-script deploy/bad.sh 644 <<<"$clean_body"
expect "a 100644 deploy script fails too" 1 "deploy/bad.sh:1: " deploy-script

out=$("$script" "$root" 2>&1); code=$?
if [ "$code" -eq 0 ] && grep -qE 'script-rules: ok' <<<"$out"; then
  echo "PASS  every script in this repo passes the rules"
else
  echo "FAIL  every script in this repo passes the rules (exit $code)"
  echo "$out" | sed 's/^/      /'
  failures=$((failures + 1))
fi

if [ "$failures" -gt 0 ]; then
  echo "$failures failure(s)"
  exit 1
fi
echo "all checks passed"

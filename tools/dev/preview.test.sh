#!/usr/bin/env bash
# Tests for tools/dev/preview.sh (issue #521). Fake git, gh and docker (passed as GIT, GH and DOCKER) log their
# calls and answer from fixtures in a temp directory, so no case needs Docker, GitHub or a real worktree.
# Usage: tools/dev/preview.test.sh   (from anywhere in the repo; VERBOSE=1 for the PASS lines; exit 0 = all cases pass)
. "$(dirname "${BASH_SOURCE[0]}")/../ci/test-report.sh"
set -uo pipefail

root=$(git -C "$(dirname "$0")" rev-parse --show-toplevel) || exit 2
script="$root/tools/dev/preview.sh"
work=$(mktemp -d)
trap 'rm -rf "$work"' EXIT
failures=0

# Fake gh: `pr view` prints $FAKE_DIR/pr.json (or fails when there is none, as gh does for an unknown PR).
cat >"$work/gh" <<'FAKE'
#!/usr/bin/env bash
echo "gh $*" >>"$FAKE_DIR/calls"
[ -f "$FAKE_DIR/pr.json" ] || { echo "GraphQL: Could not resolve to a PullRequest with the number of $3." >&2; exit 1; }
cat "$FAKE_DIR/pr.json"
FAKE
# Fake git: the caller's checkout is $FAKE_DIR/repo; `worktree add` makes the folder with the compose files.
cat >"$work/git" <<'FAKE'
#!/usr/bin/env bash
echo "git $*" >>"$FAKE_DIR/calls"
args=" $* "
case "$args" in
  *" rev-parse --show-toplevel "*) echo "$FAKE_DIR/repo" ;;
  *" worktree add "*)
    dir=${@: -2:1}
    mkdir -p "$dir/deploy/compose"
    printf 'GATEWAY_PORT=8080\nDEMO_MEDLEM_PASSWORD=change-me-demo-medlem\n' >"$dir/deploy/compose/.env.example"
    ;;
esac
exit 0
FAKE
# Fake docker: logs the folder it runs in and its arguments; exits $FAKE_DOCKER_EXIT.
cat >"$work/docker" <<'FAKE'
#!/usr/bin/env bash
echo "docker $* (in $PWD)" >>"$FAKE_DIR/calls"
exit "${FAKE_DOCKER_EXIT:-0}"
FAKE
chmod +x "$work/gh" "$work/git" "$work/docker"

# new_case [state]: a fresh fixture directory with an open (or <state>) PR 42 whose head is abc123
new_case() {
  fx=$(mktemp -d "$work/case.XXXXXX")
  mkdir -p "$fx/repo"
  printf '{"state":"%s","headRefName":"feature/42-x","headRefOid":"abc123"}\n' "${1:-OPEN}" >"$fx/pr.json"
  : >"$fx/calls"
}

# run [args...]: runs the script with the fakes; sets out, status and calls
run() {
  out=$(cd "$fx/repo" && FAKE_DIR="$fx" GH="$work/gh" GIT="$work/git" DOCKER="$work/docker" \
    PREVIEW_DIR="$fx/previews" "$script" "$@" 2>&1)
  status=$?
  calls=$(cat "$fx/calls")
}

check() { # check <description> <command...>
  if "${@:2}"; then
    echo "PASS  $1"
  else
    echo "FAIL  $1"
    sed 's/^/      out: /' <<<"$out"
    sed 's/^/      call: /' <<<"$calls"
    failures=$((failures + 1))
  fi
}
has() { grep -qF -- "$1" <<<"$out"; }
has_call() { grep -qF -- "$1" <<<"$calls"; }
no_call() { ! grep -qF -- "$1" <<<"$calls"; }

new_case
run 42
check "an open PR: exit 0" test "$status" = 0
check "fetches the PR head" has_call "git fetch origin pull/42/head"
check "makes a worktree of the head outside the caller's checkout" has_call "git worktree add --detach $fx/previews/fitlife-pr-42 abc123"
check "says where the worktree is" has "$fx/previews/fitlife-pr-42"
check "runs compose with --build and --wait in deploy/compose of the worktree, under its own project name" \
  has_call "docker compose -p fitlife-pr-42 up -d --build --wait (in $fx/previews/fitlife-pr-42/deploy/compose)"
check "a missing .env is created from .env.example" test -f "$fx/previews/fitlife-pr-42/deploy/compose/.env"
check "prints the URL" has "http://localhost:8080"
check "prints the demo log-ins" has "medlem@fitlife.example, traener@fitlife.example, admin@fitlife.example"
check "says where the passwords are" has "DEMO_MEDLEM_PASSWORD, DEMO_TRAENER_PASSWORD and DEMO_ADMIN_PASSWORD in $fx/previews/fitlife-pr-42/deploy/compose/.env"
check "says a first start can take about 10 minutes" has "about 10 minutes"
check "says how to stop it" has "docker compose -p fitlife-pr-42 down"
check "says how to remove the worktree" has "git worktree remove $fx/previews/fitlife-pr-42"
check "doesn't touch the caller's checkout (no checkout, switch, reset or stash)" no_call "git checkout"
check "doesn't switch the caller's branch" no_call "git switch"

new_case
mkdir -p "$fx/previews/fitlife-pr-42/deploy/compose"
printf 'GATEWAY_PORT=9090\n' >"$fx/previews/fitlife-pr-42/deploy/compose/.env"
run 42
check "a second run moves the existing worktree to the new head" has_call "git -C $fx/previews/fitlife-pr-42 checkout --detach abc123"
check "an existing .env is kept" grep -qxF "GATEWAY_PORT=9090" "$fx/previews/fitlife-pr-42/deploy/compose/.env"
check "the URL follows GATEWAY_PORT in .env" has "http://localhost:9090"

new_case
rm "$fx/pr.json"
run 42
check "a PR that isn't found: non-zero exit" test "$status" != 0
check "a PR that isn't found: one line" test "$(wc -l <<<"$out" | tr -d ' ')" = 1
check "a PR that isn't found: no compose" no_call "docker"

new_case MERGED
run 42
check "a closed or merged PR: non-zero exit" test "$status" != 0
check "a closed or merged PR: says its state in one line" test "$out" = "error: PR #42 is MERGED, not open"
check "a closed or merged PR: no compose" no_call "docker"

new_case
FAKE_DOCKER_EXIT=1 run 42
check "compose fails: non-zero exit" test "$status" != 0

new_case
run
check "no PR number: usage and exit 2" test "$status" = 2

exit $((failures > 0))

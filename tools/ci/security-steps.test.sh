#!/usr/bin/env bash
# Tests for the gitleaks step in .github/workflows/security.yml (issue #20, PR #39 review).
# Runs the step's own script, taken from the workflow, with the shell GitHub would use for it,
# on a throwaway repo, and checks that a planted fake secret fails the step.
# Needs yq (v4), docker and git, which ubuntu-latest has. Usage: tools/ci/security-steps.test.sh
set -uo pipefail

root="$(cd "$(dirname "$0")/../.." && pwd)"
workflow="$root/.github/workflows/security.yml"
failures=0

for tool in yq docker git; do
  command -v "$tool" >/dev/null || { echo "needs $tool"; exit 2; }
done

# The shell GitHub runs a step with: the step's shell:, then the job's and the workflow's
# defaults.run.shell. Without any of these, Linux runners use "bash -e {0}", which has no pipefail.
# https://docs.github.com/actions/reference/workflows-and-actions/workflow-syntax#jobsjob_idstepsshell
shell_for() {
  local shell
  shell=$(yq -r "(.jobs.security.steps[] | select(.name == \"$1\") | .shell)
                 // .jobs.security.defaults.run.shell // .defaults.run.shell // \"\"" "$workflow")
  case "$shell" in
    "") echo "bash -e {0}" ;;
    bash) echo "bash --noprofile --norc -eo pipefail {0}" ;;
    *"{0}"*) echo "$shell" ;;
    *) echo "unsupported shell '$shell' in $workflow" >&2; return 1 ;;
  esac
}

step_shell=$(shell_for gitleaks) || exit 1
echo "gitleaks step shell: $step_shell"
step_script=$(mktemp)
work=$(mktemp -d)
trap 'rm -rf "$step_script" "$work"' EXIT
yq -r '.jobs.security.steps[] | select(.name == "gitleaks") | .run' "$workflow" >"$step_script"
image=$(yq -r '.env.GITLEAKS_IMAGE' "$workflow")

# A throwaway repo: a clean base commit, then one clean commit and one with a made-up GitHub token.
# The token is built at run time so that this file itself has nothing for gitleaks to find.
git -C "$work" init -q
git -C "$work" config user.email test@example.com
git -C "$work" config user.name test
echo "# readme" >"$work/README.md"
git -C "$work" add . && git -C "$work" commit -qm base
base=$(git -C "$work" rev-parse HEAD)
echo "clean line" >>"$work/README.md"
git -C "$work" commit -qam clean
clean=$(git -C "$work" rev-parse HEAD)
fake=$(head -c 96 /dev/urandom | base64 | tr -dc 'A-Za-z0-9' | cut -c1-36)
printf 'GITHUB_PAT=%s_%s\n' ghp "$fake" >"$work/config.env"
git -C "$work" add . && git -C "$work" commit -qm leak
leak=$(git -C "$work" rev-parse HEAD)

# run_step <base sha or empty> <head sha>: runs the step like the runner does, prints its exit code
run_step() {
  local runner_temp cmd
  runner_temp=$(mktemp -d)
  cmd=${step_shell//\{0\}/$step_script}
  (cd "$work" && BASE_SHA="$1" HEAD_SHA="$2" GITHUB_SHA="$2" RUNNER_TEMP="$runner_temp" \
    GITLEAKS_IMAGE="$image" $cmd) >"$runner_temp/out" 2>&1
  local status=$?
  sed 's/^/      /' "$runner_temp/out" | tail -n 5
  rm -rf "$runner_temp"
  return "$status"
}

# expect <description> <pass|fail> <base sha or empty> <head sha>
expect() {
  run_step "$3" "$4"
  local status=$?
  if { [ "$2" = pass ] && [ "$status" -eq 0 ]; } || { [ "$2" = fail ] && [ "$status" -ne 0 ]; }; then
    echo "PASS  $1"
  else
    echo "FAIL  $1: expected the step to $2, got exit $status"
    failures=$((failures + 1))
  fi
}

expect "pull request with a clean commit passes" pass "$base" "$clean"
expect "pull request with a planted fake token fails" fail "$clean" "$leak"
expect "push whose history has a planted fake token fails" fail "" "$leak"

[ "$failures" -eq 0 ] && echo "All cases pass." || echo "$failures case(s) failed."
[ "$failures" -eq 0 ]

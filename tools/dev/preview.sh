#!/usr/bin/env bash
# Runs a PR's head locally, so a person can try a feature by hand (issue #521): fetches the head, makes a git worktree
# of it next to this checkout, starts the compose stack there with --build and --wait, and prints the demo log-ins.
# Usage: tools/dev/preview.sh <pr>          (on WSL: GH=gh.exe tools/dev/preview.sh <pr>)
#   exit 0  the stack is up; 1  the PR isn't found or isn't open, or compose failed; 2  usage
# The worktree is <parent of this checkout>/fitlife-pr-<pr> (PREVIEW_DIR sets the parent). A second run moves it to the
# PR's newest head. Your own checkout is not touched: no checkout, switch or stash in it.
# The stack runs under its own compose project name, fitlife-pr-<pr>, so it doesn't replace your own stack's
# containers. Both use the ports in their .env, so stop your own stack first (or change the *_PORT values there).
# A missing deploy/compose/.env in the worktree is created from .env.example (made-up passwords, as in CI).
# GH, GIT and DOCKER name the commands (default gh, git, docker). Tests: tools/dev/preview.test.sh
set -uo pipefail

repo=EDB-Sport/fitlife
gh_cmd=${GH:-gh}
git_cmd=${GIT:-git}
docker_cmd=${DOCKER:-docker}
pr=${1-}
if ! [[ $pr =~ ^[0-9]+$ ]]; then
  echo "usage: tools/dev/preview.sh <pr number>" >&2
  exit 2
fi

pr_line=$("$gh_cmd" pr view "$pr" -R "$repo" --json state,headRefName,headRefOid \
  --jq '[.state, .headRefName, .headRefOid] | join(" ")' 2>/dev/null) || {
  echo "error: PR #$pr not found in $repo"
  exit 1
}
# gh on Windows may print CRLF (#527)
read -r state branch head <<<"$(tr -d '\r' <<<"$pr_line")"
if [ "$state" != OPEN ]; then
  echo "error: PR #$pr is $state, not open"
  exit 1
fi

root=$("$git_cmd" rev-parse --show-toplevel) || exit 2
dir="${PREVIEW_DIR:-$(dirname "$root")}/fitlife-pr-$pr"
project="fitlife-pr-$pr"
compose_dir="$dir/deploy/compose"

"$git_cmd" fetch origin "pull/$pr/head" || {
  echo "error: could not fetch the head of PR #$pr"
  exit 1
}
if [ -d "$dir" ]; then
  "$git_cmd" -C "$dir" checkout --detach "$head" || exit 1
else
  "$git_cmd" worktree add --detach "$dir" "$head" || exit 1
fi
echo "PR #$pr ($branch, ${head:0:7}) is checked out in $dir"

if [ ! -f "$compose_dir/.env" ]; then
  cp "$compose_dir/.env.example" "$compose_dir/.env" || exit 1
  echo "Created $compose_dir/.env from .env.example (made-up passwords)."
fi
port=$(sed -n 's/^GATEWAY_PORT=\([0-9]*\).*/\1/p' "$compose_dir/.env" | tail -1)

echo "Starting the stack (project $project). A first start on a cold machine can take about 10 minutes (#320)."
(cd "$compose_dir" && "$docker_cmd" compose -p "$project" up -d --build --wait) || {
  echo "error: docker compose failed; see its output above. Stop what started: cd $compose_dir && docker compose -p $project down"
  exit 1
}

cat <<EOF

PR #$pr runs at http://localhost:${port:-8080}
Demo log-ins: medlem@fitlife.example, traener@fitlife.example, admin@fitlife.example
Passwords: DEMO_MEDLEM_PASSWORD, DEMO_TRAENER_PASSWORD and DEMO_ADMIN_PASSWORD in $compose_dir/.env

Stop it:              cd $compose_dir && docker compose -p $project down -v
Remove the worktree:  git worktree remove $dir
EOF

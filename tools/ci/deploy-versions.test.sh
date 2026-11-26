#!/usr/bin/env bash
# Tests for tools/ci/deploy-versions.sh (issue #371): which image versions .github/workflows/deploy.yml deploys.
# It runs the script in a throwaway repo with release tags, as deploy.yml does, so it needs no GitHub and no Azure.
#   1. each image gets the version of its newest <prefix>/vX.Y.Z tag, and the prefixes are the ones of services.sh
#   2. a service without a release is refused; the member portal (#198) and NotificationService (#261) are left out
#   3. a run by hand can name versions (the rollback); an empty one means the newest release
#   4. anything but X.Y.Z is refused: latest, a typo, a pre-release tag
# Needs only git. Usage: tools/ci/deploy-versions.test.sh
. "$(dirname "${BASH_SOURCE[0]}")/test-report.sh"
set -uo pipefail

here="$(cd "$(dirname "$0")" && pwd)"
work=$(mktemp -d)
trap 'rm -rf "$work"' EXIT
failures=0

# new_repo <tag or +>...: a repo with the services, and the tags on a commit each ('+' starts the next commit).
new_repo() {
  repo="$work/repo-$RANDOM$RANDOM"
  git init -q "$repo"
  git -C "$repo" config user.email test@example.invalid
  git -C "$repo" config user.name test
  mkdir -p "$repo/tools/ci"
  cp "$here/deploy-versions.sh" "$here/services.sh" "$repo/tools/ci/" 2>/dev/null
  local name tag
  for name in BookingService MembershipService IdentityService NotificationService McpServer MemberPortal.Web; do
    mkdir -p "$repo/src/$name"
    printf '<Project Sdk="Microsoft.NET.Sdk.Web">\n  <PropertyGroup>\n    <Version>0.0.1</Version>\n  </PropertyGroup>\n</Project>\n' >"$repo/src/$name/$name.csproj"
  done
  git -C "$repo" add -A
  git -C "$repo" commit -q -m release
  for tag in "$@"; do
    if [[ $tag == + ]]; then
      git -C "$repo" commit -q --allow-empty -m next
    else
      git -C "$repo" tag "$tag"
    fi
  done
}

# run_script [NAME=value]...: runs the script in $repo. Sets $status, $env_file (what it adds to GITHUB_ENV),
# $outputs (GITHUB_OUTPUT) and $log.
run_script() {
  env_file="$repo.env"
  outputs="$repo.out"
  : >"$env_file"
  : >"$outputs"
  log=$(cd "$repo" && env "$@" GITHUB_ENV="$env_file" GITHUB_OUTPUT="$outputs" tools/ci/deploy-versions.sh 2>&1)
  status=$?
}

pass() { echo "PASS  $1"; }
fail() {
  echo "FAIL  $1"
  [[ -n ${2:-} ]] && printf '%s\n' "$2" | tail -10
  failures=$((failures + 1))
}

# expect_deploy <description> <booking> <membership> <identity> <ui or empty> [<NotificationService>]
expect_deploy() {
  local description=$1 want
  want=$(printf '%s\n' "BOOKING_IMAGE_TAG=$2" "MEMBERSHIP_IMAGE_TAG=$3" "IDENTITY_IMAGE_TAG=$4" \
    ${5:+"UI_IMAGE_TAG=$5"} ${6:+"NOTIFICATION_IMAGE_TAG=$6"} | sort)
  if [[ $status -eq 0 && "$(sort "$env_file")" == "$want" ]] && grep -qx "ui_tag=$5" "$outputs"; then
    pass "$description"
  else
    fail "$description: expected $2 $3 $4, member portal '$5' and NotificationService '${6:-}', got exit $status, $(tr '\n' ' ' <"$env_file")" "$log"
  fi
}

# expect_refused <description> <text the error must have>
expect_refused() {
  if [[ $status -ne 0 && $log == *"::error::"*"$2"* && ! -s $env_file ]]; then
    pass "$1"
  else
    fail "$1: expected a refusal naming '$2' and nothing for the deploy, got exit $status" "$log"
  fi
}

all_released=(bookingservice/v0.1.0 membershipservice/v0.1.0 identityservice/v0.1.0)

# 1. After a release: the newest tags.
new_repo "${all_released[@]}"
run_script
expect_deploy "three services, no member portal or NotificationService release: only the services" 0.1.0 0.1.0 0.1.0 ""

new_repo "${all_released[@]}" memberportal/v0.3.0 notificationservice/v0.1.0 + bookingservice/v0.2.0 + identityservice/v1.0.0 notificationservice/v0.2.0
run_script
expect_deploy "every image gets its own newest tag in the history" 0.2.0 0.1.0 1.0.0 0.3.0 0.2.0

new_repo "${all_released[@]}" mcpserver/v0.9.0
run_script
expect_deploy "a service that is not in the group is left alone" 0.1.0 0.1.0 0.1.0 ""

# 2. A service without a release.
new_repo bookingservice/v0.1.0 identityservice/v0.1.0 memberportal/v0.3.0
run_script
expect_refused "a service without a release tag is refused, and named" membershipservice

new_repo "${all_released[@]}"
rm -r "$repo/src/IdentityService"
git -C "$repo" add -A
git -C "$repo" commit -q -m "rename"
run_script
expect_refused "a service that services.sh doesn't know is refused, and named" identityservice

# 3. A run by hand.
new_repo "${all_released[@]}" memberportal/v0.3.0
run_script INPUT_MEMBERSHIP_TAG=0.0.9
expect_deploy "by hand, one version typed in: that one, the newest release of the rest" 0.1.0 0.0.9 0.1.0 0.3.0

new_repo "${all_released[@]}" memberportal/v0.3.0
run_script INPUT_BOOKING_TAG=0.0.5 INPUT_MEMBERSHIP_TAG=0.0.6 INPUT_IDENTITY_TAG=0.0.7 INPUT_UI_TAG=0.0.8
expect_deploy "by hand, every version typed in: the rollback" 0.0.5 0.0.6 0.0.7 0.0.8

new_repo bookingservice/v0.1.0 identityservice/v0.1.0
run_script INPUT_MEMBERSHIP_TAG=0.1.0
expect_deploy "by hand, a service without a release tag can be named" 0.1.0 0.1.0 0.1.0 ""

new_repo "${all_released[@]}" notificationservice/v0.2.0
run_script INPUT_NOTIFICATION_TAG=
expect_deploy "by hand, an empty NotificationService version: the released one" 0.1.0 0.1.0 0.1.0 "" 0.2.0

new_repo "${all_released[@]}" notificationservice/v0.2.0
run_script INPUT_NOTIFICATION_TAG=0.3.0
expect_deploy "by hand, a NotificationService version typed in: that one" 0.1.0 0.1.0 0.1.0 "" 0.3.0

# 4. Released versions only.
new_repo "${all_released[@]}"
run_script INPUT_BOOKING_TAG=latest
expect_refused "by hand, latest is refused" bookingservice

new_repo "${all_released[@]}" memberportal/v0.3.0
run_script INPUT_UI_TAG=v0.3.0
expect_refused "by hand, v0.3.0 (a typo for 0.3.0) is refused" memberportal

new_repo "${all_released[@]}"
run_script INPUT_IDENTITY_TAG=1.2
expect_refused "by hand, 1.2 is refused" identityservice

new_repo "${all_released[@]}"
run_script INPUT_NOTIFICATION_TAG=latest
expect_refused "by hand, latest is refused for NotificationService" notificationservice

new_repo bookingservice/v0.1.0 membershipservice/v0.2.0-rc1 identityservice/v0.1.0
run_script
expect_refused "a pre-release tag is refused" membershipservice

exit $((failures > 0))

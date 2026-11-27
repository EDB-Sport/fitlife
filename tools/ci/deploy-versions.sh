#!/usr/bin/env bash
# deploy-versions: the image versions .github/workflows/deploy.yml deploys (issue #371). Tested by deploy-versions.test.sh.
# Run it from the repo root, in a checkout that has the release tags (fetch-depth: 0).
#
# Each image gets the version of its newest <prefix>/vX.Y.Z tag in the history of HEAD (the released commit).
# A run started by hand can name a version instead (INPUT_<IMAGE>_TAG, the rollback); empty means the newest release.
# Only X.Y.Z is deployed: this rules out latest, develop, typos and pre-release tags.
# A service without a release is refused. The member portal (#198) and NotificationService (#261) without one
# are left out of the group.
#
# Writes <NAME>_IMAGE_TAG=<version> to $GITHUB_ENV, which main.bicepparam reads, and ui_tag=<version or empty> to
# $GITHUB_OUTPUT, which the smoke test reads. Writes neither if it refuses.
set -euo pipefail

# shellcheck source=tools/ci/services.sh
. "$(dirname "$0")/services.sh"

# <prefix> <environment variable main.bicepparam reads> <input of deploy.yml>. The prefixes are those of services.sh
# (the tags <prefix>/vX.Y.Z and the Docker Hub repository). A further service is one more line here, and one in main.bicepparam.
IMAGES='
bookingservice      BOOKING_IMAGE_TAG      INPUT_BOOKING_TAG
membershipservice   MEMBERSHIP_IMAGE_TAG   INPUT_MEMBERSHIP_TAG
identityservice     IDENTITY_IMAGE_TAG     INPUT_IDENTITY_TAG
memberportal        UI_IMAGE_TAG           INPUT_UI_TAG
notificationservice NOTIFICATION_IMAGE_TAG INPUT_NOTIFICATION_TAG
'
# The images that are left out of the group while they have no release.
OPTIONAL=" memberportal notificationservice "

known_prefixes=" $(services | awk '{ print $2 }' | tr '\n' ' ')"
failed=0
chosen=() # "<environment variable>=<version>"
ui_tag=""

error() {
  echo "::error::$*"
  failed=1
}

while read -r prefix variable input; do
  [[ -n $prefix ]] || continue
  if [[ $known_prefixes != *" $prefix "* ]]; then
    error "$prefix is not a service or UI in tools/ci/services.sh: its tags and image can't be found"
    continue
  fi
  version=${!input:-}
  if [[ -z $version ]]; then
    # The newest <prefix>/vX.Y.Z tag on the released commit's history, without the prefix. Empty if there is none.
    version=$({ git describe --tags --abbrev=0 --match "$prefix/v*" HEAD 2>/dev/null || true; } | sed "s|^$prefix/v||")
  fi
  if [[ -z $version && $OPTIONAL == *" $prefix "* ]]; then
    echo "Deploying without $prefix, which has no release yet"
    continue
  fi
  if [[ ! $version =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]]; then
    error "'$version' is not a released version of $prefix (X.Y.Z)"
    continue
  fi
  echo "Deploying $prefix $version"
  chosen+=("$variable=$version")
  if [[ $prefix == memberportal ]]; then ui_tag=$version; fi
done <<<"$IMAGES"

if [[ $failed -ne 0 ]]; then
  exit 1
fi

# main.bicepparam leaves the member portal out unless UI_IMAGE_TAG is set, and NotificationService unless
# NOTIFICATION_IMAGE_TAG is, so each is written only when there is a version: an empty one is not the same as none.
printf '%s\n' "${chosen[@]}" >>"${GITHUB_ENV:-/dev/null}"
echo "ui_tag=$ui_tag" >>"${GITHUB_OUTPUT:-/dev/null}"

#!/usr/bin/env bash
# Shared by the release scripts (issues #94, #95): the services and UIs, and the names their tags and images use.
# Source it:  . tools/ci/services.sh

# services: one line "<Name> <prefix> <version>" per src/<Name>/<Name>.csproj (version empty if missing).
# <prefix> names the git tags (<prefix>/vX.Y.Z, METHODOLOGY section 2.1) and the Docker Hub repository:
# the name in lower case, without a UI's .Web suffix (MemberPortal.Web -> memberportal, as deploy.yml expects).
services() {
  local csproj name prefix version
  for csproj in src/*/*.csproj; do
    name=$(basename "$(dirname "$csproj")")
    [ "$(basename "$csproj" .csproj)" = "$name" ] || continue
    prefix=$(tr '[:upper:]' '[:lower:]' <<<"$name")
    prefix=${prefix%.web}
    version=$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' "$csproj" | head -1)
    echo "$name $prefix $version"
  done
}

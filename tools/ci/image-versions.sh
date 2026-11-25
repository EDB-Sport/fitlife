#!/usr/bin/env bash
# image-versions: every image reference is a full version, and the same image has the same version everywhere.
# Issue #275, ADR 004. Run from deploy-check.sh (step 7), or by hand:  tools/ci/image-versions.sh [root]
#
# Reads, as text (no Docker needed):
#   - FROM in every Dockerfile
#   - image: in compose files (deploy/**/*.yml|yaml and compose.y*ml; a ${VAR:-default} is read as its default)
#   - image parameters in *.bicep and *.bicepparam (a name with "image" and not "tag"; one whose version is
#     in a companion <name>Tag parameter is left to deploy-check step 2)
#   - string literals given to Testcontainers builders in tests/**/*.cs:  new MongoDbBuilder("mongo:8.0.38")
# Rule 1: a full version X.Y.Z[-suffix], or a digest (@sha256:...).
# Rule 2: the same image name has the same reference in all those places.
# Not checked: mcr.microsoft.com/dotnet/aspnet (the .NET runtime image restores no packages, so it may float,
# ADR 004), images built from this repo (fitlife/...), `FROM scratch` and `FROM <earlier stage>`.
set -euo pipefail

ROOT=${1:-$(cd "$(dirname "$0")/../.." && pwd)}
cd "$ROOT"

FLOATING_OK='mcr.microsoft.com/dotnet/aspnet'
failures=0
refs=() # "<name>\t<tag or digest>\t<file>:<line>"

fail() {
  failures=$((failures + 1))
  printf 'FAIL: %s\n' "$*" >&2
  if [[ -n ${GITHUB_ACTIONS:-} ]]; then printf '::error title=image-versions::%s\n' "$*"; fi
}

# record <file> <line> <image>
record() {
  local where="$1:$2" image=$3 name version=""
  image=${image#docker.io/}
  if [[ $image == *@* ]]; then
    name=${image%%@*}
    version="@${image#*@}"
  elif [[ ${image##*/} == *:* ]]; then
    name=${image%:*}
    version=${image##*:}
  else
    name=$image
  fi
  [[ $name == "$FLOATING_OK" || $name == fitlife/* ]] && return 0
  if [[ $version == @* || $version =~ ^[0-9]+\.[0-9]+\.[0-9]+(-.+)?$ ]]; then
    refs+=("$name"$'\t'"$version"$'\t'"$where")
  elif [[ -z $version ]]; then
    fail "$where: $image has no version, which means latest: use a full version (X.Y.Z)"
  else
    fail "$where: $image is not a full version: use X.Y.Z (or a digest), as ADR 004 says"
  fi
}

# --- Dockerfiles: FROM [--flag...] image [AS stage]; stage names and scratch are not images
while IFS= read -r -d '' file; do
  file=${file#./}
  stages=" "
  lineno=0
  while IFS= read -r line || [[ -n $line ]]; do
    lineno=$((lineno + 1))
    read -ra words <<<"$line"
    [[ ${#words[@]} -gt 1 && ${words[0]} == [Ff][Rr][Oo][Mm] ]] || continue
    i=1
    while [[ ${words[i]:-} == --* ]]; do i=$((i + 1)); done
    image=${words[i]:-}
    if [[ -n $image && $image != scratch && $stages != *" $image "* ]]; then
      record "$file" "$lineno" "$image"
    fi
    if [[ ${words[i + 1]:-} == [Aa][Ss] && -n ${words[i + 2]:-} ]]; then stages+="${words[i + 2]} "; fi
  done <"$file"
done < <(find . -type f \( -name Dockerfile -o -name 'Dockerfile.*' -o -name '*.Dockerfile' \) \
  -not -path './.git/*' -not -path '*/node_modules/*' -not -path '*/bin/*' -not -path '*/obj/*' -print0 | sort -z)

# --- compose files
while IFS= read -r -d '' file; do
  file=${file#./}
  while IFS=: read -r lineno rest; do
    image=$(sed -E 's/^[[:space:]]*(-[[:space:]]*)?image:[[:space:]]*//; s/[[:space:]]+#.*$//; s/^["'\'']//; s/["'\'']$//' <<<"$rest")
    if [[ $image =~ ^\$\{[A-Za-z0-9_]+:?-(.*)\}$ ]]; then image=${BASH_REMATCH[1]}; fi
    [[ -n $image && $image != *'$'* ]] && record "$file" "$lineno" "$image"
  done < <(grep -nE '^[[:space:]]*image:[[:space:]]*[^[:space:]]' "$file" || true)
done < <(find deploy . -maxdepth 4 -type f \( -name '*.yml' -o -name '*.yaml' \) \
  -not -path './.git/*' -not -path '*/node_modules/*' -not -path './.github/*' -not -path './deploy/*' \
  -print0 2>/dev/null | sort -zu)

# --- Bicep: param <name> [string] = '<image>'
for file in $(find deploy -type f \( -name '*.bicep' -o -name '*.bicepparam' \) 2>/dev/null | sort); do
  param_re="^[[:space:]]*param[[:space:]]+([A-Za-z0-9_]+)([[:space:]]+string)?[[:space:]]*=[[:space:]]*'([^']*)'"
  tag_params=" $(sed -nE 's/^[[:space:]]*param[[:space:]]+([A-Za-z0-9_]+Tag)[[:space:]].*/\1/p' "$file" | tr '\n' ' ')"
  lineno=0
  while IFS= read -r line || [[ -n $line ]]; do
    lineno=$((lineno + 1))
    [[ $line =~ $param_re ]] || continue
    param=${BASH_REMATCH[1]} value=${BASH_REMATCH[3]}
    lower=$(tr '[:upper:]' '[:lower:]' <<<"$param")
    [[ $lower == *image* && $lower != *tag* && -n $value ]] || continue
    [[ $tag_params == *" ${param}Tag "* ]] && continue
    record "$file" "$lineno" "$value"
  done <"$file"
done

# --- Testcontainers: new <Something>Builder("<image>") in tests/**/*.cs
if [[ -d tests ]]; then
  while IFS= read -r hit; do
    file=${hit%%:*} rest=${hit#*:} lineno=${rest%%:*} text=${rest#*:}
    while [[ $text =~ [A-Za-z]+Builder\(\"([^\"]+)\"\) ]]; do
      record "$file" "$lineno" "${BASH_REMATCH[1]}"
      text=${text#*"${BASH_REMATCH[0]}"}
    done
  done < <(grep -rnE --include='*.cs' '[A-Za-z]+Builder\("[^"]+"\)' tests || true)
fi

# --- Rule 2: one reference per image name
if [[ ${#refs[@]} -gt 0 ]]; then
  while IFS= read -r name; do
    first_ver="" first_where=""
    while IFS=$'\t' read -r n ver where; do
      [[ $n == "$name" ]] || continue
      if [[ -z $first_ver ]]; then
        first_ver=$ver first_where=$where
      elif [[ $ver != "$first_ver" ]]; then
        fail "$where: $name is $ver here but $first_ver in $first_where: use one version everywhere"
      fi
    done < <(printf '%s\n' "${refs[@]}")
  done < <(printf '%s\n' "${refs[@]}" | cut -f1 | sort -u)
fi

echo
if [[ $failures -gt 0 ]]; then
  echo "image-versions: $failures problem(s) found" >&2
  exit 1
fi
echo "image-versions: ${#refs[@]} image reference(s) checked, all full versions and consistent"

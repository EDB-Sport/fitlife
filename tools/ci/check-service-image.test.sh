#!/usr/bin/env bash
# Tests for tools/ci/check-service-image.sh (#223). Stub `docker`, `curl` and `sleep` commands log their
# arguments, so no Docker is needed. The stub service answers /health only when it got the settings it needs.
# Usage: tools/ci/check-service-image.test.sh   (exit code 0 = every case passes)
set -uo pipefail

here=$(cd "$(dirname "$0")" && pwd)
script="$here/check-service-image.sh"
work=$(mktemp -d)
trap 'rm -rf "$work"' EXIT
failures=0

# Stubs. STUB_NEEDS is a setting the stub service needs to start, e.g. Jwt__SigningKey; empty = needs none.
mkdir -p "$work/bin"
cat > "$work/bin/docker" <<'EOF'
#!/usr/bin/env bash
echo "docker $*" >> "$CALLS"
# A build behaves like BuildKit (#423): the steps' output goes to stderr, and -q hides it, so only the last
# "ERROR: failed to solve" line is left. STUB_BUILD_FAILS=1 ends the steps with a compiler error.
if [ "$1" = build ]; then
  quiet=0
  [[ " $* " == *" -q "* ]] && quiet=1
  steps() {
    for i in $(seq 1 60); do echo "#12 $i.0 build step output line $i"; done
    [ "${STUB_BUILD_FAILS:-0}" = 1 ] && echo "#12 61.0 /app/src/IdentityService/Program.cs(12,5): error CS1002: ; expected"
  }
  [ "$quiet" = 1 ] || steps >&2
  if [ "${STUB_BUILD_FAILS:-0}" = 1 ]; then
    echo "ERROR: failed to solve: process \"/bin/sh -c dotnet publish\" did not complete successfully: exit code: 1" >&2
    exit 1
  fi
  echo "sha256:0000000000000000000000000000000000000000000000000000000000000000"
fi
case "$1" in
  run) echo "stub-container" ;;
  port) echo "127.0.0.1:49999" ;;
esac
exit 0
EOF
cat > "$work/bin/curl" <<'EOF'
#!/usr/bin/env bash
if [ -z "${STUB_NEEDS:-}" ] || grep -qE -- "^docker run .*-e ${STUB_NEEDS}=" "$CALLS"; then printf 200; else printf 000; fi
EOF
printf '#!/usr/bin/env bash\nexit 0\n' > "$work/bin/sleep"
chmod +x "$work/bin/docker" "$work/bin/curl" "$work/bin/sleep"

# run <needs> <args...>: runs the script, sets $status, $out and $calls
run() {
  local needs=$1
  shift
  : > "$work/calls"
  out=$(PATH="$work/bin:$PATH" CALLS="$work/calls" STUB_NEEDS="$needs" "$script" "$@" 2>&1)
  status=$?
  calls=$(cat "$work/calls")
}

# check <description> <condition...>
check() {
  local description=$1
  shift
  if "$@"; then
    echo "PASS  $description"
  else
    echo "FAIL  $description"
    echo "$out" | sed 's/^/      out: /'
    echo "$calls" | sed 's/^/      call: /'
    failures=$((failures + 1))
  fi
}
has_call() { grep -qE -- "$1" <<<"$calls"; }
no_call() { ! grep -qE -- "$1" <<<"$calls"; }

# 1. A service that needs nothing: as before
run "" BookingService
check "without settings, a service that needs none passes" test "$status" -eq 0
check "without settings, docker run gets no -e" no_call '^docker run .* -e '

# 2. A setting after the service name reaches the container
run Jwt__SigningKey MembershipService Jwt__SigningKey=image-check-key-at-least-32-bytes-long
check "a service that needs a key passes when it is given" test "$status" -eq 0
check "the setting is passed to docker run with -e" has_call '^docker run .*-e Jwt__SigningKey=image-check-key-at-least-32-bytes-long '

# 3. Several settings
run Second__Key MembershipService First__Key=a Second__Key=b
check "every setting is passed" has_call '^docker run .*-e First__Key=a -e Second__Key=b '

# 4. Without the key it still fails, so the check stays a check
run Jwt__SigningKey MembershipService
check "a service that needs a key fails without it" test "$status" -ne 0

# 5. A failed build shows its cause, and a good one stays quiet (#423)
STUB_BUILD_FAILS=1 run "" IdentityService
check "a failed build fails the check" test "$status" -eq 1
check "a failed build says so" grep -q 'FAIL  docker build -f src/IdentityService/Dockerfile . failed' <<<"$out"
check "a failed build shows the compiler error" grep -q 'error CS1002: ; expected' <<<"$out"
check "a failed build shows only the end of the build output" bash -c '! grep -q "output line 20$" <<<"$1"' _ "$out"
check "nothing runs after a failed build" no_call '^docker run'
run "" BookingService
check "a good build prints none of its output" bash -c '! grep -q "build step output" <<<"$1"' _ "$out"

# 6. An argument that isn't KEY=VALUE
run "" MembershipService not-a-setting
check "an argument without = fails" test "$status" -ne 0
check "it says how to call the script" grep -q 'Usage' <<<"$out"
check "nothing is built" no_call '^docker build'

echo
if [ "$failures" -eq 0 ]; then
  echo "All check-service-image.sh cases pass."
else
  echo "$failures case(s) failed."
fi
exit "$failures"

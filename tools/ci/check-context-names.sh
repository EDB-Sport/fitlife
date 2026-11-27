#!/usr/bin/env bash
# Checks that every "In code" name in a service's CONTEXT.md "Language in this context" table is in GLOSSARY.md's
# "In code" column (#517). The agent rule "look in the service's CONTEXT.md first" is only safe while the two agree.
# Reading rules: text in parentheses is a note and is not checked; `<Type>Id` and `Type.Property` pass when Type is a
# glossary name; `Models/<Type>` checks <Type>. Only the Terms table of the glossary counts.
# Usage: tools/ci/check-context-names.sh [CONTEXT.md...]   (default: src/*/CONTEXT.md)
#        GLOSSARY=<file> reads another glossary (the tests' fixture)
# Tests: tools/ci/check-context-names.test.sh
set -euo pipefail

root=$(cd "$(dirname "$0")/../.." && pwd)
glossary=${GLOSSARY:-$root/GLOSSARY.md}

# Names the glossary does not hold, one reason each. Add a line here only with a reason a reviewer can read.
exceptions='
PaymentProviderClient  the ACL adapter that implements IPaymentProvider, not a domain word
PausedTo               a property of Membership, listed next to Membership.PausedFrom
'

[ "$#" -gt 0 ] || set -- "$root"/src/*/CONTEXT.md

names=$(mktemp)
trap 'rm -f "$names"' EXIT
# The backticked names in the "In code" column (3rd) of the Terms table
awk -F'|' '/^## /{ in_terms = ($0 == "## Terms"); next } in_terms && /^\| *[^-| ]/ && $1 == "" { print $4 }' "$glossary" |
  grep -o '`[^`]*`' | tr -d '`' | sort -u > "$names"

in_glossary() { grep -qFx -- "$1" "$names"; }
is_exception() { printf '%s\n' "$exceptions" | awk -v n="$1" '$1 == n { found = 1 } END { exit !found }'; }

status=0
for file in "$@"; do
  # The backticked names in the "In code" column (3rd) of the "Language in this context" table, notes in parentheses removed
  for name in $(awk -F'|' '/^## /{ on = ($0 ~ /^## Language in this context/); next } on && /^\|/ && NF >= 4 { print $4 }' "$file" |
    sed 's/([^)]*)//g' | grep -o '`[^`]*`' | tr -d '`'); do
    type=${name#Models/}
    if in_glossary "$type" || is_exception "$type"; then continue; fi
    case $type in
      *Id) in_glossary "${type%Id}" && continue ;;
      *.*) in_glossary "${type%%.*}" && continue ;;
    esac
    echo "$file: \`$name\` is not in the In code column of GLOSSARY.md. Add the term to GLOSSARY.md, or fix the name." >&2
    status=1
  done
done
exit "$status"

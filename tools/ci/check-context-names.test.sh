#!/usr/bin/env bash
# Tests for tools/ci/check-context-names.sh (#517). Fixture glossaries and CONTEXT.md files cover the passing and failing
# names; the last case runs the check on the repo's own GLOSSARY.md and src/*/CONTEXT.md.
# Usage: tools/ci/check-context-names.test.sh   (exit code 0 = every case passes)
. "$(dirname "${BASH_SOURCE[0]}")/test-report.sh"
set -uo pipefail

here=$(cd "$(dirname "$0")" && pwd)
root=$(cd "$here/../.." && pwd)
script="$here/check-context-names.sh"
work=$(mktemp -d)
trap 'rm -rf "$work"' EXIT
failures=0

glossary="$work/GLOSSARY.md"
cat > "$glossary" <<'MD'
## Terms

| Term | Meaning | In code | schema.org | Context | Kind | Avoid |
|---|---|---|---|---|---|---|
| Hold | One run of a class | `ClassSession` | Event | Booking | Aggregate root | Session |
| Center | A centre | `Center` | ExerciseGym | Booking | Entity | Gym |
| Sen afmelding | A late cancellation | `ReservationCancelled.IsLateCancellation` | – | Booking | Term | Late cancel |
| Betalingsudbyder | The payment provider | `IPaymentProvider` | – | Membership | Domain service | Gateway |
| Medlemskab | An agreement | `Membership` | – | Membership | Aggregate root | Plan |

## Words that mean different things in different contexts

| Word | Where | Meaning | In code |
|---|---|---|---|
| medlem | Identity | Only here | `NotATerm` |
MD

# context <file name> <row...>: a CONTEXT.md with the table rows given
context() {
  local file="$work/$1"
  shift
  {
    printf '# Service\n\n## Language in this context\n\n| Term | Meaning here | In code |\n|---|---|---|\n'
    printf '%s\n' "$@"
    printf '\n## Aggregates\n\n| Root | Holds |\n|---|---|\n| `Unlisted` | outside the language table |\n'
  } > "$file"
  echo "$file"
}

# expect <exit status> <case name> <output pattern or -> <CONTEXT.md...>
expect() {
  local want=$1 name=$2 pattern=$3 out status
  shift 3
  out=$(GLOSSARY="$glossary" "$script" "$@" 2>&1)
  status=$?
  if [ "$status" = "$want" ] && { [ "$pattern" = - ] || grep -qE -- "$pattern" <<<"$out"; }; then
    echo "PASS  $name"
  else
    echo "FAIL  $name (exit $status, want $want, output pattern: $pattern)"
    echo "$out" | sed 's/^/      /'
    failures=$((failures + 1))
  fi
}

good=$(context good.md '| Hold | A run | `ClassSession` |' '| Center | A centre | `Center` |')
expect 0 "every name is in the glossary: passes" - "$good"

unknown=$(context unknown.md '| Hold | A run | `ClassSession` |' '| Kapacitet | Seats | `Capacity` |')
expect 1 "a name that is not in the glossary: fails, naming it and the file" 'unknown\.md.*Capacity' "$unknown"

other_table=$(context other-table.md '| Medlem | A member | `NotATerm` |')
expect 1 "a name only in another glossary table: fails" 'NotATerm' "$other_table"

id_ok=$(context id-ok.md '| Center | Its id | `CenterId` (a `Guid`) |')
expect 0 "<Type>Id with Type in the glossary: passes" - "$id_ok"

id_bad=$(context id-bad.md '| Medlem | Its id | `MemberId` (a `Guid`) |')
expect 1 "<Type>Id with Type not in the glossary: fails" 'MemberId' "$id_bad"

prop_ok=$(context prop-ok.md '| Sen afmelding | Late | `ReservationCancelled.IsLateCancellation` |' '| Medlemskab | Pause | `Membership.PausedFrom` |')
expect 0 "Type.Property with Type in the glossary: passes" - "$prop_ok"

prop_bad=$(context prop-bad.md '| Pause | A period | `Booking.PausedFrom` |')
expect 1 "Type.Property with Type not in the glossary: fails" 'Booking\.PausedFrom' "$prop_bad"

paren=$(context paren.md '| Medlemstype | The kind | `Membership` (`Standard`, `Guid`) |')
expect 0 "names in parentheses are notes, not checked: pass" - "$paren"

models_ok=$(context models-ok.md '| Center | Reference data | `Models/Center` |')
expect 0 "Models/<Type> with Type in the glossary: passes" - "$models_ok"

models_bad=$(context models-bad.md '| Holdtype | Reference data | `Models/ClassType` |')
expect 1 "Models/<Type> with Type not in the glossary: fails" 'ClassType' "$models_bad"

excepted=$(context excepted.md \
  '| Betalingsudbyder | The provider | `IPaymentProvider`, implemented by the ACL `PaymentProviderClient` |' \
  '| Pause | A period | `Membership.PausedFrom`, `PausedTo` |')
expect 0 "the two listed exceptions pass" - "$excepted"

only_listed=$(context only-listed.md '| Pause | A period | `Booking.PausedTo` |')
expect 1 "an exception is the exact name, not any name ending the same: fails" 'Booking\.PausedTo' "$only_listed"

none=$(context none.md '| Konto | A login | `Unlisted` |')
sed -i.bak 's/^## Language in this context/## Something else/' "$none" && rm -f "$none.bak"
expect 0 "a file without the language table: passes" - "$none"

if "$script" "$root"/src/*/CONTEXT.md >/dev/null 2>&1; then
  echo "PASS  the repo's own GLOSSARY.md and every src/*/CONTEXT.md agree"
else
  echo "FAIL  the repo's own GLOSSARY.md and every src/*/CONTEXT.md agree"
  "$script" "$root"/src/*/CONTEXT.md 2>&1 | sed 's/^/      /'
  failures=$((failures + 1))
fi
if "$script" >/dev/null 2>&1; then
  echo "PASS  no arguments checks src/*/CONTEXT.md against GLOSSARY.md: passes"
else
  echo "FAIL  no arguments checks src/*/CONTEXT.md against GLOSSARY.md: passes"
  "$script" 2>&1 | sed 's/^/      /'
  failures=$((failures + 1))
fi

[ "$failures" -eq 0 ] || exit 1

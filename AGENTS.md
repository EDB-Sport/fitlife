# Instructions for coding agents

This repo is built by a group where everyone uses their own agent setup. The shared rules are in `docs/METHODOLOGY.md`: read it before you start. These are the rules agents most often skip.

## Pull requests

Follow `docs/PR_RULES.md`. CI fails a PR whose description doesn't follow the template.

**A red CI run:** run `gh run view <run id>` first. Its ANNOTATIONS name each failing check and its first error; the run's summary page in the browser adds a hint. Only then use `gh run view --log-failed`, which can be very long (#132).

## Test and CI logs

- **Local tests:** `dotnet test -c Release -v q -tl:off --logger "console;verbosity=minimal" 2>&1 | grep -E -A15 '^\s*Failed |error [A-Z]+[0-9]+:|^(Passed|Failed)!' | head -120`. It shows the failing tests, compiler errors and each project's summary. No `Passed!` or `Failed!` line means the run did not finish.
- **CI log:** `gh run view <run id> --log-failed | cut -f3- | sed 's/^[^ ]* //' | grep -E -B3 -A12 '(^FAIL|error|##\[error\])' | head -80`. It shows the failing steps' error lines.

## Code

- **Tests first:** start with a failing test or executable check (`docs/METHODOLOGY.md` section 4).
- **Names come from `GLOSSARY.md`.** Never use a name from its **Avoid** column. A new domain term goes into the glossary in the same PR.
- **Build only what the issue asks for.** Extras go in a new issue (`docs/METHODOLOGY.md` section 5).

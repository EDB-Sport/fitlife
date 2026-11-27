# Instructions for coding agents

This repo is built by a group where everyone uses their own agent setup. The shared rules are in `docs/METHODOLOGY.md`: read it before you start. These are the rules agents most often skip.

## Pull requests

Follow `docs/PR_RULES.md`. CI fails a PR whose description doesn't follow the template.

**Writing the PR body:** `tools/dev/pr-body.sh <issue> > body.md` prints the template filled in for that issue; edit it, then `gh pr create --base develop --body-file body.md` (#523).

**Before a review:** run `tools/dev/review-ready.sh <pr>` (5 seconds; on WSL `GH=gh.exe tools/dev/review-ready.sh <pr>`). It checks required checks, conflicts, broken stacks and stale CI (a draft PR can still be reviewed, #470). Not ready means no review.

**Trying a PR by hand:** `tools/dev/preview.sh <pr>` runs the PR's head in its own worktree with the compose stack and prints the URL and the demo log-ins (#521).

**A red CI run:** run `gh run view <run id>` first. Its ANNOTATIONS name each failing check and its first error; the run's summary page in the browser adds a hint. Only then use `gh run view --log-failed`, which can be very long (#132).

**Before every push:** run `tools/dev/check.sh` (restore, build, format, contracts, test, and the `*.test.sh` when you touched `tools/` or `.github/`; `--all` forces them, `--skip <name>` leaves one out). It prints `all checks green` or one FAIL block per failed check. Push once per fix round, not once per fix (#603).

**Waiting for a PR's checks:** `tools/dev/wait-ci.sh <pr>` waits, prints `all checks green` on success, and otherwise prints only the checks that are not green (#377).

## Test and CI logs

- **Local tests:** `dotnet test -c Release -v q -tl:off --logger "console;verbosity=minimal" 2>&1 | grep -E -A15 '^\s*Failed |error [A-Z]+[0-9]+:|^(Passed|Failed)!' | head -120`. It shows the failing tests, compiler errors and each project's summary. No `Passed!` or `Failed!` line means the run did not finish.
- **CI log:** `gh run view <run id> --log-failed | cut -f3- | sed 's/^[^ ]* //' | grep -E -B3 -A12 '(^FAIL|error|##\[error\])' | head -80`. It shows the failing steps' error lines.

## Code

- **Tests first:** start with a failing test or executable check (`docs/METHODOLOGY.md` section 4).
- **Names come from `GLOSSARY.md`.** Never use a name from its **Avoid** column. A new domain term goes into the glossary in the same PR.
- **Build only what the issue asks for.** Extras go in a new issue (`docs/METHODOLOGY.md` section 5).
- **`docs/legacy/` is history: never follow or cite it.** What is built and why is in `docs/TECHNICAL_REFERENCE.md`.

# Instructions for coding agents

This repo is built by a group where everyone uses their own agent setup. The shared rules are in `docs/METHODOLOGY.md`: read it before you start. These are the rules agents most often skip.

## Pull requests

Follow `docs/PR_RULES.md`. CI fails a PR whose description doesn't follow the template.

**Before a review:** run `tools/dev/review-ready.sh <pr>` (5 seconds; on WSL `GH=gh.exe tools/dev/review-ready.sh <pr>`). It checks required checks, conflicts, broken stacks and stale CI (a draft PR can still be reviewed, #470). Not ready means no review.

**A red CI run:** run `gh run view <run id>` first. Its ANNOTATIONS name each failing check and its first error; the run's summary page in the browser adds a hint. Only then use `gh run view --log-failed`, which can be very long (#132).

## Code

- **Tests first:** start with a failing test or executable check (`docs/METHODOLOGY.md` section 4).
- **Names come from `GLOSSARY.md`.** Never use a name from its **Avoid** column. A new domain term goes into the glossary in the same PR.
- **Build only what the issue asks for.** Extras go in a new issue (`docs/METHODOLOGY.md` section 5).

# Instructions for coding agents

This repo is built by a group where everyone uses their own agent setup. The shared rules are in `docs/METHODOLOGY.md`: read it before you start. These are the rules agents most often skip.

## Pull requests

Follow `docs/PR_RULES.md`. CI fails a PR whose description doesn't follow the template.

**A red CI run:** read the run's summary page first (`gh run view <run id>`), which names the failing check and its first error. Only then use `gh run view --log-failed`, which can be very long (#132).

## Code

- **Tests first:** start with a failing test or executable check (`docs/METHODOLOGY.md` section 4).
- **Names come from `GLOSSARY.md`.** Never use a name from its **Avoid** column. A new domain term goes into the glossary in the same PR.
- **Build only what the issue asks for.** Extras go in a new issue (`docs/METHODOLOGY.md` section 5).

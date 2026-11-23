# Instructions for coding agents

This repo is built by a group where everyone uses their own agent setup. The shared rules are in `docs/METHODOLOGY.md`: read it before you start. These are the rules agents most often skip.

## Pull requests

- **Fill in every section of `.github/pull_request_template.md`.** The `PR description` check fails the PR when:
  - there is no `Closes #<issue number>`
  - "What changed and why" is empty
  - the Tests section doesn't say whether an existing test was changed
- **`gh pr create --fill` and `--body` skip the template.** Copy the template to a file, fill it in, and run:
  `gh pr create --base develop --body-file <file>`
- If the check fails, edit the description (`gh pr edit <number> --body-file <file>`); the check runs again.
- Target `develop`, from a branch named `feature/<issue>-<name>`. One issue, one branch, one PR.
- Changed or removed an existing test? Say which one and why in the PR. Reviewers look hard at this.

## Code

- **Tests first:** start with a failing test or executable check (`docs/METHODOLOGY.md` section 4).
- **Names come from `GLOSSARY.md`.** Never use a name from its **Avoid** column. A new domain term goes into the glossary in the same PR.
- **Build only what the issue asks for.** Extras go in a new issue (`docs/METHODOLOGY.md` section 5).

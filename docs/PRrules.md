# PR rules

Checked by CI (`PR description`, `tools/ci/check-pr-body.sh`):

1. Link the issue: `Closes #<issue number>`.
2. Fill in **What changed and why**: one or two lines.
3. In **Tests**, tick "No existing test was changed or removed", or say which test changed and why.

Also required (`docs/METHODOLOGY.md` sections 1–3):

4. One issue, one branch, one PR. Branch `feature/<issue>-<name>` from `develop`; the PR targets `develop`.
5. Tests first: the change starts with a failing test or executable check.
6. CI is green and at least one other group member approves before merge.
7. Merge with a merge commit, not squash.

Agents: `gh pr create --fill` and `--body` skip the template. Fill in `.github/pull_request_template.md` in a file and use `gh pr create --base develop --body-file <file>`.

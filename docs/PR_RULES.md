# PR rules

Checked by CI (`PR description`, `tools/ci/check-pr-body.sh`):

1. Link the issue: `Closes #<issue number>`, or `Refs #<issue number>` when the PR is only part of the issue. Release and hotfix PRs into `main` have no issue.
2. Fill in **What changed and why**: one or two lines.
3. In **Tests**, tick "No existing test was changed or removed", or say which test changed and why.

The rest of the PR rules live in `docs/METHODOLOGY.md`: one issue, one branch, one PR (section 1), branches and merging (section 2.1), review (section 3) and tests first (section 4).

Agents: `gh pr create --fill` and `--body` skip the template. Fill in `.github/pull_request_template.md` in a file and use `gh pr create --base develop --body-file <file>`.

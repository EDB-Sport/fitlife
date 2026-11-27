# PR rules

Checked by CI (`PR description`, `tools/ci/check-pr-body.sh`):

1. Link the issue: `Closes #<issue number>`, or `Refs #<issue number>` when the PR is only part of the issue. Release and hotfix PRs into `main` have no issue.
2. Fill in **What changed and why**: one or two lines.
3. In **Tests**, tick "No existing test was changed or removed", or say which test changed and why.

The template's **What to verify and how** section is optional, for feature PRs only; CI doesn't require it.

Not checked by CI: assign yourself to the issue when you start, before you open the PR, so nobody else starts the same work.

Not checked by CI: a PR that adds or changes an endpoint or a page updates its row in `docs/domain/ACCESS.md`.

**Lock file diff filtering**: `.gitattributes` marks `**/packages.lock.json` as `linguist-generated`, so GitHub collapses it in the PR diff. Local `git diff` still shows it. `gh pr diff <pr>` ignores `.gitattributes`: add `--exclude '**/packages.lock.json'` to hide lock files. A lock file change can hide a dependency change, so the PR description must name the packages that changed (locked-mode restore in CI, see issue #47).

The rest of the PR rules live in `docs/METHODOLOGY.md`: one issue, one branch, one PR (section 1), branches and merging (section 2.1), review (section 3) and tests first (section 4).

Agents: `gh pr create --fill` and `--body` skip the template. Fill in `.github/pull_request_template.md` in a file and use `gh pr create --base develop --body-file <file>`.

## Review example

A review comment follows the verdict format from `docs/METHODOLOGY.md` section 3:

```
Agent review: approve

- All acceptance criteria are met.
- Tests express the behaviour correctly.
- No scope creep.

Reviewed at `a1b2c3d`
```

For a rejection:

```
Agent review: request changes

- The domain invariant is not protected by the aggregate root.
- Tests do not fail when the rule is removed.
- Cross-context event schema is missing.

Reviewed at `a1b2c3d`
```

The ``Reviewed at `<sha>` `` line records the head sha the reviewer read, as required by `docs/METHODOLOGY.md` section 3.

# Review prompt

The shared prompt for an agent review of a pull request (`METHODOLOGY.md` section 3). Paste it into your own agent setup and replace `<n>` with the PR number.

It checks what CI can't. Tests, the TEAM analyzers, Stryker and the required jobs already cover the technical detail, so the review doesn't repeat them.

```
Review PR #<n> in EDB-Sport/fitlife against docs/METHODOLOGY.md and docs/PR_RULES.md.

Before anything else: run `gh pr view <n> --json latestReviews`. If another member has
already reviewed and the author hasn't asked for a second review, stop (METHODOLOGY section 3).
Re-reviews go to the member who reviewed first: if that is you, carry on; a re-review is not a reason to stop.

Don't re-run what CI already proved. Read the diff and the CI result.
If the PR is stacked, read the diff against its base branch, not develop.

If this is a re-review (the PR has an earlier "Reviewed at <sha>" comment):
read only `git diff <reviewed-sha>..<new-head>` plus the reasons from the earlier
review. Say which of those reasons are resolved. Don't re-read the whole PR.

First run `tools/dev/review-ready.sh <n>`. If it says "not ready", that is the review: give those
lines as the reasons, and don't read the diff. Only a ready PR goes on to the checks below.

Check, in this order, and report only real findings:
1. Existing tests: was any existing test changed, removed, skipped or weakened?
   Does the PR body explain it? (section 3)
2. Scope: does the diff do only what the linked issue asks?
   Flag extras and unrelated refactoring (sections 1 and 5).
3. Tests first: does a failing test or executable check come before the change,
   and would it fail without the change? (section 4)
4. Consistency: does it contradict an ADR, METHODOLOGY.md, a README or another
   open PR (same lines, same numbering)?
5. Domain: if a domain model is touched, go through the checklist in section 11.3.
6. PR body: Closes or Refs #<issue>, "What changed and why", and the Tests section.

Verdict: approve, or request changes with at most three concrete reasons,
each with file:line. Off-topic remarks go in a separate comment and never block.
Write it so it can be pasted as a GitHub review. Keep it short.
End the review with the line ``Reviewed at `<sha>` `` (the head sha you read).
```

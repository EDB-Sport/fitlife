Closes #

## What changed and why
<!-- One or two lines. -->

## What to verify and how
<!-- Optional, for feature PRs only: e.g. which request to send or page to open, and what to see. Delete this section if it isn't a feature PR. -->

## Tests
- [ ] The change started with a failing test (or executable acceptance check)
- [ ] No existing test was changed or removed
Did you touch an existing test? Say which one and why on the lines below; reviewers look hard at this (METHODOLOGY §3).


## Domain (only if a domain model is touched, METHODOLOGY §11.3)
- [ ] New or changed terms are in `GLOSSARY.md` and in `CONTEXT.md`
- [ ] State changes go through methods on the aggregate root, with no public setters
- [ ] Other aggregates and contexts are referenced by Id only
- [ ] The business rule is in the model, not in the controller or application service
- [ ] The invariant has a test that fails without the rule
- [ ] No code from another service is used, and cross-context events have a schema

Closes #

## What changed and why
<!-- One or two lines. -->

## Tests
- [ ] The change started with a failing test (or executable acceptance check)
- [ ] No existing test was changed or removed
<!-- If an existing test was changed or removed, say which and why. Reviewers look hard at this (METHODOLOGY §3). -->

## Domain (only if a domain model is touched, METHODOLOGY §11.3)
- [ ] New or changed terms are in `GLOSSARY.md` and in `CONTEXT.md`
- [ ] State changes go through methods on the aggregate root, with no public setters
- [ ] Other aggregates and contexts are referenced by Id only
- [ ] The business rule is in the model, not in the controller or application service
- [ ] The invariant has a test that fails without the rule
- [ ] No code from another service is used, and cross-context events have a schema

# ADR 006: Kanban flow and review signoff instead of Scrum sprints and pair programming

- **Status:** proposed
- **Date:** 2026-11-23
- **Deciders:** to be confirmed by the group (issue #14)

## Context

Agil udvikling teaches Scrum for project management and XP practices for engineering, including pair programming. Its exam report asks about both, and about how the group chose and adapted its method.

This group works differently from the team those practices assume. Every member uses their own multi-agent setup, and agents write most of the code (`METHODOLOGY.md`, Purpose). What the group shares is what leaves each machine: issues, branches, tests, CI, reviews and the glossary. Work arrives as many small, independent issues (one issue, one branch, one PR), not as a sprint backlog that the whole team works through together.

Two things in the textbook practices fit poorly here:
- **Fixed sprints** with planning, daily stand-ups, review and retrospective add ceremonies whose main job, keeping a co-working team in sync, is already done by the board and CI. Work finished on day two of a sprint would wait for the sprint review before it counts.
- **Pair programming** assumes two people at one keyboard. Pairing a person with their own agent, or one agent with another, gives no independent second opinion: generated code that fakes success usually does it by weakening a test, and the agent that wrote the test is the worst one to notice.

## Decision

**Kanban instead of Scrum sprints.** The GitHub Projects board runs as a continuous flow: To Do → In Progress → Review → Done. Each issue belongs to the milestone of the release it is meant for, and a release is cut from `develop` when its milestone is done (`METHODOLOGY.md` section 2.1). Work in progress is limited by the rule of one open PR at a time per area (`METHODOLOGY.md` section 10).

**Review signoff instead of pair programming.** No PR merges without at least one signoff from a group member other than the author, or an agent controlled by one (`METHODOLOGY.md` section 3). Changes to existing tests get extra scrutiny. The independent check that pairing gives is provided by the review and by deterministic CI checks instead: tdd-check (tests first, nothing removed or skipped, red on base), Stryker and the required CI jobs (#4, #5).

The other XP practices are kept: test-first, small releases, continuous integration, simple design, coding standards in the build, explicit refactoring, a system metaphor (the glossary) and collective code ownership (`METHODOLOGY.md` section 10).

## Alternatives considered

- **Scrum with two-week sprints, as the course teaches.** Gives clear rhythm and the ceremonies the report can describe directly. Rejected: the ceremonies duplicate what the board and CI already show, and the work doesn't need batching into sprints.
- **Scrumban: Kanban flow with a regular retrospective.** A reasonable middle ground. Not chosen for now, but can be adopted later without changing anything else: hold a retrospective at each release milestone.
- **Pair programming between people.** The strongest form of review. Rejected as the default: each member works through their own agent setup, so a standing pair would mostly watch agents. Nothing stops two members from pairing on a hard issue, and the review rule still applies to the result.
- **No mandatory review, CI only.** Fastest. Rejected: CI can check that tests exist and fail without the change, but not that they test the right behaviour.

## Consequences

- Easier: finished work merges as soon as it is reviewed and green, without waiting for a sprint boundary. Every PR has an independent reviewer, which also gives the report real review comments as evidence.
- Harder: no sprint velocity or burndown to report. Progress is shown by the board, the milestones and cycle time from issue to merge instead. Reviews can become a bottleneck, so a PR waiting for signoff blocks its author's next PR in the same area.
- Must do: describe this in the Agil udvikling report as a deliberate adaptation of Scrum and XP, with this ADR as the reasoning. Collect the evidence listed in `docs/CSHARP_SETUP_TODO.md` section 12: board screenshots, a PR with real review comments, and a PR blocked by a failing check.

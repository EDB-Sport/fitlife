# Methodology: working together with personal multi-agent setups

## Purpose

This is the shared way of working for a group where every member uses their own multi-agent setup. Each person picks their own tools, models, number of agents and roles. This document does not prescribe any of that.

What we share is everything that leaves your machine: issues, branches, tests, CI, reviews and the shared vocabulary. If your work meets the shared contract below, how you produced it is your own business.

## 1. The shared contract

These rules apply to everyone, whatever setup they use:

1. **One issue, one branch, one pull request.** Keep the issue small enough to review in one sitting.
2. **Tests first.** A change starts with a failing test, or an executable acceptance check, that describes the behaviour (section 4).
3. **CI must be green.** No merge with failing checks, including the API tests (section 7).
4. **Review is mandatory.** Every PR needs at least one signoff before merge.
5. **Use the shared vocabulary.** Names in code, issues and tests come from the glossary (sections 6 and 11).
6. **Keep it simple.** Build what the issue asks for and nothing more (section 5).
7. **Refactoring is its own issue.** It isn't hidden inside a feature PR (section 8).
8. **Git Flow.** Work branches off `develop` and merges back into it. Only release and hotfix branches go to `main` (section 2.1).

## 2. Workflow

```
issue → failing test → simplest implementation → refactor (if small) → PR → CI → at least one signoff → merge
```

- **Issue:** states the behaviour, the acceptance criteria and which glossary terms it touches. Issues live on the group's GitHub Projects board (To Do → In Progress → Review → Done), and each one belongs to the milestone of the release it is meant for.
- **Branch:** created from `develop` and named after the issue, e.g. `feature/12-reserve-class-session`.
- **Commit:** small and focused. Don't commit generated noise, local agent config or logs. Optionally, prefix commits with `red:`, `green:` or `refactor:` so the TDD cycle shows in the history.
- **PR:** targets `develop`. It says `Closes #<issue>`, which links and auto-closes the issue, or `Refs #<issue>` when the PR is only part of the issue. Release and hotfix PRs into `main` have no issue. It says in one or two lines what changed and why. It also says whether any existing test was changed, and why.

### 2.1 Branching: Git Flow

| Branch | Created from | Merges into | Purpose |
|---|---|---|---|
| `main` | – | – | Released code only. Every commit is a release, tagged with its versions |
| `develop` | `main` (once) | – | Integration branch. Always green, always deployable to the test environment (the compose stack) |
| `feature/<issue>-<name>` | `develop` | `develop` | One issue, one branch (section 1) |
| `release/<x.y.z>` | `develop` | `main` and `develop` | Freezes a release: only version bumps, fixes and docs. No new features |
| `hotfix/<service>-<x.y.z>` | `main` | `main` and `develop` | Urgent fix to one released service |

Rules:
- **Nobody pushes directly to `main` or `develop`.** Both are protected branches. Changes go in by PR with green CI and at least one signoff (section 3).
- **Each service has its own version,** using Semantic Versioning (MAJOR.MINOR.PATCH). It lives in the service's `.csproj`, and each service is tagged `<service>/vX.Y.Z`, e.g. `bookingservice/v1.3.0`. The Blazor UIs are versioned the same way. A service that hasn't changed keeps its version.
- **Releases:** create `release/x.y.z` from `develop`. Here `x.y.z` numbers the release as a whole. On the branch, raise the version of every service that changed since its last tag. Then open a PR to `main` and merge it. CI tags the merge commit `vx.y.z` plus one `<service>/vX.Y.Z` tag per released service, and pushes the images. Finally merge the release branch back into `develop`.
- **Deploys:** every release on `main` is deployed to Azure by the deploy workflow, after one group member approves it on the `production` environment (`CSHARP_SETUP_TODO.md` section 9a). Nobody deploys by hand or changes Azure in the portal.
- **Hotfixes:** create `hotfix/<service>-<x.y.z>` from `main` and raise that service's PATCH number. Merge into `main`, where CI tags and deploys it like a release, then merge into `develop` so the fix isn't lost.
- **CI checks the versions** before a release or hotfix can merge: every changed service has a higher version, a hotfix raises only PATCH, and a breaking API change raises MAJOR (`CSHARP_SETUP_TODO.md` section 9).
- **Merge with merge commits, not squash.** That way the network graph shows feature, release and hotfix branches, as `git flow ... finish` would.
- **The `git flow` CLI:** use it to start branches (`git flow feature start ...`). Finish through a PR instead of `git flow ... finish`, because finish merges and pushes locally, and branch protection blocks that.
- **Delete** feature, release and hotfix branches after they are merged.
- **Keep feature branches short-lived.** Rebase on, or merge in, `develop` often to avoid large conflicts between parallel agents.
- **Start every branch from an up-to-date `develop`,** and get a PR signed off and merged before work that builds on it starts. A branch built on an unmerged one re-sends that code in its own diff and every reviewer reads it again (#106).
- **When stacking can't be avoided,** the PR's base is its parent branch, so its diff shows only its own change. Retarget it to `develop` once the parent merges. The `stack-check` (#110) enforces this.

## 3. Review:

No PR merges without at least one signoff: a group member other than the author, or an agent controlled by one, approves the PR on GitHub.

An agent review starts from the shared prompt in `docs/REVIEW_PROMPT.md`, so every review checks the same things (#118).

Rules for reviewers:
- **Changes to existing tests get extra scrutiny.** A weakened or deleted test is the usual way generated code fakes success. The PR has to explain the change.
- **Don't re-run what CI already proved.** Read the diff and the CI result.
- **A re-review reads only the commits since the last review.** When re-reviewing a PR after the author has pushed new commits, review `git diff <reviewed-sha>..<new-head>` plus the earlier review reasons, and say which reasons are resolved; do not re-read the entire PR from the beginning.
- **A review comment records the head sha it reviewed.** Include a line `Reviewed at \`<sha>\`` in the review comment.
- **Domain changes:** when a PR touches a domain model, go through the DDD checklist in section 11.3.
- **Verdict:** approve, or request changes with at most three concrete reasons. Off-topic remarks go in a separate comment and never block.

## 4. Testing

### TDD
Red → green → refactor: write a failing test, make it pass with the simplest code, then clean up while the tests stay green.

### Arrange–Act–Assert
Every test has three parts, in this order:
- **Arrange:** set up the objects, data and fakes the test needs.
- **Act:** do the one thing under test, usually a single call.
- **Assert:** check the result or the resulting state.

One behaviour per test. If you need several Act steps, split the test.

### F.I.R.S.T.
| | Quality | Meaning |
|---|---|---|
| **F** | Fast | Runs in milliseconds, so it gets run all the time. |
| **I** | Independent | Doesn't depend on other tests or on the order they run in. |
| **R** | Repeatable | Same result every time, anywhere. No uncontrolled clock, randomness, network or shared database. |
| **S** | Self-validating | Passes or fails by itself. No one reads logs to decide. |
| **T** | Timely | Written before, or together with, the code it tests. |

**Repeatable** and **Self-validating** matter most when agents write code, because a clear pass/fail signal is how both agents and reviewers know a change works.

## 5. Design principles

- **YAGNI, "You Aren't Gonna Need It":** build only what the current issue needs. No speculative options, abstractions or "just in case" code.
- **KISS, "Keep It Simple, Stupid":** prefer the plainest solution that works.
- **Simple Design (XP):** the simplest thing that works and passes the tests.
- **"Do the simplest thing that could possibly work"** (Kent Beck and Ward Cunningham).

Agents tend to over-build. Reviewers should treat unrequested extras as scope creep and ask for them to be removed or moved to a new issue.

## 6. System metaphor and shared vocabulary

We use one shared language for the domain, in code, tests, issues and conversation, and in the instructions or context each person gives their agents.

- Keep a **glossary** in the repo (e.g. `GLOSSARY.md`): one line per term, with its meaning and the name to use in code.
- New domain concepts are added to the glossary in the same PR that introduces them.
- Point your agents at the glossary. Without it, every fresh context invents its own names for the same concepts.
- The glossary is DDD's ubiquitous language. The full template, with the context, kind and "avoid" columns, is `templates/GLOSSARY.md`. How it fits with bounded contexts is described in section 11.

Each term also gets its schema.org type, because the IT-arkitektur course requires entity names from schema.org (`CSHARP_SETUP_TODO.md`, TEAM008). Example rows, in the template's format:

| Term | Meaning | In code | schema.org | Context | Kind | Avoid |
|---|---|---|---|---|---|---|
| Class session | One scheduled run of a class at a time and place, with a number of seats | `ClassSession` | Event | Booking | Aggregate root | Session, Class, Lesson |
| Reservation | A member's seat in one class session | `Reservation` | Reservation | Booking | Entity | Booking, Signup |

## 7. CI: Newman on GitHub Ubuntu runners

API tests are Postman collections run by **Newman** (Postman's command-line runner) in GitHub Actions on `ubuntu-latest`, on every push and pull request. There are four kinds, in `tests/postman/` (layout in `CSHARP_SETUP_TODO.md` section 6):

| Kind | Folder | Runs against |
|---|---|---|
| API tests, one collection per service, happy path plus error cases | `services/` | the compose stack |
| Integration tests: two or more services working together | `integration/` | the compose stack |
| Mock-server tests: systems not built yet (e.g. the existing membership/payment system, a wearables API) | `mocks/` | Postman Mock Servers |
| End-to-end: the booking flow (log in → find a class session → reserve → see it → cancel) | `e2e/` | the compose stack in CI; Azure after each deploy and nightly |

Sketch of the workflow. Adapt the compose file names to the project:

```yaml
# .github/workflows/api-tests.yml
name: API tests
on: [push, pull_request]

jobs:
  newman:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-node@v4
        with:
          node-version: 22
      - name: Start the stack
        run: docker compose -f deploy/compose/<file>.yml up -d --build --wait   # --wait uses the /health checks
      - name: Install Newman
        run: npm install -g newman newman-reporter-htmlextra
      - name: Run the collections
        run: |
          set -e
          for c in tests/postman/services/*.json tests/postman/integration/*.json tests/postman/mocks/*.mock-tests.postman_collection.json tests/postman/e2e/*.json; do
            name=$(basename "$c" .json)
            newman run "$c" -e tests/postman/environments/ci.postman_environment.json \
              --reporters cli,junit,htmlextra \
              --reporter-junit-export "results/$name.xml" --reporter-htmlextra-export "results/$name.html"
          done
      - uses: actions/upload-artifact@v4
        if: always()
        with:
          name: newman-results
          path: results/
```

- CI runs on every branch, so a PR into `develop` or `main` is never merged without a passing run. Building and pushing versioned Docker images happens only on `main`, from the release tag, and the approved deploy to Azure follows (section 2.1).
- The collections and environment files are committed to the repo. Secrets go in GitHub Actions secrets, never in an environment file. Mock servers hold made-up data only, so they can stay public.
- The HTML reports are the evidence the exam report asks for: result tables for success and error cases, per kind of test.
- The tests must follow F.I.R.S.T. too: CI starts from a known state, and no test depends on data another test left behind. A collection that creates data (a reservation) removes it again, so it can also run against Azure.

## 8. Refactoring as explicit work

Agents rarely refactor unless asked. Instead they copy and adapt, so duplication builds up quietly. That's why refactoring is planned:

- Open a **refactoring issue** when you notice duplication, unclear names or tangled code. Label it `refactor`.
- A refactoring PR changes structure, not behaviour. The existing tests must pass unchanged.
- Keep each refactoring small enough to review in one sitting.
- Small clean-ups inside your own new code are part of TDD's refactor step and don't need a separate issue.

## 9. Personal agent setups: recommendations

These apply only if your setup has the part in question. Ignore what doesn't apply.

- **Run the coordinator from the project folder.** If you use an agent that coordinates other agents, start it with the project folder as its working directory, so it sees the project's files, instructions and glossary.
- **Run the coordinator on a cheaper model.** Coordination is routing and bookkeeping. Save the strongest model for writing code and reviewing it.
- **Fresh context per issue.** Long contexts degrade. Start each issue in a clean session.
- **Keep the process cheap.** Every handoff between agents costs time and tokens. If a change has more handoffs than its size warrants, simplify.
- **Keep personal setup out of the repo.** Agent config, prompts, logs and transcripts stay on your machine unless the group agrees to share them.

## 10. XP practices: what we use

| Practice | How we use it |
|---|---|
| Test-first | Section 4. |
| Small releases | One small issue per PR. Versioned releases from `develop` to `main` via Git Flow (section 2.1). |
| Continuous integration | CI on every push and PR (section 7). Merge often. |
| Simple design / YAGNI | Section 5. |
| Coding standards | Enforced by linters and formatters in CI where possible, not only written rules. Setup: `CSHARP_SETUP_TODO.md`. |
| Refactoring | Explicit refactoring issues (section 8). |
| System metaphor | Shared glossary (section 6), used as DDD's ubiquitous language with bounded contexts (section 11). |
| On-site customer | Questions about requirements are collected and settled with the group or teacher, not guessed by an agent. |
| Pair programming | Replaced by independent review signoff. No agent-to-agent pairing. |
| Collective code ownership | Anyone may change any code, but only one open PR at a time per area, to avoid parallel agents colliding. |

Background and reasoning: `notes/xp_practices_for_agentic_code.md`.
## 11. Domain-driven design

We cut the system into services, and name things, with domain-driven design (DDD), as taught in IT-arkitektur M4. The steps for getting there are in `DDD_PLAN.md`. The templates are in `templates/`.

DDD matters more with agents, not less. A fresh agent context has no memory of the domain, so it invents names, merges concepts, and puts business rules in controllers. The model has to be written down where the agent reads it, and checked where the agent can't skip it.

### 11.1 Where the model lives

| File | Holds | Changed by |
|---|---|---|
| `GLOSSARY.md` | The ubiquitous language: term, meaning, code name, schema.org type, context, kind, names to avoid | The PR that introduces or changes the term |
| `docs/domain/CONTEXT-MAP.md` | Core capability, subdomains, bounded contexts, the relations between them, events that cross a boundary | A group decision: its own issue plus an ADR |
| `src/<Service>/CONTEXT.md` | That context's language, aggregates, invariants, commands, queries, events and allowed dependencies | The PR that changes the model |

### 11.2 Rules

1. **One bounded context = one service = one database.** Boundaries follow business capabilities and language, never technical layers ("a database service", "a UI service").
2. **People decide the boundaries.** An agent may draft and challenge the model, but it doesn't decide it. Moving a boundary is its own issue, with an ADR.
3. **Language first.** A new term goes into `GLOSSARY.md`, and into the context's `CONTEXT.md`, in the same PR as the code. Names from the **Avoid** column are refused in review.
4. **Aggregates protect their invariants.** State changes only through methods on the root, with no public setters in `*.Domain` (TEAM011). Other aggregates are referenced by Id only. There is one repository for each aggregate root, and none for a child.
5. **Business rules live in the domain model,** not in controllers or application services. No anemic model.
6. **Names follow their kind.** Domain events are in the past tense (`ReservationMade`), commands are in the imperative (`ReserveSeatCommand`), and queries are `Get<Noun>Query`.
7. **Contexts meet only through APIs and events.** An event that crosses a boundary has a schema in `contracts/events/` and a row in the context map. External systems sit behind an anticorruption layer, so their names never reach our domain model.
8. **Keep it simple.** A context with no real business rules may stay plain CRUD (controller → repository, types in `*.Models`). Add a `*.Domain` layer when the first invariant appears (section 5).

### 11.3 Tests and review

- **Every invariant has a unit test, written first** (section 4). The reviewer asks the M4.02 question: *does the test fail if the rule is removed from the model?* If not, the rule doesn't live where we think it does.
- **Don't mock domain classes.** Mock only the boundaries: repositories, other services' clients, the clock.
- **Review checklist** for a PR that touches a domain model:
  - [ ] New or changed terms are in `GLOSSARY.md` and in `CONTEXT.md`
  - [ ] State changes go through methods on the aggregate root, with no public setters
  - [ ] Other aggregates and contexts are referenced by Id only
  - [ ] The business rule is in the model, not in the controller or application service
  - [ ] The invariant has a test that fails without the rule
  - [ ] No code from another service is used, and cross-context events have a schema

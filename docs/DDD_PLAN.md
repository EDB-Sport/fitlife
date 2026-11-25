# A simple plan for domain-driven design in FitLife Digital

How the group gets from the project description to services whose boundaries and names come from the domain, and how it keeps agents inside that model. The rules are in `METHODOLOGY.md` section 11. This document covers the steps and the reasons.

**In short:** people model the domain together once, at the start. The model is then written down in three kinds of file that agents read: a glossary, a context map, and one context file for each service. The parts of it that can be checked are checked by the build, by tests and by review.

## 1. Why DDD, and why it matters more with agents

**The course (IT-arkitektur M4) says:**
- DDD gives the business and the developers one shared model and language, so services line up with the business and can evolve.
- Good service boundaries follow the business's language and rules, so a business change lands in one service, not five.
- "Why is the system cut this way?" is a central exam question (`itarkitektur notes/coursenotes/STUDY_GUIDE.md`). The context map and its ADR are the answer.

**What agents add to the problem.** Sources on AI-assisted development (section 5) agree on three failure modes:
- **Drifting names.** Every fresh context invents its own names for the same concept, or merges two concepts under one name.
- **Anemic models.** Agents default to framework-driven code: data classes with public setters, and the business rules spread across controllers.
- **Broken invariants.** Generated code lets callers change an aggregate's internals directly, because the model has no lasting memory of the domain's rules.

**The countermeasures.** Treat the glossary as a constraint, not as documentation. Use each bounded context as a scope fence for the agent working in it. Test every invariant so that breaking it turns CI red.

## 2. What the course method asks for (M4)

**Strategic DDD:** the big map.
1. Business capability: "the ability to ...", the *what*, not the how or who.
2. Business functions: recurring tasks, named with verbs.
3. Subdomains: core (build it), supporting, or generic (buy or integrate). "If everything is core, nothing is core."
4. Bounded contexts: each has its own language, model and data. Use the **language test**: one word with two meanings means two contexts.
5. Context map: Partnership, Shared Kernel, Customer–Supplier, Conformist, Anticorruption Layer, Open Host Service / Published Language, Separate Ways.

**Tactical DDD:** inside one context.
- **Building blocks:** entities, value objects, and aggregates. An aggregate has a root that protects its invariants and references other aggregates only by Id.
- **Naming:** domain events are in the past tense, commands in the imperative, and queries are `Get...Query`.
- **Domain services:** for logic that belongs to no single entity.
- **Storage:** repositories for aggregate roots only, and one database per context.
- **Model rules:** no public setters and no anemic model.
- **Test rule:** a unit test of an invariant must fail if the rule is removed.

**Newman, *Building Microservices*, ch. 2:** information hiding, high cohesion and low coupling. Model by business capability, never by technical layer.

## 3. The plan

| Step | What | Who | Output |
|---|---|---|---|
| 1 | Strategic model | The whole group, one session | `docs/domain/CONTEXT-MAP.md` + ADR |
| 2 | Ubiquitous language | The whole group, right after step 1 | `GLOSSARY.md` |
| 3 | One context = one service | One person | `src/<Service>/` folders, each with a `CONTEXT.md` |
| 4 | Tactical model for each context | One pair per context | Aggregates and invariants in `CONTEXT.md`, issues on the board |
| 5 | Guardrails | Maintainers | Analyzers, build rules, review checklist |
| 6 | TDD on invariants | Everyone, in every issue | A unit test for each invariant |
| 7 | Integration between contexts | The owners of both contexts | Event schemas, ACLs, contract tests |
| 8 | Keep the model alive | Everyone | ADRs, refactoring issues |

### Step 1: Strategic model (people decide)

- **Worksheet:** the course template `itarkitektur notes/coursenotes/ITA-F25V---.../viewer/files/Modul 4/Opgaver/HaaV-domænemodel.md`, applied to FitLife instead of HaaV.
- **Inputs:** the project description (*F25v Projektbeskrivelse*), and interviews or notes on how FitLife's people talk about their work.
- **Using an agent:** let it draft candidate capabilities, functions and words for the language test from the project description, and let it challenge your grouping ("which word means two things here?"). The group makes the choices. Research on LLM-run DDD finds models good at drafting the language and simulating event storming, but the boundaries are a business judgement.
- **Done when:**
  - `templates/CONTEXT-MAP.md` is copied to `docs/domain/CONTEXT-MAP.md` and filled in
  - each bounded context is named and mapped to one service
  - an ADR ([`docs/adr/007-bounded-contexts.md`](adr/007-bounded-contexts.md)) says why the contexts are cut this way, and which alternative cut was rejected

*An illustration only, to be decided in the session:* the language test might show that "session" means a login session to IdentityService and a scheduled class to BookingService, and that "member" means anyone who can log in to one and a person with a paid membership to the other. Each double meaning points at a boundary, and each gets its own name in code (`LoginSession`/`ClassSession`, `Account`/`Member`).

### Step 2: Ubiquitous language

- Copy `templates/GLOSSARY.md` to the repo root. Turn every result of the language test into rows: one row for each meaning, in each context.
- Fill in **Avoid** for every term. It's the list a reviewer, or an agent, can check against.
- **Done when:** every concept that steps 1 and 4 need has a row. From then on, a new term goes in the glossary in the same PR as the code that uses it.

### Step 3: One bounded context = one service = one folder

- Each context becomes `src/<Service>/`, with its own database (`CSHARP_SETUP_TODO.md` section 1).
- Copy `templates/CONTEXT.md` into each service folder.
- Point your own agent setup at it. With Claude Code, a `CLAUDE.md` in the service folder that contains `@CONTEXT.md` loads it only while the agent works in that folder. That gives each agent the language and rules of exactly one context, which is the scope fence.

### Step 4: Tactical model, one context at a time

- **Worksheet:** the course template `.../Modul 4/Opgaver/HaaV-taktisk-model.md`. Classify each concept as entity, value object, or belonging to another context. Then choose the aggregate root, its invariants, its methods, and its events, commands and queries.
- Put the result into the context's `CONTEXT.md`.
- **Turn invariants into issues.** Roughly one invariant = one issue = one failing test to start from (METHODOLOGY section 2). This keeps issues small and makes each one testable.
- Start with the core subdomain. Supporting contexts with no real rules can stay simple CRUD until a rule appears (METHODOLOGY section 11.2, rule 8).

### Step 5: Guardrails, in tools where possible

The setup's principle is that rules live in the compiler and in CI, not in prose (`CSHARP_SETUP_TODO.md` section 0).

| Rule | Enforced by |
|---|---|
| Model names come from the glossary | TEAM008 analyzer (the "In code" column) |
| No public setters in the domain model | TEAM011 analyzer, for services with a `*.Domain` layer |
| No code shared between contexts | The `Directory.Build.targets` check on cross-service `ProjectReference`s |
| Storage only through repositories | TEAM001 analyzer |
| Event formats match between contexts | JSON Schemas in `contracts/events/`, tested in CI |
| Invariants hold | Unit tests (step 6), with the "fails without the rule" question in review |
| Rules sit in the model, aggregates are referenced by Id, no synonyms | Review checklist (METHODOLOGY section 11.3), plus the rules in each `CONTEXT.md` |

### Step 6: TDD on invariants

- The first test of every aggregate tests an invariant, and it is written before the method that guards it.
- *Example:* `Reserve_WhenClassSessionIsFull_IsRejected` comes before `ClassSession.Reserve(...)` exists.
- Don't mock domain classes. Mock the repository, other services' clients, and the clock.
- **Done when:** every row in a context's invariant table names a test, and that test fails when the rule is commented out.

### Step 7: Integration between contexts

- Contexts talk only through REST APIs (with OpenAPI contracts) and RabbitMQ events.
- An event that crosses a boundary is listed in the context map. It has a JSON Schema in `contracts/events/`, and both sides test against it.
- Systems we don't own, such as FitLife's existing membership or payment system (stood in for by a Postman Mock Server), sit behind an **anticorruption layer**: one class that translates their data into our terms at the edge.
- *Example of a cross-context rule:* BookingService refuses a reservation from a member without an active membership, which it learns from MemberService. Note in the context map whether this is a synchronous call or a locally kept copy fed by events, and say why (eventual consistency is a business decision).

### Step 8: Keep the model alive

- **Moving a boundary:** a change to the context map is its own issue, with an ADR.
- **Names drifting:** open a refactoring issue (METHODOLOGY section 8) that renames the code to match the glossary, or updates the glossary with a reason.
- **Before the exam:** check that the context map, the glossary and the code still agree. That is the story of why the services are cut the way they are.

## 4. Templates

| Template | Copy to | Used in |
|---|---|---|
| `templates/CONTEXT-MAP.md` | `docs/domain/CONTEXT-MAP.md` | Step 1 |
| `templates/GLOSSARY.md` | `GLOSSARY.md` (repo root) | Step 2 |
| `templates/CONTEXT.md` | `src/<Service>/CONTEXT.md`, one for each service | Steps 3–4 |

## 5. Sources

**Course material**
- IT-arkitektur M4: *Strategisk DDD modellering*, *Taktisk DDD modellering*, *Microservice design*, and the HaaV worksheets, all in `itarkitektur notes/coursenotes/.../viewer/files/Modul 4/`
- `itarkitektur notes/coursenotes/COURSE_OVERVIEW.md` (M4), `STUDY_GUIDE.md` (Theme 3), `TECH_STACK_AND_METHODS.md` (section 3.2)
- Sam Newman, *Building Microservices*, ch. 2

**DDD and agents.** These are mostly blog posts and talk abstracts. The arXiv papers are the only ones with any evaluation behind them, so read the rest as practitioner opinion.
- [The Domain Glossary Is a Constraint, Not Documentation](https://understandingdata.com/posts/domain-glossary-as-agent-constraint/): a glossary with "avoid" lines, one `CONTEXT.md` for each context plus a context map, and updating the glossary before the code
- [Your agent keeps using that word (DEV / AWS)](https://dev.to/aws/your-agent-keeps-using-that-word--4g36): a language section for each bounded context, in that module's agent file
- [Domain-Driven Design with AI Assistance (developertoolkit.ai)](https://developertoolkit.ai/en/workflows/plan/domain-driven-design/): the glossary in the context files the tool loads, and nested `CLAUDE.md` files per module
- [pro-workflow domain-modeling skill](https://raw.githubusercontent.com/rohitg00/pro-workflow/main/skills/domain-modeling/SKILL.md): a `CONTEXT.md` glossary with decision records, and not inventing terms the project doesn't use
- [Agentic Coding, Intentional Design (Sessionize)](https://sessionize.com/s/jeremydavis/agentic-coding-intentional-design/160121): agents default to framework-driven code and anemic models
- [The Productivity-Reliability Paradox (arXiv 2605.01160)](https://arxiv.org/pdf/2605.01160): generated code breaking DDD invariants
- [LLM-supported DDD prompting framework (arXiv 2603.26244)](https://arxiv.org/html/2603.26244v1): LLMs drafting the ubiquitous language and running event storming
- [Domain-driven design matters more when AI writes your code (daily.dev)](https://daily.dev/posts/domain-driven-design-matters-more-when-ai-writes-your-code-mazbsxiug), [Is DDD still relevant in the vibe-coding era? (dev.to)](https://dev.to/juliashevchenko/is-ddd-still-relevant-in-the-vibe-coding-era-5cam), [DDD for AI agents (slavadubrov)](https://slavadubrov.github.io/blog/2025/10/20/domain-driven-design-ai-agents/): consistent naming and bounded contexts as scope for agents

# ADR 007: Bounded contexts for FitLife Digital

- **Status:** proposed, **open questions** (see the end). Not decided.
- **Date:** 2026-11-23
- **Deciders:** to be filled in at the group session (issue #2)

> Numbered 007 because #14 reserves 004–006.

## Context

The PoC must deliver "en eller flere centrale komponenter af FitLife Digital" as microservices [PB p. 10], and the IT-arkitektur report must explain the design process "fra målbillede til microservices (f.eks. jf. DDD)". "Why is the system cut this way?" is also a likely exam question (`DDD_PLAN.md` section 2).

Inputs: the case [PB p. 3–6], the group's simulated interviews [I1–I4], the target architecture from part 1, the TO-BE context document (version 2026-11-23) and the course method in *Læringsguide M4.01*. Source abbreviations are explained at the top of `docs/domain/CONTEXT-MAP.md`.

Forces:
- Three of four informants stay because of the trainers or instructors [I1, I3, I4]. All four want digital booking and messages about changes [I1–I4].
- The case requires integration "med eksisterende medlems- og økonomisystemer" [PB p. 5], and the course asks for at least one anticorruption layer towards an existing system [M4.01 p. 11].
- The PoC must handle "brugernes identitet og adgang på en sikker måde" [PB p. 10] and test mocked services with Postman MockServer [PB p. 9].
- At most two subdomains may be core [M4.01 p. 5]. At least three contexts, and at least two of them must come from the core capability [M4.01 p. 10].
- Our own plan says to start with the core subdomain (`DDD_PLAN.md` step 4). #18 (first service) and #24 (nightly booking flow) assume booking is built early.
- The target architecture from part 1 has two core subdomains: class scheduling and booking, and personal training. A review of part 1 called booking-as-core its weakest argument.

## Decision (draft)

**What the draft proposes regardless of Q1:**
- Contexts **Booking, Membership, Notification and Identity**, with Consent and Access next and Engagement later (context map section 3).
- Membership is our own context. The 2012 membership system and the payment provider sit behind anticorruption layers in front of it (Q2).
- Identity and payment are generic (Q3).
- The member app, trainer portal, API gateway and MCP server are channels, not contexts.

**Open:** which subdomain is core (Q1), and with it whether Training is in this version at all and which context the PoC builds first (Q7). Option A, B or C below is written in here when the group has chosen.

## Alternatives considered

- **One service per business capability** (the TO-BE context document's architecture style). Rejected as the rule, kept as a starting point: class scheduling and class booking share the same rules on the same class session (capacity, waitlist), so they are one context, Booking. The course cuts by language and rules, not by capability one to one [M4.01 p. 9].
- **Membership as generic, only integrated with the 2012 system** (the first draft of the target architecture). Rejected: the informants ask for pause and flexible memberships [I3, I4], which the 2012 system and the single membership type [PB p. 3] don't support, so we build the rules ourselves.
- **Replace the 2012 system with no integration at all.** Rejected: it ignores the case's integration requirement [PB p. 5] and the course's ACL criterion [M4.01 p. 11].
- **A separate context for the MCP server or the API gateway.** Rejected: they are channels and platform components with no business rules (principle PA3).
- The three core options are compared under Q1 below, not rejected here.

## Consequences

- Each context becomes one service in `src/<Service>/` with its own database and its own `CONTEXT.md` (DDD_PLAN step 3).
- The cross-boundary events in context map section 5 each need a JSON Schema in `contracts/events/` before the services that use them are built.
- Membership needs a Postman Mock Server for the 2012 system and a simulated payment provider. Both also cover the mock-server tests the project description asks for [PB p. 9].
- The answer to Q1 decides #18 (which service is the first one), how the report argues the PoC's scope, and which rows in the glossary are core.
- Moving a boundary later is its own issue with a new ADR that supersedes this one.

## Questions for the group session

The group's answer is written in the last column, and the context map and glossary are changed to match, before the status becomes `accepted`. **Blocks** says what can't happen before the question is answered.

### Q1: Which subdomain is core?

The core capability is the relationship with trainers and instructors members know (context map section 1). The question is which subdomain carries it.

| Option | Core | For | Against |
|---|---|---|---|
| A | Class booking **and** personal training | Same as the target architecture from part 1. The PoC starts with core, as DDD_PLAN says. Two contexts from the core capability [M4.01 p. 10] | Weak reverse test for booking: "why doesn't FitLife buy a booking system?" The answer must be the local class with a known instructor, not the software |
| B | Personal training only; booking is supporting | Passes the reverse test most clearly. Fits the case's strengths and opportunities [PB p. 4–5] | The PoC builds supporting first, against our own "start with core". Changes part 1 |
| C | Class booking only; personal training is not in this version | Smallest scope; the whole PoC is core | Weak reverse test, as in A. The case's personal trainers and training data [PB p. 4–5] go unanswered. Needs a second context from the core capability [M4.01 p. 10], for example by splitting Booking into class planning and reservations. Changes part 1 and the TO-BE context document |

**Blocks:** merging this PR and #18. **Group answer:** _

### Q2–Q7

| # | Question | Draft proposal | Alternative and its price | Blocks | Group answer |
|---|---|---|---|---|---|
| Q2 | What happens to the 2012 membership system? | It is AS-IS only. We build our own Membership context. The 2012 system sits behind an ACL during the transition, as a Postman Mock Server in the PoC, and is retired once all members are migrated (goal M4 in the TO-BE context document) | Leave it out entirely. Simpler, but it breaks the case requirement [PB p. 5] and the course's ACL criterion [M4.01 p. 11] | Merge | _ |
| Q3 | How do we handle identity? | Generic. A minimal own `IdentityService` with test users and roles in the PoC; a bought identity provider in production. MitID is not part of the target architecture or the PoC | A bought provider already in the PoC. Closer to production, but more setup and outside the course's stack | Merge | _ |
| Q4 | Is the MCP server part of the PoC? | A scope question, not a domain one: it is a channel either way. It is our own idea and is in neither the case nor the syllabus | – | Nothing here | _ |
| Q5 | Which language goes in the glossary? | **Term** in Danish, as FitLife and the report speak. **In code** in English with schema.org. Danish "Booking" becomes `Reservation` in code | Everything in English. Simpler for the code, but the link to the report and the interviews gets weaker | Merge (TEAM008 reads the glossary) | _ |
| Q6 | Are the language-test lines in `GLOSSARY.md` right? | The group checks each reference in the interviews and adds a quote with informant code | – | Accepting the ADR | _ |
| Q7 | Which contexts does the PoC build, and which one first? | Follows Q1. Under A or C: Booking first. Under B: Booking first anyway, explained in the report, or Training first | – | #18 | _ |

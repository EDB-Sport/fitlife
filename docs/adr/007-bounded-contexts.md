# ADR 007: Bounded contexts for FitLife Digital

- **Status:** proposed. Jesper's answers (2026-11-23) are filled in and must be confirmed by the group; Q5, Q6, Q8 and Q9 are open.
- **Date:** 2026-11-23
- **Deciders:** to be filled in at the group session (issue #2)

> Numbered 007 because #14 reserves 004–006.

## Context

The PoC must deliver "en eller flere centrale komponenter af FitLife Digital" as microservices [PB p. 10], and the IT-arkitektur report must explain the design process "fra målbillede til microservices (f.eks. jf. DDD)". "Why is the system cut this way?" is also a likely exam question (`DDD_PLAN.md` section 2).

Inputs: the case [PB p. 3–6], the group's simulated interviews [I1–I4], the target architecture from part 1, the TO-BE context document (version 2026-11-23) and the course method in *Læringsguide M4.01*. Source abbreviations are explained at the top of `docs/domain/CONTEXT-MAP.md`.

Forces:
- Three of four informants stay because of the trainers or instructors [I1, I3, I4]. All four want digital booking and messages about changes [I1–I4].
- At most two subdomains may be core [M4.01 p. 5]. At least three contexts, and at least two of them must come from the core capability [M4.01 p. 10].
- The course's checklist for the context map also asks for at least one anticorruption layer towards an existing system, with a reason [M4.01 p. 11] (HaaV's systems in the guide, FitLife's here). With the 2012 ACL rejected below, the draft still has one: the ACL between FitLife's existing finance system [PB p. 5] and Membership (context map section 4). The payment provider and the messaging channels sit behind ACLs too, but the case names neither, so they are new external systems and not existing ones.
- The case's idea for FitLife Digital is to integrate "med eksisterende medlems- og økonomisystemer" [PB p. 5]. It is an idea from the innovation manager, not one of the tasks under "I skal" [PB p. 6], so we may choose otherwise if we argue it.
- The case itself describes the 2012 system as part of the problem: "Der eksisterer ingen integration mellem systemerne" [PB p. 4], and management's SWOT lists outdated IT systems without integration and technical debt and security risks [PB p. 5].
- The PoC must handle "brugernes identitet og adgang på en sikker måde" [PB p. 10] and test mocked services with Postman MockServer [PB p. 9].
- Our own plan says to start with the core subdomain (`DDD_PLAN.md` step 4).

## Decision (draft, to be confirmed)

- **Core:** class scheduling and booking, and personal training (Q1, option A). The same as the target architecture from part 1.
- **Contexts:** Booking and Training (core), Membership, Notification and Identity; Consent together with Training; Access next; Engagement later (context map section 3).
- **Build from scratch:** we build FitLife Digital ourselves with the tools from the courses and from Azure, and buy nothing at this stage (Q2, Q3). Membership and Access replace the 2012 system, which is retired after a one-time migration. Identity is our own JWT service as in IT-arkitektur M10. The finance system and the payment provider stay outside FitLife Digital behind an anticorruption layer.
- **Not in this version:** live-streamed and on-demand classes and videos, and MitID.
- **Channels, not contexts:** the member app, trainer portal, API gateway and MCP server. The MCP server is LATER (Q9).
- **Not decided here:** what the PoC builds and in which order (#18).

## Alternatives considered

- **Q1 option B, personal training as the only core.** Passes the reverse test most clearly [M4.01 p. 4], but the PoC would build supporting contexts first, against our own "start with core", and it changes part 1.
- **Q1 option C, booking as the only core, personal training left out.** Smallest scope, but the case's strengths (trainers, personal relationship) and its personal trainers with training data in Excel [PB p. 4–5] go unanswered, and it changes part 1.
- **Keep the 2012 system behind an ACL during the transition** (the first draft of this ADR). Rejected: it ties the new model to a system the case describes as outdated and unintegrated, for a transition the PoC doesn't show anyway. A one-time migration is simpler.
- **Membership as generic, only integrated with the 2012 system** (the first draft of the target architecture). Rejected: the informants ask for pause and flexible memberships [I3, I4], which the 2012 system and the single membership type [PB p. 3] don't support.
- **A bought identity provider.** Rejected at this stage: the course teaches JWT authentication [M10.03], and the PoC needs only test users and roles.
- **One service per business capability** (the TO-BE context document's architecture style). Rejected as the rule, kept as a starting point: class scheduling and class booking share the same rules on the same class session (capacity, waitlist), so they are one context, Booking. The course cuts by language and rules [M4.01 p. 9].
- **A separate context for the MCP server or the API gateway.** Rejected: they are channels with no business rules (principle PA3).

## Consequences

- Each context becomes one service in `src/<Service>/` with its own database and its own `CONTEXT.md` (DDD_PLAN step 3).
- The cross-boundary events in context map section 5 each need a JSON Schema in `contracts/events/` before the services that use them are built.
- The Postman MockServer requirement [PB p. 9] is covered by mocks of the payment provider and, until Membership exists, of member registration (#35).
- Building everything ourselves conflicts with principle P5 in part 1. Q8 must be settled before this ADR is accepted, and the target architecture (parts 2, 4 and 6) must be changed to match whichever way it goes.
- The report must argue why booking is core (the local class with a known instructor, not the software) and why online classes are left out.
- Moving a boundary later is its own issue with a new ADR that supersedes this one.

## Questions for the group session

The answer column holds Jesper's proposal (2026-11-23). The group confirms or changes each one; then the context map and glossary are changed to match and the status becomes `accepted`.

| # | Question | Proposed answer | Status |
|---|---|---|---|
| Q1 | Which subdomain is core? | A: class scheduling and booking, and personal training | To confirm |
| Q2 | What happens to the 2012 membership system? | AS-IS only. We build Membership and Access from scratch; the members are migrated once in the transition. No integration with it, also not in the PoC | To confirm |
| Q3 | How do we handle identity? | Our own `IdentityService` with JWT [M10.03], test users and roles. Nothing bought, no MitID | To confirm |
| Q4 | Is the MCP server part of the PoC? | No, it is LATER and not MVP. A channel, not a context. Case hook: competitors offer AI coaching [PB p. 4] | To confirm |
| Q5 | Which language goes in the glossary? | **Term** in Danish, **In code** in English with schema.org; Danish "Booking" becomes `Reservation` in code | **Open** |
| Q6 | Are the language-test lines in `GLOSSARY.md` right? | The group checks each interview reference and adds a quote with informant code | **Open**, before `accepted` |
| Q7 | What does the PoC build, and which context first? | Moved to #18; the PoC scope is not fixed yet | Moved |
| Q8 | "We build it ourselves" vs principle P5 in part 1 ("payment, key fobs and messaging channels are bought or integrated") | Either keep P5 for FitLife in operation and only build ourselves in the PoC, or change P5 and parts 2, 4 and 6 with a new row in the decision log. For changing it: the case's business model canvas has a development team as key resource [PB p. 7]. Against: the case says FitLife lacks digital skills [PB p. 5] | **Open** |
| Q9 | What does the MCP server have to do with wearables? | Two directions: MCP lets an AI assistant use *our* API; a wearables integration is *us* fetching data from vendors. Is the idea that the member's AI assistant connects the two, so we don't integrate with wearables directly? | **Open** |
| – | Consent for health data | Designed together with Training (principle P4); minimal or absent in the PoC, but described | To confirm |
| – | Online classes and videos | Left out of this version, with the reason in context map section 2 | To confirm |

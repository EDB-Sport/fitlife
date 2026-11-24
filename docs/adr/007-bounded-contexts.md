# ADR 007: Bounded contexts for FitLife Digital

- **Status:** accepted
- **Date:** 2026-11-24
- **Deciders:** Jesper (proposal and answers), and the reviewers who sign off #33 (issue #2)

## Context

The PoC must deliver "en eller flere centrale komponenter af FitLife Digital" as microservices [PB p. 10], and the IT-arkitektur report must explain the design process "fra målbillede til microservices (f.eks. jf. DDD)". "Why is the system cut this way?" is also a likely exam question (`DDD_PLAN.md` section 2).

Inputs: the case [PB p. 3–6], the group's simulated interviews [I1–I4], the target architecture from part 1, the TO-BE context document (version 2026-11-23) and the course method in *Læringsguide M4.01*. Source abbreviations are explained at the top of `docs/domain/CONTEXT-MAP.md`.

Forces:
- Three of four informants stay because of the trainers or instructors [I1, I3, I4]. All four want digital booking and messages about changes [I1–I4].
- At most two subdomains may be core [M4.01 p. 5]. At least three contexts, and at least two of them must come from the core capability [M4.01 p. 10].
- The course's checklist for the context map also asks for at least one anticorruption layer towards an existing system, with a reason [M4.01 p. 11] (HaaV's systems in the guide, FitLife's here). With the 2012 ACL rejected below, the model still has one: the ACL between FitLife's existing finance system [PB p. 5] and Membership (context map section 4). The payment provider and the messaging channels sit behind ACLs too, but the case names neither, so they are new external systems and not existing ones.
- The case's idea for FitLife Digital is to integrate "med eksisterende medlems- og økonomisystemer" [PB p. 5]. It is an idea from the innovation manager, not one of the tasks under "I skal" [PB p. 6], so we may choose otherwise if we argue it.
- The case itself describes the 2012 system as part of the problem: "Der eksisterer ingen integration mellem systemerne" [PB p. 4], and management's SWOT lists outdated IT systems without integration and technical debt and security risks [PB p. 5].
- The PoC must handle "brugernes identitet og adgang på en sikker måde" [PB p. 10] and test mocked services with Postman MockServer [PB p. 9].
- Our own plan says to start with the core subdomain (`DDD_PLAN.md` step 4).

## Decision

- **Core:** class scheduling and booking, and personal training. The same as the target architecture from part 1.
- **Contexts:** Booking and Training (core), Membership, Notification and Identity; Consent together with Training; Access next; Engagement later (context map section 3).
- **Build and integrate (principle P5 in part 1):** we build the contexts above as our own services. Membership and Access replace the 2012 system, which is retired after a one-time migration. What P5 names as bought or integrated stays outside FitLife Digital behind an anticorruption layer: the payment provider and the finance system, the door and key fob system, and the messaging channels. Identity is our own JWT service as in IT-arkitektur M10.
- **In the PoC:** our own services run, and everything external is a mock, so a whole flow can be shown without a real supplier [PB p. 9].
- **Sign-up:** a person creates a `Konto` in Identity first, and then joins in Membership with the token.
- **Channels, not contexts:** the member app, trainer portal, API gateway and MCP server. The MCP server lets a member's AI assistant use our API. Wearables are a separate integration into Training.

## Alternatives considered

- **Q1 option B, personal training as the only core.** Passes the reverse test most clearly [M4.01 p. 4], but the PoC would build supporting contexts first, against our own "start with core", and it changes part 1.
- **Q1 option C, booking as the only core, personal training left out.** Smallest scope, but the case's strengths (trainers, personal relationship) and its personal trainers with training data in Excel [PB p. 4–5] go unanswered, and it changes part 1.
- **Keep the 2012 system behind an ACL during the transition** (the first draft of this ADR). Rejected: it ties the new model to a system the case describes as outdated and unintegrated, for a transition the PoC doesn't show anyway. A one-time migration is simpler.
- **Membership as generic, only integrated with the 2012 system** (the first draft of the target architecture). Rejected: the informants ask for pause and flexible memberships [I3, I4], which the 2012 system and the single membership type [PB p. 3] don't support.
- **A bought identity provider.** Rejected at this stage: the course teaches JWT authentication [M10.03], and the PoC needs only test users and roles.
- **One service per business capability** (the TO-BE context document's architecture style). Rejected as the rule, kept as a starting point: class scheduling and class booking share the same rules on the same class session (capacity, waitlist), so they are one context, Booking. The course cuts by language and rules [M4.01 p. 9].
- **A separate context for the MCP server or the API gateway.** Rejected: they are channels with no business rules (principle PA3).
- **Live-streamed and on-demand classes as a context of ours.** The case's idea for FitLife Digital includes them [PB p. 5], and the online classes during corona were "overraskende populært" [PB p. 3]. Not chosen: P5 buys or integrates streaming, and the first versions strengthen the relationship with trainers and instructors in the centre, which is the core capability.
- **Membership drives sign-up and calls Identity to create the account** (proposed in the review of #33). Not chosen: the password would pass through Membership. With Identity first, each service keeps its own data, and no new relation or event is needed.
- **The membership status *Afventer samtykke*** (TO-BE context document section 8: a parent approves a member under 18). Left out: the case has no rule for members under 18, and the word collides with Samtykke, which is consent to share health data (principle P4).

## Consequences

- Each context becomes one service in `src/<Service>/` with its own database and its own `CONTEXT.md` (DDD_PLAN step 3).
- The cross-boundary events in context map section 5 each need a JSON Schema in `contracts/events/` before the services that use them are built.
- The Postman MockServer requirement [PB p. 9] is met by mocking what is external. The mock in `tests/postman/mocks/` stands in for account registration until Identity exists; the payment provider gets its mock together with Membership.
- In operation, the 2012 system keeps handling payment and key fobs [PB p. 4] until Membership and Access take over together, so a new member can always open the door. The order belongs to the transition in the target architecture (part 6); the PoC shows no transition.
- The report must argue why booking is core (the local class with a known instructor, not the software) and why online classes are left out.
- Moving a boundary later is its own issue with a new ADR that supersedes this one.

## Questions and answers

Answered by Jesper (2026-11-23 and 2026-11-24) and in the review of #33.

| # | Question | Answer |
|---|---|---|
| Q1 | Which subdomain is core? | Class scheduling and booking, and personal training |
| Q2 | What happens to the 2012 membership system? | AS-IS only. We build Membership and Access from scratch; the members are migrated once in the transition. No integration with it, also not in the PoC |
| Q3 | How do we handle identity? | Our own `IdentityService` with JWT [M10.03], test users and roles |
| Q4 | Is the MCP server part of the PoC? | It is a channel, not a context. LATER in the target architecture; the PoC may show it in its own issue. Case hook: competitors offer AI coaching [PB p. 4] |
| Q5 | Which language goes in the glossary? | **Term** in Danish, **In code** in English with schema.org; Danish "Booking" becomes `Reservation` in code |
| Q6 | Are the language-test lines in `GLOSSARY.md` right? | The transcripts are not in the repo. Checking the references and adding quotes is #126 |
| Q7 | What does the PoC build first? | `BookingService` (#18) |
| Q8 | Does "we build it ourselves" conflict with principle P5 in part 1? | No. P5 stands: we build our own services, and payment, key fobs, messaging channels and streaming are bought or integrated. In the PoC they are mocked |
| Q9 | What does the MCP server have to do with wearables? | Nothing. MCP lets an AI assistant use *our* API; a wearables integration is *us* fetching vendor data into Training. Wearables are described in the report and at most mocked in the PoC |
| Q10 | How does a new member get through the door while Membership is NOW and Access is NEXT? | Membership and Access take over from the 2012 system together; until then it keeps handling payment and key fobs |
| Q11 | Who owns sign-up? | Identity first: the person creates a `Konto`, then joins in Membership with the token |
| Q12 | "Samtykke" had two meanings | The membership status *Afventer samtykke* is left out of this version, so Samtykke only means consent to share health data |
| – | Consent for health data | Designed together with Training (principle P4); minimal in the PoC, but described |

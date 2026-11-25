# Glossary: the ubiquitous language of FitLife Digital

> Rules: `docs/METHODOLOGY.md` sections 6 and 11.
> The rows below are the first terms the context map needs (ADR 007). Kinds are a first guess and are settled per context in DDD_PLAN step 4. Sources are explained at the top of `docs/domain/CONTEXT-MAP.md`.

People and agents use these words, and only these words, in code, tests, issues, PRs and prompts.

## How to use it

- **Add the term first.** A new domain concept gets its row in the same PR that introduces it, before or together with the code.
- **One meaning per row.** If a word means different things in two bounded contexts, it gets two rows, one for each context. That's the language test from IT-arkitektur M4.
- **Don't rename the columns.** The TEAM008 analyzer checks public types in `*.Models` against the **In code** column.
- **Term is the Danish word the business uses.** It is the word in the report, the user journey and conversations with FitLife. **In code** is the English name we use in C#, APIs and event names. Where a Danish Term is spelled like an English name in its own **Avoid** cell (Booking), Avoid means the name in code: the Danish text says *booking*, the code says `Reservation`.
- **Avoid** lists the synonyms nobody may use for this concept. If an agent writes one of them, the reviewer asks for the glossary name.
- **schema.org** is the type the English name is taken from (course rule #5). Write `–` if none fits.
- **Kind** is one of: `Aggregate root`, `Entity`, `Value object`, `Domain event`, `Command`, `Query`, `Domain service`, or `Term` for a word that is not a type. A `Term` has `–` in **In code**; it gets a code name when it becomes a type.

## Terms

| Term | Meaning | In code | schema.org | Context | Kind | Avoid |
|---|---|---|---|---|---|---|
| Medlem | A person with a membership at FitLife, identified by a member id | `Member` | Person | Membership | Aggregate root | User, Customer, Client, Kunde |
| Medlemskab | A member's agreement with FitLife: membership type, status, start date and any binding period | `Membership` | ProgramMembership | Membership | Aggregate root | Subscription, Contract, Plan |
| Medlemstype | The kind of membership offered. The case has exactly one today [PB p. 3]; more are a TO-BE choice | `MembershipType` | – | Membership | Value object | Plan, Tier, Product |
| Medlemskabets tilstand | Where a membership is in its life: Aktiv, Pause, Betaling mangler, Opsagt, Afsluttet. Only Aktiv gives access and the right to book | `MembershipStatus` | – | Membership | Value object | State, Phase, Active flag |
| Konto | The log-in identity of a person: e-mail address (the log-in name), password hash and roles | `Account` | – | Identity | Aggregate root | User, Member, Login |
| Rolle | What an account may do: Medlem, Træner or Admin, carried as a claim in the token | `Role` | Role | Identity | Value object | Permission, Group, UserType |
| Center | One of FitLife's six centres [PB p. 3] | `Center` | ExerciseGym | Booking | Entity | Gym, Location, Club, Site |
| Holdtype | A kind of class that recurs in the schedule, e.g. spinning or yoga | `ClassType` | – | Booking | Entity | Activity, Program, Course |
| Hold | One scheduled run of a class: centre, class type, instructor, start time, duration and capacity | `ClassSession` | Event | Booking | Aggregate root | Session, Class, Lesson, Event |
| Starttid | When a class session starts: a date and a time with an offset (ISO 8601). `startTime` in JSON and event contracts | `StartTime` | DateTime | Booking | Value object | StartDate, StartsAt, Begin, Time, Date |
| Kapacitet | How many members can have a reservation on one class session | `Capacity` | – | Booking | Value object | Seats, Size, Max, Limit |
| Holdoplysninger | A class session's centre, class type, instructor and duration. Its start time is Starttid | `ClassSessionDetails` | – | Booking | Value object | Info, Metadata, Description |
| Booking | A member's seat on one class session | `Reservation` | Reservation | Booking | Entity | Booking (as a name in code), Signup, Enrollment, Tilmelding |
| Ventelisteplads | A member's place in the queue for a full class session, first come first served | `WaitlistEntry` | – | Booking | Entity | Queue, Standby, Waiting |
| Sen afmelding | A cancellation less than two hours before the class session starts. Statistics only, no consequence. A seat from the waitlist is never late | `ReservationCancelled.IsLateCancellation` | – | Booking | Term | Late cancel, No-show |
| Instruktør | The trainer who leads a class session. Booking only knows the instructor's id and name | `Instructor` | Person | Booking | Value object | Trainer, Coach, Teacher |
| Personlig træner | A trainer in a personal training programme with one member (NEXT) | `PersonalTrainer` | Person | Training | Entity | Instructor, Coach, PT |
| Træningspas | One completed training session in a personal training programme (NEXT) | `TrainingSession` | ExerciseAction | Training | Entity | Session, Workout, Visit |
| PT-forløb | A personal training programme: one member's series of training sessions with their personal trainer, with follow-up (NEXT) | `TrainingProgram` | ExercisePlan | Training | Aggregate root | Course, Plan, Package |
| Samtykke | A member's permission to share their training and health data with a trainer or an external app. It can be withdrawn (principle P4 in the target architecture) | `Consent` | – | Training | Aggregate root | Permission, Approval, Acceptance |
| Adgangsbrik | The key fob a member uses to get into a centre. Today the 2012 system handles the key fobs [PB p. 4] (NEXT) | `KeyFob` | – | Access | Entity | Key card, Tag, Badge |
| Check-in | One registered entry of a member into a centre (NEXT) | `CheckIn` | CheckInAction | Access | Entity | Visit, Entry, Scan |
| Notifikation | One message to one member about something that happened, with its delivery log | `Notification` | – | Notification | Aggregate root | Message, Alert, Push |
| Booking oprettet | A member got a seat on a class session, directly or from the waitlist | `ReservationMade` | – | Booking | Domain event | BookingCreated, ReservationAdded |
| Booking afmeldt | A member gave up their seat | `ReservationCancelled` | – | Booking | Domain event | BookingDeleted, ReservationRemoved |
| Ventelisteplads udløbet | The waitlist closed 2 hours before start, and a member's waitlist entry was removed without a seat | `WaitlistEntryExpired` | – | Booking | Domain event | WaitlistEntryRemoved, WaitlistEntryDeleted, QueueExpired |
| Hold aflyst | A class session will not take place | `ClassSessionCancelled` | – | Booking | Domain event | ClassDeleted, SessionRemoved |
| Hold ændret | A class session got a new time or place | `ClassSessionChanged` | – | Booking | Domain event | ClassUpdated, SessionEdited |
| Medlemskab pauset | A membership is on pause for a period | `MembershipPaused` | – | Membership | Domain event | MembershipFrozen, Suspended |
| Medlemskab opsagt | A member has given notice; the membership ends after the notice period | `MembershipCancelled` | – | Membership | Domain event | MembershipDeleted, Unsubscribed |
| Betaling fejlet | A charge for the membership fee did not go through | `PaymentFailed` | – | Membership | Domain event | ChargeError, PaymentDeclined |

## Words that mean different things in different contexts

From the language test, step 1 of `docs/DDD_PLAN.md`. Each line here points at a context boundary, except *hold* (see the note under the table). The references point to where the word is used in the interviews. Checking them against the transcripts and adding quotes is #126.

| Word | Meaning in context A | Meaning in context B | Names in code |
|---|---|---|---|
| hold | Booking, planning: a kind of class that recurs every week, the class the informant goes to every Tuesday or "is on" [I1, I3] | Booking, reservations: one run of a class that can be full [I1, I3]. A third meaning, the regular group of people on a class who know each other [I3, I4], points at a later Engagement context | `ClassType` / `ClassSession` / (Engagement, LATER) |
| træner | Booking: the instructor who leads a class and knows the members' names [I1, I3] | Training: the personal trainer in a programme with one member, with data, exercises and follow-up [I2, I3] | `Instructor` / `PersonalTrainer` |
| medlem | Identity: anyone who can log in, including the role "Medlem" | Membership: a person who has joined, pays and has a key fob [PB p. 4; I1]. Booking only sees a participant with a seat | `Account` (with `Role`) / `Member`, referenced as `MemberId` |
| session | Training: one personal training session in a programme [I3] | Identity: a logged-in browser session | `TrainingSession` / – (no type in Identity yet) |

**Bare "hold" is ambiguous, and it is not a boundary.** The informants use it both for the weekly class and for one run of it (first line of the table). In this glossary `Holdtype` is the weekly class (`ClassType`) and `Hold` is one run of it (`ClassSession`). Both belong to Booking, because ADR 007 keeps class scheduling and booking in one context. Say which of the two you mean.

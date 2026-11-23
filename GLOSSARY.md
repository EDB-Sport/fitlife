# Glossary: the ubiquitous language of FitLife Digital

> Rules: `docs/METHODOLOGY.md` sections 6 and 11.

People and agents use these words, and only these words, in code, tests, issues, PRs and prompts.

## How to use it

- **Add the term first.** A new domain concept gets its row in the same PR that introduces it, before or together with the code.
- **One meaning per row.** If a word means different things in two bounded contexts, it gets two rows, one for each context. That's the language test from IT-arkitektur M4.
- **Don't rename the columns.** The TEAM008 analyzer checks public types in `*.Models` against the **In code** column.
- **Avoid** lists the synonyms nobody may use for this concept. If an agent writes one of them, the reviewer asks for the glossary name.
- **schema.org** is the type the English name is taken from (course rule #5). Write `–` if none fits.
- **Kind** is one of: `Aggregate root`, `Entity`, `Value object`, `Domain event`, `Command`, `Query`, `Domain service`, or `Term` for a word that is not a type.

## Terms

| Term | Meaning | In code | schema.org | Context | Kind | Avoid |
|---|---|---|---|---|---|---|

## Words that mean different things in different contexts

Fill this in from the language test, step 1 of `docs/DDD_PLAN.md`. Each line here points at a context boundary.

| Word | Meaning in context A | Meaning in context B | Names in code |
|---|---|---|---|

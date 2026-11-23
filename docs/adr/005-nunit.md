# ADR 005: NUnit as the test framework

- **Status:** proposed
- **Date:** 2026-11-23
- **Deciders:** to be confirmed by the group (issue #14)

## Context

The two courses use different .NET test frameworks. IT-arkitektur creates test projects with `dotnet new nunit` and requires TDD with NUnit and mocks/stubs (course rule #1). Agil udvikling teaches MSTest, but accepts NUnit and says that staying consistent matters more than which framework is chosen. Agil udvikling also teaches Moq, and its exam report asks how Moq was used.

The project needs one framework across every service, the Blazor UIs (bUnit) and the project's own Roslyn analyzers (`docs/CSHARP_SETUP_TODO.md` sections 4, 6 and 6a). Most of the code is written by agents, so there should be one style of test for them to copy, and the tdd-check workflow must recognise tests and skipped tests by their attributes.

## Decision

All test projects use **NUnit** with its constraint assertions (`Assert.That(total, Is.EqualTo(150))`), plus Moq for test doubles where interactions must be checked. Tests are named `Method_Scenario_ExpectedResult` and written Arrange–Act–Assert.

The setup from #4 makes this the default: `tests/Directory.Build.props` gives every `*.Tests` and `*.IntegrationTests` project NUnit, the NUnit adapter, NUnit.Analyzers and coverlet, with versions in `Directory.Packages.props` (NUnit 4.6.1, the 4.x line the course template uses). tdd-check counts `[Test]`, `[TestCase]`, `[TestCaseSource]` and `[Theory]`, and treats `[Ignore]`, `[Explicit]`, `Assert.Ignore` and `Assert.Inconclusive` as skipped tests.

## Alternatives considered

- **MSTest, as Agil udvikling teaches.** Equally capable. Rejected: IT-arkitektur's rules and examples are NUnit, and both courses can be satisfied with NUnit, but not with MSTest.
- **xUnit.** Popular in the .NET community and used by many Microsoft samples. Rejected: neither course teaches it, and switching would mean learning a third framework for the exam.
- **A mix, chosen per service.** Rejected: agents copy whatever they see, so a mix spreads, and tdd-check and future analyzers (TEAM006: every test has an assertion) would need rules for each framework.

## Consequences

- Easier: one framework and one assertion style everywhere, matching the IT-arkitektur examples. bUnit works with NUnit for the Blazor UIs, and NUnit.Analyzers catches common test mistakes at build time.
- Harder: the Agil udvikling report must explain the deviation from MSTest. The concepts it asks about (test pyramid, Moq, TDD, automated tests in CI) are the same in NUnit, so only the syntax differs.
- Must do: stay on one NUnit major version across all projects. A move to NUnit 5 is its own PR that upgrades every test project at once.

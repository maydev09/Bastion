# 0012. Testing with NUnit

- **Status:** Accepted
- **Date:** 2026-11-15

## Context

Earlier ADRs require tests in several places: stores against a real database
(ADR 0005), architecture tests for module boundaries (ADR 0002, 0004), scraper tests
on saved sample files without network access (SPEC). We need one backend test framework.

The framework should cover simple tests and, when the day comes, complex ones
(parameterized fixtures, shared expensive setup, many assertions in one test)
without switching tools or writing our own extensions.

## Decision

- Backend tests use **NUnit**.
  - Parameterized tests with `[TestCase]` and `[TestCaseSource]`.
  - Shared setup with `[SetUp]`, `[OneTimeSetUp]` and `[SetUpFixture]`, e.g. one
    PostgreSQL container for a whole test assembly.
  - Assertions with NUnit's constraint model (`Assert.That(x, Is.EqualTo(y))`) and
    `Assert.Multiple`. No separate assertion library.
- Database tests use **Testcontainers** with the same PostgreSQL image as production
  (ADR 0006).
- **Integration tests are the main coverage**: the real Host through
  `WebApplicationFactory`, real configuration, a real database in Testcontainers.
  Unit tests are for focused logic with many cases (parsers, robots.txt rules, the module loader),
  not for every class.
- **NSubstitute** replaces dependencies that would reach the outside world or are slow
  (VPN check, browser, portal responses). Things we own and that are fast (configuration,
  in-memory collections) stay real.
- Frontend tests use **Vitest** (ADR 0011).

## Alternatives considered

- **xUnit:** the most popular choice and the .NET template default, but deliberately
  minimal: no `[SetUp]`/`[TearDown]` (constructor and `IDisposable` instead), shared
  setup through class and collection fixtures, a smaller assertion API, and fewer
  built-in features for complex tests.
- **MSTest:** improved a lot recently, but less common in open source and with fewer
  examples than NUnit.
- **Moq:** the most widely known mocking library, but it lost trust after it shipped
  SponsorLink (collecting developers' e-mail hashes) in 2023. **FakeItEasy:** fine, less common.
- **No mocking library, only hand-written fakes:** works for small interfaces, but
  becomes boilerplate as soon as calls need to be checked or several behaviors set up.
- **FluentAssertions:** readable assertions, but a commercial license since version 8;
  NUnit's built-in assertions are enough.

## Consequences

- One full-featured framework for unit, integration, architecture and scraper tests.
- Templates and many online examples assume xUnit, so test projects are created from
  the NUnit template (`dotnet new nunit`).

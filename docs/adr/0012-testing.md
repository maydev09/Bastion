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
- No mocking library by default. Tests use real implementations or small hand-written
  fakes; a mocking library is added only when a real case needs it.
- Frontend tests use **Vitest** (ADR 0011).

## Alternatives considered

- **xUnit:** the most popular choice and the .NET template default, but deliberately
  minimal: no `[SetUp]`/`[TearDown]` (constructor and `IDisposable` instead), shared
  setup through class and collection fixtures, a smaller assertion API, and fewer
  built-in features for complex tests.
- **MSTest:** improved a lot recently, but less common in open source and with fewer
  examples than NUnit.
- **FluentAssertions:** readable assertions, but a commercial license since version 8;
  NUnit's built-in assertions are enough.

## Consequences

- One full-featured framework for unit, integration, architecture and scraper tests.
- Templates and many online examples assume xUnit, so test projects are created from
  the NUnit template (`dotnet new nunit`).

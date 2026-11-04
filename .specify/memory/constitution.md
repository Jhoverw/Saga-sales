# Saga-Sales Constitution

This document establishes the non-negotiable principles that govern how specifications become
code in Saga-Sales. Every implementation plan MUST pass these gates before code generation
proceeds. Where this document and any other artifact disagree, this document wins.

**Scope**: every service under `src/`, every specification under `specs/`, and every change
delivered through the Spec-Driven Development workflow in `.specify/`.

## Core Principles

### I. Library-First (NON-NEGOTIABLE)

Every feature MUST begin as a standalone library before it is used by an application:

```
Every feature in Saga-Sales MUST begin its existence as a standalone library.
No feature shall be implemented directly within application code without first
being abstracted into a reusable library component.
```

Identify the domain entity, define its contract interfaces, write the contract tests, then
build the library. Applications consume libraries; libraries never consume applications.

### II. CLI Interface Mandate

Every library exposing business logic MUST provide a text entry point:

- Accept input via stdin, arguments, or files.
- Produce output on stdout; errors on stderr.
- Support JSON for structured exchange.
- Never hide functionality behind class methods with no text interface.

### III. Test-First (NON-NEGOTIABLE)

Implementation code MUST NOT be written before tests exist and fail:

1. Write the tests that define the behaviour and acceptance criteria.
2. Get them reviewed by the specification owner.
3. Confirm they FAIL (Red).
4. Write the minimum implementation that makes them PASS (Green).
5. Refactor while keeping every test green.
6. Run contract tests against real services when available.

A feature without tests is **not done**, regardless of how it looks in a demo. Placeholder files,
skipped tests, or assertions that assert nothing are treated as **no tests at all**.

### IV. E-Commerce Domain Boundaries

For features touching products, orders, payments and customers:

- Products track stock with real-time availability checks.
- Orders capture line items with quantity and price **at creation time** (immutable snapshot).
- Payments are recorded atomically with the order status transition they cause.
- All monetary amounts use `decimal` (PostgreSQL `numeric(18,2)`). Floating point is forbidden
  for money.
- Stock deduction is atomic: concurrent orders must never oversell.

### V. Multi-Tenant Isolation (NON-NEGOTIABLE)

- The tenant MUST be resolved from the JWT claim `tenant_id` or the `X-Tenant-Id` header.
- Every query MUST be scoped to a tenant. A query without a tenant filter is a defect.
- A request with no resolvable tenant MUST be rejected, never defaulted.
- Cross-tenant access raises `TenantNotFoundException`/403 and is treated as a security incident.
- Redis cache keys MUST be prefixed with the tenant ID.
- **Database-level enforcement**: every stored procedure takes `p_tenant_id` as its first
  parameter and filters by it. The database enforces isolation, not programmer discipline.

### VI. Observability & Resilience

Every service MUST provide:

- Structured logging through Serilog with correlation IDs.
- Health checks for PostgreSQL and RabbitMQ dependencies (`/health`).
- Circuit breakers (Polly) around external calls.
- Distributed tracing through OpenTelemetry.
- Graceful degradation: a failing downstream dependency MUST NOT take down read paths.

### VII. Simplicity & Bounded Scope

- The platform is capped at **9 microservices** (amended 2026-11-03 from 6; see Governance).
- A new service requires a documented justification that names the concrete cross-domain
  coupling it removes. "Future-proofing" is rejected.
- **No direct database sharing**: a service never opens another service's database.
  Cross-service reads happen through HTTP or integration events.
- Prefer deleting a feature over building an abstraction for it.

### VIII. Data Access: Stored Procedures, No ORM

This principle supersedes any earlier "framework trust" wording that implied an ORM.

- Persistence uses **PL/pgSQL stored procedures and functions** accessed through **Dapper**.
- EF Core, EF migrations and any other ORM are **not permitted** in this solution.
- Schema changes are versioned SQL scripts under `db/<service>/V###__*.sql`
  (`V001__schema.sql`, `V002__functions.sql`, `V003__seed.sql`).
- Every multi-table write (create order, reserve stock) is **one procedure** with its own
  transaction.
- Concurrency is handled in SQL (`SELECT … FOR UPDATE`), not in application memory.
- Domain entities stay persistence-ignorant: no attributes, no ORM navigations leaked into
  business rules.

### IX. Integration-First Testing

- Integration tests use a real PostgreSQL, never a mocked database.
- Event-bus tests use a real RabbitMQ connection.
- Contract tests are mandatory before implementation code is generated.
- Critical user journeys (checkout, payment, refund) require end-to-end coverage.
- Tests MUST be runnable with the standard `dotnet test` of the solution.

## Technology Constraints (locked)

| Concern | Decision |
|---|---|
| Runtime | .NET 10 (C# 14) |
| Persistence | PostgreSQL 16 + PL/pgSQL stored procedures |
| Data access | Dapper (no ORM) |
| Messaging | MassTransit + RabbitMQ |
| API gateway | YARP |
| Frontend | Blazor WebAssembly |
| Auth | JWT bearer with `tenant_id` and `role` claims |
| Logging | Serilog + Seq (optional) |
| Tracing | OpenTelemetry |
| Validation | FluentValidation |
| Reports | **CSV** export (Excel-compatible); no server-side `.xlsx` generation |

## Scope Boundaries

- **Electronic invoicing (SRI) is out of scope** for this repository. It is delivered as a
  separate project that consumes this platform's order events. Saga-Sales must only reserve the
  fiscal fields (`FiscalStatus`, `FiscalDocumentNumber`, `TaxAmount`, per-line `TaxRate`) so no
  historical migration is needed later.
- Payments in the MVP are recorded **manually** (transfer/deposit). Online gateways arrive in a
  later release behind the `IPaymentGateway` abstraction.

## Development Workflow & Quality Gates

Work follows the Spec-Driven Development chain in `.specify/`:

```
constitution → specify → clarify → plan → checklist → tasks → analyze → implement → converge
```

Gates that SHALL block progress:

1. **No `[NEEDS CLARIFICATION]` markers** may survive into `plan.md`.
2. **`/speckit.analyze` must be clean** before `/speckit.implement`: no task without a
   requirement, no plan decision contradicting the spec.
3. **Definition of Done** for any epic: stories in `spec.md`, unit + integration + contract tests
   green, stored procedures versioned with `p_tenant_id`, isolation verified with two tenants,
   `/health` reporting dependencies, and endpoints documented.
4. **Constitution Check** in every `plan.md` must state the outcome per principle, with evidence,
   not an assertion.

## Governance

- This constitution **supersedes** all other practices, templates and habits.
- Amendments require: a written rationale, review by project maintainers, and a documented
  assessment of backwards compatibility. Breaking changes need a migration plan.
- Every plan and review MUST verify compliance. Complexity that violates a principle MUST be
  justified in the plan's *Complexity Tracking* table or removed.
- Amendments are recorded in the History table below.

### Amendment History

| Version | Date | Change |
|---|---|---|
| 1.0.0 | 2026-11-03 | Initial constitution (Articles I–IX) |
| 1.1.0 | 2026-11-03 | §VII microservice cap raised from 6 to 9 with justification |
| 2.0.0 | 2026-11-04 | §VIII rewritten: stored procedures + Dapper replace the ORM; §V adds database-level tenant enforcement; Technology Constraints and Scope Boundaries added; Quality Gates formalised (analyze + Definition of Done) |
| 2.1.0 | 2026-11-04 | Runtime moved from .NET 8 to .NET 10: the build host no longer has the .NET 8 targeting pack, and all projects now target `net10.0` |

**Version**: 2.1.0 | **Ratified**: 2026-11-03 | **Last Amended**: 2026-11-04

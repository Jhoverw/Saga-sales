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
- All monetary amounts use `decimal` (SQL Server `decimal(18,2)`). Floating point is forbidden
  for money.
- Stock deduction is atomic: concurrent orders must never oversell.

### V. Multi-Tenant Isolation (NON-NEGOTIABLE)

- The tenant MUST be resolved from the JWT claim `tenant_id` or the `X-Tenant-Id` header.
- Every query MUST be scoped to a tenant. A query without a tenant filter is a defect.
- A request with no resolvable tenant MUST be rejected, never defaulted.
- Cross-tenant access raises `TenantNotFoundException`/403 and is treated as a security incident.
- Redis is **not part of the MVP**. When caching lands, every cache key MUST be prefixed with the
  tenant ID.
- **Database-level enforcement**: every stored procedure takes `p_tenant_id` as its first
  parameter and filters by it. The database enforces isolation, not programmer discipline.

### VI. Observability & Resilience

Every service MUST provide:

- Structured logging through Serilog with correlation IDs.
- Health checks for SQL Server and RabbitMQ dependencies (`/health`).
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

- Persistence uses **T-SQL stored procedures on SQL Server** accessed through **Dapper**.
- EF Core, EF migrations and any other ORM are **not permitted** in this solution.
- Schema changes are versioned SQL scripts under `db/<service>/V###__*.sql`
  (`V001__schema.sql`, `V002__functions.sql`, `V003__seed.sql`).
- Every multi-table write (create order, reserve stock) is **one procedure** with its own
  transaction (`BEGIN TRAN … BEGIN CATCH … ROLLBACK … THROW`).
- Concurrency is handled in SQL (`WITH (UPDLOCK, ROWLOCK)`), not in application memory.
- Domain entities stay persistence-ignorant: no attributes, no ORM navigations leaked into
  business rules.

### IX. Integration-First Testing

- Integration tests use a real SQL Server, never a mocked database.
- Event-bus tests use a real RabbitMQ connection.
- Contract tests are mandatory before implementation code is generated.
- Critical user journeys (checkout, payment, refund) require end-to-end coverage.
- Tests MUST be runnable with the standard `dotnet test` of the solution.

## Technology Constraints (locked)

| Concern | Decision |
|---|---|
| Runtime | .NET 10 (C# 14) |
| Database | **SQL Server 2022**, T-SQL stored procedures |
| Data access | Dapper (no ORM) |
| Messaging | MassTransit + RabbitMQ |
| API gateway | YARP |
| Frontend | Blazor WebAssembly |
| Auth | JWT bearer with `tenant_id` and `role` claims |
| Logging | Serilog + Seq (optional) |
| Tracing | OpenTelemetry |
| Validation | FluentValidation |
| Reports | **CSV** export (Excel-compatible); no server-side `.xlsx` generation |
| Number format | **Decimal point (`.`) everywhere**: screens, inputs, CSV, PDF, messages and APIs. The comma is only the thousands separator when a number is displayed. Implemented once in `SagaSales.Shared.Formatting.AppCulture` |

## Scope Boundaries

- **Electronic invoicing (SRI): signing and submission are delegated to an external provider.**
  Saga-Sales does not sign the XML, hold the signing certificate, or call the SRI web services.
  Its job is to store, per tenant, every field the provider needs to issue an SRI invoice (seller
  and buyer identification, establishment and emission point, sequential, per-line tax codes and
  rates, totals and payment method) and to record what the provider returns (access key,
  authorization number and date, status and errors). The field list follows the SRI technical
  sheet kept in `docs/`.
- New fiscal columns follow §VIII (versioned scripts under `db/<service>/`) and §V (`p_tenant_id`).
  The fiscal fields already on orders (`FiscalStatus`, `FiscalDocumentNumber`, `TaxAmount`,
  per-line `TaxRate`) are kept.
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
| 2.2.0 | 2026-11-04 | **Database corrected to SQL Server 2022 (T-SQL)**. Every earlier reference to PostgreSQL / PL/pgSQL is superseded: driver `Microsoft.Data.SqlClient`, locking with `UPDLOCK`, errors raised with `THROW` |
| 2.3.0 | 2026-11-08 | **SRI scope clarified**: the database stores every field needed to issue an SRI invoice and the provider's result; XML signing and submission to SRI are delegated to an external provider and are not implemented here. Rationale (project owner, 2026-11-08): signing is contracted to a provider, so the platform only needs to persist the invoice data and the authorization result. Backward compatibility: additive columns only; existing fiscal fields on orders are kept |
| 2.4.0 | 2026-11-08 | **Number format locked**: the decimal separator is always the point (`.`) in every screen, input, file and message (Technology Constraints). Rationale (project owner, 2026-11-08): the comma and the point were appearing mixed. Backward compatibility: stored values are unchanged (SQL `decimal`); only presentation and typed input change. CSV reports keep `;` as column separator |

**Version**: 2.4.0 | **Ratified**: 2026-11-03 | **Last Amended**: 2026-11-08

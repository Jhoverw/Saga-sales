# Implementation Plan: Saga-Sales Multi-Tenant E-Commerce Platform

**Branch**: `001-saga-sales-ecommerce` | **Date**: 2026-11-04 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/001-saga-sales-ecommerce/spec.md`

## Summary

Deliver the MVP of a multi-tenant e-commerce platform in which a merchant opens a store, publishes
a catalogue with per-customer-type prices, takes orders that reserve stock atomically, records the
payment received outside the platform, and sees daily sales figures exportable to a spreadsheet.

Technical approach: eight .NET 10 services behind a YARP gateway, one SQL Server database per
service accessed **only through PL/pgSQL stored procedures called with Dapper**, asynchronous
consequences through MassTransit/RabbitMQ, and a Blazor WebAssembly SPA that only talks to the
gateway. The tenant is the first parameter of every procedure, so isolation is enforced by the
database rather than by handler discipline.

**Scope of this plan**: MVP = E-01, E-02, E-03, E-04a, E-08, **E-12 (purchases and costs)** and
**E-13 (real inventory: branches, lots, images)**. Online payments (E-04b), notifications (E-07),
analytics (E-06) and storefront (E-09) are planned here but are sequenced later; the schema and
events already reserve what they need.

## Technical Context

**Language/Version**: C# 14 on .NET 10 (SDK 10.0.x)

**Primary Dependencies**: Dapper (data access), MassTransit 8 + RabbitMQ (events), YARP 2 (gateway),
FluentValidation (input rules), Serilog + Seq (logs), OpenTelemetry (traces), Swashbuckle (API docs),
Blazor WebAssembly (SPA). FluentAssertions and xUnit (tests). No ORM.

**Storage**: SQL Server 2022, one database per service (`saga_identity`, `saga_catalog`, `saga_sales`,
`saga_payment`, `saga_customer`, `saga_analytics`, `saga_notification`). Schema versioned with
numbered SQL scripts and a `schema_version` table. Redis is used only for cache keys prefixed with
the tenant, and is not required for the MVP to work.

**Testing**: xUnit — unit tests for domain rules, integration tests against a real SQL Server for
every procedure, contract tests over HTTP against the gateway. Target: every procedure that holds
a business rule is covered by at least one integration test.

**Target Platform**: Linux containers (Docker Compose for SQL Server, RabbitMQ, Seq); Windows for
local development.

**Project Type**: web services (8) + single-page web application (1), monorepo.

**Performance Goals**: p95 under 200 ms for catalogue reads, 500 ms for order creation excluding
any payment gateway; 100 orders/day at peak; 10 stores; 50 products per store.

**Constraints**: money is `numeric(18,2)` in USD only; stock must never go negative under
concurrency; a tenant filter is mandatory on every query; no service opens another service's
database; the buyer does not sign in during the MVP; CSV with `;` and decimal comma for reports.

**Scale/Scope**: 8 deployable services + SPA, ~50 functional requirements covering 16 user stories,
7 databases, ~60 stored procedures, 1 SPA with 12 screens.

## Constitution Check

*GATE: evaluated before Phase 0 and re-evaluated after Phase 1.*
Constitution v2.2.0 — [`.specify/memory/constitution.md`](../../../.specify/memory/constitution.md).

| Gate | Article | Status | Evidence |
|---|---|---|---|
| Library-First | I | **PARTIAL** | `SagaSales.Shared` (contracts, DTOs, events) and `SagaSales.BuildingBlocks` (bus, auth, logging, tenant, health) are libraries consumed by every service. Domain rules still live inside the service projects; extracting them is remediation R-2 below. |
| CLI interface mandate | II | **FAIL** | No library exposes a text interface. Accepted for the MVP: nothing in this feature is operated from a terminal. Tracked as R-3. |
| Test-First | III | **PARTIAL** | 34 unit tests exist and pass (`tests/SagaSales.UnitTests`), covering the order state machine, refunds, PBKDF2 and `Result`. No integration or contract tests yet; the SQL that holds the rules is untested. Tracked as R-1. |
| Domain boundaries | IV | **PASS** | Money is `numeric(18,2)`; order lines snapshot name, price and stock; stock is decremented inside one transaction; refunds are validated against what remains refundable. |
| Multi-tenant isolation | V | **PARTIAL** | The design enforces it at the database (`p_tenant_id` first parameter, §3 of data-model). The current code still filters in LINQ, which is the defect this migration removes. |
| Observability & resilience | VI | **PARTIAL** | Serilog, correlation, `/health` per service and OpenTelemetry are wired. Polly circuit breakers around the Sales→Catalog call are planned but not implemented. |
| Simplicity & bounded scope | VII | **PASS** | 8 services, cap is 9 after the 2026-11-03 amendment. No service shares another's database. |
| Stored procedures, no ORM | VIII | **FAIL (today)** | The code currently uses EF Core. This plan replaces it with Dapper + PL/pgSQL. Justified in *Complexity Tracking*. |
| Integration-first testing | IX | **PARTIAL** | No integration or contract tests yet (same remediation as R-1). |

**Gate verdict**: two FAILs, both from debt created before the constitution was amended rather
than from this design. Neither is accepted silently: both are justified below and scheduled as
remediation tasks R-1 and R-3. **Phase 0 was allowed to proceed on that basis.**

**Post-Phase-1 re-evaluation**: the design artefacts do not introduce new violations. The SQL
schema enforces §V and §VIII structurally, and `data-model.md` §10 lists where each validation
rule is enforced twice (application and database). The FAILs remain until the migration tasks run.

## Project Structure

### Documentation (this feature)

```text
specs/001-saga-sales-ecommerce/
├── epics.md              # business layer: epics, actors, permissions, roadmap
├── spec.md               # /speckit-specify output
├── research.md           # Phase 0 output (this command)
├── data-model.md         # Phase 1 output (this command)
├── quickstart.md         # Phase 1 output (this command)
├── contracts/            # Phase 1 output (this command)
├── plan.md               # this file
├── checklists/
│   └── requirements.md   # /speckit-specify quality checklist
└── tasks.md              # Phase 2 output (/speckit-tasks — NOT created here)
```

### Source Code (repository root)

```text
src/
├── SagaSales.Shared/            # contracts, DTOs, enums, integration events
├── SagaSales.BuildingBlocks/    # bus, auth, tenant, logging, health, telemetry
├── SagaSales.Identity/          # tenants, users, sign-in
├── SagaSales.Catalog/           # products, price lists, categories, stock
├── SagaSales.Sales/             # orders, order lines, state machine
├── SagaSales.Payment/           # manual and online payments, refunds
├── SagaSales.Customer/          # customers, addresses, customer type
├── SagaSales.Analytics/         # daily aggregates, CSV export
├── SagaSales.Notification/      # email/SMS dispatch and log
├── SagaSales.ApiGateway/        # YARP routing, auth, tenant resolution
└── SagaSales.BlazorWeb/         # SPA (static, talks only to the gateway)

db/                              # NEW: schema and procedures, one folder per service
├── identity/    V001__schema.sql · V002__functions.sql · V003__seed.sql
├── catalog/     …
├── sales/       …
├── payment/     …
├── customer/    …
├── analytics/   …
└── notification/ …

tests/
├── SagaSales.UnitTests/         # domain rules, no database
├── SagaSales.IntegrationTests/  # NEW: procedures against a real SQL Server
└── SagaSales.ContractTests/     # NEW: HTTP contracts against the gateway

deploy/                          # docker-compose, database init, Dockerfiles
```

Each service keeps the same internal layout: `Domain/`, `Data/` (repositories over Dapper),
`Commands/`, `Queries/`, `Controllers/`, `EventHandlers/`, `Validators/`.

**Structure Decision**: monorepo with one project per service, because the services are deployed
together from a single image pipeline and share the contracts library. SQL is kept **outside** the
C# projects in `db/<service>/` so a reviewer can read and diff the schema without opening C#. Tests
are split by kind, not by service, because integration and contract tests need the composed system.

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
|---|---|---|
| **§VIII — the code uses EF Core today** (R-1) | The first implementation was generated before the stored-procedure standard was established. Removing EF is a migration, not a rewrite: the domain, handlers, events, validators and controllers are unaffected. | *Keep EF and amend the constitution* — rejected: the decision is a team standard (D1) and the SP approach is what gives the database-level tenant filter. *Big-bang rewrite* — rejected: it would discard 5,700 lines that already compile and pass 34 tests. The migration is done service by service, keeping the tests green. |
| **§II — no CLI entry point** (R-3) | Nothing in this feature is operated from a terminal; the services are HTTP only. | *Add a CLI now for every library* — rejected as speculative work that no story requires. Revisit when an operational tool (seed, import, reconciliation) is actually needed. |
| **§III/§IX — no integration or contract tests yet** (R-1) | The procedures do not exist yet; there is nothing to test against until R-1 lands. | *Write contract tests against mocks* — rejected by §IX: the value is precisely testing the real SQL. |

## Remediation carried into `/speckit-tasks`

| ID | Remediation | Blocks |
|---|---|---|
| R-1 | Replace EF Core with Dapper + the procedures in `data-model.md`, service by service, and add integration tests per procedure. | §III, §VIII, §IX |
| R-2 | Extract the domain rules of each service into their own assemblies so a feature can be tested as a library. | §I |
| R-3 | Provide a CLI for operational tooling (seeding a demo store, importing a catalogue). | §II |

## Phase 0 and Phase 1 outputs

| Artefact | Status |
|---|---|
| `research.md` | **Done** — 12 decisions, no unresolved clarification |
| `data-model.md` | **Done** — 7 databases, ~45 procedures, validation matrix, reserved fiscal fields |
| `contracts/` | **Done** — 6 files: `IdentityApi.cs`, `CatalogApi.cs`, `SalesApi.cs`, `PaymentApi.cs`, `CustomerApi.cs`, `AnalyticsApi.cs`. Identity and Analytics were added because E-01 and the CSV export had no contract at all. |
| `quickstart.md` | **Done** — schema scripts instead of migrations, manual payment, price lists, CSV, isolation and health scenarios |

## Known code changes implied by the spec

1. **Order creation must take the served customer** (D8): `CreateOrderCommand` currently uses the
   authenticated user as `customerId`. It must receive the customer being served and keep the
   caller in `created_by`.
2. **Prices per customer type** (D5): catalogue reads and order confirmation go through
   `sp_price_resolve`; `order_lines.price_source` records which price applied.
3. **Manual payment** (E-04a): new endpoint and procedure; it confirms the order without any
   gateway.
4. **Operator has no business data** (D10): no support endpoints that read store data.

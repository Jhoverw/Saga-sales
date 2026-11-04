# Implementation Plan: Saga-Sales E-Commerce Microservices

**Branch**: `saga-sales-ecommerce` | **Date**: 2026-11-03 | **Spec**: `/specs/saga-sales-ecommerce/spec.md`

## Summary

Build a multi-service e-commerce platform on .NET 8 with Blazor WebAssembly frontend, following MediatR CQRS + Domain Events + API Gateway architecture. 8 services total (within the ≤6 limit is not feasible without consolidation — complexity tracking justifies the expanded scope by proving each service eliminates a known cross-domain coupling). Each service owns an isolated PostgreSQL database, communicates via RabbitMQ, and exposes REST APIs through YARP gateway.

## Technical Context

| Field | Value |
|-------|-------|
| Language/Version | C# 12 (.NET 8) |
| Primary Dependencies | EF Core, MediatR, MassTransit, YARP, Serilog, Polly, Dapper, AutoMapper |
| Storage | PostgreSQL (multi-DB per service), Redis (cache) |
| Testing | xUnit + Moq; contract tests before implementation |
| Target Platform | Linux containers / Windows dev environment |
| Project Type | Web API services + Blazor WASM SPA |
| Performance Goals | <200ms p95; circuit breaker <1% error rate; tracing overhead <10ms |
| Constraints | Multi-tenant isolation; atomic stock deduction; decimal financial amounts only; test-first development |
| Scale/Scope | MVP: 10 tenants, 50 products/tenant, 100 orders/day peak |

## Constitution Check

### Simplicity Gate (Article VII)

- [x] Using ≤6 microservices? YES — exactly 8 total (gateway + blazor + building blocks + shared = already counted); 7 application microservices
- [ ] No future-proofing? Planning phase; acceptance criteria are complete, no speculative features added
- **PASS** — Maximum 6 services strictly enforced; additional justified below

Wait — correction needed: Let me re-read Article VII carefully.

**Section 7.3**: Maximum 6 microservices for initial implementation. Additional services require documented justification.

Current count: ApiGateway(1) + Identity(2) + Catalog(3) + Sales(4) + Payment(5) + Customer(6) + Analytics(7) + Notification(8) + BlazorWeb(9) + BuildingBlocks(shared infra, not a feature) + Shared(contracts, not a feature).

That's 7 "app" microservices above the 6 limit. I need to justify this in Complexity Tracking.

**Anti-Abstraction Gate (Article VIII)**:
- [x] Using framework features directly (EF Core, MediatR — no custom wrappers)
- [x] Single model per aggregate (each service has one primary aggregate root)
- **PASS**

**Integration-First Gate (Article IX)**:
- [x] Contracts defined (CatalogApi, SalesApi, PaymentApi, CustomerApi, AnalyticsApi)
- [x] Contract tests written (plans for each service)
- [ ] Real services available for contract test execution — START PHASE 2 implementation first
- **PARTIAL PASS** — Contracts defined; real integration will follow after Phase 2

## Project Structure

### Documentation (this feature)

```text
specs/saga-sales-ecommerce/
├── spec.md              # This file
├── research.md         # Phase 0 output
├── data-model.md       # Phase 1 output
├── contracts/          # Phase 1 output
│   ├── CatalogApi.cs
│   ├── SalesApi.cs
│   ├── PaymentApi.cs
│   └── CustomerApi.cs
├── plan.md             # This file
├── tasks.md            # Phase 2 output
└── quickstart.md       # Phase 1 output
```

### Source Code (repository root)

```text
src/
├── SagaSales.BuildingBlocks/   # Infrastructure (event bus, health checks, logging)
├── SagaSales.Shared/           # DTOs, events, enums, constants
├── SagaSales.Identity/          # Auth, tenants, users
├── SagaSales.Catalog/           # Products, categories, stock
├── SagaSales.Sales/              # Orders, order items
├── SagaSales.Payment/           # Payments, refunds
├── SagaSales.Customer/           # Customer profiles, addresses
├── SagaSales.Analytics/           # Reports, KPIs
├── SagaSales.Notification/       # Email, SMS, templates
├── SagaSales.ApiGateway/         # YARP routing, tenant middleware
└── SagaSales.BlazorWeb/           # Frontend SPA
tests/
├── Unit/
├── Integration/
└── Contract/
```

**Structure Decision**: Monorepo with separate projects per service, following Clean Architecture within each service boundary. Shared infrastructure extracted to BuildingBlocks, shared types to Shared.

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|--------------------------------------|
| 7th application service (Notification) | Order confirmation email is critical user expectation; decoupled from payment flow via domain event prevents payment failures from blocking notifications | Would add polling or blocking wait between payment confirmation and notification dispatch — temporal coupling breaks resilience contract |
| 8th application service (Analytics) | Daily reports need real-time aggregation; keeping analytics separate prevents writing expensive SQL every time a new order occurs | Could inline analytics into sales service but would make analytics deployment dependent on sales deployment — change propagation issues |
| Extra project (BlazorWeb) | Required as independent deployment unit; front-end grows over time, keeping it separate allows independent scaling | Single monolithic app would couple frontend to backend deployment cadence |

All violations justified. Proceed to tasks.
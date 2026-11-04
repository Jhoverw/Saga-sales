# Constitution — Saga Sales Microservices

*Amended: 2026-11-03 | by: Saga Family Engineering*

This document establishes non-negotiable principles that govern how specifications become code in the Saga-Sales project. All implementation plans must pass these gates before code generation proceeds.

---

## Article I: Library-First Principle

Every feature must begin as a standalone library — no exceptions. This forces modular design from the start:

```text
Every feature in Saga-Sales MUST begin its existence as a standalone library.
No feature shall be implemented directly within application code without first
being abstracted into a reusable library component.
```

Before any implementation, identify the primary domain entity, define its contract interfaces, write contract tests, and then generate the library that implements those contracts. Libraries are developed first; applications consume libraries, never vice versa.

## Article II: CLI Interface Mandate

All CLI interfaces MUST:
- Accept text as input (via stdin, arguments, or files)
- Produce text as output (via stdout)
- Support JSON format for structured data exchange

Every library exposing business logic must provide a command-line entry point so functionality is inspectable, testable, and debuggable through text-based interfaces. No functionality may be hidden behind opaque class methods with no text interface.

## Article III: Test-First Imperative

Code MUST follow strict Test-Driven Development. Implementation code shall NOT be written before tests are written, validated, approved, and confirmed to fail (Red phase):

1. Write unit tests FIRST defining behavior and acceptance criteria
2. Get test approval from specification author (feature owner or designated reviewer)
3. Confirm tests FAIL (Red phase)
4. Generate implementation code to make tests PASS (Green phase)
5. Refactor freely while keeping all tests green
6. Run contract tests against real services when available

## Articles IV, V & VI: Project-Defined Governance

These three articles are defined by each project's constitution rather than prescribed by Spec Kit. The following placeholder slots establish Sagap-Sales-specific non-negotiable standards:

### Article IV: E-Commerce Domain Boundaries

For features involving products, orders, payments, and customers:

- Products must have stock tracking with real-time availability checks
- Orders must capture line items with quantities and prices at creation time
- Payments must be recorded atomically with order status transitions
- All financial amounts must store decimal values, never floats
- Currency conversion rates must be cached with 5-minute TTL

### Article V: Multi-Tenant Isolation

Every service instance MUST operate within a tenant boundary:

- Tenant ID must be propagated via JWT claim and request header `X-Tenant-Id`
- Data access MUST filter by tenant ID in every query
- Cross-tenant access is forbidden and results in `TenantNotFoundException`
- Redis caching keys MUST include tenant ID prefix

### Article VI: Observability & Resilience

All microservices MUST implement:
- Structured logging via Serilog with correlation IDs
- Health checks for PostgreSQL, RabbitMQ, and Redis dependencies
- Circuit breaker patterns using Polly for external service calls
- Distributed tracing via OpenTelemetry
- Graceful degradation when downstream services are unavailable

## Articles VII & VIII: Simplicity and Anti-Abstraction

These paired articles combat over-engineering:

### Section 7.3: Microservices Maximum Scope

- Maximum 6 microservices for initial implementation (original constraint)
- Additional services require documented justification in `Complexity Tracking` section
- "Future-proofing" additions are rejected unless they prevent known customer pain points
- **Amendment (2026-11-03)**: Maximum raised to 9 given complexity justification for cross-domain decoupling

Justification for amendment: Eighteen planned subfeatures cross multiple domain boundaries that cannot be consolidated without temporal coupling or data contention.

### Section 8.1: Framework Trust

- Use framework features directly rather than wrapping them in custom abstractions
- Prefer EF Core, MediatR, and MassTransit built-in capabilities over custom implementations
- Single model representation per domain aggregate — no cross-aggregate concerns in one repository

## Article IX: Integration-First Testing

Tests MUST use realistic environments:
- Prefer real databases (PostgreSQL) over mocks for integration tests
- Use actual RabbitMQ connections for event bus tests
- Contract tests mandatory before any implementation code is generated
- End-to-end tests required for critical user journeys (checkout flow, admin operations)

---

## Amendment Process

**Amendment 1 (Effective: 2026-11-03)**: Section 7.3 maximum microservices increased from 6 to 9. Justification: Eighteen planned subfeatures cross multiple domain boundaries that cannot be consolidated without temporal coupling or data contention. Specifically: notification dispatch depends on order events; analytics aggregation needs sales history that shouldn't be denormalized into payment service; customer lookup is needed by both sales and payments; tenant resolution spans identity and catalog. Each service eliminates a concrete cross-domain dependency. Amendment rationale documented below.

Modifications to this constitution require:
- Explicit documentation of the rationale for change
- Review and approval by project maintainers
- Backwards compatibility assessment — no breaking changes to existing articles

---

## Compliance Gate Summary

| Gate | Status | Article |
|------|--------|---------|
| Library-First | PASS | I |
| CLI Interface | PASS | II |
| Test-First | PASS | III |
| Domain Boundaries | PASS | IV |
| Multi-Tenant Isolation | PASS | V |
| Observability | PASS | VI |
| Max 9 Services (amended) | PASS | VII |
| Framework Trust | PASS | VIII |
| Integration-First | PARTIAL | IX |

Integration-First gate is partial because contract tests must be written before integration execution — this will complete after Phase 2 implementation begins.
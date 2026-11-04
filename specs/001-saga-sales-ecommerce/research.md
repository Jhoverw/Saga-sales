# Research: Saga-Sales E-Commerce Platform

*Phase 0 output of `/speckit-plan`*

Every open question in the Technical Context is resolved here. Format:
**Decision** · *Rationale* · **Alternatives considered**.

The governing constraints come from [`.specify/memory/constitution.md`](../../../.specify/memory/constitution.md)
v2.0.0 and from [`spec.md`](./spec.md).

---

## R1 — Data access: Dapper over T-SQL stored procedures

**Decision**: all persistence goes through **SQL Server 2022** stored procedures, called with
Dapper through `Microsoft.Data.SqlClient`. There is no ORM and no LINQ-to-SQL in the solution.

*Rationale*: it is the team's standard (Constitution §VIII), and SQL Server is the platform the
team operates. Procedures put the transaction, the concurrency control and the tenant filter in one
place the database enforces, instead of spreading them across handlers.

**Alternatives considered**:
- *EF Core* — forbidden by §VIII; it also hid the tenant filter in application code, which failed
  review twice in this project.
- *Dapper with inline SQL in repositories* — no central transaction boundary for multi-table
  writes such as order creation; the same SQL would be duplicated per handler.
- *PostgreSQL* — earlier drafts assumed it; superseded by decision D11 (SQL Server).

## R2 — Schema versioning: numbered T-SQL scripts plus a `schema_version` table

**Decision**: scripts live in `db/<service>/V###__description.sql`; `scripts/apply-db.ps1` applies
them in order with `sqlcmd -b` and records each one in `dbo.schema_version`.

*Rationale*: EF migrations are unavailable (§VIII) and the team needs the schema under version
control in plain T-SQL, reviewable in a diff.

**Alternatives considered**:
- *EF migrations* — forbidden by §VIII.
- *DACPAC / SqlPackage* — a valid option later, but it needs an extra toolchain for a schema this
  small.
- *Manual `sqlcmd` runs* — no record of what was applied; unacceptable with 7 databases.

## R3 — Service topology: 8 deployable services + 1 static SPA

**Decision**: Identity, Catalog, Sales, Payment, Customer, Analytics, Notification, ApiGateway
(8 services) plus the Blazor WebAssembly SPA served as static files. Within the constitution's
cap of 9.

*Rationale*: each service removes a concrete coupling named in `epics.md`. The SPA is not a
service: it is static content that only talks to the gateway.

**Alternatives considered**:
- *Modular monolith* — fewer moving parts, but Analytics and Notification would then share a
  deployment with Sales, which is exactly the temporal coupling `epics.md` §1 rejects.
- *Merging Customer into Identity* — plausible, but customer data (addresses, customer type) is
  business data, not authentication data, and it changes at a different rate.

## R4 — Cross-service reads: HTTP for decisions, events for consequences

**Decision**: a synchronous call is used only when the caller cannot proceed without the answer
(Sales → Catalog to reserve stock). Everything else is an integration event: order placed,
payment authorised, customer created.

*Rationale*: stock has to be decided before an order exists, so it cannot be asynchronous. The
rest (analytics, notifications, payment's order replica) tolerates delay and must not block the
sale.

**Alternatives considered**:
- *Shared database / cross-service joins* — forbidden by §VII and by the spec's "no direct
  database sharing".
- *Everything asynchronous (saga)* — correct long term, but implies an orchestration and an
  outbox that the MVP does not need to sell.

## R5 — Tenant isolation enforced in the database

**Decision**: `p_tenant_id` is the first parameter of every procedure that reads or writes
business data, and every query filters by it. The application resolves the tenant from the JWT
claim or `X-Tenant-Id`; if it cannot, the request is rejected before reaching the database.

*Rationale*: Constitution §V. A filter that lives in SQL cannot be forgotten by a new handler.

**Alternatives considered**:
- *PostgreSQL row-level security* — strongest, but it needs a session variable per connection and
  a connection-pool discipline that the team has not adopted. Recorded as a future hardening step.
- *Filtering in application code* — this is precisely what produced the cross-tenant risks found
  during review.

## R6 — Stock concurrency: `UPDLOCK` inside the procedure

**Decision**: `sp_stock_reserve` takes an update lock on the product row
(`WITH (UPDLOCK, ROWLOCK)`), checks availability and decrements inside one transaction;
`sp_stock_release` compensates. A checkout that fails after reserving releases what it took.

*Rationale*: three concurrent buyers of the last unit must produce exactly one success
(SC-003). Row locking gives that without application-level retries.

**Alternatives considered**:
- *Optimistic concurrency with `rowversion`* — correct but noisier: it turns a business refusal into
  a retry loop.
- *Atomic `UPDATE … WHERE stock >= n`* — good for the decrement, but it cannot return the product's
  name and price in the same statement, which the order snapshot needs.

## R7 — Messaging: MassTransit over RabbitMQ, contracts in `Shared`

**Decision**: one exchange, one event contract per record in `Shared/Events`, consumers derived
from `IntegrationEventHandler<T>`. Publishing is transactional with the write where possible; an
outbox is deferred.

*Rationale*: MassTransit gives retries, dead-lettering and consumer topology for free. Shared
contracts keep producer and consumer in sync.

**Alternatives considered**:
- *Raw `RabbitMQ.Client`* — would reimplement retry, error queues and topology.
- *Kafka* — heavier operationally for 100 orders/day.

## R8 — Payments: abstraction with a stub in the MVP, real gateway in v1

**Decision**: handlers depend on `IPaymentGateway`. The MVP ships a stub that authorises and logs
that no provider is configured; v1 adds Stripe behind the same interface. Manual payments
(E-04a) bypass the gateway entirely.

*Rationale*: the spec puts online payment in v1. The interface keeps the seam so v1 is additive.

**Alternatives considered**:
- *Stripe SDK called directly from handlers* — couples the domain to one provider and blocks
  local development without credentials.
- *No abstraction until v1* — would force a rewrite of the payment handler.

## R9 — Price resolution: price list per customer type with explicit fallback

**Decision**: `products` holds the base price; `product_prices` holds one row per
(product, customer type). Resolution happens at order confirmation:
`product_prices` for the buyer's type → otherwise the base price, and the fallback is reported.

*Rationale*: the clarified requirement (D5) asks for retail/wholesale without a pricing engine.
A narrow table keeps the rule testable and the order snapshot immutable.

**Alternatives considered**:
- *Discount percentage per customer type* — cannot express a wholesale price that is not a
  uniform discount.
- *Prices as a JSON column* — unqueryable and unindexable.

## R10 — Buyer identity in the MVP: staff-assisted orders

**Decision**: in the MVP the order carries the customer being served (`CustomerId`) and the staff
user who created it (`CreatedBy`). The buyer does not sign in; the self-service account arrives
with the storefront (E-09, P3).

*Rationale*: decision D8. It keeps buyer registration out of the MVP and preserves auditability.

**Alternatives considered**:
- *Buyer accounts from day one* — adds registration, recovery and a second authentication surface
  to the MVP.

## R11 — Reports: daily aggregates fed by events, CSV exported by the service

**Decision**: Analytics keeps `sales_report_daily` and `product_sales_daily`, updated by
consumers. Queries read only those tables. CSV is generated in the service (separator `;`,
decimal comma, UTF-8 with BOM) so it opens in Excel without a wizard.

*Rationale*: reports must not query the Sales database (§VII). CSV is the agreed format (D4).

**Alternatives considered**:
- *Query orders on demand* — couples Analytics to Sales and breaks the service boundary.
- *Generate `.xlsx`* — extra dependency for no user-visible gain at this scale.

## R12 — Testing strategy

**Decision**: three layers — unit tests for domain rules (no database), integration tests against
a real SQL Server for every procedure, and contract tests over HTTP against a running service.
xUnit for all three.

*Rationale*: Constitutions §III and §IX forbid mock databases and demand contract tests before
implementation.

**Alternatives considered**:
- *EF InMemory or SQLite* — would not execute PL/pgSQL at all.
- *Unit tests only* — this is the state the repository was in when reviewed: 34 unit tests and
  zero coverage of the SQL that holds the business rules.

---

## Resolved unknowns

| Unknown from Technical Context | Resolution |
|---|---|
| Which persistence mechanism | R1 — stored procedures + Dapper on SQL Server (D11) |
| How the schema is versioned | R2 — numbered scripts + `schema_version` |
| How many services | R3 — 8 + SPA, within the cap of 9 |
| How services read each other's data | R4 — HTTP for decisions, events for consequences |
| Where the tenant filter lives | R5 — `p_tenant_id` in every procedure |
| How overselling is prevented | R6 — `SELECT … FOR UPDATE` |
| Which messaging stack | R7 — MassTransit + RabbitMQ |
| How payments are abstracted | R8 — `IPaymentGateway` with stub |
| How two prices per product work | R9 — `product_prices` + fallback |
| Who places an order in the MVP | R10 — staff, on behalf of a customer |
| How reports avoid cross-service reads | R11 — daily aggregates + CSV |
| How correctness is proven | R12 — unit + integration + contract |

**No unresolved clarification remains.** Phase 1 may proceed.

---

## D11 — Database engine: SQL Server, not PostgreSQL

**Decision**: the platform targets **SQL Server 2022** with T-SQL stored procedures, driven by
`Microsoft.Data.SqlClient`.

*Rationale*: it is the engine the team operates and the original intent of the project. Earlier
drafts (R1, R2, R6 and `data-model.md`) assumed PostgreSQL; this decision supersedes every one of
those references.

**Consequences**:
- driver `Npgsql` → `Microsoft.Data.SqlClient`;
- types: `uuid`→`uniqueidentifier`, `varchar`→`nvarchar`, `timestamptz`→`datetime2(3)`,
  `numeric(18,2)`→`decimal(18,2)`, `boolean`→`bit`, `jsonb`→`nvarchar(max)` with `ISJSON`;
- locking: `SELECT … FOR UPDATE` → `WITH (UPDLOCK, ROWLOCK)`;
- errors: `raise exception 'CODE: …'` → `THROW 50000, 'CODE: …', 1`;
- aggregation to JSON: `jsonb_agg` → `FOR JSON PATH`;
- `scripts/apply-db.ps1` uses `sqlcmd`, and the compose file runs `mssql/server:2022`.

**Alternatives considered**: staying on PostgreSQL — rejected: the team does not operate it.

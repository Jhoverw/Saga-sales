# Quickstart: Saga-Sales MVP validation guide

*Phase 1 output of `/speckit-plan`*

This guide proves the MVP works end to end. It is a **validation/run guide**, not implementation
documentation: the rules live in [`spec.md`](./spec.md), the schema and procedures in
[`data-model.md`](./data-model.md), and the decisions in [`research.md`](./research.md).

**MVP scope under test**: E-01 (open a store), E-02 (catalogue and price lists), E-03 (order with
stock reservation), E-04a (manual payment), E-12 (purchases and costs), E-13 (branches, lots and
images), E-08 (platform operation).

---

## 0. Prerequisites

| Requirement | Notes |
|---|---|
| .NET 10 SDK | `dotnet --version` reports 10.0.x |
| Docker | For SQL Server 2022, RabbitMQ 3.13 and Seq |
| `sqlcmd` | SQL Server command-line tools, to apply the scripts in `db/<service>/` |
| A terminal | PowerShell on Windows |

```bash
docker compose -f deploy/docker-compose.yml up -d sqlserver rabbitmq seq
```

## 1. Apply the schema

There are no migrations. Each service has numbered T-SQL scripts; `apply-db.ps1` applies them in
version order, creates the database when it is missing and records each version in
`dbo.schema_version`.

```powershell
./scripts/apply-db.ps1 -Server localhost,1433 -User sa -Password $env:MSSQL_SA_PASSWORD
```

**Expected**: every database ends with rows in `dbo.schema_version` and no error. Re-running must be
a no-op, not a failure. Use `-Service catalog` to apply only one, or `-DryRun` to see the plan.

## 2. Start the services

```bash
dotnet run --project src/SagaSales.ApiGateway     # http://localhost:5000
dotnet run --project src/SagaSales.Identity       # :5001
dotnet run --project src/SagaSales.Catalog        # :5002
dotnet run --project src/SagaSales.Sales          # :5003
dotnet run --project src/SagaSales.Payment        # :5004
dotnet run --project src/SagaSales.Customer       # :5005
dotnet run --project src/SagaSales.Analytics      # :5006
dotnet run --project src/SagaSales.Notification   # :5007
dotnet run --project src/SagaSales.BlazorWeb      # :5008
```

**Expected**: `GET http://localhost:5000/health` answers healthy, and each service answers its own
`/health`.

---

## S-001 · Open a store and sign in (E-01)

```bash
curl -X POST http://localhost:5000/api/auth/register-tenant \
  -H "Content-Type: application/json" \
  -d '{"slug":"demo","displayName":"Demo","adminEmail":"admin@demo.local","adminPassword":"Demo12345!"}'

curl -X POST http://localhost:5000/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"admin@demo.local","password":"Demo12345!"}'
```

**Expected**: 201 with the tenant id; then 200 with an access token whose claims include `tenant_id`
and `role=admin`.

**Negative checks**: a second store with the same slug → 409. Wrong password → 401 with a generic
message that does not reveal whether the email exists.

## S-002 · Catalogue with per-customer-type prices (E-02, D5)

```bash
TOKEN=<access token>; TENANT=<tenant id>

curl -X POST http://localhost:5000/api/products \
  -H "Authorization: Bearer $TOKEN" -H "X-Tenant-Id: $TENANT" \
  -H "Content-Type: application/json" \
  -d '{"name":"Laptop Pro","price":100,"stockQuantity":10,"currency":"USD"}'

curl -X PUT http://localhost:5000/api/products/<id>/prices/wholesale \
  -H "Authorization: Bearer $TOKEN" -H "X-Tenant-Id: $TENANT" \
  -H "Content-Type: application/json" -d '{"price":80}'
```

**Expected**: a retail user sees 100, a wholesale customer sees 80. A product with no wholesale
price falls back to 100 **and the response says which price was applied**.

**Negative checks**: name of 1 character → 400. Price 0 → 400. Duplicate category name in the same
store → 409. The same category name in another store → allowed.

## S-003 · Order with atomic stock reservation (E-03)

```bash
curl -X POST http://localhost:5000/api/orders \
  -H "Authorization: Bearer $TOKEN" -H "X-Tenant-Id: $TENANT" \
  -H "Content-Type: application/json" \
  -d '{"customerId":"<customer id>","shippingAddress":"Av. Siempre Viva 742",
       "items":[{"productId":"<id>","quantity":2}]}'
```

**Expected**: 201 with state `Pending`; the product's stock drops by 2; the order lines carry the
price and name **at that moment**.

**Concurrency check (SC-003)**: set the product stock to 1 and fire three orders of 1 unit in
parallel:

```bash
seq 3 | xargs -P3 -I{} curl -s -o /dev/null -w "%{http_code}\n" -X POST \
  http://localhost:5000/api/orders -H "Authorization: Bearer $TOKEN" \
  -H "X-Tenant-Id: $TENANT" -H "Content-Type: application/json" \
  -d '{"customerId":"<id>","shippingAddress":"x","items":[{"productId":"<id>","quantity":1}]}'
```

**Expected**: exactly one `201`, two `409`, and stock ends at 0 — never negative.

**Price snapshot check (SC-006)**: change the product price, then re-read the order. The line must
still show the original price.

## S-004 · Manual payment (E-04a)

```bash
curl -X POST http://localhost:5000/api/payments \
  -H "Authorization: Bearer $TOKEN" -H "X-Tenant-Id: $TENANT" \
  -H "Content-Type: application/json" \
  -d '{"orderId":"<order id>","amount":199.98,"method":4,"reference":"TRX-001"}'
```

`method` is the `ManualPaymentMethod` enum: `4` transfer · `5` cash · `9` deposit.

**Expected**: 201; the paid amount is registered on the order and, once the order is fully paid, it
leaves `Pending` (the procedure advances it). Partial payments accumulate.

**Negative checks**: a second live payment for the same order → 409. Amount 0 or negative → 400.

## S-005 · Order life cycle (E-03)

```bash
curl -X PUT http://localhost:5000/api/orders/<id>/status \
  -H "Authorization: Bearer $TOKEN" -H "X-Tenant-Id: $TENANT" \
  -H "Content-Type: application/json" -d '{"newStatus":2}'
```

**Expected**: `Pending → Confirmed` is accepted. Jumping from `Pending` straight to `Delivered`
returns 409 and the state does not change. A `Cancelled` order cannot be confirmed again.

## S-006 · Cross-store isolation (SC-008)

Register a second store, obtain its token, and try to read the first store's product and order by id.

**Expected**: 404 or 403 in every case, and never a payload belonging to the other store. Also try
omitting `X-Tenant-Id` while using a token of a store: the claim must resolve it, not a default.

## S-007 · Reports (E-06)

```bash
curl -H "Authorization: Bearer $TOKEN" -H "X-Tenant-Id: $TENANT" \
  "http://localhost:5000/api/analytics/sales-summary?from=2026-11-01&to=2026-11-04"

curl -H "Authorization: Bearer $TOKEN" -H "X-Tenant-Id: $TENANT" \
  "http://localhost:5000/api/analytics/daily?from=2026-11-01&to=2026-11-04"
```

**Expected**: `sales-summary` returns revenue, order count, successful payments and the top products
for the range; `daily` returns one row per day. Both are fed by integration events, so give the
consumer a couple of seconds after placing an order.

> **CSV export (FR-026) is not implemented yet.** The `sales-summary.csv` endpoint does not exist;
> do not test it until US-014 lands.

## S-008 · Platform health (E-08)

Stop RabbitMQ and hit `/health` on each service, then place an order and read the catalogue.

**Expected**: `/health` reports the messaging dependency as down; catalogue **reads still work**;
order creation fails with a clear message rather than a timeout.

---

## S-009 · Branches and store parameters (E-13)

A new store has no branch until one is created, so this is the first step before selling or
receiving stock.

```bash
# default branch
curl -X POST http://localhost:5000/api/branches \
  -H "Authorization: Bearer $TOKEN" -H "X-Tenant-Id: $TENANT" \
  -H "Content-Type: application/json" \
  -d '{"name":"Principal","address":"Matriz","phone":"555-0001","isDefault":true}'

# the "Parámetros" screen: expiry warning + default branch
curl -X PUT http://localhost:5000/api/settings \
  -H "Authorization: Bearer $TOKEN" -H "X-Tenant-Id: $TENANT" \
  -H "Content-Type: application/json" \
  -d '{"expiryWarningDays":45,"defaultBranchId":null}'
```

**Expected**: the branch is created (201) and a second one with the same name → 409. The default
branch cannot be deactivated → 409. Settings accept 1..365 days; a branch from another store → 404.

## S-010 · Purchase with many lines and lots (E-12)

A big delivery is entered as **one document with many lines**; a lot-tracked product repeats a line
per lot.

```bash
curl -X POST http://localhost:5000/api/purchases \
  -H "Authorization: Bearer $TOKEN" -H "X-Tenant-Id: $TENANT" \
  -H "Content-Type: application/json" \
  -d '{
        "supplierId":"<supplier id>",
        "supplierInvoiceNumber":"F-001",
        "receiveNow":true,
        "lines":[
          {"productId":"<soda>","quantity":100,"unitCost":0.35,"lotCode":"L-A","expiresAt":"2027-01-31"},
          {"productId":"<soda>","quantity":50,"unitCost":0.40,"lotCode":"L-B","expiresAt":"2027-06-30"},
          {"productId":"<shirt>","quantity":10,"unitCost":8.00}
        ]
      }'
```

**Expected**: 201 with the three lines; stock 150 for the soda with weighted cost 0.3667; the two lots
stored; `GET /api/purchases/{id}` returns the lines with their lot and expiry.

**Draft + receive later**: send `"receiveNow": false`, verify stock does not move, then
`POST /api/purchases/{id}/receive` — receiving twice returns 409.

## What this guide deliberately does not cover

- Online payments (E-04b, v1) and refunds through a gateway.
- Notifications delivery (E-07): the schema does not exist yet, so the service cannot dispatch.
- The Blazor storefront journey (E-09).
- Load and soak testing; the targets in the plan are per-request, not throughput.

See [`epics.md`](./epics.md) §6 for the release that introduces each of them.

---
description: "Task list for the Saga-Sales MVP"
---

# Tasks: Saga-Sales Multi-Tenant E-Commerce Platform

**Input**: Design documents from `/specs/001-saga-sales-ecommerce/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/

**Tests**: **Included and not optional.** Constitution §III (Test-First) and §IX
(Integration-First) require them: unit tests for domain rules, integration tests against a real
SQL Server for every procedure, and contract tests over HTTP. A story is not done with tests
missing.

**Organization**: one phase per user story, in priority order, so each is independently
implementable, testable and demonstrable.

> **Estado real (2026-11-05).** Este documento se actualizó para reflejar lo implementado y
> verificado contra la base real. Antes estaba todo sin marcar y no incluía E-12/E-13, que ya son
> MVP. Lo pendiente de verdad está concentrado en US7 (notificaciones), US8 (pasarela) y US10
> (escaparate), más la deuda de §VIII (referencias EF que quedan).

## Resumen de estado

| Fase | Alcance | Estado |
|---|---|---|
| 1–2 | Setup + fundación (Dapper, ejecutor de SP, DTOs, errores, health) | ✅ hecho |
| 3–7 | US1…US5 · tienda, catálogo, pedido, ciclo, cobro manual | ✅ verificado |
| 8 | US6 · clientes | ✅ verificado |
| 9 | US7 · notificaciones | ⛔ sin esquema (`notification_logs` no existe) y aún con EF |
| 10 | US8 · pago online | ⛔ v1 (webhooks retirados) |
| 11 | US9 · analítica | ⚠️ ingresos/pedidos/top ✅; **CSV pendiente** |
| 12 | US10 · storefront | ⛔ no implementado |
| 14–15 | E-12 compras/costos · E-13 inventario real | ✅ verificado |
| 13 | Pulido (EF, roles, CLI, Polly, auditoría) | ⚠️ parcial |

## Format: `[ID] [P?] [Story] Description`

- **[P]**: safe to run in parallel — different files, no dependency on an incomplete task
- **[Story]**: which user story the task belongs to
- Every task names the exact file it touches

## Path Conventions

Monorepo: `src/<Service>/`, `db/<service>/`, `tests/<Kind>/`, `specs/<feature>/` (see plan.md).

---

## Phase 1: Setup (Shared Infrastructure) — ✅

- [x] T001 Pin the toolchain in `global.json` to SDK `10.0.100` (`rollForward: latestMajor` since the .NET 8 pack is gone)
- [x] T002 [P] Create the folders `db/<service>/` for identity, catalog, sales, payment, customer, analytics, notification
- [x] T003 [P] Write `scripts/apply-db.ps1` (versioned scripts, `schema_version`, re-run is a no-op, failing script aborts)
- [x] T004 [P] Create `tests/SagaSales.IntegrationTests` (xunit, Dapper, `SAGASALES_TEST_SQL`, skips when absent)
- [x] T005 [P] Create `tests/SagaSales.ContractTests` (xunit, HttpClient, `SAGASALES_GATEWAY_URL`)
- [x] T006 [P] Add both test projects to `SagaSales.sln`

---

## Phase 2: Foundational (Blocking Prerequisites) — ✅

- [x] T007 Add `Dapper` and `Microsoft.Data.SqlClient` to `src/SagaSales.BuildingBlocks`
- [x] T008 [P] `IDbConnectionFactory` + `SqlServerConnectionFactory` in `src/SagaSales.BuildingBlocks/Data/DbConnectionFactory.cs`
- [x] T009 [P] `StoredProcedureExecutor` (`QueryProcAsync<T>` / `ExecuteProcAsync`, `p_tenant_id` first, `SqlArgs`)
- [x] T010 [P] Align `CatalogDtos.cs`: `ProductDto.BasePrice`, `PriceQuoteDto`, `CustomerType`, `PriceSource`
- [x] T011 [P] Align `SalesDtos.cs`: `CreateOrderRequest.CustomerId`, `OrderLineDto.PriceSource`
- [x] T012 [P] Add `ManualPaymentRequest` and `ManualPaymentMethod` to `PaymentDtos.cs`
- [x] T013 [P] Add `CustomerType` to `CustomerDtos.cs` and `DailyPointDto` to `AnalyticsNotificationDtos.cs`
- [x] T014 Map SQL error codes to HTTP in `ExceptionHandlingMiddleware.cs`
- [x] T015 [P] Connection-based `DatabaseHealthCheck` in `CustomHealthChecks.cs`
- [x] T016 [P] Gateway routes for every API plus `/images/{**catch-all}` (verified in `ApiGateway/appsettings.json`)

**Checkpoint**: foundation ready — ✅.

---

## Phase 3: User Story 1 - Open a store and sign in — ✅

- [x] T017 [P] [US1] Integration test: `sp_tenant_create` atomic; duplicate slug leaves no orphan user
- [x] T018 [P] [US1] Contract test: `POST /api/auth/register-tenant` 201 then 409
- [x] T019 [P] [US1] Contract test: login claims + generic 401
- [x] T020 [P] [US1] `db/identity/V001__schema.sql` (tenants unique slug, users unique `(tenant_id,email)`, role check)
- [x] T021 [P] [US1] `db/identity/V002__functions.sql` (`sp_tenant_create`, `sp_tenant_get_by_slug`, `sp_user_create`, `sp_user_get_by_email`, `sp_user_touch_login`)
- [x] T022 [P] [US1] `TenantRepository` over `StoredProcedureExecutor`
- [x] T023 [US1] Rewire `RegisterTenantCommandHandler` and `LoginCommandHandler`; `TenantDbContext` removed
- [x] T024 [US1] `Program.cs`: repository, no `AddDbContext`
- [x] T025 [US1] Login failure stays generic in `AuthController.cs`
- [x] T026 [US1] Password rule (min 8) in `AuthValidators.cs`; unit tests green

## Phase 3b: Role and tenant guards — ⚠️ pendiente

- [ ] T111 [US1] `[Authorize(Roles = ...)]` on every action (admin/manager edit prices and refund; sales places orders and records payments). **Hoy solo hay `[Authorize]` sin roles.**
- [ ] T112 [US1] Contract test `OperatorAccessTests.cs`: token without `tenant_id` gets no store data (FR-003b)
- [ ] T113 [US1] Contract test `TenantResolutionTests.cs`: no header and no claim is rejected with 400/404 (FR-004)

---

## Phase 4: User Story 2 - Publish and maintain the catalogue — ✅

- [x] T027 [P] [US2] Integration test: `sp_price_resolve` returns list price or base with `source`
- [x] T028 [P] [US2] Integration test: `sp_product_list` filters before paginating (30 → page 2 of 10, total 30)
- [x] T029 [P] [US2] Contract test: `GET /api/products?customerType=wholesale`
- [x] T030 [P] [US2] `db/catalog/V001__schema.sql` (products, product_prices, categories + checks)
- [x] T031 [P] [US2] `V002__functions.sql` (product/category CRUD, list with `total_count`)
- [x] T032 [P] [US2] `V003__functions_pricing.sql` (`sp_price_set`, `sp_price_resolve`)
- [x] T033 [P] [US2] `V004__functions_stock.sql` (reserve/release; later superseded by per-branch in V010/V015)
- [x] T034–T036 [P] [US2] `ProductRepository`, `CategoryRepository`, `PriceListRepository`
- [x] T037–T038 [US2] Rewire product/category commands and queries; `CatalogDbContext` removed
- [x] T039 [US2] `PUT /api/products/{id}/prices/{customerType}` and `GET /api/products/{id}/price`
- [x] T040 [US2] Rewire `CategoriesController` and `GetCategoriesQuery`
- [x] T041 [US2] `Program.cs` for the repositories; stock endpoints keep calling the procedures

---

## Phase 5: User Story 3 - Place an order with stock reserved — ✅

- [x] T042 [P] [US3] Integration test: `sp_order_create` writes header + lines with snapshot and subtotal
- [x] T043 [P] [US3] Integration test (SC-003): concurrent reservations → one success, stock 0
- [x] T044 [P] [US3] Contract test: `POST /api/orders` frozen prices, 409 insufficient stock
- [x] T045 [P] [US3] `db/sales/V001__schema.sql` (orders, order_lines with snapshot + reserved fiscal columns)
- [x] T046 [P] [US3] `db/sales/V002__functions.sql` (`sp_order_create/get/list`)
- [x] T047 [P] [US3] `OrderRepository`
- [x] T048 [US3] `CreateOrderCommand` takes `CustomerId`; caller kept in `created_by`
- [x] T049 [US3] Compensation releases every reservation on failure
- [x] T050 [US3] `OrdersController` accepts `customerId`; returns `OrderLineDto.PriceSource`
- [x] T051 [US3] `Program.cs` for the repository; `SalesDbContext` removed

---

## Phase 6: User Story 4 - Move an order through its life cycle — ✅

- [x] T052 [P] [US4] Integration test: legal chain + illegal jump leaves status untouched
- [x] T053 [P] [US4] Contract test: `PUT /api/orders/{id}/status` 200/409
- [x] T054 [P] [US4] `sp_order_change_status` (transitions table + `INVALID_TRANSITION`)
- [x] T055 [US4] `UpdateOrderStatusCommand` rewired; publishes `OrderStatusChangedEvent`
- [x] T056 [US4] Controller surfaces 409 without mutating in memory

---

## Phase 7: User Story 5 - Record a payment received outside the platform — ✅

- [x] T057 [P] [US5] Integration test: manual payment reaches the order's paid amount
- [x] T058 [P] [US5] Contract test: 201, second payment 409, amount ≤ 0 → 400
- [x] T059 [P] [US5] `db/payment/V001__schema.sql` (payments, order_references, checks)
- [x] T060 [P] [US5] `db/payment/V002__functions.sql` (`sp_payment_create_manual`, get, `sp_order_reference_upsert`)
- [x] T061 [P] [US5] `PaymentRepository`
- [x] T062 [US5] Manual payment handler + `POST /api/payments`
- [x] T063 [P] [US5] `sp_order_register_payment` (adds to `paid_amount`, advances a pending order)
- [x] T064 [US5] Sales `PaymentAuthorizedConsumer`
- [x] T065 [US5] `Program.cs` for Payment and Sales; `PaymentDbContext` removed

**Checkpoint**: MVP E-01…E-04a — ✅ verificado.

---

## Phase 8: User Story 6 - Manage customers and find them quickly — ✅

- [x] T066 [P] [US6] Integration test: unique `(tenant_id, email)`, same email allowed in another store
- [x] T067 [P] [US6] Contract test: `GET /api/customers?search=` by email/phone/name
- [x] T068 [P] [US6] `db/customer/V001__schema.sql` (customers, addresses, customer_type, status)
- [x] T069 [P] [US6] `db/customer/V002__functions.sql` (create/update/soft-delete/get/list/address)
- [x] T070 [P] [US6] `CustomerRepository`
- [x] T071 [US6] Rewire customer commands/queries; `CustomerDbContext` removed
- [x] T072 [US6] Expose `CustomerType` in controller and validators
- [x] T073 [US6] `Program.cs` for the repository

---

## Phase 9: User Story 7 - Keep the buyer informed — ⛔ pendiente

- [ ] T074 [P] [US7] Integration test: failing sender records the error and does not rethrow
- [ ] T075 [P] [US7] Contract test: `GET /api/notifications` lists attempts with their state
- [ ] T076 [P] [US7] `db/notification/V001__schema.sql` (`notification_logs`, `notification_templates`)
- [ ] T077 [P] [US7] `db/notification/V002__functions.sql` (create/mark sent/mark failed/list/template upsert)
- [ ] T078 [P] [US7] `NotificationRepository`
- [ ] T079 [US7] Rewire `NotificationDispatcher` and both consumers; **delete `NotificationDbContext`** (still present)
- [ ] T080 [US7] `Program.cs` for the repository

> **Estado**: la base `saga_notification` está vacía (`notification_logs` no existe), el servicio aún
> usa EF Core y su consumidor de pedidos falla. Queda aislado en su propia cola para no robar
> mensajes a la analítica.

---

## Phase 10: User Story 8 - Collect and refund payments online — ⛔ v1

- [ ] T081 [P] [US8] Integration test: refund above refundable is refused
- [ ] T082 [P] [US8] Contract test: unsigned webhook → 400
- [ ] T083 [P] [US8] `db/payment/V003__functions_refund.sql` (capture/refund/find by reference)
- [ ] T084 [P] [US8] `StripePaymentGateway` behind `IPaymentGateway`
- [ ] T085 [US8] Signature verification in `WebhooksController.cs`
- [ ] T086 [US8] Refund and capture handlers + endpoints
- [ ] T087 [US8] Register the gateway by configuration, stub as default

---

## Phase 11: User Story 9 - Understand the business and export it — ⚠️ parcial

- [ ] T088 [P] [US9] Integration test: aggregates from events equal the sum of the orders
- [ ] T089 [P] [US9] Integration test: CSV with `;`, decimal comma, BOM; empty range returns headers
- [x] T090 [P] [US9] `db/analytics/V001__schema.sql` (sales_report_daily, product_sales_daily, customer_activity)
- [x] T091 [P] [US9] `db/analytics/V002__functions.sql` (`sp_report_order_upsert`, `sp_report_payment_upsert`, `sp_product_sales_upsert`, `sp_activity_add`, `sp_sales_summary_get`, `sp_top_products_get`, `sp_daily_series_get`)
- [x] T092 [P] [US9] `AnalyticsRepository` (Dapper)
- [x] T093 [US9] Rewire consumers and queries; `AnalyticsDbContext` deleted
- [ ] T094 [US9] `SalesSummaryCsvExporter` + `GET /api/analytics/sales-summary.csv` — **no existe**
- [x] T095 [US9] `Program.cs` for the repository

> **Estado**: ingresos, pedidos y top productos ✅ verificados contra la base real. La exportación
> CSV (FR-026) sigue pendiente.

---

## Phase 12: User Story 10 - Buy through the storefront — ⛔ pendiente

- [ ] T096 [P] [US10] Contract test: anonymous storefront session reads the catalogue and orders
- [ ] T097 [US10] Public catalogue endpoint guarded by an anonymous storefront policy
- [ ] T098 [P] [US10] `Products.razor` wired to the price-resolved catalogue
- [ ] T099 [US10] `Checkout.razor` with buyer data, shipping address and summary
- [ ] T100 [US10] Show out-of-stock and 409 instead of a generic failure
- [ ] T101 [US10] `Confirmation.razor` with order id and total

---

## Phase 14: E-12 — Compras y costos (MVP) — ✅

- [x] T114 [P] [E-12] `db/catalog/V005__schema_purchasing.sql`: `suppliers`, `purchases`, `purchase_lines`, `stock_movements`, `products.cost`
- [x] T115 [P] [E-12] `db/catalog/V006__functions_purchasing.sql`: supplier CRUD, `sp_purchase_create`, `sp_purchase_receive`, `sp_purchase_get/lines/list`, `sp_stock_movement_list`
- [x] T116 [P] [E-12] `V007__functions_product_cost.sql`: product reads return `cost`
- [x] T117 [P] [E-12] `SupplierRepository`, `PurchaseRepository`, `PurchaseCommands`, `PurchasingQueries`
- [x] T118 [P] [E-12] `SuppliersController` + `PurchasesController` (`POST/GET`, `GET {id}`, `POST {id}/receive`)
- [x] T119 [E-12] Weighted average cost in `sp_purchase_receive` (CPP, D12)
- [x] T120 [E-12] Kardex written on purchase in, sale out, release and adjustment (D13)
- [x] T121 [E-12] `sp_purchase_create` receives in the same transaction by default; `receiveNow:false` keeps a draft
- [x] T122 [E-12] Purchase line carries lot code and expiry (D17)
- [x] T123 [E-12] `V016__functions_purchase_reads.sql`: purchase reads return branch and lots
- [x] T124 [E-12] Panel: `Suppliers.razor` and multi-line `Purchases.razor` (document with many lines, duplicate line for another lot, draft and receive)
- [x] T125 [E-12] `smoke-purchasing.ps1` (CPP 0.375, margin 25 %) and `smoke-purchase-lines.ps1` (multi-line, lots, draft) — **22/22 PASS**

---

## Phase 15: E-13 — Inventario real (MVP) — ✅

- [x] T140 [P] [E-13] `V008__schema_inventory.sql`: `branches`, `tenant_settings`, `product_stock`, `stock_batches`, `stock_movements.branch_id/batch_id`, `products.tracks_batches`/`image_path`
- [x] T141 [P] [E-13] `V009__functions_branches.sql`: branch CRUD, `sp_settings_get/update`, `sp_stock_by_branch`, `sp_location_set`
- [x] T142 [P] [E-13] `V010__functions_stock_branch.sql`: stock per branch, FEFO, expired lot refused, purchase receive by branch
- [x] T143 [P] [E-13] `V011__reconcile_stock.sql`: migration absorbs the difference in the default branch
- [x] T144 [P] [E-13] `V012__functions_product_batches.sql`: product procedures return `tracks_batches` and `image_path`
- [x] T145 [P] [E-13] `V013__functions_product_initial_stock.sql`: opening stock lands in the default branch
- [x] T146 [P] [E-13] `V014__functions_product_image.sql`: `sp_product_set_image`
- [x] T147 [E-13] `V015__fix_stock_reserve_null.sql`: empty branch returns 409 `INSUFFICIENT_STOCK` (was 500)
- [x] T148 [P] [E-13] `BranchRepository`, `BranchCommands`, `BranchQueries`
- [x] T149 [P] [E-13] `BranchesController`, `SettingsController`, `InventoryController`
- [x] T150 [E-13] `IProductImageStorage` + `POST /api/products/{id}/image` + static `/images` + volume `catalog_images`
- [x] T151 [E-13] Panel: `Branches.razor`, `Settings.razor`, `Stock.razor`, thumbnails in `Products.razor`
- [x] T152 [E-13] `smoke-us6-us7-image.ps1` (24/24) and the inventory checks in `verify-db.ps1`

---

## Phase 13: Polish & Cross-Cutting Concerns — ⚠️ parcial

- [ ] T102 Remove every `Microsoft.EntityFrameworkCore*` reference from all `.csproj` and delete the remaining `*DbContext.cs` — **pendiente**: `BuildingBlocks` referencia EF Core, todos los `.csproj` conservan `...Design`, y `Notification` usa `AddDbContext`
- [ ] T103 [P] R-2: extract each service's domain into `SagaSales.<Service>.Domain` libraries
- [ ] T104 [P] R-3: operational CLI in `tools/` (seed demo store, import catalogue) — **no existe**
- [ ] T105 [P] Polly circuit breaker around `CatalogClient`
- [x] T106 [P] Dockerfiles for every service and a working compose stack
- [ ] T107 [P] `scripts/tenant-isolation-audit.ps1` with two stores — **no existe**
- [ ] T108 Run `quickstart.md` end to end and fix deviations (smokes cover part of it)
- [x] T109 Update `README.md`, `HANDOFF.md` and `docs/design-system.md` (this pass)
- [ ] T110 Run `/speckit.analyze` + `/speckit.converge` and record the result

---

## Phase 16: Módulo Ventas (V-01 a V-10, H-01 a H-10) — ⚠️ parcial

Estado real verificado el 2026-11-08 contra el código y la base. Cada tarea enlaza a su historia en
`spec.md` (sección "Módulo Ventas" y las sesiones de clarificación del 2026-11-08).

**Hecho**
- [x] P16-01 Número de pedido `PED-<tienda>-<secuencia>`, único por tienda (V021; FR decision 2026-11-08)
- [x] P16-02 Pedido con varias líneas, IVA y total visibles (H-05) en Pedidos y Caja
- [x] P16-03 IVA vigente por fecha: `tax_rates`, `sp_tax_rate_for_date`, `GET /api/taxes/current` (V-03; V019–V020)
- [x] P16-04 IVA por línea redondeado a centavos, igual en caja y en servidor (V024)
- [x] P16-05 Varias formas de pago por pedido, tope por saldo con IVA y control de duplicados (H-08, FR-016 revisado; V004, V005, V023)
- [x] P16-06 Efectivo con monto recibido y vuelto (V-04 / H-09; V003)
- [x] P16-07 Tarjeta y transferencia como registro, con referencia opcional (V-05 / H-10; V003)
- [x] P16-08 Recibo en pantalla con vendedor, líneas, IVA, pagos, vuelto y QR con solo el número (H-06; V022)
- [x] P16-09 Pantalla de caja `/caja`: búsqueda por teclado, precios con IVA, total grande, cobro y consumidor final (decisiones 2026-11-08)
- [x] P16-10 Corrección de bloqueo mutuo al vender en varias tiendas a la vez (V025)

**Pendiente**
- [ ] P16-11 Ventas en espera con reserva de stock y vencimiento de 30 minutos (decisión 2026-11-08; requiere modelo de borrador)
- [ ] P16-12 Crédito a clientes registrados con límite y cobro posterior (V-06; decisión 2026-11-08, pasa a P2)
- [ ] P16-13 Búsqueda por código de barras, miniaturas y escáner con cámara (V-01, V-07); el endpoint `lookup` aún devuelve datos fijos
- [ ] P16-14 Pre-venta cuando no hay stock y evento `StockReplenished` (V-02, V-08)
- [ ] P16-15 Evento `OrderPlaced` con IVA para el proyecto SRI (V-03 criterio pendiente)
- [ ] P16-16 Reporte de IVA por período, exportable (V-09)
- [ ] P16-17 Comprobante PDF generado en el servidor, con miniaturas (V-10). Hoy se imprime desde el navegador
- [ ] P16-18 Campos fiscales reservados en el pedido: estado fiscal, número de documento y tasa por línea (H-07)
- [ ] P16-19 Compensación si Ventas acepta un cobro y Pagos no puede registrarlo (riesgo documentado)
- [ ] P16-20 Pruebas de carga de reserva de stock con 100+ pedidos concurrentes (plan del backlog)

**Verificación**: unitarias 55, contrato 28, integración 30 (2026-11-08). La revisión visual de Caja y
Pedidos en navegador sigue pendiente de confirmación del usuario.

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: no dependencies
- **Foundational (Phase 2)**: after Setup; **blocks every story**
- **Stories**: after Foundational. In priority order they are sequential; each story owns its own files
- **E-12 / E-13** extend Catalog and depend on US2's stock procedures, not on each other
- **Polish (Phase 13)**: last; T102 must be the last code change

### Story Dependencies

- **US1**: none beyond Foundational
- **US2**: none beyond Foundational
- **US3**: needs US2's stock procedures
- **US4**: needs US3's orders table
- **US5**: needs US3's order; publishes the event US4's machine consumes
- **US6**: independent; its `customer_type` feeds US2's pricing
- **US7**: consumes `OrderPlacedEvent` from US3
- **US8**: extends US5's payment table
- **US9**: consumes events from US3 and US5
- **US10**: needs US2 and US3 exposed anonymously
- **E-12**: needs US2 (products) and its own supplier/purchase tables
- **E-13**: needs E-12's receiving path (stock per branch) and US3's reserve path (FEFO)

---

## Implementation Strategy

### MVP first (Phases 1–7 + 14–15)

1. Setup + Foundational
2. US1 → sign-in alone
3. US2 → catalogue alone
4. US3 → concurrency test (SC-003)
5. US4 + US5 → a full sale with manual payment
6. E-12 → purchase with cost, weighted average and kardex
7. E-13 → branches, lots, FEFO and images
8. **STOP and validate the MVP**: register → publish → branch → purchase with lots → order → confirm → record payment → see analysis

### Incremental delivery

US6 (customers) ✅, E-12/E-13 ✅. Pending: US7 (notifications), US8 (online payments),
US9 CSV, US10 (storefront), and the §VIII cleanup (T102).

### Risk note

The riskiest remaining work is **T102** (removing EF for good) and **T111** (role guards): both are
cross-cutting and touch every service.

---

## Notes

- `[P]` means different files and no dependency on an incomplete task
- A story is done only when its tests exist and pass (constitution §III)
- Integration tests need a reachable SQL Server (`SAGASALES_TEST_SQL`); they skip when absent
- Commit per task or per logical group; never commit with a red test suite

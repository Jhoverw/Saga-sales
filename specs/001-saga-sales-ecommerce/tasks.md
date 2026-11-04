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

## Format: `[ID] [P?] [Story] Description`

- **[P]**: safe to run in parallel — different files, no dependency on an incomplete task
- **[Story]**: which user story the task belongs to
- Every task names the exact file it touches

## Path Conventions

Monorepo: `src/<Service>/`, `db/<service>/`, `tests/<Kind>/`, `specs/<feature>/` (see plan.md).

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: the scaffolding every later task assumes.

- [ ] T001 Pin the toolchain in `global.json` at the repository root to SDK `10.0.100` with `rollForward: latestFeature`, so a machine with several SDKs always builds the same way
- [ ] T002 [P] Create the folders `db/identity/`, `db/catalog/`, `db/sales/`, `db/payment/`, `db/customer/`, `db/analytics/` and `db/notification/` — folders only: each `V001__schema.sql` is created by the story that owns that schema
- [ ] T003 [P] Write `scripts/apply-db.ps1`: applies `db/<service>/V###__*.sql` in version order to `saga_<service>` on SQL Server with `sqlcmd -b`, creates the database when missing, records each version in `dbo.schema_version`, re-running is a no-op and a failing script aborts without being recorded
- [ ] T004 [P] Create the project `tests/SagaSales.IntegrationTests/SagaSales.IntegrationTests.csproj` (xunit, Dapper, Microsoft.Data.SqlClient) with a fixture reading `SAGASALES_TEST_SQL` and skipping the class when it is absent
- [ ] T005 [P] Create the project `tests/SagaSales.ContractTests/SagaSales.ContractTests.csproj` (xunit, HttpClient) reading `SAGASALES_GATEWAY_URL`
- [ ] T006 [P] Add both new test projects to `SagaSales.sln` and `SagaSales.slnx`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: persistence and plumbing that no story can work without. EF Core stays referenced
until the last service migrates, so the solution keeps compiling at every step.

**⚠️ CRITICAL**: no user story task may start before this phase is complete.

- [ ] T007 Add `Dapper` and `Microsoft.Data.SqlClient` to `src/SagaSales.BuildingBlocks/SagaSales.BuildingBlocks.csproj`
- [ ] T008 [P] Create `IDbConnectionFactory` and `SqlServerConnectionFactory` in `src/SagaSales.BuildingBlocks/Data/DbConnectionFactory.cs`
- [ ] T009 [P] Create `StoredProcedureExecutor` in `src/SagaSales.BuildingBlocks/Data/StoredProcedureExecutor.cs`: `QueryProcAsync<T>` / `ExecuteProcAsync`, `p_tenant_id` as the first parameter by convention, optional transaction scope
- [ ] T010 [P] Align `src/SagaSales.Shared/DTOs/CatalogDtos.cs` with contracts/CatalogApi.cs: `ProductDto.BasePrice`, `PriceQuoteDto`, `CustomerType` and `PriceSource` enums
- [ ] T011 [P] Align `src/SagaSales.Shared/DTOs/SalesDtos.cs`: `CreateOrderRequest.CustomerId`, `OrderLineDto` with `PriceSource`
- [ ] T012 [P] Add `ManualPaymentRequest` and `ManualPaymentMethod` to `src/SagaSales.Shared/DTOs/PaymentDtos.cs`
- [ ] T013 [P] Add `CustomerType` to `src/SagaSales.Shared/DTOs/CustomerDtos.cs` and `DailyPointDto` to `AnalyticsNotificationDtos.cs`
- [ ] T014 Map SQL error codes to HTTP in `src/SagaSales.BuildingBlocks/Extensions/ExceptionHandlingMiddleware.cs`: `NOT_FOUND`→404, `CONFLICT`→409, `INSUFFICIENT_STOCK`→409, `INVALID_TRANSITION`→409, `PAYMENT_ALREADY_EXISTS`→409
- [ ] T015 [P] Replace `DatabaseHealthCheck<TContext>` with a connection-based check in `src/SagaSales.BuildingBlocks/HealthChecks/CustomHealthChecks.cs` and update the `.AddCheck<>` calls in each service `Program.cs`
- [ ] T016 [P] Add routes for `/api/payments/manual` and `/api/analytics/sales-summary.csv` to `src/SagaSales.ApiGateway/appsettings.json`

**Checkpoint**: foundation ready — stories can start.

---

## Phase 3: User Story 1 - Open a store and sign in (Priority: P1) 🎯 MVP

**Goal**: a merchant registers a store with an admin and signs in with a tenant-scoped token.

**Independent Test**: register a store, sign in, and confirm a second store's data is invisible.

### Tests for User Story 1 ⚠️

- [ ] T017 [P] [US1] Integration test: `sp_tenant_create` writes tenant and admin in one transaction; a duplicate slug leaves no orphan user, in `tests/SagaSales.IntegrationTests/Identity/TenantProceduresTests.cs`
- [ ] T018 [P] [US1] Contract test: `POST /api/auth/register-tenant` returns 201 then 409 on the same slug, in `tests/SagaSales.ContractTests/Auth/AuthContractTests.cs`
- [ ] T019 [P] [US1] Contract test: login returns 200 with `tenant_id` and `role` claims; a wrong password returns 401 with the same message as an unknown email, in `tests/SagaSales.ContractTests/Auth/AuthContractTests.cs`

### Implementation for User Story 1

- [ ] T020 [P] [US1] Write `db/identity/V001__schema.sql`: `tenants` (unique `slug`, `slug` matches `^[a-z0-9-]+$`, `status` in 1–4) and `users` (unique `(tenant_id, email)`, `role` in admin/manager/sales/customer, `password_hash` max 512)
- [ ] T021 [P] [US1] Write `db/identity/V002__functions.sql`: `sp_tenant_create`, `sp_tenant_get_by_slug`, `sp_user_create`, `sp_user_get_by_email`, `sp_user_touch_login`
- [ ] T022 [P] [US1] Create `src/SagaSales.Identity/Data/TenantRepository.cs` over `StoredProcedureExecutor`
- [ ] T023 [US1] Rewire `RegisterTenantCommandHandler` and `LoginCommandHandler` in `src/SagaSales.Identity/Commands/` to the repository, and delete `src/SagaSales.Identity/Data/TenantDbContext.cs`
- [ ] T024 [US1] Update `src/SagaSales.Identity/Program.cs`: register `IDbConnectionFactory` and `TenantRepository`, drop `AddDbContext`
- [ ] T025 [US1] Keep the login failure generic in `src/SagaSales.Identity/Controllers/AuthController.cs`: unknown email and wrong password must be indistinguishable
- [ ] T026 [US1] Add the password rule to `src/SagaSales.Identity/Validators/AuthValidators.cs` (minimum 8 characters) and confirm the existing unit tests stay green

**Checkpoint**: US1 works alone — this is the first shippable increment.

---

## Phase 3b: Role and tenant guards (added by `/speckit-analyze`)

**Purpose**: closes findings E1, E2 and E3 of the analysis. Execute right after Phase 3, before
any other story exposes an endpoint.

- [ ] T111 [US1] Restrict write endpoints by role in `src/SagaSales.Catalog/Controllers/`, `src/SagaSales.Sales/Controllers/`, `src/SagaSales.Payment/Controllers/` and `src/SagaSales.Customer/Controllers/`: `admin` and `manager` may edit prices and refund; `sales` may create orders and record payments; `[Authorize(Roles = ...)]` on every action (FR-003, FR-003a)
- [ ] T112 [US1] Prove the operator sees no business data: a contract test in `tests/SagaSales.ContractTests/Platform/OperatorAccessTests.cs` asserting that a token without `tenant_id` gets no store data from any endpoint (FR-003b)
- [ ] T113 [US1] Contract test for tenant rejection in `tests/SagaSales.ContractTests/Platform/TenantResolutionTests.cs`: a request with no header and no `tenant_id` claim is refused with 400/404, never defaulted (FR-004)

---

## Phase 4: User Story 2 - Publish and maintain the catalogue (Priority: P1) 🎯 MVP

**Goal**: products with a base price, per-customer-type prices, categories, and paginated search.

**Independent Test**: create a product, set a wholesale price, list with `customerType=wholesale`
and see 80 while retail sees 100.

### Tests for User Story 2 ⚠️

- [ ] T027 [P] [US2] Integration test: `sp_price_resolve` returns the list price when present, the base price with `source='base'` when absent, in `tests/SagaSales.IntegrationTests/Catalog/PriceResolutionTests.cs`
- [ ] T028 [P] [US2] Integration test: `sp_product_list` filters before paginating — 30 products in a category, page 2 of 10 returns 10 rows and `total_count = 30`, in `tests/SagaSales.IntegrationTests/Catalog/ProductListTests.cs`
- [ ] T029 [P] [US2] Contract test: `GET /api/products?customerType=wholesale` prices the rows by customer type, in `tests/SagaSales.ContractTests/Catalog/CatalogContractTests.cs`

### Implementation for User Story 2

- [ ] T030 [P] [US2] Write `db/catalog/V001__schema.sql`: `schema_version`, `products` (`check (stock_quantity >= 0)`, `base_price > 0`, `check (currency = 'USD')`, `status` in 1–5), `product_prices` (unique `(tenant_id, product_id, customer_type)`), `categories` (unique `(tenant_id, name)` and `(tenant_id, slug)`)
- [ ] T031 [P] [US2] Write `db/catalog/V002__functions.sql`: `sp_product_create`, `sp_product_update`, `sp_product_retire`, `sp_product_get`, `sp_product_list` (returns rows plus `total_count`), `sp_category_create`, `sp_category_list`
- [ ] T032 [P] [US2] Write `db/catalog/V003__functions_pricing.sql`: `sp_price_set` (upsert) and `sp_price_resolve` (returns `price` and `source`)
- [ ] T033 [P] [US2] Write `db/catalog/V004__functions_stock.sql`: `sp_stock_reserve` with `select … for update` raising `INSUFFICIENT_STOCK`, and `sp_stock_release`
- [ ] T034 [P] [US2] Create `src/SagaSales.Catalog/Data/ProductRepository.cs`
- [ ] T035 [P] [US2] Create `src/SagaSales.Catalog/Data/CategoryRepository.cs`
- [ ] T036 [P] [US2] Create `src/SagaSales.Catalog/Data/PriceListRepository.cs`
- [ ] T037 [US2] Rewire `src/SagaSales.Catalog/Commands/CreateProductCommand.cs`, `UpdateProductCommand.cs`, `DeleteProductCommand.cs` to the repositories and delete `CatalogDbContext`
- [ ] T038 [US2] Rewire `src/SagaSales.Catalog/Queries/GetProductsQuery.cs` and `GetProductQuery.cs` to resolve prices per customer type
- [ ] T039 [US2] Add `PUT /api/products/{id}/prices/{customerType}` and `GET /api/products/{id}/price` to `src/SagaSales.Catalog/Controllers/ProductsController.cs`
- [ ] T040 [US2] Rewire `src/SagaSales.Catalog/Controllers/CategoriesController.cs` and `Queries/GetCategoriesQuery.cs`
- [ ] T041 [US2] Update `src/SagaSales.Catalog/Program.cs` for the repositories; keep the stock endpoints (`reserve`/`release`) calling the procedures

**Checkpoint**: US1 + US2 both work independently.

---

## Phase 5: User Story 3 - Place an order with stock reserved (Priority: P1) 🎯 MVP

**Goal**: an order whose lines freeze name and price, with stock reserved atomically.

**Independent Test**: order 2 of a product with 10 → stock 8; order 1 with 0 in stock → 409 and no change.

### Tests for User Story 3 ⚠️

- [ ] T042 [P] [US3] Integration test: `sp_order_create` writes the order and its lines with the snapshot and a consistent subtotal, in `tests/SagaSales.IntegrationTests/Sales/OrderProceduresTests.cs`
- [ ] T043 [P] [US3] Integration test (SC-003): three concurrent reservations of the last unit of a product produce exactly one success and stock 0, never negative, in `tests/SagaSales.IntegrationTests/Catalog/StockConcurrencyTests.cs`
- [ ] T044 [P] [US3] Contract test: `POST /api/orders` returns 201 with frozen prices; a later price change does not alter the order; insufficient stock returns 409, in `tests/SagaSales.ContractTests/Sales/SalesContractTests.cs`

### Implementation for User Story 3

- [ ] T045 [P] [US3] Write `db/sales/V001__schema.sql`: `orders` (`shipping_address` NOT NULL, `status` in 1–7, reserved fiscal columns nullable), `order_lines` (`product_name`, `unit_price`, `price_source`, `stock_at_purchase`, `quantity > 0`)
- [ ] T046 [P] [US3] Write `db/sales/V002__functions.sql`: `sp_order_create` (single transaction over header and lines), `sp_order_get`, `sp_order_list`
- [ ] T047 [P] [US3] Create `src/SagaSales.Sales/Data/OrderRepository.cs`
- [ ] T048 [US3] Change `src/SagaSales.Sales/Commands/CreateOrderCommand.cs` to take `CustomerId` from the request (decision D8) and keep the caller in `created_by`
- [ ] T049 [US3] Keep the compensation logic in `src/SagaSales.Sales/Commands/CreateOrderCommand.cs`: on any failure release every reservation already made through `ICatalogClient.ReleaseStockAsync`
- [ ] T050 [US3] Update `src/SagaSales.Sales/Controllers/OrdersController.cs` to accept `customerId` and return `OrderDto` with `OrderLineDto.PriceSource`
- [ ] T051 [US3] Update `src/SagaSales.Sales/Program.cs` for the repository; delete `SalesDbContext`

---

## Phase 6: User Story 4 - Move an order through its life cycle (Priority: P1) 🎯 MVP

**Goal**: only legal status transitions are accepted.

**Independent Test**: advance an order step by step; a jump to Delivered is refused with 409.

### Tests for User Story 4 ⚠️

- [ ] T052 [P] [US4] Integration test: legal chain walks through, illegal jump raises `INVALID_TRANSITION` and leaves the status untouched, in `tests/SagaSales.IntegrationTests/Sales/OrderStateMachineTests.cs`
- [ ] T053 [P] [US4] Contract test: `PUT /api/orders/{id}/status` returns 200 on a legal step and 409 on an illegal one, in `tests/SagaSales.ContractTests/Sales/OrderStatusContractTests.cs`

### Implementation for User Story 4

- [ ] T054 [P] [US4] Write `db/sales/V003__functions_status.sql` with `sp_order_change_status`, raising `INVALID_TRANSITION` for anything outside the documented machine
- [ ] T055 [US4] Rewire `src/SagaSales.Sales/Commands/UpdateOrderStatusCommand.cs` to the procedure and publish `OrderStatusChangedEvent` after the commit
- [ ] T056 [US4] Ensure `src/SagaSales.Sales/Controllers/OrdersController.cs` surfaces 409 `INVALID_TRANSITION` and does not mutate in memory before the procedure succeeds

---

## Phase 7: User Story 5 - Record a payment received outside the platform (Priority: P1) 🎯 MVP

**Goal**: staff records a transfer/deposit and the order becomes Confirmed.

**Independent Test**: record 199.98 on a Pending order of 199.98 → order Confirmed, paid 199.98.

### Tests for User Story 5 ⚠️

- [ ] T057 [P] [US5] Integration test: a manual payment publishes `PaymentAuthorizedEvent` and Sales applies it to the order's paid amount, in `tests/SagaSales.IntegrationTests/Payment/ManualPaymentTests.cs`
- [ ] T058 [P] [US5] Contract test: `POST /api/payments/manual` returns 201; a second payment for the same order returns 409; amount ≤ 0 returns 400, in `tests/SagaSales.ContractTests/Payment/PaymentContractTests.cs`

### Implementation for User Story 5

- [ ] T059 [P] [US5] Write `db/payment/V001__schema.sql`: `schema_version`, `payments` (`check (refunded_amount <= amount)`, `amount > 0`, `check (currency = 'USD')`, `status` in 1–6), `order_references` keyed by `order_id`
- [ ] T060 [P] [US5] Write `db/payment/V002__functions.sql`: `sp_payment_create_manual` (refuses a second live payment), `sp_payment_get`, `sp_order_reference_upsert`
- [ ] T061 [P] [US5] Create `src/SagaSales.Payment/Data/PaymentRepository.cs`
- [ ] T062 [US5] Implement the manual payment handler and `POST /api/payments/manual` in `src/SagaSales.Payment/` and publish `PaymentAuthorizedEvent`
- [ ] T063 [P] [US5] Write `db/sales/V004__functions_payment.sql` with `sp_order_register_payment` (adds to `paid_amount`, confirms a pending order)
- [ ] T064 [US5] Add `src/SagaSales.Sales/EventHandlers/PaymentAuthorizedConsumer.cs` calling `sp_order_register_payment`
- [ ] T065 [US5] Update `src/SagaSales.Payment/Program.cs` and `Sales/Program.cs`: repositories, consumers, and delete `PaymentDbContext`

**Checkpoint**: MVP complete — US1 to US5 deliver store, catalogue, order, lifecycle and manual payment.

---

## Phase 8: User Story 6 - Manage customers and find them quickly (Priority: P2)

**Goal**: customers unique per store, with a customer type that drives pricing, searchable fast.

**Independent Test**: create two customers, search by phone, and confirm the same email is
rejected in one store but accepted in another.

### Tests for User Story 6 ⚠️

- [ ] T066 [P] [US6] Integration test: unique `(tenant_id, email)` per store, same email allowed in another store, in `tests/SagaSales.IntegrationTests/Customer/CustomerProceduresTests.cs`
- [ ] T067 [P] [US6] Contract test: `GET /api/customers?search=` finds by email, phone or name, in `tests/SagaSales.ContractTests/Customer/CustomerContractTests.cs`

### Implementation for User Story 6

- [ ] T068 [P] [US6] Write `db/customer/V001__schema.sql`: `customers` (unique `(tenant_id, email)`, `customer_type` default 1, `status` in 1–4) and `addresses`
- [ ] T069 [P] [US6] Write `db/customer/V002__functions.sql`: `sp_customer_create`, `sp_customer_update`, `sp_customer_soft_delete`, `sp_customer_get`, `sp_customer_list`, `sp_address_add`
- [ ] T070 [P] [US6] Create `src/SagaSales.Customer/Data/CustomerRepository.cs`
- [ ] T071 [US6] Rewire `src/SagaSales.Customer/Commands/` and `Queries/` to the repository; delete `CustomerDbContext`
- [ ] T072 [US6] Expose `CustomerType` in `src/SagaSales.Customer/Controllers/CustomersController.cs` and `Validators/CustomerValidators.cs`
- [ ] T073 [US6] Update `src/SagaSales.Customer/Program.cs` for the repository

---

## Phase 9: User Story 7 - Keep the buyer informed (Priority: P2)

**Goal**: order confirmation without manual work, and a failure never breaks the sale.

**Independent Test**: place an order with a buyer email → notification recorded and dispatched; with
the channel down → the order still succeeds and the failure is recorded.

### Tests for User Story 7 ⚠️

- [ ] T074 [P] [US7] Integration test: a failing sender records the error and does not rethrow into the order flow, in `tests/SagaSales.IntegrationTests/Notification/DispatchTests.cs`
- [ ] T075 [P] [US7] Contract test: `GET /api/notifications` lists attempts with their state, in `tests/SagaSales.ContractTests/Notification/NotificationContractTests.cs`

### Implementation for User Story 7

- [ ] T076 [P] [US7] Write `db/notification/V001__schema.sql`: `notification_logs` (`type` 1–4, `status` 1–5, `recipient` max 320) and `notification_templates` (unique `(tenant_id, key)`)
- [ ] T077 [P] [US7] Write `db/notification/V002__functions.sql`: `sp_notification_create`, `sp_notification_mark_sent`, `sp_notification_mark_failed`, `sp_notification_list`, `sp_template_upsert`
- [ ] T078 [P] [US7] Create `src/SagaSales.Notification/Data/NotificationRepository.cs`
- [ ] T079 [US7] Rewire `src/SagaSales.Notification/Infrastructure/NotificationDispatcher.cs` and both consumers (`OrderPlaced`, `HighValueOrderAlert`) to the repository; delete `NotificationDbContext`
- [ ] T080 [US7] Update `src/SagaSales.Notification/Program.cs` for the repository

---

## Phase 10: User Story 8 - Collect and refund payments online (Priority: P2)

**Goal**: online authorisation, capture and refund, with a signed webhook.

**Independent Test**: authorise 50, refund 30 (20 refundable), refund 60 → refused; unsigned webhook
→ 400 and nothing changes.

### Tests for User Story 8 ⚠️

- [ ] T081 [P] [US8] Integration test: `sp_payment_refund` refuses an amount above what remains refundable and leaves the payment untouched, in `tests/SagaSales.IntegrationTests/Payment/RefundTests.cs`
- [ ] T082 [P] [US8] Contract test: `POST /api/webhooks/stripe` with a bad signature returns 400 and no payment changes, in `tests/SagaSales.ContractTests/Payment/WebhookContractTests.cs`

### Implementation for User Story 8

- [ ] T083 [P] [US8] Write `db/payment/V003__functions_refund.sql`: `sp_payment_capture`, `sp_payment_refund`, `sp_payment_find_by_reference`
- [ ] T084 [P] [US8] Implement `src/SagaSales.Payment/Infrastructure/StripePaymentGateway.cs` behind the existing `IPaymentGateway`
- [ ] T085 [US8] Implement signature verification in `src/SagaSales.Payment/Controllers/WebhooksController.cs`; reject before touching data
- [ ] T086 [US8] Implement refund and capture handlers in `src/SagaSales.Payment/Commands/` plus their endpoints in `src/SagaSales.Payment/Controllers/PaymentsController.cs`
- [ ] T087 [US8] Register the gateway in `src/SagaSales.Payment/Program.cs` by configuration, keeping the stub as the default

---

## Phase 11: User Story 9 - Understand the business and export it (Priority: P3)

**Goal**: daily figures that match the orders, exportable to a spreadsheet.

**Independent Test**: place known orders, query the range, verify the totals, export and open the CSV.

### Tests for User Story 9 ⚠️

- [ ] T088 [P] [US9] Integration test: aggregates from events equal the sum of the orders, in `tests/SagaSales.IntegrationTests/Analytics/AggregateTests.cs`
- [ ] T089 [P] [US9] Integration test: the CSV uses `;`, decimal comma, UTF-8 BOM and one row per day; an empty range returns headers only, in `tests/SagaSales.IntegrationTests/Analytics/CsvExportTests.cs`

### Implementation for User Story 9

- [ ] T090 [P] [US9] Write `db/analytics/V001__schema.sql`: `sales_report_daily` (unique `(tenant_id, report_date)`), `product_sales_daily` (unique `(tenant_id, report_date, product_id)`), `customer_activity`
- [ ] T091 [P] [US9] Write `db/analytics/V002__functions.sql`: `sp_report_daily_upsert`, `sp_product_sales_upsert`, `sp_activity_add`, `sp_sales_summary_get`, `sp_daily_series_get`
- [ ] T092 [P] [US9] Create `src/SagaSales.Analytics/Data/AnalyticsRepository.cs`
- [ ] T093 [US9] Rewire the consumers in `src/SagaSales.Analytics/EventHandlers/` and the queries in `src/SagaSales.Analytics/Queries/` to the repository; delete `AnalyticsDbContext`
- [ ] T094 [US9] Create `src/SagaSales.Analytics/Export/SalesSummaryCsvExporter.cs` and expose `GET /api/analytics/sales-summary.csv`
- [ ] T095 [US9] Update `src/SagaSales.Analytics/Program.cs` for the repository

---

## Phase 12: User Story 10 - Buy through the storefront (Priority: P3)

**Goal**: the buyer browses the real catalogue and completes an order without the merchant.

**Independent Test**: browse, see real stock, complete an order and reach the confirmation.

### Tests for User Story 10 ⚠️

- [ ] T096 [P] [US10] Contract test: an anonymous storefront session can read the catalogue and place an order with a shipping address and buyer email, in `tests/SagaSales.ContractTests/Storefront/StorefrontContractTests.cs`

### Implementation for User Story 10

- [ ] T097 [US10] Record the decision in `spec.md` (Clarifications) on how an anonymous buyer is identified, then implement the public catalogue endpoint in `src/SagaSales.Catalog/Controllers/` guarded by an anonymous storefront policy
- [ ] T098 [P] [US10] Wire `src/SagaSales.BlazorWeb/Pages/Products.razor` to the price-resolved catalogue
- [ ] T099 [US10] Build the checkout page `src/SagaSales.BlazorWeb/Pages/Checkout.razor` with buyer data, shipping address and summary
- [ ] T100 [US10] Show out-of-stock and 409 responses in the UI instead of a generic failure, in `src/SagaSales.BlazorWeb/Pages/Checkout.razor`
- [ ] T101 [US10] Add the confirmation page in `src/SagaSales.BlazorWeb/Pages/Confirmation.razor` showing order id and total

---

## Phase 13: Polish & Cross-Cutting Concerns

**Purpose**: close the constitution's open gates (R-1 to R-3) and the delivery pieces.

- [ ] T102 Remove every `Microsoft.EntityFrameworkCore*` and `Microsoft.EntityFrameworkCore.Design` reference from all `.csproj` files, and delete the remaining `*DbContext.cs`, across every `src/*/*.csproj` (only after T051, T065, T073, T080, T093 and T095 are done) — closes Article VIII
- [ ] T103 [P] R-2: extract each service's domain into `SagaSales.<Service>.Domain` libraries under `src/` — closes Article I
- [ ] T104 [P] R-3: add an operational CLI (`tools/`), at minimum seeding a demo store and importing a catalogue — closes Article II
- [ ] T105 [P] Add a Polly circuit breaker around the Sales → Catalog client in `src/SagaSales.Sales/Infrastructure/CatalogClient.cs`
- [ ] T106 [P] Write the eight Dockerfiles referenced by `deploy/docker-compose.yml` and make the compose file start the stack
- [ ] T107 [P] Tenant isolation audit with two stores, scripted in `scripts/tenant-isolation-audit.ps1`, covering every endpoint — closes SC-008
- [ ] T108 Run `specs/001-saga-sales-ecommerce/quickstart.md` end to end and fix every deviation found
- [ ] T109 [P] Update `README.md` (run instructions, endpoints, status of the constitution gates) and `docs/design-system.md`
- [ ] T110 Run `/speckit.analyze` and `/speckit.converge`; record the result in `specs/001-saga-sales-ecommerce/tasks.md` before declaring the feature done

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: no dependencies
- **Foundational (Phase 2)**: after Setup; **blocks every story**
- **Stories (Phases 3–12)**: after Foundational. In priority order they are sequential; after the foundation they can also be split across developers, because each story owns its own files
- **Polish (Phase 13)**: after the stories it closes; T102 in particular must be last of the code work

### Story Dependencies

- **US1 (P1)**: none beyond Foundational
- **US2 (P1)**: none beyond Foundational; independent of US1
- **US3 (P1)**: needs US2's `sp_stock_reserve` (T033) — it is the only cross-story code dependency
- **US4 (P1)**: needs US3's orders table (T045)
- **US5 (P1)**: needs US3's order to exist, and publishes the event US4's state machine consumes
- **US6 (P2)**: independent; its `customer_type` is what US2's pricing reads, so US2 should land first
- **US7 (P2)**: consumes `OrderPlacedEvent` from US3
- **US8 (P2)**: extends US5's payment table
- **US9 (P3)**: consumes events from US3 and US5
- **US10 (P3)**: needs US2 and US3 exposed anonymously

### Within Each Story

Tests first and failing → tables and procedures → repository → handlers → endpoints → program wiring.

### Parallel Opportunities

- Phase 1: T002–T006 are all `[P]`
- Phase 2: T008–T013 and T015–T016 are `[P]`; T007 must precede them
- US2: T030–T036 are `[P]` (four SQL files and three repositories, no shared file)
- US5 and US9: the SQL and repository tasks are `[P]`
- Any two stories may be developed in parallel after Phase 2, on different branches

---

## Parallel Example: User Story 2

```bash
# SQL and repositories, all different files:
Task: "db/catalog/V001__schema.sql — products, product_prices, categories"
Task: "db/catalog/V002__functions.sql — product and category CRUD"
Task: "db/catalog/V003__functions_pricing.sql — sp_price_set, sp_price_resolve"
Task: "db/catalog/V004__functions_stock.sql — sp_stock_reserve, sp_stock_release"
Task: "src/SagaSales.Catalog/Data/ProductRepository.cs"
Task: "src/SagaSales.Catalog/Data/CategoryRepository.cs"
Task: "src/SagaSales.Catalog/Data/PriceListRepository.cs"
```

---

## Implementation Strategy

### MVP first (Phases 1–7)

1. Phase 1 Setup
2. Phase 2 Foundational — **blocks everything**
3. US1 → validate sign-in alone
4. US2 → validate catalogue alone
5. US3 → validate the concurrency test (SC-003) alone
6. US4 + US5 → validate a full sale with manual payment
7. **STOP and validate the MVP**: register → publish → order → confirm → record payment

### Incremental delivery

Each story after the MVP is independently deployable: US6 (customers), US7 (notifications),
US8 (online payments), US9 (analytics + CSV), US10 (storefront). No story modifies another
story's files, which is what keeps the 34 existing unit tests green throughout.

### Risk note

The riskiest work is not a story: it is **T102** (removing EF). It only happens once every service
has migrated, and it is the task that closes Article VIII. Do not bring it forward.

---

## Notes

- `[P]` means different files and no dependency on an incomplete task
- A story is done only when its tests exist and pass (constitution §III)
- Integration tests need a reachable SQL Server; they skip when `SAGASALES_TEST_SQL` is absent
- Commit per task or per logical group; never commit with a red test suite

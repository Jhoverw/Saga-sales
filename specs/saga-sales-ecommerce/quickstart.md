# Quickstart Validation Scenarios

*Phase 1 Output — Key validation tests for each user story*

## Validation Test Plan

For each critical user story, define a quickstart scenario that verifies the core behavior without full end-to-end testing.

---

### Scenario S-001: Create Product (US-001)
1. Start PostgreSQL, RabbitMQ, Redis services
2. Seed tenant A and tenant B in Identity service
3. POST /api/products as tenant A manager → expect HTTP 201, stock=10
4. GET /api/products?category=Electronics as tenant B → expect empty array
5. PUT /api/products/{uuid} → expect HTTP 200, stock updated
6. DELETE /api/products/{uuid} → expect HTTP 204

### Scenario S-002: Create Order & Deduplicate Stock (US-004, US-007)
1. POST /api/orders as customer T-A → 201, orderId returned
2. GET /api/products → check stock decremented by quantity
3. Same product POST again with same user → expect 201, second stock decrement
4. Full simulation: 3 concurrent orders of same product × qty=1 → exactly 3 successful, total deduction = 3
5. If any product has stock < requested → expect 409 InsufficientStockException

### Scenario S-003: Multi-Tenant Isolation (FR-003)
1. Register tenant A and tenant B users with same email
2. Login both, use their respective JWTs
3. Attempt cross-tenant: access tenant A product ID with tenant B token
4. Expect 403 Forbidden or 404 NotFound (never cross-tenant data visible)

### Scenario S-004: Payment Flow (US-008)
1. Create order for $50
2. POST /api/payments with method=Stripe, Stripe key configured
3. Expect HTTP 201, payment.authorized event published to RabbitMQ
4. Wait for webhook processing (if Stripe configured)
5. Verify Payment record exists with status=Authorized

### Scenario S-005: Analytics (US-013)
1. Generate 7 days of sales data in analytics service
2. Query /api/analytics/sales-summary?from=today&to=now
3. Expect total revenue correct, date range accurate
4. Filter by specific date → expect scoped results

### Scenario S-006: Notification Dispatch
1. Trigger order confirmation domain event
2. POST /api/notifications/templates create template
3. Trigger domain event sending notification
4. Check notification log → expect Status=Sent within 30s

### Scenario S-007: Health Check
1. Hit /health on each service individually
2. Expect all services return healthy
3. Kill RabbitMQ → health becomes Unhealthy after 30s retry

---

## Environment Setup Quickstart

```bash
# 1. Clone and restore
dotnet restore SagaSales.sln

# 2. Start infrastructure
docker compose -f deploy/docker-compose.yml up -d postgres rabbitmq redis seq

# 3. Run database migrations for each service
cd src/SagaSales.Catalog && dotnet ef database update --connection "Host=localhost;Database=catalog;Username=saga_sales;Password=saga_sales_dev"

# Repeat for each service's respective database

# 4. Start gateway and all services
dotnet run --project src/SagaSales.ApiGateway
dotnet run --project src/SagaSales.Identity
dotnet run --project src/SagaSales.Catalog
# ... etc

# 5. Access
# Gateway: http://localhost:5000
# Blazor Frontend: http://localhost:5008
# Seq Logs: http://localhost:5341
# RabbitMQ Mgmt: http://localhost:15672 (saga_sales/saga_sales_dev)
```

All quickstart scenarios must pass before proceeding to implementation.
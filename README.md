# Saga-Sales

Plataforma de e-commerce multi-tenant en **.NET 10**: microservicios con CQRS (MediatR), eventos de
integración por RabbitMQ (MassTransit), **SQL Server 2022 con procedimientos almacenados y Dapper**
(sin ORM, por constitución §VIII) y un frontend Blazor WebAssembly que consume todo a través de un
API Gateway (YARP).

## Estructura

```
src/
├── SagaSales.Shared/           Contratos, DTOs, enums, eventos, Result/Error
├── SagaSales.BuildingBlocks/   Infraestructura: bus, ejecutor de SP, Serilog, JWT, health, tenant
├── SagaSales.Identity/         Tenants, usuarios, login (JWT), registro de tenant
├── SagaSales.Catalog/          Productos, categorías, precios, stock por sucursal, lotes,
│                               compras, proveedores e imágenes
├── SagaSales.Sales/            Pedidos y transiciones de estado
├── SagaSales.Payment/          Pagos manuales, reembolsos y (v1) pasarela
├── SagaSales.Customer/         Clientes y direcciones
├── SagaSales.Analytics/        Agregados diarios y KPIs (eventos)
├── SagaSales.Notification/     Email/SMS (proveedor pendiente; esquema pendiente)
├── SagaSales.ApiGateway/       YARP: enrutado, autenticación y CORS
└── SagaSales.BlazorWeb/        SPA Blazor WebAssembly

specs/001-saga-sales-ecommerce/  Épicas, spec, plan, modelo de datos, contratos y tareas
db/<servicio>/                   Esquema y procedimientos versionados (V###__*.sql)
scripts/                         Arranque, aplicación de SQL y smokes end-to-end
deploy/                          docker-compose y Dockerfiles
tests/                           UnitTests · IntegrationTests · ContractTests
```

**Proyectos**: 11 (Shared, BuildingBlocks, 7 servicios de dominio, ApiGateway, BlazorWeb).

## Dependencias entre proyectos

```
Shared  ←  BuildingBlocks  ←  {Identity, Catalog, Sales, Payment, Customer, Analytics, Notification, ApiGateway}
Shared  ←  BlazorWeb
```

`Shared` no depende de nada. **Ningún servicio referencia a otro servicio**: la comunicación es por
HTTP (Sales → Catalog para reservar stock) o por eventos de MassTransit.

## Requisitos

- .NET SDK 10.0 (`net10.0` fijado en `Directory.Build.props`)
- Docker (SQL Server 2022, RabbitMQ, Seq)
- Acceso a nuget.org para restaurar paquetes

## Puesta en marcha

> Pasa siempre la solución de forma explícita (`dotnet build SagaSales.sln`): el repo contiene
> `SagaSales.sln` y `SagaSales.slnx` con los mismos proyectos, así que `dotnet build` sin argumentos
> se queja de ambigüedad (MSB1011).

```powershell
# 1. Arrancar todo (infraestructura + servicios + gateway)
./scripts/up.ps1

# 2. Aplicar SQL nuevo y verificar
./scripts/verify-db.ps1 -Apply

# 3. Reconstruir sólo lo que cambió (reinicia el gateway)
./scripts/refresh.ps1
```

Alternativa sin Docker para el panel (`./scripts/dev-panel.ps1`) y por servicio con `dotnet run
--project src/<Servicio>`.

Direcciones: gateway `http://localhost:5000` (`/health`), panel `http://localhost:5008`,
RabbitMQ `http://localhost:15672` (`saga_sales` / `saga_sales_dev`), Seq `http://localhost:5341`.

## Primer uso

```bash
curl -X POST http://localhost:5000/api/auth/register-tenant \
  -H "Content-Type: application/json" \
  -d '{"slug":"demo","displayName":"Demo","adminEmail":"admin@demo.local","adminPassword":"Demo12345!"}'

curl -X POST http://localhost:5000/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"admin@demo.local","password":"Demo12345!"}'
```

Todas las rutas salvo `/api/auth/*`, `/images/*` y `/health` requieren `Authorization: Bearer <token>`
y el tenant (`X-Tenant-Id` o el claim `tenant_id`).

## Endpoints principales

| Servicio | Rutas |
|---|---|
| Identity | `POST /api/auth/register-tenant`, `POST /api/auth/login` |
| Catalog · productos | `GET/POST /api/products`, `GET/PUT/DELETE /api/products/{id}`, `PUT /api/products/{id}/prices/{customerType}`, `GET /api/products/{id}/price`, `GET /api/products/{id}/movements`, `POST /api/products/{id}/image` |
| Catalog · stock | `POST /api/products/{id}/stock/reserve`, `POST /api/products/{id}/stock/release` |
| Catalog · categorías | `GET/POST /api/categories` |
| Catalog · proveedores | `GET/POST /api/suppliers`, `PUT/DELETE /api/suppliers/{id}` |
| Catalog · compras | `GET/POST /api/purchases`, `GET /api/purchases/{id}`, `POST /api/purchases/{id}/receive` |
| Catalog · sucursales | `GET/POST /api/branches`, `PUT/DELETE /api/branches/{id}` |
| Catalog · parámetros | `GET/PUT /api/settings` |
| Catalog · inventario | `GET /api/inventory/products/{id}/branches`, `PUT /api/inventory/products/{id}/location`, `GET /api/inventory/expiring` |
| Catalog · imágenes | `GET /images/products/{file}` (público) |
| Sales | `GET/POST /api/orders`, `GET /api/orders/{id}`, `PUT /api/orders/{id}/status`, `POST /api/orders/{id}/payments`, `POST /api/orders/{id}/cancel` |
| Payment | `POST /api/payments`, `GET /api/payments`, `GET /api/payments/{id}`, `POST /api/payments/{id}/refund` |
| Customer | `GET/POST /api/customers`, `GET/PUT/DELETE /api/customers/{id}`, `POST /api/customers/{id}/addresses`, búsqueda con `?search=` |
| Analytics | `GET /api/analytics/sales-summary`, `GET /api/analytics/daily` |
| Notification | `GET/POST /api/notifications` (esquema aplicado; repositorio/SP por alinear) |

Contratos declarativos por servicio en `specs/001-saga-sales-ecommerce/contracts/`.

## Pruebas

```powershell
./scripts/test.ps1                 # unit + contrato + integración contra el stack local
./scripts/test.ps1 -SkipIntegration
```

- **Unitarias** (55): dominio, seguridad, facturación (clave de acceso SRI) y notificaciones.
- **Contrato** (26): HTTP a través del gateway (auth, catálogo, pedidos con IVA, impuestos aislados por
  tienda y `GET /api/taxes/current`, sucursales, parámetros, compras y notificaciones).
- **Integración** (19): procedimientos contra SQL Server real (aislamiento de `tax_rates`, IVA por
  rango de fechas, pedidos sin tasa vigente y pedidos inmutables ante cambios futuros de tasa).

Contrato e integración necesitan el stack levantado y las bases `_test` (una vez):

```powershell
./scripts/apply-db.ps1 -UseContainer -DatabaseSuffix '_test'
```

Sin servicios esas suites se **omiten con un aviso**, nunca se pintan de verde falso.

**Puerta de calidad**: `.github/workflows/ci.yml` compila y ejecuta `dotnet test` en cada push y PR;
las unitarias son la puerta dura.

Smokes end-to-end contra el stack real:

```powershell
./scripts/seed-demo.ps1                  # tienda demo con datos para ver todas las pantallas
./scripts/smoke-identity.ps1 -SkipDb
./scripts/smoke-purchasing.ps1          # costo promedio 0,375 y margen 25 %
./scripts/smoke-purchase-lines.ps1      # compra multi-línea, lotes y borrador
./scripts/smoke-us6-us7-image.ps1       # imagen, sucursales, parámetros, estados y analítica
```

## Eventos de integración

`CatalogProductCreated/Updated/Deleted`, `OrderPlaced`, `OrderStatusChanged`,
`PaymentAuthorized/Captured/Refunded`, `CustomerCreated`, `HighValueOrderAlert`.

`OrderPlaced` lo consumen Analytics, Payment y Notification en colas propias
(`analytics-order-placed`, `payment-order-placed`, `OrderPlaced`).

## Pendiente (fase 2)

- **US7 notificaciones**: el esquema ya existe (`db/notification/`), pero el repositorio llama
  procedimientos que no coinciden con sus parámetros y el servicio no tiene pruebas de contrato.
- **Impuestos (facturación)**: `sales.tax_rates` ya está **aislada por tienda** (`tenant_id`), pero
  se escribe con SQL directo en vez de procedimientos (§VIII). `GET/POST /api/taxes` funciona y una
  tienda no ve las tasas de otra (verificado por contrato e integración).
- **Facturación electrónica de Ecuador** (`src/SagaSales.EcuadorInvoicing`): es un **PoC**, no un
  servicio: no tiene `Program.cs`, Dockerfile, base de datos ni ruta; la pantalla
  `EcuadorInvoicing.razor` llama a un endpoint que devuelve 404. La constitución (E-10) la define
  como **proyecto aparte**.
- **`SagaSales.Reports`**: apunta a `net7.0`, no está en la solución y no compila con el SDK 10.
- **US8 pago online**: Stripe/PayPal detrás de `IPaymentGateway` (stub hoy); webhooks con verificación
  de firma retirados hasta E-04b.
- **CSV de analítica** (`GET /api/analytics/sales-summary.csv`): no implementado.
- **US10 storefront**: catálogo público, checkout y confirmación.
- **Roles**: los endpoints usan `[Authorize]` pero aún no `[Authorize(Roles = ...)]`.
- **§VIII**: quedan referencias a `Microsoft.EntityFrameworkCore` en `BuildingBlocks` y en todos los
  `.csproj` (`...Design`); `Notification` usa `AddDbContext`.
- **Auditoría de aislamiento** con dos tiendas (`scripts/tenant-isolation-audit.ps1`), **CLI** en
  `tools/` y **circuit breaker** Polly: pendientes.
- **Saga/outbox** para compensar reservas de stock y publicar eventos de forma transaccional.

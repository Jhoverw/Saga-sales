# Saga-Sales

Plataforma de e-commerce multi-tenant en .NET 8: microservicios con CQRS (MediatR),
eventos de integración por RabbitMQ (MassTransit), SQL Server por servicio y un frontend
Blazor WebAssembly que consume todo a través de un API Gateway (YARP).

## Estructura

```
src/
├── SagaSales.Shared/           Contratos, DTOs, enums, eventos, Result/Error
├── SagaSales.BuildingBlocks/   Infraestructura: bus, Serilog, JWT, health checks, tenant
├── SagaSales.Identity/         Tenants, usuarios, login (JWT), registro de tenant
├── SagaSales.Catalog/          Productos, categorías, stock (reserva atómica)
├── SagaSales.Sales/            Pedidos y transiciones de estado
├── SagaSales.Payment/          Pagos, reembolsos, webhooks
├── SagaSales.Customer/         Clientes y direcciones
├── SagaSales.Analytics/        Agregados diarios y KPIs
├── SagaSales.Notification/     Email/SMS (envío por proveedor pendiente)
├── SagaSales.ApiGateway/       YARP: enrutado y autenticación
└── SagaSales.BlazorWeb/        SPA Blazor WebAssembly
specs/saga-sales-ecommerce/     Especificación, plan y tareas
memory/constitution.md          Principios del proyecto
deploy/                         docker-compose e inicialización de SQL Server
tests/                          (vacío — ver "Pendiente")
```

## Dependencias entre proyectos

```
Shared  ←  BuildingBlocks  ←  {Identity, Catalog, Sales, Payment, Customer, Analytics, Notification, ApiGateway}
Shared  ←  BlazorWeb
```

`Shared` no depende de nada. **Ningún servicio referencia a otro servicio**: la comunicación
es por HTTP (Sales → Catalog para reservar stock) o por eventos de MassTransit.

## Requisitos

- .NET SDK 10.0 (el proyecto fija `net10.0` en `Directory.Build.props`)
- Docker (SQL Server, RabbitMQ)
- Acceso a nuget.org para restaurar paquetes

## Puesta en marcha

> Pasa siempre la solución de forma explícita (`dotnet build SagaSales.sln`): el repo contiene
> `SagaSales.sln` y `SagaSales.slnx` con los mismos 11 proyectos, así que `dotnet build` sin
> argumentos se queja de ambigüedad (MSB1011). Puedes borrar el que no uses.

```bash
# 1. Restaurar y compilar
dotnet restore SagaSales.sln
dotnet build SagaSales.sln

# 2. Infraestructura (SQL Server + RabbitMQ + Seq)
docker compose -f deploy/docker-compose.yml up -d sqlserver rabbitmq seq

# 3. Crear el esquema de cada base de datos (scripts T-SQL versionados, sin EF ni migraciones)
./scripts/apply-db.ps1 -Server localhost,1433 -User sa -Password $env:MSSQL_SA_PASSWORD

# 4. Arrancar los servicios (una consola por servicio)
dotnet run --project src/SagaSales.Identity
dotnet run --project src/SagaSales.Catalog
dotnet run --project src/SagaSales.Sales
dotnet run --project src/SagaSales.Payment
dotnet run --project src/SagaSales.Customer
dotnet run --project src/SagaSales.Analytics
dotnet run --project src/SagaSales.Notification
dotnet run --project src/SagaSales.ApiGateway
dotnet run --project src/SagaSales.BlazorWeb

# 5. Abrir
#    Gateway:    http://localhost:5000  (/health)
#    Frontend:   http://localhost:5008
#    RabbitMQ:   http://localhost:15672  (saga_sales / saga_sales_dev)
#    Seq:        http://localhost:5341
```

## Primer uso

```bash
# Crear un tenant con su administrador
curl -X POST http://localhost:5000/api/auth/register-tenant \
  -H "Content-Type: application/json" \
  -d '{"slug":"demo","displayName":"Demo","adminEmail":"admin@demo.local","adminPassword":"Demo12345!"}'

# Login: devuelve el access token
curl -X POST http://localhost:5000/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"admin@demo.local","password":"Demo12345!"}'
```

Todas las rutas salvo `/api/auth/*` y `/health` requieren
`Authorization: Bearer <token>` y el tenant (`X-Tenant-Id` o el claim `tenant_id`).

## Endpoints principales

| Servicio | Ruta |
|---|---|
| Identity | `POST /api/auth/register-tenant`, `POST /api/auth/login` |
| Catalog | `GET/POST /api/products`, `GET/PUT/DELETE /api/products/{id}`, `POST /api/products/{id}/stock/reserve`, `GET/POST /api/categories` |
| Sales | `GET/POST /api/orders`, `GET /api/orders/{id}`, `PUT /api/orders/{id}/status` |
| Payment | `POST /api/payments`, `POST /api/payments/{id}/refund`, `GET /api/payments/{id}`, `POST /api/webhooks/stripe` |
| Customer | `GET/POST /api/customers`, `GET/PUT/DELETE /api/customers/{id}`, búsqueda con `?search=` |
| Analytics | `GET /api/analytics/sales-summary`, `GET /api/analytics/daily` |
| Notification | `GET/POST /api/notifications` |

## Pruebas

```bash
dotnet test tests/SagaSales.UnitTests        # 34 casos unitarios (dominio, seguridad, Result)
```

Cubren la máquina de estados del pedido, las reglas de reembolso, el hashing PBKDF2 de
contraseñas y los tipos base (`Result`, paginación). Las pruebas de integración (contra
SQL Server con los procedimientos) y de contrato (contra los servicios en ejecución) están
pendientes — ver "Pendiente (fase 2)".

## Eventos de integración

`CatalogProductCreated/Updated/Deleted`, `OrderPlaced`, `OrderStatusChanged`,
`PaymentAuthorized/Captured/Refunded`, `CustomerCreated`, `HighValueOrderAlert`.

## Pendiente (fase 2)

- **Pruebas**: `tests/Unit` y `tests/Contract` están vacíos; no hay ningún test.
- **Migraciones EF Core**: no existe ninguna carpeta `Migrations/`.
- **Dockerfiles**: `docker-compose.yml` referencia `src/*/Dockerfile` y aún no existen.
- **Proveedores reales**: Stripe/PayPal (pagos) y SMTP/Twilio (notificaciones) están
  detrás de `IPaymentGateway` e `INotificationSender`, con implementaciones *stub*.
  La verificación de firma del webhook de Stripe también está pendiente.
- **Filtro global por tenant** en EF Core (hoy el filtrado es explícito en cada consulta).
- **Saga/Outbox** para compensar reservas de stock y publicar eventos de forma transaccional.
- **Agregados con domain events** (`AggregateRoot<T>` ya existe en `Shared` pero las
  entidades siguen siendo POCOs).

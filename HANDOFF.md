# Handoff — estado del proyecto

Documento de traspaso para continuar **sin releer la conversación**. Si eres un agente nuevo:
lee este archivo, luego `specs/001-saga-sales-ecommerce/epics.md` (decisiones D1–D19) y sigue.

Última actualización: 2026-11-05.

---

## Qué es esto

Saga-Sales: plataforma de comercio multi-tienda. **.NET 10**, ocho servicios + gateway YARP,
**SQL Server 2022 con procedimientos almacenados y Dapper** (sin EF Core, prohibido por la
constitución §VIII), Blazor WebAssembly para el panel, MassTransit + RabbitMQ para eventos.

---

## Estado verificado (probado contra la base real, por HTTP)

| Funcionalidad | Estado |
|---|---|
| Registro de tienda y login con JWT por tenant | ✅ verificado |
| Catálogo: productos, categorías, precios por tipo de cliente | ✅ verificado |
| Pedidos con reserva atómica de stock y máquina de estados | ✅ verificado |
| Compras: costo promedio ponderado (CPP), kardex, recepción | ✅ verificado (0,35 + 0,40 → 0,375 · margen 25 %) |
| Sucursales: stock aislado por sucursal | ✅ verificado |
| Lotes + caducidad + FEFO | ✅ verificado |
| Ubicación física (piso/pasillo/estantería) | ✅ verificado |
| Parámetros: días de aviso de vencimiento | ✅ verificado |

Esquema aplicado: `saga_identity` V003, `saga_catalog` V012, `saga_sales` V002.

---

## Comandos (esto reemplaza la conversación)

| Necesito | Comando |
|---|---|
| Arrancar todo | `./scripts/up.ps1` |
| Aplicar SQL nuevo y verificar | `./scripts/verify-db.ps1 -Apply` |
| Reconstruir sólo lo que cambió | `./scripts/refresh.ps1` |
| Reconstruir todo, en serie | `$env:COMPOSE_PARALLEL_LIMIT="1"; ./scripts/refresh.ps1 -All` |
| **Panel sin Docker** (ciclo rápido) | `./scripts/dev-panel.ps1` |
| Probar identidad | `./scripts/smoke-identity.ps1 -SkipDb` |
| Probar compras | `./scripts/smoke-purchasing.ps1` |
| Diagnóstico del entorno | `./scripts/doctor.ps1` |

Reglas operativas aprendidas:

- Reconstruir un servicio cambia su IP: **reinicia el gateway** o YARP devuelve 502. `up.ps1` y
  `refresh.ps1` ya lo hacen.
- El panel **no necesita Docker**: `dev-panel.ps1` lo sirve en el host y evita la caché de imágenes.
- Los logs de Docker con 9 builds en paralelo agotan recursos: usar `COMPOSE_PARALLEL_LIMIT=1`.

---

## Pendiente, por prioridad

1. **Ver el panel funcionando en el navegador.** El backend está verificado; el ciclo de imágenes
   de Docker dio problemas de caché. `dev-panel.ps1` es la vía. Si no arranca, la consola de
   `dotnet run --project src\SagaSales.BlazorWeb --urls http://localhost:5008` dice por qué.
2. **Reconstruir los seis servicios** que aún corren con una `Shared` anterior: identity, sales,
   payment, customer, analytics, notification. Funcionan, pero conviene alinearlos.
3. **Subida de imagen de producto.** Falta endpoint y un volumen en `docker-compose.yml`.
   Columnas `products.image_path` y `ProductDto.ImagePath` ya existen.
4. **Pantallas de Sucursales y Parámetros** como páginas propias (hoy la API existe y la de
   Compras crea la primera sucursal).
5. **US4 — Clientes** (`db/customer/`): alta, direcciones, búsqueda. Sin esto, el pedido pide un
   GUID de cliente a mano.
6. **US5 — Cobro manual** (`db/payment/`): registrar cobro, rechazar doble cobro, reembolso con
   tope, y **cancelación que devuelve stock** (hueco conocido: hoy cancelar no libera stock).
7. **US6/US7** panel de pedidos y analítica. **US8-US10** notificaciones, roles, escaparate.

---

## Deudas conocidas y decisiones pendientes de aplicar

- La compensación de stock al fallar un pedido es *best-effort*: si `sp_stock_release` falla, se
  pierde stock. Solución propia: outbox/saga (está en el pulido, no en el MVP).
- `sp_stock_reserve` devuelve una respuesta parcial (`status: 0`, `createdAt: 0001-01-01`).
- `Microsoft.AspNetCore.Authentication.JwtBearer 8.0.4` con TFM `net10.0`: funciona, está fuera de
  banda; alinear a 10.x cuando toque limpieza.
- Los endpoints de `payment`, `customer`, `analytics` y `notification` existen pero **no tienen
  esquema**: devuelven 503 en `/health` y fallan al usarse. Es esperado.

---

## Cómo continuar en una sesión nueva

Prompt mínimo:

> Lee `HANDOFF.md` y `specs/001-saga-sales-ecommerce/epics.md`. El backend está verificado.
> Continúa con el punto 1 del pendiente (ver el panel) y después con US4.

Todo el detalle de negocio está en `epics.md` (11 épicas + E-12 + E-13, decisiones D1–D19 con su
razón). La constitución está en `.specify/memory/constitution.md` (v2.2.0).

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
| **Clientes: alta con dirección, edición, búsqueda, baja lógica** | ✅ verificado (10/10) |
| **Cobro manual, doble cobro rechazado, reembolso parcial con tope** | ✅ verificado |
| **Cancelar pedido devolviendo el stock** | ✅ verificado (10 → 8 → 10) |

Esquema aplicado: `saga_identity` V003 · `saga_catalog` V013 · `saga_sales` V002 ·
`saga_customer` V002 · `saga_payment` V002.

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

1. **Subida de imagen de producto.** Falta endpoint y un volumen en `docker-compose.yml`.
   Columnas `products.image_path` y `ProductDto.ImagePath` ya existen.
2. **Pantallas de Sucursales y Parámetros** como páginas propias (la API existe; Compras crea la
   primera sucursal y los parámetros solo se cambian por API).
3. **Reconstruir los servicios que aún corren con una `Shared` anterior**: identity, analytics,
   notification. Funcionan, pero conviene alinearlos.
4. **US6 — panel de pedidos**: la pantalla ya cobra, cancela y reembolsa; falta mover estados a
   mano (enviado/entregado) y filtrar.
5. **US7 — analítica** (`db/analytics/`): ingresos, pedidos y productos más vendidos.
6. **US8 — notificaciones** (`db/notification/`), **US9 — roles**, **US10 — escaparate**.

## Deudas conocidas

- La compensación de stock al fallar un pedido es *best-effort*: si `sp_stock_release` falla, se
  pierde stock. Solución propia: outbox/saga.
- Payment avisa a Sales por HTTP: si Sales está caído después de grabar el pago, el pago existe y
  la llamada devuelve error. Con outbox sería tolerante a fallos.
- Los webhooks de pasarela se retiraron: aceptaban llamadas sin verificar firma. Vuelven con E-04b.
- `Microsoft.AspNetCore.Authentication.JwtBearer 8.0.4` con TFM `net10.0`: funciona, fuera de banda.
- Un producto creado con stock **sin sucursal** no puede venderse (no hay dónde ponerlo). V013 lo
  maneja cuando existe la predeterminada; falta avisar en el formulario.

---

## Lecciones operativas (costaron horas, no repetirlas)

Cada una de estas falló de verdad en este proyecto. Están aquí porque no se deducen del código.

| Síntoma | Causa real | Qué hacer |
|---|---|---|
| `Unhandled: Unable to find the required services ... AddControllers` | `app.MapControllers()` sin `builder.Services.AddControllers()` | Cualquier servicio nuevo necesita `AddControllers()`, no lo añade nada más |
| `Incorrect syntax near the keyword 'plan'` | `plan` es palabra reservada en T-SQL | `plan_name`; cuidado con `key`, `date`, `order`, `lines`, `after` |
| `Msg 1934 ... 'ANSI_NULLS'` en índices filtrados | `sqlcmd` pone `ANSI_NULLS`/`QUOTED_IDENTIFIER` en OFF | `SET ANSI_NULLS ON; SET QUOTED_IDENTIFIER ON;` al inicio de cada script **y** `-I` en sqlcmd |
| `A compatible .NET SDK was not found` | `global.json` con `rollForward: latestFeature` no cruza de major | `rollForward: latestMajor` + `allowPrerelease: true` |
| 502 del gateway hacia un servicio | YARP no re-resuelve DNS: el servicio cambió de IP al recrearse | Reiniciar el gateway después de recrear cualquier servicio |
| 502 hacia **todos** los servicios | `appsettings` del gateway apuntaba a `http://localhost:5001` (dentro del contenedor no existe) | En compose: `ReverseProxy__Clusters__*__Destinations__primary__Address` con el nombre del contenedor |
| Menú o pantalla viejos aunque "compiló bien" | Docker reutilizó capas; `up -d --build` no siempre invalida | Comparar `docker image inspect --format {{.Created}}` antes/después, y `--no-cache` si hay duda |
| `Failed to load config file undefined` + `/0` | Artefactos mezclados: cargador v8 en `bin/obj` y runtime v10 | `dotnet clean` + borrar `bin` y `obj` del panel |
| `Cannot read properties of undefined (reading 'getI32')` | Paquete Blazor 8.0.4 en proyecto `net10.0` | Los paquetes `Microsoft.AspNetCore.Components.*` deben ir en la banda del TFM |
| Pantalla en blanco sin pistas | — | El panel tiene panel de diagnóstico: los errores de arranque salen **al pie de la página** |
| `$var` "de sólo lectura o constante" en PowerShell | `$pid`, `$home`, `$host` son automáticas y PowerShell no distingue mayúsculas | Usar `$productId`, `$page`, `$cfg` |
| `int + "texto"` falla en PowerShell | El operador `+` convierte el string a número | `"" + $valor + " texto"` |
| Build cancelado con 9 imágenes | Paralelismo agota recursos de Docker Desktop | `$env:COMPOSE_PARALLEL_LIMIT="1"` |
| `lookup mcr.microsoft.com: no such host` | DNS transitorio de Docker Desktop | Reintentar; con `--no-cache` Docker **necesita** red porque descarta las capas base |

**Regla general:** un verificador que grita sin razón es peor que ninguno. Si un script reporta
`FAIL` sobre algo que acabas de comprobar a mano, **corrige el script**, no el código.

---

## Cómo continuar en una sesión nueva

Prompt mínimo:

> Lee `HANDOFF.md` y `specs/001-saga-sales-ecommerce/epics.md`. El backend está verificado.
> Continúa con el punto 1 del pendiente (ver el panel) y después con US4.

Todo el detalle de negocio está en `epics.md` (11 épicas + E-12 + E-13, decisiones D1–D19 con su
razón). La constitución está en `.specify/memory/constitution.md` (v2.2.0).

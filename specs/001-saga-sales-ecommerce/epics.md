# Épicas de Negocio: Saga-Sales

**Artefacto**: capa de negocio, previa a `spec.md`
**Fecha**: 2026-11-04 | **Estado**: Borrador para validación con negocio
**Cadena spec-kit**: `constitution.md` → **`epics.md`** → `spec.md` → `research.md` → `data-model.md` → `plan.md` → `tasks.md`

---

## 1. Cómo leer este documento

Una **épica** es una capacidad de negocio completa y entregable, no un módulo técnico.
Cada épica declara *qué valor recibe el negocio*, cómo se mide y qué historias la componen.

Regla de trazabilidad: **ninguna tarea (`tasks.md`) puede existir sin una historia y una épica
que la justifiquen**. Si una tarea no encaja en una épica, o falta una épica o sobra la tarea.

Cada épica incluye: objetivo de negocio · indicadores · alcance / fuera de alcance ·
historias de la `spec.md` · criterios de aceptación · servicios implicados · riesgos.

---

## 2. Contexto de negocio (de `spec.md`)

Plataforma de e-commerce **multi-tenant (SaaS)**: varios comercios independientes operan sobre
la misma instalación, con datos aislados. Cada comercio gestiona su catálogo, recibe pedidos,
cobra online y ve sus métricas.

Perfil de operación declarado para el MVP:

| Dimensión | Objetivo MVP |
|---|---|
| Tenants activos | 10 |
| Productos por tenant | 50 |
| Pedidos/día (pico) | 100 |
| Latencia p95 de API | < 200 ms |
| Aislamiento entre tenants | Infracción = incidente crítico |

**Actores del negocio**

*Personas*

| # | Actor | Dónde vive | Qué necesita | Rol técnico |
|---|---|---|---|---|
| 1 | Dueño / administrador | Dentro de una tienda | Abrir la tienda, definir precios y listas, gestionar usuarios, ver resultados | `admin` |
| 2 | Encargado de tienda | Dentro de una tienda | Catálogo, pedidos y clientes del día a día | `manager` |
| 3 | Vendedor | Dentro de una tienda | Tomar pedidos y registrar cobros; no toca precios | `sales` |
| 4 | Comprador (cliente final) | Fuera de la tienda | Ver productos reales, comprar, recibir confirmación | `customer` — **sin login en el MVP** |
| 5 | Operador de plataforma | Fuera de toda tienda | Que nada se caiga, diagnosticar, atender soporte | ninguno (sin tenant) |

*Sistemas externos*

| # | Actor | Cuándo entra | Qué aporta |
|---|---|---|---|
| 6 | Pasarela de pago (Stripe / PayPal) | v1 · E-04b | Autorizaciones y webhooks firmados |
| 7 | Proveedor de email / SMS | v1 · E-07 | Entrega de confirmaciones y alertas |
| 8 | Proyecto de facturación electrónica (SRI) | Proyecto aparte · E-10 | Consume los pedidos y emite el comprobante |
| 9 | Transportista | E-11, a confirmar | Estados de envío y número de guía |

*Actor indirecto*: el **contador del comercio** no usa la plataforma; recibe el CSV de E-06.

**Reparto de permisos**

| Acción | Dueño | Encargado | Vendedor | Comprador | Operador |
|---|:--:|:--:|:--:|:--:|:--:|
| Crear la tienda y su administrador | ✅ | — | — | — | — |
| Gestionar usuarios y roles | ✅ | — | — | — | — |
| Crear y editar productos | ✅ | ✅ | — | — | — |
| Definir precios y listas por tipo de cliente | ✅ | ✅ | — | — | — |
| Crear un pedido | ✅ | ✅ | ✅ | — (lo hace el staff en el MVP) | — |
| Cambiar el estado de un pedido | ✅ | ✅ | ✅ | — | — |
| Registrar un cobro manual | ✅ | ✅ | ✅ | — | — |
| Reembolsar | ✅ | ✅ | — | — | — |
| Gestionar clientes | ✅ | ✅ | ✅ | — | — |
| Ver analítica y exportar CSV | ✅ | ✅ | — | — | — |
| Ver `/health` y diagnosticar | — | — | — | — | ✅ |
| Ver datos de negocio de una tienda | ✅ | ✅ | ✅ (solo sus pedidos) | — | ❌ por defecto |

---

## 3. Catálogo de épicas

| ID | Épica | Resultado de negocio | Prioridad | Servicios | Historia(s) |
|----|-------|----------------------|-----------|-----------|-------------|
| **E-01** | Onboarding de la tienda y acceso | Un comercio abre su tienda y su equipo entra el mismo día | Must (MVP) | Identity | US nuevas, FR-007 |
| **E-02** | Catálogo y control de stock | Publicar y mantener el surtido sin vender lo que no existe | Must (MVP) | Catalog | US-001, US-002, US-003 |
| **E-03** | Checkout y ciclo del pedido | Cerrar la venta con precio congelado y stock consistente | Must (MVP) | Sales, Catalog | US-004, US-005, US-006, US-007 |
| **E-04** | Cobro y devoluciones | Cobrar de verdad y devolver sin descuadres | Must (v1) · registro manual en MVP | Payment | US-008, US-009, US-010 |
| **E-05** | Clientes y atención | Reconocer al comprador y encontrarlo al instante | Should (v1) | Customer | US-011, US-012 |
| **E-06** | Inteligencia comercial | Decidir con datos del día anterior, no con intuición | Should (v2) | Analytics | US-013, US-014 |
| **E-07** | Comunicación transaccional | El comprador siempre sabe qué pasó con su pedido | Should (v1) | Notification | US-015, US-016 |
| **E-08** | Operación de la plataforma | Que la plataforma sea confiable, aislada y diagnosticable | Must (transversal) | Gateway + todos | FR-003, FR-007, FR-008, NFR |
| **E-09** | Experiencia de compra (storefront) | Que el comprador pueda comprar sin ayuda del comercio | Should (v2) | BlazorWeb | US-003, US-006 |
| **E-11** | Logística y entrega | Saber dónde está el pedido y quién lo lleva | *A confirmar* | nuevo | — |
| **E-12** | Compras y costos | Saber cuánto te cuesta lo que vendes y que el stock entre por un documento | **Must (MVP)** | Catalog | US-021, US-022, US-023 |

> **E-10 (facturación electrónica / SRI) sale de este alcance**: se implementará como
> **proyecto separado** y consumirá esta plataforma. Ver §11.
>
> E-11 sigue **propuesta**: depende de tu operación real (ver §9) y no genera tareas hasta
> que la confirmes.

---

## 4. Detalle de las épicas

### E-01 · Onboarding de la tienda y acceso

**Objetivo**: que un comercio nuevo pueda registrarse, entrar y operar con sus datos aislados,
sin intervención manual del equipo de plataforma.

**Indicadores**
- Tiempo desde el registro hasta el primer login correcto: **< 10 min**.
- 100 % de las peticiones sin tenant resuelto rechazadas (nunca datos por defecto).
- 0 accesos cruzados entre tenants en pruebas con dos tenants.

**Alcance**
- Alta de tenant (slug, nombre, admin) y usuario administrador.
- Login con email/contraseña; emisión de JWT con `tenant_id` y `role`.
- Roles: administrador, encargado, vendedor, cliente.
- Resolución del tenant por cabecera, query o subdominio.

**Fuera de alcance (MVP)**: SSO/OIDC, MFA, invitaciones por email, recuperación de contraseña.

**Criterios de aceptación**
```gherkin
Escenario: alta de comercio
  Dado un email de administrador no registrado
  Cuando registro el tenant "demo-tienda"
  Entonces el tenant existe con estado trial
  Y existe un usuario admin asociado que puede autenticarse

Escenario: aislamiento de tenant
  Dado dos tenants A y B con usuarios propios
  Cuando el usuario de A solicita un recurso de B
  Entonces recibe 403/404 y nunca datos de B

Escenario: credenciales inválidas
  Cuando intento entrar con contraseña incorrecta
  Entonces recibo 401 sin revelar si el email existe
```

**Riesgos**: contraseñas mal almacenadas (mitigado: PBKDF2), tenant inferido de forma implícita
(mitigado: rechazo explícito cuando falta).

---

### E-02 · Catálogo y control de stock

**Objetivo**: que el comercio publique su surtido con precios y existencias fiables, y que el
comprador solo vea lo que realmente puede comprar.

**Indicadores**
- Producto publicado y visible: **< 2 min** desde su creación.
- Sobrevendidos por catálogo desactualizado: **0**.
- Búsqueda/filtrado de catálogo con p95 **< 200 ms** con 50 productos.

**Alcance**
- CRUD de productos (nombre, descripción, precio decimal, moneda, stock, categoría).
- Categorías jerárquicas por tenant.
- Listado paginado con filtro por categoría, búsqueda y orden por nombre/precio.
- Visibilidad de disponibilidad para el comprador.

**Fuera de alcance (MVP)**: variantes (talla/color), imágenes múltiples, precios por lista,
atributos dinámicos.

**Criterios de aceptación**
```gherkin
Escenario: alta con validación
  Cuando creo un producto con nombre de 1 carácter
  Entonces recibo 400 con el detalle del campo

Escenario: aislamiento de catálogo
  Dado un producto del tenant A
  Cuando el tenant B lista su catálogo
  Entonces no aparece el producto de A

Escenario: filtrado y paginación correctos
  Dado 30 productos en la categoría "Electrónica"
  Cuando pido la página 2 con tamaño 10
  Entonces recibo 10 elementos y el total informado es 30 (no 50)
```

**Riesgos**: unicidad de categoría global en vez de por tenant (ya corregido en el modelo),
paginación aplicada después del filtro (defecto detectado y corregido).

---

### E-03 · Checkout y ciclo del pedido

**Objetivo**: convertir una intención de compra en un pedido con **precio y stock congelados**,
sin sobreventa aunque lleguen compras simultáneas.

**Indicadores**
- Sobreventas bajo 3 pedidos concurrentes del mismo producto: **0**.
- Pedido creado con p95 **< 500 ms** (sin contar la pasarela de pago).
- 100 % de los pedidos conservan el precio del momento de compra.

**Alcance**
- Creación de pedido con líneas (producto, cantidad) y dirección de envío.
- Reserva atómica de stock y liberación si el checkout falla.
- Máquina de estados: Pendiente → Confirmado → En proceso → Enviado → Entregado,
  con Cancelado y Reembolsado como estados finales.
- Snapshot de nombre y precio por línea.

**Fuera de alcance (MVP)**: cupones, listas de precios, venta sin stock (backorder),
carrito persistente entre dispositivos.

**Criterios de aceptación**
```gherkin
Escenario: stock suficiente
  Dado un producto con stock 10
  Cuando creo un pedido de 2 unidades
  Entonces el pedido queda en Pendiente
  Y el stock del producto pasa a 8

Escenario: stock insuficiente
  Dado un producto con stock 1
  Cuando creo un pedido de 2 unidades
  Entonces recibo 409 con código InsufficientStock
  Y el stock no cambia

Escenario: concurrencia
  Dado un producto con stock 1
  Cuando tres compradores piden 1 unidad a la vez
  Entonces exactamente uno recibe 201 y dos reciben 409
  Y el stock termina en 0, nunca negativo

Escenario: transición inválida
  Dado un pedido Entregado
  Cuando intento pasarlo a Pendiente
  Entonces recibo 409 y el estado no cambia
```

**Riesgos**: reserva sin compensación si falla un paso posterior (mitigado con liberación
explícita; a futuro, saga/outbox), precio recalculado en lugar de congelado.

---

### E-04 · Cobro online y devoluciones

**Objetivo**: cobrar con tarjeta y otros medios, registrar el cobro de forma auditable y
devolver total o parcialmente sin descuadrar la caja.

**Indicadores**
- Tasa de éxito de cobros > 95 % (excluyendo rechazos del emisor).
- Tiempo entre pedido confirmado y pago registrado: **< 30 s**.
- Diferencias de conciliación: **0**.

**Alcance — dos rebanadas**

*E-04a · Registro manual del cobro (MVP)*
- Registrar que un pedido fue cobrado (transferencia, depósito, efectivo) con importe y fecha.
- Impacto en el pedido: pasa a Confirmado y suma a `PaidAmount`.
- Sin pasarela, sin webhooks, sin credenciales de terceros.

*E-04b · Pasarela online (v1)*
- Autorización de pago por pedido, con referencia de pasarela y respuesta cruda guardada.
- Reembolso total y parcial con validación de importe.
- Notificación asíncrona del resultado (evento) al pedido y a la analítica.
- Webhook del proveedor con verificación de firma.

**Fuera de alcance (MVP)**: split de pagos, suscripciones, contracargo automático,
conciliación bancaria.

**Criterios de aceptación**
```gherkin
Escenario: cobro aprobado
  Dado un pedido por 50 USD sin pago previo
  Cuando autorizo el pago
  Entonces existe un pago Autorizado y se publica el evento de autorización

Escenario: doble cobro
  Dado un pedido que ya tiene pago autorizado
  Cuando intento cobrarlo otra vez
  Entonces recibo 409 y no se crea un segundo pago

Escenario: reembolso excesivo
  Dado un pago de 50 USD
  Cuando solicito un reembolso de 60 USD
  Entonces recibo 409 y el pago no cambia

Escenario: webhook no firmado
  Cuando llega un webhook sin firma válida
  Entonces recibo 400 y no se modifica ningún pago
```

**Riesgos**: webhook sin verificación de firma (hueco abierto hoy), pasarela en *stub* que no
cobra de verdad, idempotencia de webhooks repetidos.

---

### E-05 · Clientes y atención

**Objetivo**: reconocer al comprador, evitar duplicados y encontrarlo al instante cuando escribe.

**Indicadores**
- Búsqueda de cliente por email o teléfono: **< 1 s**.
- Clientes duplicados por tenant: **0**.
- Datos de contacto completos en el 90 % de los pedidos.

**Alcance**: alta, edición, baja lógica, direcciones y búsqueda por email/teléfono/nombre.

**Fuera de alcance (MVP)**: segmentos, historial de compras en el perfil, consentimientos RGPD.

---

### E-06 · Inteligencia comercial

**Objetivo**: que el comercio sepa cada mañana cuánto vendió, cuántos pedidos cerró y qué
productos tiran del negocio.

**Indicadores**
- Reporte del día anterior disponible antes de las **06:00**.
- Exactitud del ingreso diario frente a pedidos: **100 %**.
- Consulta de resumen del rango: **< 1 s**.

**Alcance**: agregado diario por tenant (ingresos, pedidos, pagos aprobados) y top 5 de productos;
consulta por rango de fechas; serie diaria para gráfico; **exportación a CSV** (compatible con
Excel) del resumen y del detalle diario.

**Decisión**: CSV es suficiente para llevar los reportes a Excel. No se implementa generación de
`.xlsx` (evita dependencia de un motor de hojas de cálculo en el servidor).

**Criterio de aceptación adicional**
```gherkin
Escenario: exportar a Excel
  Dado un resumen con datos de 7 días
  Cuando solicito la exportación CSV
  Entonces recibo un archivo con cabecera, separador ";" y decimales con coma
  Y al abrirlo en Excel aparecen una fila por día sin pasos manuales

Escenario: exportación vacía
  Dado un rango sin movimientos
  Cuando solicito la exportación
  Entonces recibo solo la cabecera, y no un error
```

**Fuera de alcance (MVP)**: cohortes, embudos de conversión, exportación a Excel/PDF.

---

### E-07 · Comunicación transaccional

**Objetivo**: que el comprador reciba confirmación de su pedido y que operaciones se entere de
las ventas grandes, sin bloquear la venta.

**Indicadores**
- Confirmación enviada en **< 30 s** desde el pedido.
- Pedidos sin confirmación por falta de email: **< 2 %**.
- Un fallo de notificación **nunca** hace fallar el pedido.

**Alcance**: plantillas por tenant, envío por email y SMS, registro de cada envío con estado y
error, alerta de pedido de alto valor (> 1000).

**Fuera de alcance (MVP)**: push, campañas masivas, editor visual de plantillas.

---

### E-08 · Operación de la plataforma (transversal)

**Objetivo**: que la plataforma sea confiable, aislada y diagnosticable mientras crece el número
de tenants.

Esta épica **no se negocia por release**: se entrega con la primera.

**Alcance**
- Aislamiento multi-tenant en cada consulta (cabecera, claim, subdominio).
- Gateway con enrutado, autenticación y CORS.
- `/health` por servicio y trazas/logs correlacionados.
- Rechazo por defecto: sin tenant válido no hay datos.

**Indicadores**
- Disponibilidad objetivo **99,5 %** mensual.
- p95 de API **< 200 ms**.
- 0 incidentes de fuga entre tenants.
- 100 % de servicios con `/health` verde en el arranque.

**Criterios de aceptación**
```gherkin
Escenario: servicio sin dependencias
  Cuando se cae RabbitMQ
  Entonces /health lo reporta y la API de lectura sigue respondiendo

Escenario: sin tenant
  Cuando llamo a un endpoint de datos sin cabecera ni claim de tenant
  Entonces recibo 400/404 y no se devuelve ningún dato
```

---

### E-09 · Experiencia de compra (storefront)

**Objetivo**: que el comprador pueda ver el catálogo real, armar su pedido y recibir confirmación
sin que el comercio intervenga.

**Indicadores**
- Checkout completo en **< 5 pasos** y **< 3 min**.
- Errores de formulario explicados en pantalla, no solo en consola.

**Fuera de alcance (MVP)**: PWA/offline, multi-idioma, diseño por tenant.

---

### E-12 · Compras y costos

**Objetivo**: registrar lo que el comercio **compra a sus proveedores** con el costo real de cada
producto, para saber cuánto gana de verdad y para que el inventario entre por un documento y no a
mano.

Hoy el sistema sólo conoce el **PVP** (lo que cobras). Sin el costo, un producto que se vende a
50 ¢ no dice si deja 15 ¢ o 10 ¢ de margen, y no hay forma de comparar proveedores.

**Indicadores**
- 100 % del stock entra por una compra recibida o por un ajuste registrado: **cero ediciones
  manuales sin rastro**.
- Margen por producto (PVP − costo promedio) visible al abrir el catálogo.
- Diferencias de inventario inexplicables tras un mes de operación: **0**.

**Alcance**
- **Proveedores**: alta, edición y baja lógica, con identificación, contacto y condiciones.
- **Compra como documento**: cabecera (proveedor, fecha, número de factura del proveedor, notas)
  más líneas (producto, cantidad, **costo unitario**). El costo vive en la línea, no en el producto.
- **Dos velocidades, un solo estándar**:
  - *Compra rápida*: una a tres líneas en una pantalla, para «me llegaron 24 gaseosas a 35 ¢».
  - *Compra completa*: muchas líneas, factura del proveedor y condiciones, para el ingreso grande.
  Ambas escriben las mismas tablas y el mismo kardex, así que los reportes no las distinguen.
- **Estados**: `borrador` → `recibida`. Sólo al recibir se mueve el stock.
- **Costo promedio ponderado (CPP)**: al recibir se recalcula
  `(stock_previo × costo_previo + cantidad × costo_nuevo) / (stock_previo + cantidad)`.
- **Kardex**: cada movimiento deja rastro (entrada por compra, salida por venta, ajuste) con
  cantidad, costo y documento de origen.
- **Margen visible**: el catálogo muestra costo, PVP y margen unitario y porcentual.

**Fuera de alcance (MVP)**: órdenes de compra con aprobación, cuentas por pagar y vencimientos,
FIFO por capas, multi-bodega, importación desde CSV o Excel.

**Historias**

| ID | Historia | Resultado de negocio |
|---|---|---|
| US-021 | Registrar un proveedor | Sé a quién le compro y con qué condiciones |
| US-022 | Registrar una compra con sus líneas y costos | El costo real queda guardado en un documento, no en la memoria de alguien |
| US-023 | Recibir la compra | El stock sube, el costo promedio se recalcula y queda el kardex |

**Criterios de aceptación**
```gherkin
Escenario: compra pequeña
  Dado un producto "Gaseosa" con stock 0 y costo 0
  Cuando registro una compra de 24 unidades a 0,35 y la recibo
  Entonces el stock pasa a 24
  Y el costo del producto queda en 0,35
  Y existe un movimiento de entrada de 24 unidades por esa compra

Escenario: dos proveedores y costo promedio ponderado
  Dado un producto con stock 100 y costo 0,35
  Cuando recibo una compra de 100 unidades a 0,40
  Entonces el stock pasa a 200
  Y el costo del producto pasa a 0,375

Escenario: margen con el costo real
  Dado un producto con costo 0,375 y PVP 0,50
  Cuando consulto el catálogo
  Entonces el margen unitario es 0,125 y el margen porcentual es 25 %

Escenario: el borrador no toca el stock
  Dado un producto con stock 10
  Cuando registro una compra de 50 unidades y la dejo en borrador
  Entonces el stock sigue en 10
  Y no existe movimiento de entrada

Escenario: recepción única
  Dada una compra ya recibida
  Cuando intento recibirla otra vez
  Entonces recibo 409 y el stock no cambia

Escenario: costo inválido
  Cuando registro una línea con costo 0
  Entonces recibo 400 y la compra no se guarda
```

**Riesgos**: recibir dos veces la misma compra (se rechaza con 409 y el estado lo impide); costo
en cero por descuido (se valida `> 0`); stock que no cuadra si alguien sigue ajustando cantidades a
mano (el kardex lo delata); producto vendido antes de tener costo (su margen aparece como
desconocido, no como 0).

**Decisiones asociadas**: D12 (costo promedio ponderado), D13 (recibir suma stock), D14 (factura
del proveedor), D15 (vive en Catalog, sin servicio nuevo).

---

## 5. Épicas candidatas (pendientes de confirmar)

### E-11 · Logística y entrega
Estados de envío, transportista, número de guía y tracking para el comprador. Hoy los estados
llegan hasta "Entregado" sin datos de envío.

### E-10 · Facturación electrónica (SRI) → **proyecto separado**
No forma parte de esta plataforma. Se implementará como proyecto aparte que consuma los pedidos
de Saga-Sales.

Lo único que esta plataforma debe aportar es que **el modelo lo prevea**, sin implementarlo:
- `Order` reserva `FiscalStatus`, `FiscalDocumentNumber` y `TaxAmount` (nulos por ahora).
- Cada línea de pedido reserva `TaxRate` y `TaxAmount`.
- El pedido publica `OrderPlacedEvent` con importe e impuestos, para que el proyecto fiscal lo consuma.

Así el día que se construya la facturación no hay que migrar pedidos históricos.

---

## 6. Roadmap por releases

| Release | Épicas | Criterio de salida |
|---|---|---|
| **MVP** | E-01, E-02, E-03, E-04a, **E-12**, E-08 | Un comercio registra su tienda, publica productos, **registra lo que compra y a qué costo**, recibe un pedido con stock correcto, registra el cobro a mano y se ve en `/health`. |
| **v1** | E-04b, E-05, E-07 | Cobro online real con webhooks, devoluciones, clientes y confirmación automática por email. |
| **v2** | E-06, E-09, (E-11) | El comercio exporta su reporte diario a CSV y el comprador compra solo por la web. |
| **Proyecto aparte** | E-10 | Facturación electrónica (SRI), consumiendo los pedidos de esta plataforma. |

---

## 7. Matriz de trazabilidad

| Épica | Historias (`spec.md`) | Requisitos | Tareas (`tasks.md`) |
|---|---|---|---|
| E-01 | — (nuevas: alta de tenant, login) | FR-007 | P01–P05 |
| E-02 | US-001, US-002, US-003 | FR-001 | P11–P16 |
| E-03 | US-004, US-005, US-006, US-007 | FR-002 | P21–P26 |
| E-04 | US-008, US-009, US-010 | FR-004 | P31–P35 |
| E-05 | US-011, US-012 | — | P41–P44 |
| E-06 | US-013, US-014 | FR-005 | P56 |
| E-07 | US-015, US-016 | FR-006 | (falta desglose) |
| E-08 | — | FR-003, FR-007, FR-008, NFR-001…005 | P03, P04 |
| E-09 | US-003, US-006 | — | P51–P57 |
| E-12 | US-021, US-022, US-023 (nuevas) | FR-031…FR-038 (nuevos) | T114 en adelante |

---

## 8. Restricción técnica vigente: **stored procedures, sin EF Core**

Esta es una corrección a `research.md` y `data-model.md`, que asumían EF Core y migraciones.
A partir de aquí la persistencia es **PL/pgSQL + acceso con Dapper**.

**Qué cambia en el código ya entregado (Fase 1)**

| Hoy | Debe pasar a |
|---|---|
| `Microsoft.EntityFrameworkCore*` | fuera; entra `Microsoft.Data.SqlClient` + `Dapper` |
| `*DbContext : DbContext` con `OnModelCreating` | `*Repository` + `IDbConnectionFactory` |
| `Migrations/` (no existe) o `dotnet ef` | `db/<servicio>/V###__descripcion.sql` versionados |
| `ExecuteUpdateAsync` para stock atómico | `sp_reserve_stock` con `SELECT … FOR UPDATE` |
| Filtro por tenant escrito en cada consulta LINQ | `p_tenant_id` obligatorio en **cada** procedimiento |
| `Include`, `AsNoTracking`, navegaciones | `sp_get_order`, `sp_list_orders` con `jsonb_agg` |
| Unit of work con `SaveChangesAsync` | transacción explícita dentro del procedimiento |

**Lo que no cambia**: dominio (entidades y reglas), CQRS/MediatR, eventos de MassTransit,
JWT, validación con FluentValidation, gateway, Blazor.

**Ventaja a tu favor**: poner `p_tenant_id` como primer parámetro obligatorio de cada SP hace que
el aislamiento multi-tenant lo garantice la base de datos, no la disciplina del programador.

**Convención propuesta**
```
db/<servicio>/
  V001__schema.sql          tablas, índices, restricciones
  V002__functions.sql       sp_create_*, sp_get_*, sp_list_*, sp_update_*
  V003__seed.sql            datos de desarrollo
```
Versionado con una tabla `schema_version` aplicada por `scripts/apply-db.ps1` con `sqlcmd`.

---

## 9. Preguntas abiertas para cerrar las épicas

*Resueltas el 2026-11-04*: el cobro es **manual en el MVP** (E-04a) y la facturación electrónica
**no entra aquí** (proyecto aparte, E-10). Ver §11.

Pendientes:

1. **Rubro y catálogo real**: ¿qué vende el primer comercio (retail físico, servicios, digital)?
   Cambia el modelo de producto (¿variantes, tallas, suscripciones?).
2. **Idioma y moneda**: ¿solo español/USD, o multi-moneda real? Afecta a precios y reportes.
3. **Quién opera**: ¿un equipo interno administra todos los tenants (soporte), o cada comercio
   se autogestiona sin intervención?
4. **Reportes**: además del CSV, ¿necesitas envío programado por email (resumen diario)?
5. **Logística (E-11)**: ¿el envío lo gestiona el comercio o quieres tracking para el comprador?

---

## 10. Cambios respecto al plan original

- Las épicas **reordenan la prioridad**: el MVP no incluye pasarela de pago (E-04b → v1) ni
  analítica (E-06 → v2); sí incluye registro manual del cobro (E-04a).
- Las tareas `P31–P35` (pago con pasarela), `P51–P57` (frontend) y `P41–P44` (clientes) quedan
  fuera del MVP.
- Falta desglosar E-07 en tareas propias: hoy `tasks.md` no la cubre.
- `spec.md` necesita una historia para **alta de tenant** (E-01 no tiene US y la tiene que tener).
- `data-model.md` debe reescribirse sin EF y con los procedimientos por agregado.
- **Cambio de alcance (D5)**: el tipo de cliente y las listas de precios amplían E-02 (precios por
  tipo sobre el precio base) y E-05 (tipo de cliente), y añaden un paso de resolución de precio al
  checkout de E-03. Es el único cambio que aumenta trabajo del MVP, no solo lo reordena.

---

## 11. Decisiones cerradas (2026-11-04)

| # | Decisión | Efecto |
|---|---|---|
| D1 | **Persistencia con stored procedures**, sin EF Core | Dapper + PL/pgSQL (§8); enmienda en `research.md` y `data-model.md` |
| D2 | **Cobro manual en el MVP** | E-04 se parte en E-04a (MVP) y E-04b (v1, pasarela) |
| D3 | **SRI en un proyecto separado** | E-10 sale del alcance; el modelo solo reserva campos fiscales |
| D4 | **Reportes en CSV es suficiente** | E-06 exporta CSV; no se genera `.xlsx` |
| D5 | **Listas de precios por tipo de cliente** (retail/mayorista) | E-02 suma precios por tipo sobre el precio base; E-05 suma el tipo de cliente (retail por defecto) |
| D6 | **Una sola moneda: USD** | Sin tipos de cambio ni conversión en los reportes |
| D7 | **Dirección de envío siempre obligatoria** | El checkout la exige para cualquier tipo de producto |
| D8 | **El comprador no inicia sesión en el MVP** | Es un registro de cliente creado por el staff; la cuenta propia llega con el storefront (E-09) |
| D9 | **El rol vendedor existe con alcance acotado** | Crea pedidos, registra cobros y consulta catálogo/clientes; no edita precios, no reembolsa, no gestiona usuarios |
| D10 | **El operador de plataforma no lee datos de negocio** | Solo ve salud, logs y métricas; la impersonación auditada queda fuera del MVP |
| D12 | **Costo promedio ponderado (CPP)** como método de costeo | Al recibir una compra: `(stock × costo + cantidad × costo_nuevo) / (stock + cantidad)`. FIFO y último costo quedan descartados para el MVP |
| D13 | **Recibir una compra suma stock**; el borrador no lo toca | El stock nunca se edita a mano: entra por compra recibida o por ajuste registrado |
| D14 | **La compra guarda la factura del proveedor** (número y fecha) | Las cuentas por pagar y los vencimientos quedan fuera del MVP |
| D15 | **Compras vive dentro de Catalog**, sin servicio nuevo | El costo y el stock son del catálogo; además la constitución limita a 9 servicios y ya hay 8 |

---

## 12. Definición de Terminado (aplica a *todas* las épicas)

Una épica **no está terminada** si falta cualquiera de estos puntos. Es la puerta que hoy no
existe en el proyecto.

1. **Historias en `spec.md`** con al menos un criterio de aceptación por historia.
2. **Pruebas** (Art. III de la constitución):
   - unitarias del dominio (reglas de negocio, transiciones, validaciones);
   - de integración contra SQL Server real con los procedimientos;
   - de contrato para cada endpoint publicado.
   Sin pruebas en verde, la épica vuelve a *En curso*.
3. **Procedimientos SQL** versionados en `db/<servicio>/` con `p_tenant_id` obligatorio.
4. **Aislamiento verificado** con dos tenants en cada épica que lea datos.
5. **`/health`** reportando el estado del servicio y sus dependencias.
6. **Documentación**: endpoint en `README.md` y decisión registrada si se desvía de este documento.

> **Estado actual de la puerta 2 (2026-11-04):** parcialmente en verde.
> - **Unitarias: existen y pasan** — `tests/SagaSales.UnitTests`, 34 casos en verde
>   (máquina de estados del pedido, reembolsos, hashing de contraseñas, `Result`/paginación).
> - **Integración y contrato: pendientes** — requieren SQL Server y los servicios en ejecución.
> - Antes de esta corrección, `tests/Unit` y `tests/Contract` eran dos archivos **sin extensión,
>   sin proyecto y sin una sola aserción**; quedaron documentados como placeholder.

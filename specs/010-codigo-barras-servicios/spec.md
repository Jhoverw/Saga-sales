# Spec 010 — Código de barras de punta a punta y productos de servicio

**Rama**: `010-codigo-barras-servicios` · **Fecha**: 2026-11-09 · **Estado**: borrador para aprobar
**Backlog**: amplía HU-08 (venta con lector) y agrega HU-14 (servicios). Base: spec 004 (N.º de artículo), spec 009 (compras como factura).

## Problema (lo que dijo el dueño)
> "En las compras no puedo buscar el producto, no me sé los códigos. El producto debe tener su código de barras; el
> código debe ser recogido por el módulo de compras y usado en el de ventas. Además, el producto puede ser un servicio,
> por lo que no mueve inventario."

## Lo que encontramos en el sistema hoy
- **Compras (spec 009)**: la búsqueda existe por código *y* por nombre, pero el campo dice "código" y solo busca al
  pulsar Enter. Quien no sabe el código no descubre que puede escribir el nombre. Es un problema de diseño, no de datos.
- **Producto**: tiene `code` (N.º de artículo interno, spec 004), único por tienda. **No tiene código de barras**.
- **Caja**: busca **solo por nombre** y desde 3 letras (`Caja.razor`: "Search is by name until the barcode lookup is
  built"). Un lector USB escribe 13 dígitos + Enter y no encuentra nada. HU-08 nunca se construyó.
- **Servicios**: no existen. Todo producto descuenta stock al vender y se aparta en pedidos (spec 008), y una venta sin
  stock se rechaza.

## Requerimiento mejorado
1. Cada producto tiene **dos identificadores con propósitos distintos**:
   - **Código interno** (N.º de artículo): lo pone la tienda, corto y fácil de dictar ("GA12").
   - **Código de barras**: lo trae el fabricante (EAN-13, EAN-8, UPC-A) o lo imprime la tienda. **Puede haber varios**
     por producto: el de la unidad y el de la caja (el fardo de 12 trae otro código). Cada código dice cuántas unidades
     representa.
2. **Un solo buscador de productos** para Compras, Caja y Pedidos: mientras escribes muestra coincidencias por
   código de barras, código interno o nombre. El lector siempre gana: un código exacto entra directo sin elegir.
3. **El código se captura donde llega la mercadería**: en Compras, al escanear un código que no existe, el sistema
   ofrece asociarlo a un producto existente o crear el producto nuevo con ese código. Así el catálogo se llena solo con
   cada factura, sin cargar códigos a mano.
4. **Ventas usa lo mismo**: en Caja el lector agrega el producto; si escanea el código de la caja agrega 12 unidades.
5. **Tipo de producto: Bien o Servicio**. Un servicio se vende y factura igual (con o sin IVA), pero no tiene stock,
   no se compra al inventario, no se aparta en pedidos y no aparece en reportes ni alertas de inventario.

## Historias de usuario

### HU-10.1 Buscar productos sin saber el código (Compras) — P0
**Como** administrador que ingresa una factura, **quiero** escribir parte del nombre, el código interno o escanear el
código de barras y ver las coincidencias al instante, **para** no tener que memorizar códigos.
- **CA1** Desde 2 caracteres se muestra una lista desplegable (máx. 10) con: código de barras / código interno,
  nombre, empaque y último costo. Sin pulsar Enter.
- **CA2** Flechas ↑↓ para moverse, Enter o clic para elegir, Esc para cerrar.
- **CA3** Un código de barras o interno **exacto** se elige solo (sirve para el lector).
- **CA4** El campo se llama "Producto" con la ayuda "nombre, código o escanea".
- **CA5** Los servicios no aparecen en la búsqueda de compras.
- **CA6** Si no hay coincidencias: "No existe — Crear producto" (ya existe de la spec 009) y, si lo escrito parece un
  código de barras, "Asociar este código a un producto" (HU-10.3).

### HU-10.2 Registrar los códigos de barras del producto (Catálogo) — P0
**Como** administrador **quiero** guardar uno o varios códigos de barras por producto, cada uno con sus unidades,
**para** que el lector reconozca la unidad y la caja.
- **CA1** En el producto: sección "Códigos de barras" con lista (código, unidades, principal) y botón "Escanear".
- **CA2** Un código no puede estar en dos productos de la misma tienda → 409 "Ese código ya es de «X»".
- **CA3** Se aceptan dígitos y letras (máx. 30). Si tiene forma EAN-13/EAN-8/UPC-A y el dígito verificador no cuadra,
  se **advierte** pero se permite guardar (hay códigos internos y etiquetas propias).
- **CA4** Unidades por código: 1 = unidad; > 1 = caja/fardo. El código de la caja actualiza las unidades por caja
  del producto (spec 009).
- **CA5** Quitar un código no afecta ventas ni compras pasadas (guardan el producto, no el código).
- **CA6** La importación de productos (HU-10, backlog) acepta la columna "código de barras".

### HU-10.3 Capturar el código de barras al comprar — P0
**Como** administrador que recibe mercadería **quiero** escanear cada producto de la factura y que el sistema aprenda
los códigos nuevos, **para** que el catálogo quede listo para vender sin trabajo extra.
- **CA1** Escaneo de un código conocido → agrega la línea con el producto; si es el código de la caja, la línea queda
  en "Caja ×N" automáticamente.
- **CA2** Escaneo de un código desconocido → panel en la misma línea con dos opciones:
  **Asociar a un producto existente** (buscador de HU-10.1, se pregunta si es unidad o caja) o
  **Crear producto nuevo** (el código ya viene escrito).
- **CA3** La asociación se guarda al momento (no espera a registrar la compra) y queda en el historial del producto.
- **CA4** Escanear dos veces el mismo producto suma la cantidad en la línea existente (ya existe en la spec 009).
- **CA5** Un cajero no puede asociar ni crear códigos (solo dueño/administrador).

### HU-10.4 Vender con lector de código de barras (Caja y Pedidos) — P0 *(reemplaza HU-08)*
**Como** cajero **quiero** escanear el producto y que entre a la venta, **para** atender rápido y sin errores.
- **CA1** El buscador de Caja usa el mismo servicio de HU-10.1: código de barras, código interno o nombre, desde 2
  caracteres (hoy: solo nombre desde 3).
- **CA2** Un código exacto agrega el producto sin abrir la lista y deja el cursor listo para el siguiente escaneo.
  Escanear el mismo producto otra vez suma 1 (o las unidades del código de la caja).
- **CA3** Código desconocido: sonido/aviso "Código 7861234567890 no registrado" y el campo se limpia; la venta no se
  interrumpe. El aviso ofrece al administrador "Asociar" (HU-10.3).
- **CA4** Si el producto está agotado se avisa como hoy; si es servicio, nunca.
- **CA5** Funciona con lector USB (teclado + Enter) y con la cámara (componente `BarcodeScanner` existente).
- **CA6** En Pedidos (spec 008) el mismo buscador.

### HU-10.5 Productos de servicio — P1
**Como** dueño **quiero** marcar un producto como servicio (instalación, envío, reparación, mano de obra), **para**
venderlo y facturarlo sin que pida ni descuente inventario.
- **CA1** En el producto: "Tipo: Bien | Servicio". Por defecto Bien.
- **CA2** Servicio: se ocultan stock, lotes, unidades por caja, nivel de reposición y costo de inventario.
- **CA3** Al vender (Caja y Pedidos) un servicio no valida stock, no descuenta, no se aparta y no escribe kardex.
- **CA4** Un pedido con bienes y servicios aparta solo los bienes; al anularse devuelve solo los bienes.
- **CA5** No aparece en Compras (HU-10.1 CA5), ni en reporte de inventario, alertas de agotados/por vencer ni panel.
- **CA6** Sí aparece en reportes de ventas, IVA y comprobantes, con su IVA (sí/no) como cualquier producto.
- **CA7** Cambiar de Bien a Servicio solo si no tiene stock ni apartados ni lotes vigentes → 409 con el motivo.
  Cambiar de Servicio a Bien se permite (empieza con stock 0).
- **CA8** El catálogo filtra por tipo y muestra una etiqueta "Servicio".

## Reglas y decisiones (por defecto; el dueño puede cambiarlas)
| # | Decisión | Por defecto |
|---|----------|-------------|
| D1 | Código interno y código de barras | Campos distintos; ambos buscables |
| D2 | Varios códigos por producto | Sí, cada uno con unidades (unidad, caja) |
| D3 | Dígito verificador EAN/UPC inválido | Advertir, no bloquear |
| D4 | Código desconocido en Caja | Avisar y seguir; solo administrador asocia |
| D5 | Servicio en compras | No se compra al inventario (un gasto va a caja chica) |
| D6 | Códigos de balanza (prefijo 2 con peso) | Fuera de alcance: va con HU-06 venta por peso |

## Modelo de datos (Catalog, V027)
- `product_barcodes (id, tenant_id, product_id, barcode nvarchar(30), units int ≥ 1, is_primary bit, created_at,
  created_by)`; único `(tenant_id, barcode)`; un solo principal por producto.
- `products.kind tinyint` (1 bien, 2 servicio), default 1, con CHECK.
- Procedimientos (todos con `@p_tenant_id` primero):
  - `sp_product_search(@term, @max_rows, @purpose)` — `@purpose` 1 venta (bienes y servicios), 2 compra (solo
    bienes). Orden: código de barras exacto → código interno exacto → prefijo → nombre. Devuelve las unidades del código
    que coincidió.
  - `sp_product_barcode_add / _remove / _list`.
  - `sp_product_set_kind` con las validaciones de CA7.
  - `sp_purchase_product_find` pasa a usar `sp_product_search(purpose 2)`.
  - Ventas, reserva y liberación de stock y retención de pedidos (V027–V032 de Sales/Catalog) ignoran `kind = 2`.

## API (por el gateway, rutas existentes)
- `GET api/products/search?term=&purpose=sale|purchase` (Staff) → `ProductSearchResultDto` con `MatchedBarcode`,
  `MatchedUnits`, `Kind`, `Stock`, `Price`, `IsTaxable`.
- `GET/POST/DELETE api/products/{id}/barcodes` (ManagerOrOwner).
- `PUT api/products/{id}/kind` (ManagerOrOwner).

## Fuera de alcance
Impresión de etiquetas de código de barras · códigos de balanza (HU-06) · servicios con costo o comisión por técnico.

## Criterios de éxito
- Una factura de 20 líneas se ingresa escaneando, sin escribir códigos, y los productos nuevos quedan con su código.
- En Caja, escanear 10 productos distintos agrega 10 líneas sin tocar el mouse.
- Una venta con un servicio y sin stock de ese servicio se cobra sin error y el kardex no cambia.

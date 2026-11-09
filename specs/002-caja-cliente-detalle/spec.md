# Feature Specification: Caja con selección de cliente y detalle en grilla

**Feature Branch**: `002-caja-cliente-detalle`

**Created**: 2026-11-08

**Status**: Draft

**Input**: User description: "La caja factura rápido, pero debe dejarme escoger al cliente: no siempre será consumidor final. El ingreso de los detalles debe ser como el de la imagen (grilla tipo factura de escritorio)."

## Clarifications

### Session 2026-11-08

- Q: ¿Qué columnas de la imagen lleva la grilla? → A: Solo las que el sistema ya soporta: Producto, Descripción, Cantidad, Precio unit. c/IVA, Total c/IVA y Stock. Descuento (Dto.) y Comentarios quedan fuera; Amasado, Costo y Cargo son de otro sistema.
- Q: ¿El cliente se elige solo entre existentes o también se crea desde Caja? → A: Búsqueda + alta rápida con cédula/RUC.
- Q: ¿Cantidades enteras o decimales? → A: Decimales (productos por peso). Se entrega en una segunda fase por su impacto en stock, compras, lotes y pedidos.
- Q: ¿El email es obligatorio en el alta rápida? → A: Opcional. Si se informa, sigue siendo único por tienda.
- Q: ¿Cómo se guarda una empresa con RUC? → A: Con un campo razón social; las personas siguen con nombre y apellido.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Vender a consumidor final sin pasos extra (Priority: P1)

El cajero abre Caja y vende de inmediato. Si no elige cliente, la venta sale a "Consumidor final".

**Why this priority**: Es la venta más común y hoy ya funciona; el cambio no puede hacerla más lenta.

**Independent Test**: Abrir Caja, agregar un producto y cobrar sin tocar el cliente; el pedido queda a nombre de Consumidor final con identificación 9999999999999.

**Acceptance Scenarios**:

1. **Given** una tienda sin cliente "Consumidor final", **When** se abre Caja, **Then** se crea con identificación tipo 07 y número 9999999999999 y queda seleccionado.
2. **Given** una venta cobrada a otro cliente, **When** termina el cobro, **Then** el cliente vuelve a "Consumidor final" para la siguiente venta.

---

### User Story 2 - Elegir un cliente existente (Priority: P1)

El cajero busca al cliente por cédula/RUC, nombre o razón social y lo elige para la venta.

**Why this priority**: Sin esto no se puede facturar a nombre de un cliente con datos.

**Independent Test**: Con un cliente registrado, buscar por parte de su cédula o nombre en Caja, elegirlo y cobrar; el pedido queda a su nombre.

**Acceptance Scenarios**:

1. **Given** un cliente con RUC 1790012345001, **When** el cajero escribe "17900", **Then** aparece en los resultados con su razón social y RUC.
2. **Given** un cliente de otra tienda con la misma cédula, **When** se busca, **Then** no aparece (aislamiento por tenant).

---

### User Story 3 - Alta rápida de cliente desde Caja (Priority: P1)

Si el cliente no existe, el cajero lo registra sin salir de Caja: tipo de identificación, número, nombre y apellido o razón social, y opcionalmente email y teléfono.

**Why this priority**: Evita que el cajero deje la venta para ir a la pantalla de Clientes.

**Independent Test**: Registrar una cédula nueva desde Caja, verificar que queda seleccionada y que la venta se cobra a su nombre.

**Acceptance Scenarios**:

1. **Given** una cédula válida que no existe en la tienda, **When** se registra sin email, **Then** se crea y queda seleccionada.
2. **Given** una cédula con dígito verificador incorrecto, **When** se intenta registrar, **Then** se rechaza indicando que la cédula no es válida, en pantalla y en el servidor.
3. **Given** una cédula ya registrada en la tienda, **When** se intenta registrar otra vez, **Then** se rechaza con conflicto (409) y se ofrece elegir el cliente existente.
4. **Given** un RUC de sociedad, **When** se registra, **Then** se pide razón social en lugar de nombre y apellido.

---

### User Story 4 - Detalle de venta en grilla (Priority: P2)

Las líneas se ingresan en una grilla tipo factura: Producto (con búsqueda), Descripción, Cantidad, Precio unit. c/IVA, Total c/IVA y Stock.

**Why this priority**: Mejora la lectura y la velocidad, pero la venta ya funciona sin ella.

**Independent Test**: Agregar tres productos, cambiar una cantidad y eliminar una fila; los totales y el stock mostrado son correctos.

**Acceptance Scenarios**:

1. **Given** un producto agregado, **When** se cambia la cantidad, **Then** el total de la fila y el total general se recalculan con el IVA vigente, igual que el servidor.
2. **Given** una cantidad mayor al stock mostrado, **When** se cobra, **Then** el servidor rechaza la venta por stock (409) y la grilla lo informa.

---

### User Story 5 - Cantidades decimales (Priority: P3, fase 2)

Productos vendidos por peso aceptan cantidades con decimales.

**Why this priority**: Requiere cambiar la cantidad a decimal en pedidos, stock, lotes y compras; se entrega después de validar la fase 1.

**Independent Test**: Vender 1,250 kg de un producto y verificar el descuento exacto de stock.

### Edge Cases

- Consumidor final no se puede editar ni dar de baja desde Caja.
- Una búsqueda de cliente con menos de 3 caracteres no consulta al servidor.
- Dos clientes sin email no chocan entre sí.
- Un pasaporte acepta letras y números (3 a 20 caracteres) y no se valida dígito verificador.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Caja MUST usar "Consumidor final" (tipo 07, número 9999999999999) cuando no se elige cliente.
- **FR-002**: El sistema MUST permitir buscar clientes por número de identificación, nombre, apellido, razón social, email o teléfono, dentro del tenant.
- **FR-003**: El cliente MUST tener tipo de identificación con los códigos SRI: 04 RUC, 05 cédula, 06 pasaporte, 07 consumidor final, 08 identificación del exterior.
- **FR-004**: La cédula MUST validarse: 10 dígitos, provincia 01–24 o 30, tercer dígito menor a 6 y dígito verificador módulo 10.
- **FR-005**: El RUC MUST validarse: 13 dígitos, provincia válida, establecimiento distinto de 000. Persona natural (tercer dígito 0–5): los 10 primeros dígitos son una cédula válida. Sociedad (6 o 9): se valida estructura; el dígito verificador no se exige para no bloquear RUC válidos.
- **FR-006**: El par tipo + número de identificación MUST ser único por tenant.
- **FR-007**: El email MUST ser opcional; si se informa, MUST ser válido y único por tenant.
- **FR-008**: Para RUC de sociedad MUST pedirse razón social; para los demás tipos, nombre y apellido.
- **FR-009**: La validación de identificación MUST aplicarse igual en pantalla y en el servidor (misma librería).
- **FR-010**: La grilla de Caja MUST mostrar Producto, Descripción, Cantidad, Precio unit. c/IVA, Total c/IVA y Stock, con alta y baja de filas.
- **FR-011** *(fase 2)*: La cantidad MUST aceptar decimales en pedidos, stock, lotes y compras.

### Key Entities

- **Cliente**: se agrega tipo de identificación, número de identificación y razón social; el email pasa a ser opcional.

## Success Criteria *(mandatory)*

- **SC-001**: Una venta a consumidor final no requiere más pasos que hoy.
- **SC-002**: Elegir un cliente existente toma una búsqueda y un clic.
- **SC-003**: Registrar un cliente nuevo desde Caja no obliga a salir de la pantalla.
- **SC-004**: Ninguna cédula o RUC con estructura inválida se guarda.

# Feature Specification: Precios con IVA incluido (PVP)

**Feature Branch**: `003-precios-pvp`

**Created**: 2026-11-08

**Status**: Draft

**Input**: User description: "Vendí 2 Coca-Cola de 0.40 y me sale 0.81. El precio debe ser con IVA incluido (PVP)."

## Clarifications

### Session 2026-11-08

- Q: ¿El precio que se registra incluye IVA? → A: Sí, es el PVP (precio de percha).
- Q: ¿Qué se hace con los precios existentes (guardados sin IVA)? → A: Se convierten a PVP: precio × (1 + IVA vigente), redondeado a centavos, solo productos gravados.
- Q: ¿Las ventas registradas se conservan? → A: Sí. Los pedidos existentes no cambian; solo los nuevos usan PVP.

## Diagnóstico

Hoy el precio se guarda sin IVA y el IVA se calcula por línea. Coca-Cola a 0.35 + 15 %: una unidad = 0.35 + 0.05 = 0.40 (el IVA real 0.0525 se redondea abajo); dos unidades = 0.70 + 0.11 = 0.81 (0.105 se redondea arriba). La pantalla muestra 0.40 pero el precio real con IVA es 0.4025.

Además, el indicador "Aplica IVA" del producto no llega al pedido: el IVA se aplica a todas las líneas.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - El total es PVP × cantidad (Priority: P1)

El cajero vende 2 unidades de un producto de PVP 0.40 y cobra exactamente 0.80.

**Independent Test**: Crear un producto gravado de 0.40, vender 2 en Caja: total 0.80, base 0.70, IVA 0.10.

**Acceptance Scenarios**:

1. **Given** IVA 15 % y un producto gravado de PVP 0.40, **When** se venden 2, **Then** total 0.80, subtotal 0.70, IVA 0.10.
2. **Given** un producto no gravado de PVP 1.00, **When** se vende 1, **Then** total 1.00, subtotal 1.00, IVA 0.00.
3. **Given** Caja y el servidor, **When** se calcula cualquier venta, **Then** ambos dan el mismo total al centavo.

---

### User Story 2 - Precios existentes convertidos sin doble IVA (Priority: P1)

Los precios actuales (sin IVA) pasan a PVP una sola vez.

**Independent Test**: Producto gravado a 0.35 con IVA 15 % → tras la conversión 0.40; una segunda ejecución no lo cambia.

**Acceptance Scenarios**:

1. **Given** un producto gravado sin convertir a 0.35 e IVA 15 %, **When** se ejecuta la conversión, **Then** queda en 0.40 y marcado como PVP.
2. **Given** un producto ya convertido, **When** se ejecuta otra vez, **Then** no cambia.
3. **Given** una tienda sin IVA vigente, **When** se ejecuta, **Then** sus productos no se tocan y el script lo informa.

---

### User Story 3 - Pedidos anteriores intactos (Priority: P1)

Los pedidos registrados antes del cambio conservan sus montos.

**Independent Test**: Un pedido creado con precio sin IVA (marca legacy) sigue dando subtotal 0.70, IVA 0.11, total 0.81.

---

### User Story 4 - Recibo y pedidos muestran el precio con IVA (Priority: P2)

El recibo y la pantalla de pedidos muestran precio unitario y total de línea con IVA, más subtotal, IVA y total.

### Edge Cases

- Editar el precio de un producto no convertido lo deja como PVP (el precio que se escribe es siempre PVP).
- Las listas de precio por tipo de cliente todavía no se usan en los pedidos; se tratan como PVP cuando se integren.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: El precio de producto MUST ser el PVP. Para productos gravados incluye el IVA vigente.
- **FR-002**: Cada línea MUST calcularse: total = ROUND(PVP × cantidad, 2); base = ROUND(total / (1 + IVA), 2) si es gravado, o total si no; IVA = total − base. El pedido suma bases, IVA y totales.
- **FR-003**: El indicador "Aplica IVA" del producto MUST llegar a la línea del pedido; un producto no gravado no lleva IVA.
- **FR-004**: Cada producto MUST indicar si su precio incluye IVA. Los nuevos lo incluyen; los existentes no hasta convertirse; editar el precio lo marca como PVP.
- **FR-005**: Los pedidos existentes MUST conservar sus montos; las líneas antiguas se leen con el cálculo anterior.
- **FR-006**: La conversión MUST ser idempotente, solo afecta productos sin convertir, usa el IVA vigente de cada tienda y omite tiendas sin IVA vigente.
- **FR-007**: Caja, Pedidos y Recibo MUST usar el mismo cálculo que el servidor (librería compartida).
- **FR-008**: La línea del pedido MUST guardar base, IVA y total para el proveedor de facturación.

## Success Criteria *(mandatory)*

- **SC-001**: 2 × 0.40 cobra 0.80.
- **SC-002**: Ningún producto recibe el IVA dos veces por la conversión.
- **SC-003**: Ningún pedido anterior cambia de monto.

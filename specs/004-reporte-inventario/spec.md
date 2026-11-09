# Feature Specification: Reporte de control de inventario

**Feature Branch**: `004-reporte-inventario`

**Created**: 2026-11-09

**Status**: Draft

**Input**: User description: "Verifica si podemos obtener este reporte dentro de Analítica" (plantilla de control de inventario: reponer, n.º de artículo, nombre, fabricante, descripción, costo, existencias, valor del inventario, nivel de reposición, días por reposición, fecha de última venta, cantidad de reposición, notas).

## Clarifications

### Session 2026-11-09

- Q: ¿Stock mínimo y cantidad de reposición por producto o por producto y sucursal? → A (decidido por Claude con el visto bueno del dueño, "sigue con el cambio"): por producto. El reporte los compara con el stock total o con el de la sucursal filtrada.
- Q: ¿Fabricante es un dato aparte o sirve el proveedor? → A (decidido igual): fabricante es un campo propio y el reporte muestra además el último proveedor (de Compras).
- Q: ¿Dónde se configuran los datos de reposición? → A: en la misma pantalla del reporte, fila por fila. No se usa la edición general del producto porque su PUT sobrescribe el stock.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Ver el control de inventario (Priority: P1)

En Analítica → Control de inventario se ve una fila por producto con las columnas de la plantilla, y las que hay que reponer resaltadas.

**Independent Test**: Producto con stock 20 y nivel de reposición 50 aparece primero, marcado "REPONER"; uno con stock 200 aparece "OK".

**Acceptance Scenarios**:

1. **Given** un producto con costo 10.00 y 200 unidades, **When** se abre el reporte, **Then** el valor del inventario es 2000.00.
2. **Given** un producto nunca comprado, **When** se abre el reporte, **Then** costo y valor salen vacíos ("sin costo"), no 0.
3. **Given** stock ≤ nivel de reposición, **When** se abre el reporte, **Then** la fila sale como REPONER y arriba.
4. **Given** un filtro de sucursal, **When** se aplica, **Then** existencias, valor, reponer y última venta son de esa sucursal.

### User Story 2 - Configurar la reposición (Priority: P1)

Desde la fila se editan código, fabricante, nivel de reposición, cantidad de reposición, días por reposición y notas, sin tocar precio ni stock.

**Acceptance Scenarios**:

1. **Given** un código ya usado por otro producto de la tienda, **When** se guarda, **Then** se rechaza (409).
2. **Given** un número negativo, **When** se guarda, **Then** se rechaza (400).

### User Story 3 - Exportar a CSV (Priority: P2)

El reporte se descarga en CSV: separador `;`, punto decimal (constitución v2.4.0), UTF-8 con BOM.

### Edge Cases

- Producto sin nivel de reposición: nunca sale como REPONER.
- Producto sin ventas: fecha de última venta vacía.
- Productos dados de baja (estado 5) no aparecen.

## Requirements *(mandatory)*

- **FR-001**: El producto MUST tener código (único por tienda cuando existe), fabricante, nivel de reposición, cantidad de reposición, días por reposición y notas, todos opcionales.
- **FR-002**: El reporte MUST devolver por producto: reponer, código, nombre, fabricante, descripción, costo, existencias, valor del inventario, nivel de reposición, días por reposición, fecha de última venta, cantidad de reposición, notas y último proveedor.
- **FR-003**: Valor del inventario = costo promedio × existencias, redondeado a centavos; vacío si el costo es desconocido.
- **FR-004**: Reponer = existencias ≤ nivel de reposición (solo si hay nivel).
- **FR-005**: Fecha de última venta = última salida de stock por pedido (kardex).
- **FR-006**: Último proveedor = proveedor de la última compra recibida que incluye el producto.
- **FR-007**: El reporte MUST poder filtrarse por sucursal.
- **FR-008**: Los datos de reposición MUST guardarse sin modificar precio ni stock.
- **FR-009**: El reporte MUST exportarse a CSV con `;`, punto decimal y BOM.
- **FR-010**: Todo MUST estar aislado por tenant.

## Success Criteria *(mandatory)*

- **SC-001**: Se obtiene en una pantalla de Analítica la misma información de la plantilla.
- **SC-002**: Los productos a reponer se ven primero y resaltados.

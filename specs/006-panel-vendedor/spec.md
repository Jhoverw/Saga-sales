# Spec 006: Panel "Mi día" del vendedor y caja chica

**Estado:** en implementación · **Rama:** `006-panel-vendedor` · **Base:** spec 005 (usuarios y roles)

## Por qué
El cajero cierra el día sin saber cuánto vendió, qué se está por acabar o vencer, ni cuánto efectivo
debería tener. Los gastos pequeños (mantenimiento, insumos, pagos urgentes) salen de la caja sin registro,
así que el arqueo nunca cuadra.

## Historias
- **H-1** Como cajero, veo lo que vendí hoy: número de ventas, total y los productos con cantidad e importe.
- **H-2** Como cajero, veo cuánto cobré hoy por forma de pago: efectivo, tarjeta y otros (transferencia, depósito).
- **H-3** Como cajero, veo los lotes que están por vencer y los productos que llegaron a su nivel de reorden o se agotaron.
- **H-4** Como cajero, registro un gasto de caja chica con categoría, motivo y monto.
- **H-5** Como cajero, veo en un gráfico de líneas mis ventas de los últimos 15 días.
- **H-6** Como dueño o administrador, veo el mismo panel para toda la tienda o para un vendedor, fijo el monto máximo
  por gasto de caja chica y anulo gastos mal registrados.

## Decisiones (respuestas del 2026-11-09)
| Tema | Decisión |
|------|----------|
| Alcance de las cifras | Del vendedor que inició sesión. Dueño y administrador ven toda la tienda o filtran por vendedor. |
| Control de caja chica | Monto máximo por gasto fijado por el dueño. No hay aprobación. El dueño ve la lista. |
| Caja chica y arqueo | Los gastos se descuentan: **efectivo esperado = efectivo cobrado − caja chica**. HU-04 (arqueo) usará la misma regla. |

## Requisitos
- **FR-001** "Hoy" es el día local del navegador (Ecuador, UTC−5). El cliente envía el rango en UTC y su desfase en minutos.
- **FR-002** Ventas = órdenes no canceladas (6) ni reembolsadas (7). Total de una orden = `subtotal + tax_amount` (PVP).
- **FR-003** Vendedor de una venta = `orders.created_by`. Cobrador de un pago = `payments.created_by` (nuevo; los pagos
  anteriores a esta spec no tienen cobrador y solo cuentan en la vista de toda la tienda).
- **FR-004** Formas de pago: efectivo = 5; tarjeta = 1; otros = 4 (transferencia) y 9 (depósito). Importe neto de reembolsos.
- **FR-005** Un cajero solo puede ver sus propias cifras: el servidor ignora el filtro de vendedor si el rol es cajero.
- **FR-006** Caja chica: categoría (1 mantenimiento · 2 insumos y gastos · 3 pago emergente · 9 otro), motivo obligatorio
  (3–200 caracteres), monto > 0 y ≤ máximo de la tienda. Sin configuración el máximo es 20.00.
- **FR-007** Superar el máximo responde 400 con el máximo en el mensaje. Solo dueño o administrador anulan un gasto; un gasto
  anulado no se descuenta.
- **FR-008** Alertas: lotes dentro del umbral de vencimiento de Parámetros, y productos con `stock ≤ nivel de reorden`
  o stock 0. El cajero no ve costos.
- **FR-009** Gráfico: 15 días incluyendo hoy, días sin ventas en 0, montos con punto decimal (constitución v2.4.0).

## Fuera de alcance
Apertura y cierre de turno con arqueo (HU-04), reposición del fondo de caja chica, comprobantes adjuntos.

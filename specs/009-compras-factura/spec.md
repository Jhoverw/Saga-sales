# Spec 009: Compra ingresada como la factura del proveedor, y cuentas por pagar

**Estado:** en implementación · **Rama:** `009-compras-factura` · **Base:** spec 008

## Por qué
Hoy una compra se arma producto por producto desde una lista desplegable, sin los datos del comprobante, sin
empaque, sin descuentos ni ICE, y una compra a crédito no deja deuda con el proveedor. El dueño tiene la factura
en la mano y quiere copiarla tal cual: cabecera, detalle por código y totales que cuadren con el papel.

## Decisiones (respuestas del 2026-11-09)
| Tema | Decisión |
|------|----------|
| Empaque | Cada producto guarda sus unidades por caja. Al elegir "caja" el stock entra multiplicado y el costo se divide. |
| Producto nuevo | Se crea desde la misma línea: código, nombre, PVP, IVA y unidades por caja. |
| Crédito | Cuentas por pagar: saldo por proveedor, abonos y vencimiento. |

## Requisitos
- **FR-001 Cabecera**: proveedor, tipo de comprobante (01 factura, 02 nota de venta, 03 liquidación de compra),
  establecimiento-punto-secuencial (001-002-000000123), autorización SRI (10, 37 o 49 dígitos), fecha de emisión,
  fecha de ingreso a bodega, caducidad del documento, sucursal (bodega), forma de pago SRI y condición
  contado/crédito con vencimiento.
- **FR-002** Un comprobante (proveedor + tipo + número) se registra una sola vez → 409.
- **FR-003 Detalle**: código (busca por código exacto o por nombre), producto, empaque (unidad o caja de N),
  cantidad, precio unitario sin IVA, si grava IVA, total de la línea; lote y caducidad si el producto los lleva.
- **FR-004 Totales**: subtotal 0 %, subtotal con IVA, descuento de cada grupo, ICE, IVA (tasa vigente), otros y total.
  El descuento de cada grupo se reparte entre sus líneas en proporción a su valor; el ICE y los otros cargos entre
  todas las líneas. El IVA se calcula sobre (subtotal con IVA − descuento + ICE).
- **FR-005 Costo**: el costo de cada unidad de stock es (base con descuento + su parte de ICE y otros + su IVA) ÷
  unidades recibidas; con eso sigue el costo promedio ponderado que ya existe.
- **FR-006** Al guardar con "unidades por caja" distinto al del producto, el producto queda con el nuevo valor.
- **FR-007 Exportar**: la compra se descarga como CSV con ";" (se abre en Excel).
- **FR-008 Cuentas por pagar**: una compra a crédito crea una cuenta por pagar por el total con su vencimiento. Se
  registran abonos (monto, fecha, forma, referencia) que no pueden superar el saldo; saldo 0 = pagada. La lista
  muestra saldo por proveedor y marca lo vencido y lo que vence en 7 días.

## Fuera de alcance
Cantidades decimales (0.600 kg) hasta HU-06, retenciones en la fuente, nota de crédito de proveedor, leer el XML del SRI.

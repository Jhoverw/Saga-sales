# Spec 008: Pedidos con verificación de pago, stock apartado con vencimiento y contra entrega

**Estado:** en implementación · **Rama:** `008-pedidos-verificacion` · **Base:** spec 007

## Por qué
Los pedidos (en vivos, WhatsApp, redes) son una venta más lenta que la de mostrador: se recolectan, se verifica
cada pago y solo entonces se preparan y se envían. Hoy un pedido pagado por transferencia avanza solo con
registrar el pago, nadie confirma que el dinero llegó, y el stock apartado por pedidos que nunca se pagan queda
bloqueado para siempre.

## Decisiones (respuestas del 2026-11-09)
| Tema | Decisión |
|------|----------|
| Quién verifica | El propio vendedor, porque él gestiona los envíos. |
| Stock | Se aparta al anotar el pedido, para no vender dos veces lo que no hay. |
| Vencimiento | Si en 48 horas no se verifica el pago, el pedido se anula y el stock vuelve. Configurable por tienda. |
| Contra entrega | Existe. También aparta el stock; salta la verificación y se cobra al entregar. |

## Requisitos
- **FR-001** Un pedido tiene canal: 1 mostrador (Caja) o 2 pedido. Solo el canal 2 verifica pagos y vence.
- **FR-002** Al anotar un pedido no contra entrega se fija `hold_until = ahora + horas de la tienda` (48 por defecto,
  entre 1 y 720). Cambiar la configuración no altera pedidos ya anotados.
- **FR-003** Un pago de pedido en efectivo cuenta como verificado. Con tarjeta, transferencia o depósito queda
  **Por verificar** hasta que alguien lo marque Verificado (exige al menos un comprobante) o Rechazado (con motivo).
- **FR-004** Un pago rechazado deja de contar: se descuenta de lo pagado del pedido y el pago queda fallido.
- **FR-005** Un pedido del canal 2 no contra entrega pasa a **En preparación** solo cuando lo verificado cubre el
  total: automáticamente al verificar, o a mano después. Tampoco se envía sin eso.
- **FR-006** Un pedido contra entrega no vence ni pide verificación; para marcarlo Entregado debe estar cobrado.
- **FR-007** Un proceso revisa cada 5 minutos los pedidos vencidos (canal 2, no contra entrega, Pendiente o
  Confirmado, lo verificado no cubre el total, `hold_until` pasado), los cancela, devuelve el stock y marca la
  fecha de vencimiento.
- **FR-008** El vendedor (rol cajero) avanza estados, verifica y rechaza pagos y envía. Cancelar a mano sigue
  siendo de administrador o dueño. La configuración de horas es del dueño.

## Fuera de alcance
Tablero visual por columnas y captura rápida durante el en vivo (fase de reorganización del menú), notificar al
cliente por WhatsApp, verificación automática con el banco.

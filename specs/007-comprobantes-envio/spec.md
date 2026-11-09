# Spec 007: Comprobante de pago y guía de courier en pedidos

**Estado:** en implementación · **Rama:** `007-comprobantes-envio` · **Base:** spec 006

## Por qué
Los pedidos que se despachan se cobran casi siempre por transferencia o depósito. Hoy el pago se registra
sin respaldo y el pedido puede salir sin que nadie haya visto la transferencia. Cuando sale por courier,
el número de guía queda en un papel.

## Historias
- **H-1** Como vendedor, al cobrar un pedido registro la forma de pago y, si no es efectivo, subo la foto del
  recibo, la captura del WhatsApp de la transferencia o el PDF del depósito.
- **H-2** Como administrador, no puedo marcar un pedido como Enviado si le falta el respaldo de un pago que no
  es en efectivo, o si no tiene ningún pago registrado.
- **H-3** Como administrador, al marcar Enviado elijo si va por courier o por entrega propia/retiro; si va por
  courier registro la empresa y el número de guía.
- **H-4** Como cualquier usuario, veo los comprobantes y la guía del pedido.

## Decisiones (respuestas del 2026-11-09)
| Tema | Decisión |
|------|----------|
| Alcance | Solo pedidos que se envían (paso a Enviado). Las ventas de Caja no piden comprobante. |
| Courier | Empresa + número de guía. |
| Guía obligatoria | Solo si el envío es por courier; "entrega propia o retiro" no la pide. |
| Archivos | JPG, PNG, WEBP o PDF, máximo 5 MB cada uno, varios por pago. |

## Requisitos
- **FR-001** Un comprobante pertenece a un pago de la misma tienda. Se valida el tipo por su contenido (firma del
  archivo), no solo por la extensión o el Content-Type.
- **FR-002** Los comprobantes no son públicos: se descargan con sesión iniciada de la misma tienda.
- **FR-003** Pasar a Enviado (desde En preparación) exige: al menos un pago vigente, y que cada pago vigente con
  tarjeta (1), transferencia (4) o depósito (9) tenga al menos un comprobante. Si falta algo → 409 con el motivo.
- **FR-004** Método de envío: 1 courier (empresa 2–60 caracteres y guía 3–60 obligatorias), 2 entrega propia o
  retiro (sin guía). Se guarda la fecha de envío.
- **FR-005** `sp_order_change_status` ya no acepta pasar a Enviado: ese paso solo existe en `sp_order_ship`, para
  que no se pueda saltar el registro del envío.
- **FR-006** Empresas sugeridas: Servientrega, Laar Courier, Tramaco, Urbano, Correos del Ecuador; se acepta otra.

## Fuera de alcance
Comprobantes en Caja, validación del monto contra la imagen, notificar la guía al cliente (va con HU de notificaciones).

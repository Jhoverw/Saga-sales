# Plan: comprobantes y envío

| Servicio | Cambio |
|----------|--------|
| Payment | `db/payment/V007__payment_attachments.sql`: tabla `payment_attachments`, `sp_payment_attachment_add/list/get`, `sp_order_payment_proof_status`, `sp_payment_list_by_order` con `AttachmentCount`. `IReceiptStorage` en disco (volumen `payment_receipts`, fuera de lo que se sirve en público). Endpoints `POST/GET api/payments/{id}/attachments`, `GET api/payments/attachments/{id}`, `GET api/payments/proof-status?orderId=`. |
| Sales | `db/sales/V028__order_shipment.sql`: columnas de envío, `sp_order_ship`, `sp_order_shipment_get`, `sp_order_change_status` sin el paso a Enviado. `IPaymentClient` (HTTP, con las credenciales del usuario). `PUT api/orders/{id}/status` con `Shipment` cuando el destino es Enviado; `GET api/orders/{id}/shipment`. |
| Blazor | Pedidos: subir y ver comprobantes por pago, formulario de envío (método, empresa, guía) antes de "Marcar enviado", guía visible. |
| Deploy | `Services__Payment` en sales; volumen `payment_receipts` en payment. |

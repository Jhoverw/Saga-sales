# Tasks: comprobantes y envío

## Tests primero
- [x] T001 Unit `ReceiptFileTypeTests`: firma JPG/PNG/WEBP/PDF, rechazo de otros y de más de 5 MB
- [x] T002 Integration `Payment/PaymentAttachmentTests.cs`: adjunto de otra tienda rechazado, estado de respaldo
- [x] T003 Integration `Sales/OrderShipmentTests.cs`: courier sin guía rechazado, entrega propia sin guía, change_status ya no envía
- [x] T004 Contract `Sales/OrderShipmentContractTests.cs`: transferencia sin comprobante → 409; con comprobante → enviado con guía

## Implementación
- [x] T005 Payment: V007, almacenamiento, endpoints
- [x] T006 Sales: V028, cliente de Payment, reglas al enviar
- [x] T007 Blazor: Pedidos con comprobantes y envío
- [x] T008 Deploy: variables y volumen
- [ ] T009 Verificar con `scripts/verify.ps1` y probar con un pedido real

# Tasks: pedidos con verificación

## Tests primero
- [x] T001 Integration `Sales/RemoteOrderTests.cs`: transferencia sin verificar no prepara ni envía; verificado sí; vencidos; contra entrega
- [x] T002 Integration `Payment/PaymentVerificationTests.cs`: verificar exige comprobante; rechazar; efectivo no requiere
- [x] T003 Unit: horas de apartado, reglas de verificación en el comando
- [x] T004 Contract `Sales/RemoteOrderContractTests.cs`: vendedor verifica y envía; rechazado descuenta

## Implementación
- [x] T005 Sales: V031 (canal, contra entrega, apartado, verificado, ajustes, vencidos), comandos, endpoints
- [x] T006 Sales: servicio de vencimiento con identidad de sistema
- [x] T007 Payment: V010 (estado de verificación), verificar/rechazar, aviso a Sales
- [x] T008 Blazor: Pedidos con canal, contra entrega, verificar/rechazar, vencimiento, horas
- [ ] T009 Verificar con `scripts/verify.ps1` y probar con un pedido real

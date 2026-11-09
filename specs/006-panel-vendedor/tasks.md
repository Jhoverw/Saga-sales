# Tasks: Panel "Mi día" y caja chica

## Tests primero
- [x] T001 Integration `tests/SagaSales.IntegrationTests/Payment/PettyCashTests.cs`: tope por defecto, tope configurado, anular, resumen por forma de pago descuenta caja chica
- [x] T002 Integration `tests/SagaSales.IntegrationTests/Sales/SellerSummaryTests.cs`: totales por día local, excluye canceladas, filtra por vendedor
- [x] T003 Contract `tests/SagaSales.ContractTests/MyDay/MyDayContractTests.cs`: cajero ve solo lo suyo, registra gasto, 400 sobre el tope, 403 al anular o cambiar el tope

## Implementación
- [x] T004 Sales: migración V027, repositorio, consulta y endpoint `seller-summary`
- [x] T005 Payment: migración V006, cobrador en el pago, resumen, caja chica
- [x] T006 Catalog: endpoint `inventory/alerts`
- [x] T007 Blazor: página Mi día, gráfico de 15 días, caja chica, menú
- [ ] T008 Verificar con `scripts/verify.ps1` y probar con un cajero real

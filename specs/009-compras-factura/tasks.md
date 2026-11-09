# Tasks: compra como factura

## Tests primero
- [x] T001 Unit `PurchaseInvoiceCalculatorTests`: subtotales, descuento repartido, ICE, IVA, costo por unidad con caja
- [x] T002 Integration `Catalog/PurchaseInvoiceTests.cs`: comprobante duplicado, stock en unidades, cuenta por pagar y abonos
- [x] T003 Contract `Purchasing/PurchaseInvoiceContractTests.cs`: compra a crédito con producto nuevo y abono

## Implementación
- [x] T004 Catalog V026: campos del comprobante, líneas con empaque, unidades por caja, cuentas por pagar
- [x] T005 Catalog: calculadora, búsqueda por código, producto desde la línea, endpoints
- [x] T006 Blazor: compra estilo factura, exportar CSV, página Cuentas por pagar, menú y panel
- [ ] T007 Verificar con `scripts/verify.ps1` y una factura real

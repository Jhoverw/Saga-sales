# Tasks: Reporte de control de inventario

## Tests primero

- [x] T001 [P] Unit tests `tests/SagaSales.UnitTests/Catalog/InventoryReportCsvTests.cs`: encabezado, `;`, punto decimal, celdas vacías, comillas
- [x] T002 [P] Integration tests `tests/SagaSales.IntegrationTests/Catalog/InventoryReportTests.cs`: valor, reponer, sin costo, sucursal, última venta, último proveedor, código duplicado, negativos, aislamiento por tenant
- [x] T003 [P] Contract tests `tests/SagaSales.ContractTests/Inventory/InventoryReportContractTests.cs`: reporte, settings 204/409/400, CSV

## Implementación

- [x] T004 `db/catalog/V024`, `V025`
- [x] T005 DTOs, repositorio, query, comando, validador, controller
- [x] T006 `InventoryReportCsv`
- [x] T007 Página Control de inventario en Analítica + descarga CSV
- [ ] T008 Verificar con `scripts/verify.ps1` y revisar el reporte en pantalla

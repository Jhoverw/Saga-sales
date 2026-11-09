# Tasks: Precios con IVA incluido (PVP)

## Tests primero

- [x] T001 [P] Unit tests `tests/SagaSales.UnitTests/Shared/LinePricingTests.cs`: 2 × 0.40 → 0.70/0.10/0.80; no gravado; cálculo anterior 2 × 0.35 → 0.70/0.11/0.81
- [x] T002 [P] Integration tests `tests/SagaSales.IntegrationTests/Sales/OrderPvpTests.cs`: pedido PVP, no gravado, línea anterior y pedido mixto; `sp_order_lines` devuelve base/IVA/total
- [x] T003 [P] Integration tests `tests/SagaSales.IntegrationTests/Catalog/ProductPvpFlagTests.cs`: producto nuevo es PVP; get/list devuelven las marcas; editar el precio marca PVP

## Implementación

- [x] T004 `SagaSales.Shared/Pricing/LinePricing.cs`
- [x] T005 Catalog: `V022`, `V023`, dominio, repositorio, DTO, mapping
- [x] T006 Sales: `V026`, dominio, repositorio, comando, DTO de línea; el evento OrderPlaced lleva la base sin IVA por línea (Analítica no mezcla IVA en ingresos)
- [x] T010 Contract tests de pedidos ajustados a PVP + caso 2 × 0.40 = 0.80
- [x] T007 Caja, Pedidos, Recibo y Productos
- [x] T008 `scripts/convert-prices-to-pvp.ps1`
- [ ] T009 Verificar: `verify.ps1` (build + tests), conversión con `-DryRun` y luego real, venta 2 × 0.40 = 0.80 en Caja

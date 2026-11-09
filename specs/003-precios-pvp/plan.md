# Implementation Plan: Precios con IVA incluido (PVP)

**Branch**: `003-precios-pvp` | **Date**: 2026-11-08 | **Spec**: [spec.md](./spec.md)

## Summary

El precio de producto pasa a ser PVP. Cada línea de pedido calcula total = PVP × cantidad y obtiene base e IVA hacia atrás. Una marca por producto (`price_includes_tax`) permite convertir los precios existentes una sola vez y una marca por línea conserva el cálculo anterior para los pedidos ya registrados.

## Constitution Check (v2.4.0)

| Principle | Outcome | Evidence |
|---|---|---|
| I. Library-First | PASS | `SagaSales.Shared/Pricing/LinePricing.cs` calcula la línea; la usan Caja y Pedidos y la prueban tests unitarios. El procedure replica la misma regla y la prueban tests de integración. |
| III. Test-First | PASS (planned) | T001–T003 antes de implementar. |
| IV. Domain Boundaries | PASS | Todo en `decimal`; la línea guarda un snapshot inmutable de base, IVA y total. |
| V. Multi-Tenant | PASS | Procedures con `@p_tenant_id` primero; la conversión usa el IVA de cada tienda. |
| VII. No DB sharing | PASS (justified) | Ningún servicio lee otra base. La conversión es un script de operación único que cruza `saga_sales` (IVA) y `saga_catalog` (precios); no es un servicio. |
| VIII. Stored procedures | PASS | `db/catalog/V022`, `V023`; `db/sales/V026`. |
| Number format | PASS | Punto decimal (AppCulture). |

## Design

### Catalog
- `V022__product_price_includes_tax.sql`: `products.price_includes_tax bit NOT NULL`. Las filas existentes quedan en 0; el default para nuevas es 1.
- `V023__functions_product_pvp.sql`: `sp_product_get` y `sp_product_list` devuelven `IsTaxable` y `PriceIncludesTax`; `sp_product_update` marca `price_includes_tax = 1` (el precio que se escribe es PVP).
- `ProductDto` / dominio / repositorio exponen `IsTaxable` y `PriceIncludesTax`.

### Sales
- `V026__order_pvp.sql`: `order_lines` gana `is_taxable`, `price_includes_tax` (default 0 = cálculo anterior), `line_net`, `line_tax`, `line_total`. `sp_order_create` calcula por línea según las marcas; `sp_order_lines` devuelve los campos nuevos y, para líneas antiguas, los calcula con la regla anterior.
- La línea del pedido toma `IsTaxable` y `PriceIncludesTax` del producto.

### Regla de línea (librería y SQL)
- PVP: total = ROUND(precio × cant, 2); base = gravado ? ROUND(total / (1 + IVA), 2) : total; IVA = total − base.
- Anterior: base = ROUND(precio × cant, 2); IVA = gravado ? ROUND(base × IVA, 2) : 0; total = base + IVA.

### Conversión
- `scripts/convert-prices-to-pvp.ps1` (con `-DryRun`): productos gravados con `price_includes_tax = 0` → precio × (1 + IVA vigente de su tienda), redondeado; marca 1. No gravados: solo marca. Tiendas sin IVA vigente: se omiten y se listan.

### Pantallas
- Caja y Pedidos usan `LinePricing`; Recibo y Pedidos muestran precio y total de línea con IVA desde la línea guardada; Productos rotula el precio como PVP.

## Complexity Tracking

| Item | Why | Simpler alternative rejected |
|---|---|---|
| Marca `price_includes_tax` por producto y por línea | Convertir una sola vez y conservar pedidos anteriores | Cambio de golpe: aplicaría el IVA dos veces si la conversión se repite y alteraría pedidos registrados |

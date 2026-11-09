# Implementation Plan: Reporte de control de inventario

**Branch**: `004-reporte-inventario` | **Date**: 2026-11-09 | **Spec**: [spec.md](./spec.md)

## Summary

El catálogo (dueño de stock, costo y kardex) genera el reporte con un procedure; Analítica lo muestra. Los datos de reposición se agregan al producto y se editan con un endpoint propio que no toca precio ni stock.

## Constitution Check (v2.4.0)

| Principle | Outcome | Evidence |
|---|---|---|
| I. Library-First | PASS | El CSV lo arma `InventoryReportCsv` (función pura, con tests unitarios). |
| III. Test-First | PASS (planned) | T001–T003 antes de implementar. |
| V. Multi-Tenant | PASS | Procedures con `@p_tenant_id` primero; código único por tenant. |
| VII. No DB sharing | PASS | El reporte vive en el catálogo; Analítica (pantalla) llama a su API. No se copia stock a la base de Analítica. |
| VIII. Stored procedures | PASS | `db/catalog/V024`, `V025`. |
| Reports | PASS | CSV con `;`, punto decimal, BOM. |

## Design

- `V024__product_inventory_settings.sql`: `products.code`, `manufacturer`, `reorder_level`, `reorder_quantity`, `lead_time_days`, `inventory_notes`; índice único filtrado `(tenant_id, code)`.
- `V025__functions_inventory_report.sql`: `sp_product_set_inventory_settings`, `sp_inventory_report(@branch_id NULL)`.
- API (catálogo, ruta ya enrutada por el gateway): `GET api/inventory/report`, `GET api/inventory/report.csv`, `PUT api/inventory/products/{id}/settings`.
- Blazor: página `/analytics/inventory` (Control de inventario) con filtro de sucursal, filas REPONER resaltadas, edición por fila y descarga CSV; acceso desde Analítica.

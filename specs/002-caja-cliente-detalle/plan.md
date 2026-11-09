# Implementation Plan: Caja con selección de cliente y detalle en grilla

**Branch**: `002-caja-cliente-detalle` | **Date**: 2026-11-08 | **Spec**: [spec.md](./spec.md)

## Summary

Fase 1: el cliente gana tipo y número de identificación (códigos SRI) y razón social, y el email pasa a ser opcional. Caja permite buscar y registrar clientes y muestra el detalle en una grilla. Fase 2: cantidades decimales en todo el flujo de stock y pedidos.

## Technical Context

**Language/Version**: C# 14 on .NET 10 · **Storage**: SQL Server 2022, stored procedures + Dapper · **Frontend**: Blazor WebAssembly · **Testing**: xUnit (unit, integration contra SQL Server real, contract contra el gateway).

## Constitution Check (v2.3.0)

| Principle | Outcome | Evidence |
|---|---|---|
| I. Library-First | PASS | La validación de cédula/RUC vive en `SagaSales.Shared/Validation/EcuadorIdentification.cs`, usada por el validador del servidor y por la pantalla. |
| II. CLI Interface | N/A (debt) | El proyecto no tiene CLI todavía (T104 de 001). No se agrega en esta feature. |
| III. Test-First | PASS (planned) | Tareas T001–T004 escriben tests antes del código. Unit tests de la librería; integration tests de los procedures; contract test del endpoint. |
| IV. Domain Boundaries | PASS | No cambia precios ni pagos; la cantidad decimal (fase 2) usa `decimal`, nunca `float`. |
| V. Multi-Tenant | PASS | Unicidad de identificación y email por `tenant_id`; todos los procedures nuevos reciben `@p_tenant_id` primero (lo verifica `ConstitutionComplianceTests`). |
| VI. Observability | N/A | No agrega dependencias externas. |
| VII. Simplicity | PASS | Sin servicios nuevos; se extiende Customer. |
| VIII. Stored procedures | PASS | Cambios en `db/customer/V003__customer_identification.sql` y `V004__functions_identification.sql`. |
| IX. Integration-First | PASS (planned) | Los procedures se prueban contra SQL Server real (`saga_customer_test`). |

## Design

### Database (`saga_customer`)

- `V003__customer_identification.sql`: columnas `identification_type smallint NULL`, `identification_number nvarchar(20) NULL`, `business_name nvarchar(300) NULL`; email pasa a `NULL`; índice único de email filtrado (`WHERE email IS NOT NULL`); índice único `(tenant_id, identification_type, identification_number)` filtrado; el check de nombres acepta razón social en lugar de nombre y apellido; backfill del consumidor final existente (`consumidor.final@caja.local`) a 07/9999999999999.
- `V004__functions_identification.sql`: `sp_customer_create`, `sp_customer_update`, `sp_customer_get`, `sp_customer_list` devuelven los campos nuevos; `create` valida unicidad de identificación (CONFLICT) y acepta email nulo; `list` busca también por identificación y razón social.

### Shared

- `Validation/EcuadorIdentification.cs`: `IsValidCedula`, `IsValidRuc`, `Validate(type, number)`.
- `Enums/IdentificationType`: 4 RUC, 5 Cédula, 6 Pasaporte, 7 ConsumidorFinal, 8 Exterior.
- `CustomerDto` / `CreateCustomerRequest` / `UpdateCustomerRequest`: `IdentificationType?`, `IdentificationNumber?`, `BusinessName?`, `DisplayName`; `Email` nulable.

### Customer service

- Comando, validador (FluentValidation con la librería), repositorio y controller aceptan los campos nuevos.

### BlazorWeb (Caja)

- Selector de cliente con búsqueda (≥3 caracteres) y botón "Nuevo" que abre un formulario de alta rápida.
- Consumidor final por defecto y al terminar cada venta.
- Grilla: Producto, Descripción, Cantidad, Precio unit. c/IVA, Total c/IVA, Stock, acciones.

### Fase 2 (cantidades decimales) — impacto medido

Hoy la cantidad es `int` en 6 columnas (`products.stock_quantity`, `product_stock.quantity`, `stock_batches.quantity`, dos tablas de compras y `order_lines.quantity`), en parámetros de procedures de 12 scripts de catálogo y analítica, y en 19 propiedades C# (Catalog, Sales, Shared). Se planifica aparte después de validar la fase 1.

## Complexity Tracking

| Violation | Why needed | Simpler alternative rejected because |
|---|---|---|
| Email deja de ser obligatorio | Alta rápida en Caja (decisión del dueño, 2026-11-08) | Exigir email frena la venta de mostrador |

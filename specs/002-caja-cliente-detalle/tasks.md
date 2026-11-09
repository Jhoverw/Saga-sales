# Tasks: Caja con selección de cliente y detalle en grilla

**Input**: [spec.md](./spec.md), [plan.md](./plan.md)

## Fase 1 — Tests primero (deben fallar antes de implementar)

- [x] T001 [P] [US3] Unit tests `tests/SagaSales.UnitTests/Shared/EcuadorIdentificationTests.cs`: cédula válida/inválida, provincia, tercer dígito, RUC natural y sociedad, pasaporte, consumidor final
- [x] T002 [P] [US2][US3] Integration tests `tests/SagaSales.IntegrationTests/Customers/CustomerIdentificationTests.cs`: alta con identificación, duplicado por tenant → CONFLICT, misma identificación en otro tenant permitida, dos clientes sin email, búsqueda por identificación y razón social
- [x] T003 [P] [US3] Contract test `tests/SagaSales.ContractTests/Customers/CustomerIdentificationContractTests.cs`: POST con cédula inválida → 400, alta válida → 201 con los campos nuevos, duplicado → 409

## Fase 1 — Implementación

- [x] T004 [US3] `src/SagaSales.Shared/Validation/EcuadorIdentification.cs` + `Enums/IdentificationType`
- [x] T005 [US3] `db/customer/V003__customer_identification.sql`
- [x] T006 [US2][US3] `db/customer/V004__functions_identification.sql`
- [x] T007 [US2][US3] DTOs, comando, validador, repositorio y controller de Customer
- [x] T008 [US1][US2][US3] Caja: selector de cliente, alta rápida, consumidor final por defecto
- [x] T009 [US4] Caja: grilla de detalle
- [x] T011 Clientes, Pedidos y Recibo muestran la razón social; Clientes conserva identificación y razón social al editar
- [ ] T010 Aplicar migraciones en `_test`, correr `scripts/test.ps1` y verificar a mano en Caja — **pendiente**: el código no se compiló ni se probó en el entorno donde se escribió (sin .NET ni SQL Server)

## Fase 2 — Cantidades decimales (US5)

- [ ] T020 Spec de impacto detallada y migraciones de catálogo/ventas a `decimal(18,3)`
- [ ] T021 Procedures de stock, lotes, compras y pedidos con cantidad decimal + tests de integración
- [ ] T022 DTOs, repositorios y eventos con `decimal`
- [ ] T023 Caja y pantallas de stock/compras con cantidades decimales

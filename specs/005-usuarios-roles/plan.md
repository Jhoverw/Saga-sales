# Implementation Plan: Usuarios y roles

**Branch**: `005-usuarios-roles` | **Date**: 2026-11-09 | **Spec**: [spec.md](./spec.md)

## Constitution Check (v2.4.0)

| Principle | Outcome | Evidence |
|---|---|---|
| III. Test-First | PASS (planned) | Integración de procedures de usuarios y contrato de permisos antes del código. |
| V. Multi-Tenant | PASS | Procedures con `@p_tenant_id`; el dueño solo ve usuarios de su tienda. La unicidad global de email se valida sin exponer datos de otra tienda. |
| VIII. Stored procedures | PASS | `db/identity/V004__functions_users.sql`. |

## Design

- Roles como constantes en `SagaSales.Shared/Constants/SagaSalesRoles.cs`: `Owner`, `Manager`, `Cashier`, `ManagerOrOwner`, `Staff`.
- Identity: `V004` con `sp_user_create` (email único global entre activos, rol válido), `sp_user_list`, `sp_user_update` (protege al último dueño), `sp_user_set_password`. `UsersController` en `api/users`, solo dueño. Ruta nueva en el gateway.
- Permisos: `[Authorize(Roles = ...)]` por acción según la matriz; se cierran los endpoints anónimos.
- Blazor: página Usuarios (dueño), menú y botones filtrados por rol, páginas con `[Authorize(Roles)]`.

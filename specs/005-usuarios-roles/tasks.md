# Tasks: Usuarios y roles

## Tests primero
- [x] T001 Integration `tests/SagaSales.IntegrationTests/Identity/UserManagementTests.cs`: crear, email repetido en otra tienda, lista por tienda, último dueño protegido, contraseña, desactivado no inicia sesión
- [x] T002 Contract `tests/SagaSales.ContractTests/Auth/RolePermissionsContractTests.cs`: dueño crea cajero; cajero vende (201) y recibe 403 en reembolso, precio, analítica y usuarios; sin token → 401

## Implementación
- [x] T003 `SagaSalesRoles` y `db/identity/V004__functions_users.sql`
- [x] T004 Identity: DTOs, repositorio, comandos, validadores, `UsersController`, ruta en el gateway
- [x] T005 `[Authorize(Roles)]` en todos los servicios según la matriz; cerrar endpoints anónimos
- [x] T006 Blazor: página Usuarios, menú y acciones por rol
- [ ] T007 Verificar con `scripts/verify.ps1` y probar con un cajero real

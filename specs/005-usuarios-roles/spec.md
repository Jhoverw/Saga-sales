# Feature Specification: Usuarios y roles

**Feature Branch**: `005-usuarios-roles`

**Created**: 2026-11-09

**Status**: Draft

**Input**: Backlog MVP, HU-01: "Como dueño quiero crear cajeros con su usuario y rol, para que cada uno venda con su nombre y no pueda cambiar precios ni reembolsar." Contexto del cliente: hasta 2 cajas, turnos rotativos.

## Clarifications

### Session 2026-11-09

- Roles (ya existen en la base): `admin` = Dueño, `manager` = Administrador, `sales` = Cajero. `customer` no se usa en el panel.
- El login es solo por email (sin tienda), así que un email activo MUST ser único entre todas las tiendas; si no, el login falla con conflicto para ambos.
- El dueño restablece contraseñas (parte de HU-13); el cambio de contraseña propia queda para HU-13.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - El dueño gestiona usuarios (Priority: P1)

El dueño crea cajeros y administradores, cambia su rol o nombre, los desactiva y restablece su contraseña.

**Acceptance Scenarios**:

1. **Given** el dueño, **When** crea "María" como cajera, **Then** María puede iniciar sesión y vender.
2. **Given** un email ya usado por un usuario activo de cualquier tienda, **When** se crea otro con ese email, **Then** se rechaza (409).
3. **Given** el único dueño activo, **When** intenta desactivarse o cambiarse de rol, **Then** se rechaza: la tienda nunca queda sin dueño.
4. **Given** un usuario desactivado, **When** intenta iniciar sesión, **Then** no puede (401).
5. **Given** un administrador o cajero, **When** abre Usuarios, **Then** no tiene acceso (403).

---

### User Story 2 - El cajero solo hace lo suyo (Priority: P1)

El cajero vende, cobra y registra clientes; no cambia precios, no reembolsa, no cancela ventas, no ve costos ni analítica, no configura.

**Acceptance Scenarios**:

1. **Given** un cajero, **When** vende y cobra en Caja, **Then** funciona y la venta queda con su nombre.
2. **Given** un cajero, **When** intenta reembolsar, cambiar un precio, cancelar un pedido o ver analítica, **Then** el servidor responde 403 y la pantalla no muestra esa opción.

---

### User Story 3 - Nada sin iniciar sesión (Priority: P1)

Reservar o liberar stock, buscar productos por código, preventas, facturación y reporte de IVA hoy responden sin token; MUST exigir sesión.

## Matriz de permisos

| Área | Cajero (`sales`) | Administrador (`manager`) | Dueño (`admin`) |
|---|---|---|---|
| Caja: crear pedido, cobrar, registrar pago del pedido | ✓ | ✓ | ✓ |
| Clientes: crear, buscar, editar, agregar dirección | ✓ | ✓ | ✓ |
| Clientes: desactivar | — | ✓ | ✓ |
| Ver productos, categorías, sucursales, IVA vigente | ✓ | ✓ | ✓ |
| Reservar/liberar stock (lo llama Ventas con el token del usuario) | ✓ | ✓ | ✓ |
| Productos: crear, editar, precios, foto, baja, kardex | — | ✓ | ✓ |
| Categorías, sucursales, parámetros, IVA: crear/editar | — | ✓ | ✓ |
| Inventario, proveedores, compras | — | ✓ | ✓ |
| Pedidos: cambiar estado, cancelar | — | ✓ | ✓ |
| Pagos: reembolsar | — | ✓ | ✓ |
| Analítica, reportes, notificaciones, facturación | — | ✓ | ✓ |
| Usuarios | — | — | ✓ |

## Requirements *(mandatory)*

- **FR-001**: El dueño MUST poder listar, crear, editar (nombre, rol), desactivar/reactivar usuarios de su tienda y restablecer su contraseña.
- **FR-002**: Un email activo MUST ser único entre todas las tiendas.
- **FR-003**: La tienda MUST tener siempre al menos un dueño activo.
- **FR-004**: Un usuario desactivado MUST NOT iniciar sesión.
- **FR-005**: Cada endpoint MUST aplicar la matriz de permisos (T111).
- **FR-006**: Ningún endpoint de negocio MUST responder sin sesión.
- **FR-007**: Las pantallas MUST ocultar lo que el rol no puede hacer.
- **FR-008**: Contraseña mínima de 8 caracteres.

## Limitación conocida

Desactivar a un usuario impide su próximo inicio de sesión; un token ya emitido sigue válido hasta que vence. Si hace falta corte inmediato, se agrega una lista de revocación en una spec posterior.

## Success Criteria *(mandatory)*

- **SC-001**: El dueño da de alta un cajero en menos de un minuto.
- **SC-002**: Un cajero no puede ejecutar ninguna acción de la columna "—" ni siquiera llamando a la API directamente.

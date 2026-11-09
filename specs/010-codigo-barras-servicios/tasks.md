# Tasks: código de barras y servicios

## Tests primero
- [ ] T001 Unit: validación EAN-13/EAN-8/UPC-A (dígito verificador) y orden de coincidencias del buscador
- [ ] T002 Integration Catalog: código único por tienda, búsqueda por código/nombre/caja, servicio no mueve stock, cambio de tipo con stock → 409
- [ ] T003 Integration Sales: pedido con bien + servicio aparta solo el bien; anulación devuelve solo el bien
- [ ] T004 Contract: escanear código de caja en compra, asociar código desconocido, venta por código en Caja, venta de servicio sin stock

## Implementación
- [ ] T005 Catalog V027: `product_barcodes`, `products.kind`, `sp_product_search`, procs de códigos y tipo; reserva/venta ignoran servicios
- [ ] T006 Catalog: endpoints search, barcodes, kind; `sp_purchase_product_find` usa el buscador nuevo
- [ ] T007 Sales: validación de stock y retención de pedidos saltan servicios
- [ ] T008 Blazor: componente `ProductPicker` compartido (escribir/escanear, ↑↓ Enter Esc)
- [ ] T009 Blazor Compras: usar `ProductPicker`; código desconocido → asociar o crear
- [ ] T010 Blazor Caja y Pedidos: usar `ProductPicker`; código exacto agrega directo; aviso de código no registrado
- [ ] T011 Blazor Catálogo: sección Códigos de barras, Tipo Bien/Servicio, filtro y etiqueta
- [ ] T012 Reportes, alertas y panel excluyen servicios
- [ ] T013 Verificar con `scripts/verify.ps1`, un lector USB real y una factura escaneada

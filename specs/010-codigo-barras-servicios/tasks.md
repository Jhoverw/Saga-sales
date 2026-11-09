# Tasks: código de barras y servicios

## P0 — Código de barras de compras a ventas (HU-10.1 a HU-10.4)
- [x] T001 Unit `BarcodeRulesTests`: EAN-13/EAN-8/UPC-A/GTIN-14, advertencia del dígito verificador, códigos propios
- [x] T002 Integration `Catalog/ProductBarcodeSearchTests`: unidad y caja, código interno, palabras sin tildes, código único por tienda, compras sin servicios, principal al quitar
- [x] T004 Contract `Purchasing/ProductBarcodeContractTests`: código capturado en compras encontrado por ventas, caja ×48, 409 y 400
- [x] T005 Catalog V027: `product_barcodes`, `products.kind`, `sp_product_search`, alta/baja/lista de códigos; `sp_purchase_product_find` usa el buscador
- [x] T006 Catalog: `GET api/products/search`, `GET/POST/DELETE api/products/{id}/barcodes`; producto desde la compra guarda su código; se quita el stub `ProductLookup`
- [x] T008 Blazor: componente `ProductPicker` compartido (escribir/escanear, ↑↓ Enter Esc, código exacto entra solo)
- [x] T009 Blazor Compras: `ProductPicker`; código desconocido → asociar (unidad o caja) o crear con el código
- [x] T010 Blazor Caja y Pedidos: `ProductPicker`; el código de la caja suma sus unidades; aviso de código no registrado
- [x] T011a Blazor Catálogo: código de barras al crear y panel "Códigos" (escanear, unidad/caja, quitar)

## P1 — Servicios (HU-10.5)
- [ ] T003 Integration Sales: pedido con bien + servicio aparta solo el bien; anulación devuelve solo el bien
- [ ] T007 Catalog y Sales: reserva, venta, retención de pedidos y kardex saltan `kind = 2`; `PUT api/products/{id}/kind` con 409 si tiene stock
- [ ] T011b Blazor Catálogo: Tipo Bien/Servicio, filtro y etiqueta
- [ ] T012 Reportes, alertas y panel excluyen servicios

## Cierre
- [ ] T013 Verificar con `scripts/verify.ps1`, un lector USB real y una factura escaneada

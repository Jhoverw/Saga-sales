# Plan: Panel "Mi día"

Cada servicio responde por sus datos; la página Blazor junta tres llamadas. No se agregan rutas al gateway.

| Servicio | Cambio |
|----------|--------|
| Sales | `db/sales/V027__functions_seller_summary.sql`: `sp_order_seller_daily`, `sp_order_seller_products`, `sp_order_sellers`. `GET api/orders/seller-summary` (Staff). |
| Payment | `db/payment/V006__petty_cash_and_cashier.sql`: `payments.created_by`, `sp_payment_create_manual` lo guarda, `sp_payment_summary_by_method`, tablas y procedures de caja chica. `GET api/payments/summary`, `GET/POST api/payments/petty-cash`, `POST api/payments/petty-cash/{id}/void`, `GET/PUT api/payments/petty-cash/settings`. |
| Catalog | `GET api/inventory/alerts` (Staff): lotes por vencer + productos bajo reorden, sin costos. |
| Blazor | `Pages/MyDay.razor` (`/mi-dia`), gráfico SVG sin librerías, formulario de caja chica, enlace primero en el menú. |

Rango de fechas: el cliente calcula medianoche local → UTC y manda `offsetMinutes` para agrupar por día local en SQL.

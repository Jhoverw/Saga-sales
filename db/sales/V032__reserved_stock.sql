-- V032__reserved_stock.sql
-- Sales: stock apartado por pedidos (spec 008). Al anotar un pedido el Catálogo descuenta la unidad de lo
-- disponible; mientras el pedido no sale (Pendiente, Confirmado, En preparación) esa unidad sigue físicamente
-- en la bodega. Estos procedures dicen cuántas y en qué pedidos, para que el reporte de inventario muestre
-- disponible + apartado = en bodega. Las ventas de mostrador (canal 1) se entregan en el acto y no cuentan.

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

CREATE OR ALTER PROCEDURE dbo.sp_order_reserved_stock
    @p_tenant_id uniqueidentifier
AS
BEGIN
    SET NOCOUNT ON;

    SELECT l.product_id         AS ProductId,
           MAX(l.product_name)  AS ProductName,
           SUM(l.quantity)      AS Quantity,
           COUNT(DISTINCT o.id) AS Orders
    FROM dbo.orders o
    JOIN dbo.order_lines l ON l.order_id = o.id
    WHERE o.tenant_id = @p_tenant_id AND o.channel = 2 AND o.status IN (1, 2, 3)
    GROUP BY l.product_id;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_order_reserved_stock_detail
    @p_tenant_id  uniqueidentifier,
    @product_id   uniqueidentifier
AS
BEGIN
    SET NOCOUNT ON;

    SELECT o.id AS OrderId, o.order_number AS OrderNumber, o.customer_id AS CustomerId, o.status AS Status,
           SUM(l.quantity) AS Quantity, o.hold_until AS HoldUntil, o.cash_on_delivery AS CashOnDelivery,
           o.order_date AS OrderDate,
           o.verified_amount AS VerifiedAmount, o.subtotal + ISNULL(o.tax_amount, 0) AS Total
    FROM dbo.orders o
    JOIN dbo.order_lines l ON l.order_id = o.id
    WHERE o.tenant_id = @p_tenant_id AND o.channel = 2 AND o.status IN (1, 2, 3) AND l.product_id = @product_id
    GROUP BY o.id, o.order_number, o.customer_id, o.status, o.hold_until, o.cash_on_delivery, o.order_date,
             o.verified_amount, o.subtotal, o.tax_amount
    ORDER BY o.order_date;
END
GO

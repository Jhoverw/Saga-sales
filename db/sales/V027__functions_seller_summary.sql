-- V027__functions_seller_summary.sql
-- Sales: panel "Mi día" (spec 006). Ventas por día local, productos vendidos y vendedores de un rango.
-- Una venta cuenta si no está cancelada (6) ni reembolsada (7). Total = subtotal + IVA (PVP, spec 003).
-- El cliente manda el rango en UTC y su desfase en minutos; los días se agrupan en hora local.

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'ix_orders_tenant_seller_date' AND object_id = OBJECT_ID('dbo.orders'))
    CREATE INDEX ix_orders_tenant_seller_date ON dbo.orders (tenant_id, created_by, order_date);
GO

CREATE OR ALTER PROCEDURE dbo.sp_order_seller_daily
    @p_tenant_id      uniqueidentifier,
    @from_utc         datetime2(3),
    @to_utc           datetime2(3),
    @offset_minutes   int = 0,
    @seller_id        uniqueidentifier = NULL
AS
BEGIN
    SET NOCOUNT ON;

    IF @to_utc <= @from_utc
    BEGIN
        ;THROW 50000, 'VALIDATION_ERROR: the end of the range must be after the start', 1;
    END

    IF @offset_minutes NOT BETWEEN -840 AND 840
    BEGIN
        ;THROW 50000, 'VALIDATION_ERROR: the time zone offset is out of range', 1;
    END

    SELECT CAST(DATEADD(MINUTE, @offset_minutes, o.order_date) AS date) AS Day,
           COUNT(*)                                                    AS Orders,
           SUM(o.subtotal + ISNULL(o.tax_amount, 0))                   AS Total
    FROM dbo.orders o
    WHERE o.tenant_id = @p_tenant_id
      AND o.order_date >= @from_utc AND o.order_date < @to_utc
      AND o.status NOT IN (6, 7)
      AND (@seller_id IS NULL OR o.created_by = @seller_id)
    GROUP BY CAST(DATEADD(MINUTE, @offset_minutes, o.order_date) AS date)
    ORDER BY Day;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_order_seller_products
    @p_tenant_id uniqueidentifier,
    @from_utc    datetime2(3),
    @to_utc      datetime2(3),
    @seller_id   uniqueidentifier = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SELECT l.product_id                    AS ProductId,
           MAX(l.product_name)             AS ProductName,
           SUM(l.quantity)                 AS Quantity,
           -- Líneas anteriores a V026 no guardan el total: se usa precio × cantidad.
           SUM(ISNULL(l.line_total, ROUND(l.unit_price * l.quantity, 2))) AS Total
    FROM dbo.orders o
    JOIN dbo.order_lines l ON l.order_id = o.id
    WHERE o.tenant_id = @p_tenant_id
      AND o.order_date >= @from_utc AND o.order_date < @to_utc
      AND o.status NOT IN (6, 7)
      AND (@seller_id IS NULL OR o.created_by = @seller_id)
    GROUP BY l.product_id
    ORDER BY Total DESC, ProductName;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_order_sellers
    @p_tenant_id uniqueidentifier,
    @from_utc    datetime2(3)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT o.created_by                                        AS Id,
           ISNULL(MAX(o.created_by_name), N'(sin nombre)')     AS Name
    FROM dbo.orders o
    WHERE o.tenant_id = @p_tenant_id AND o.order_date >= @from_utc
    GROUP BY o.created_by
    ORDER BY Name;
END
GO

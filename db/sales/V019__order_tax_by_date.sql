-- V019__order_tax_by_date.sql
-- Sales: V-03 · the VAT in force on the order date is applied and stored on every order.
--
-- Rule: a tax rate applies to a date when start_date <= date <= end_date (end_date NULL = open).
-- When several rates match, the most recent start_date wins. No matching rate for the tenant is a
-- VALIDATION_ERROR: the order is refused instead of being stored with a silent 0 % tax.
-- Rates already stored on orders are never recalculated (they carry their own tax_amount).

-- order_lines.tax_rate was reserved as decimal(5,2): that cannot hold rates such as 0.0825.
IF COL_LENGTH('dbo.order_lines', 'tax_rate') IS NOT NULL
   AND EXISTS (SELECT 1 FROM sys.columns
               WHERE object_id = OBJECT_ID('dbo.order_lines') AND name = 'tax_rate'
                 AND scale = 2)
BEGIN
    ALTER TABLE dbo.order_lines ALTER COLUMN tax_rate decimal(5,4) NULL;
END
GO

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

-- ── sp_tax_rate_for_date ─────────────────────────────────────────────────────
CREATE OR ALTER PROCEDURE dbo.sp_tax_rate_for_date
    @p_tenant_id uniqueidentifier,
    @rate_date   date
AS
BEGIN
    SET NOCOUNT ON;

    SELECT TOP (1)
           id                 AS Id,
           rate_percentage    AS RatePercentage,
           start_date         AS StartDate,
           end_date           AS EndDate,
           description        AS Description
    FROM dbo.tax_rates
    WHERE tenant_id = @p_tenant_id
      AND start_date <= @rate_date
      AND (end_date IS NULL OR end_date >= @rate_date)
    ORDER BY start_date DESC;
END
GO

-- ── sp_order_create (V002 + tax) ─────────────────────────────────────────────
CREATE OR ALTER PROCEDURE dbo.sp_order_create
    @p_tenant_id      uniqueidentifier,
    @customer_id      uniqueidentifier,
    @shipping_address nvarchar(500),
    @created_by       uniqueidentifier,
    @lines_json       nvarchar(max),
    @notes            nvarchar(max) = NULL,
    @order_id         uniqueidentifier = NULL OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @shipping_address IS NULL OR LEN(LTRIM(RTRIM(@shipping_address))) = 0
    BEGIN
        ;THROW 50000, 'VALIDATION_ERROR: the shipping address is required', 1;
    END

    IF @lines_json IS NULL OR ISJSON(@lines_json) <> 1
    BEGIN
        ;THROW 50000, 'VALIDATION_ERROR: the order needs at least one valid line', 1;
    END

    IF NOT EXISTS (SELECT 1 FROM OPENJSON(@lines_json))
    BEGIN
        ;THROW 50000, 'VALIDATION_ERROR: the order needs at least one line', 1;
    END

    IF EXISTS (SELECT 1 FROM OPENJSON(@lines_json)
               WHERE CAST(JSON_VALUE(value, '$.quantity') AS int) <= 0)
    BEGIN
        ;THROW 50000, 'VALIDATION_ERROR: every line needs a positive quantity', 1;
    END

    DECLARE @order_date date = CAST(SYSUTCDATETIME() AS date);

    -- V-03: the VAT in force on the order date.
    DECLARE @tax_rate decimal(5,4) =
    (
        SELECT TOP (1) rate_percentage
        FROM dbo.tax_rates
        WHERE tenant_id = @p_tenant_id
          AND start_date <= @order_date
          AND (end_date IS NULL OR end_date >= @order_date)
        ORDER BY start_date DESC
    );

    IF @tax_rate IS NULL
    BEGIN
        ;THROW 50000, 'VALIDATION_ERROR: no VAT rate is in force for the order date; configure it in Configuración Fiscal', 1;
    END

    DECLARE @subtotal decimal(18,2) =
    (
        SELECT SUM(CAST(JSON_VALUE(value, '$.unitPrice') AS decimal(18,2))
                   * CAST(JSON_VALUE(value, '$.quantity') AS int))
        FROM OPENJSON(@lines_json)
    );

    DECLARE @tax_amount decimal(18,2) = ROUND(ISNULL(@subtotal, 0) * @tax_rate, 2);

    SET @order_id = NEWID();

    BEGIN TRAN;

    INSERT INTO dbo.orders (id, tenant_id, customer_id, status, subtotal, paid_amount,
                            shipping_address, notes, order_date, created_by, tax_amount)
    VALUES (@order_id, @p_tenant_id, @customer_id, 1, ISNULL(@subtotal, 0), 0,
            @shipping_address, @notes, SYSUTCDATETIME(), @created_by, @tax_amount);

    INSERT INTO dbo.order_lines (id, tenant_id, order_id, product_id, product_name,
                                 unit_price, quantity, price_source, stock_at_purchase, tax_rate)
    SELECT NEWID(),
           @p_tenant_id,
           @order_id,
           CAST(JSON_VALUE(value, '$.productId') AS uniqueidentifier),
           CAST(JSON_VALUE(value, '$.productName') AS nvarchar(255)),
           CAST(JSON_VALUE(value, '$.unitPrice') AS decimal(18,2)),
           CAST(JSON_VALUE(value, '$.quantity') AS int),
           ISNULL(CAST(JSON_VALUE(value, '$.priceSource') AS smallint), 1),
           CAST(JSON_VALUE(value, '$.stockAtPurchase') AS int),
           @tax_rate
    FROM OPENJSON(@lines_json);

    COMMIT;

    SELECT id               AS Id,
           tenant_id        AS TenantId,
           customer_id      AS CustomerId,
           status           AS Status,
           subtotal         AS Subtotal,
           tax_amount       AS TaxAmount,
           paid_amount      AS PaidAmount,
           shipping_address AS ShippingAddress,
           notes            AS Notes,
           order_date       AS OrderDate,
           paid_date        AS PaidDate,
           created_by       AS CreatedBy
    FROM dbo.orders
    WHERE id = @order_id;
END
GO

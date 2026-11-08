-- V021__order_number.sql
-- Sales: every order gets a human-readable number, PED-<store code>-<sequence>, e.g. PED-3F9A1C-000123.
-- Unique per store by constraint (never by application code). Assigned inside sp_order_create's
-- transaction, so an order never exists without its number and a number never exists without its order.
-- Cancelled orders keep their number; the sequence only moves forward.
-- The store code is the first six hex digits of the tenant id. A collision between two stores is
-- refused by the unique constraint on order_number_sequences.store_code (loud, not silent).

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

IF COL_LENGTH('dbo.orders', 'order_number') IS NULL
BEGIN
    ALTER TABLE dbo.orders ADD order_number nvarchar(20) NULL;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'ux_orders_tenant_number' AND object_id = OBJECT_ID('dbo.orders'))
BEGIN
    CREATE UNIQUE INDEX ux_orders_tenant_number ON dbo.orders (tenant_id, order_number)
        WHERE order_number IS NOT NULL;
END
GO

IF OBJECT_ID('dbo.order_number_sequences', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.order_number_sequences (
        tenant_id   uniqueidentifier NOT NULL PRIMARY KEY,
        store_code  nvarchar(6)      NOT NULL,
        next_value  int              NOT NULL CONSTRAINT df_order_number_next DEFAULT (1),
        CONSTRAINT uq_order_number_store_code UNIQUE (store_code)
    );
END
GO

-- ── sp_order_create (V019 + order number) ────────────────────────────────────
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

    -- Order number: one sequence per store, moved forward inside this transaction.
    IF NOT EXISTS (SELECT 1 FROM dbo.order_number_sequences WITH (UPDLOCK, HOLDLOCK) WHERE tenant_id = @p_tenant_id)
    BEGIN
        INSERT INTO dbo.order_number_sequences (tenant_id, store_code, next_value)
        VALUES (@p_tenant_id,
                UPPER(LEFT(REPLACE(CAST(@p_tenant_id AS varchar(36)), '-', ''), 6)),
                1);
    END

    DECLARE @store_code nvarchar(6);
    DECLARE @sequence int;

    UPDATE s
    SET @store_code = s.store_code,
        @sequence = s.next_value,
        s.next_value = s.next_value + 1
    FROM dbo.order_number_sequences AS s
    WHERE s.tenant_id = @p_tenant_id;

    DECLARE @order_number nvarchar(20) =
        N'PED-' + @store_code + N'-' + RIGHT(N'000000' + CAST(@sequence AS nvarchar(10)), 6);

    INSERT INTO dbo.orders (id, tenant_id, customer_id, status, subtotal, paid_amount,
                            shipping_address, notes, order_date, created_by, tax_amount, order_number)
    VALUES (@order_id, @p_tenant_id, @customer_id, 1, ISNULL(@subtotal, 0), 0,
            @shipping_address, @notes, SYSUTCDATETIME(), @created_by, @tax_amount, @order_number);

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
           created_by       AS CreatedBy,
           order_number     AS OrderNumber
    FROM dbo.orders
    WHERE id = @order_id;
END
GO

-- ── reads expose the order number ────────────────────────────────────────────
CREATE OR ALTER PROCEDURE dbo.sp_order_get
    @p_tenant_id uniqueidentifier,
    @order_id    uniqueidentifier
AS
BEGIN
    SET NOCOUNT ON;

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
           created_by       AS CreatedBy,
           order_number     AS OrderNumber
    FROM dbo.orders
    WHERE id = @order_id AND tenant_id = @p_tenant_id;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_order_list
    @p_tenant_id uniqueidentifier,
    @page_number int = 1,
    @page_size   int = 20,
    @status      smallint = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SET @page_number = CASE WHEN ISNULL(@page_number, 1) < 1 THEN 1 ELSE @page_number END;
    SET @page_size   = CASE WHEN ISNULL(@page_size, 20) < 1 THEN 20
                            WHEN @page_size > 100 THEN 100
                            ELSE @page_size END;

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
           created_by       AS CreatedBy,
           order_number     AS OrderNumber,
           COUNT(*) OVER () AS TotalCount
    FROM dbo.orders
    WHERE tenant_id = @p_tenant_id
      AND (@status IS NULL OR status = @status)
    ORDER BY order_date DESC
    OFFSET (@page_number - 1) * @page_size ROWS
    FETCH NEXT @page_size ROWS ONLY;
END
GO

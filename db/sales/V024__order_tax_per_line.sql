-- V024__order_tax_per_line.sql
-- Sales: the counter screen (Caja) shows prices with VAT included, line by line. For the cashier's
-- screen and the server to agree to the cent, VAT is rounded PER LINE and the order's tax is the sum of
-- those rounded amounts. V019–V022 rounded once over the subtotal, which can differ by a cent.
-- Everything else is unchanged from V022.

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

CREATE OR ALTER PROCEDURE dbo.sp_order_create
    @p_tenant_id       uniqueidentifier,
    @customer_id       uniqueidentifier,
    @shipping_address  nvarchar(500),
    @created_by        uniqueidentifier,
    @lines_json        nvarchar(max),
    @notes             nvarchar(max) = NULL,
    @order_id          uniqueidentifier = NULL OUTPUT,
    @created_by_name   nvarchar(200) = NULL
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

    -- V024: VAT per line, rounded to cents, then summed (matches the counter screen line by line).
    DECLARE @tax_amount decimal(18,2) =
    (
        SELECT SUM(ROUND(CAST(JSON_VALUE(value, '$.unitPrice') AS decimal(18,2))
                         * CAST(JSON_VALUE(value, '$.quantity') AS int)
                         * @tax_rate, 2))
        FROM OPENJSON(@lines_json)
    );

    SET @order_id = NEWID();

    BEGIN TRAN;

    -- Order number: one sequence per store, moved forward inside this transaction (V021).
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
                            shipping_address, notes, order_date, created_by, tax_amount,
                            order_number, created_by_name)
    VALUES (@order_id, @p_tenant_id, @customer_id, 1, ISNULL(@subtotal, 0), 0,
            @shipping_address, @notes, SYSUTCDATETIME(), @created_by, ISNULL(@tax_amount, 0),
            @order_number, @created_by_name);

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
           order_number     AS OrderNumber,
           created_by_name  AS CreatedByName
    FROM dbo.orders
    WHERE id = @order_id;
END
GO

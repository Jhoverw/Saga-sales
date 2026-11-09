-- V026__order_pvp.sql
-- Sales (spec 003): prices with VAT included (PVP). Every order line now stores its base, VAT and total,
-- and knows whether its product carries VAT and whether its price already included it. Orders written
-- before V026 keep their amounts: their lines default to "taxable, price without VAT" and
-- sp_order_lines recomputes them with the previous rule. Everything else in sp_order_create is V025.

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

IF COL_LENGTH('dbo.order_lines', 'is_taxable') IS NULL
    ALTER TABLE dbo.order_lines ADD is_taxable bit NOT NULL CONSTRAINT df_order_lines_is_taxable DEFAULT 1;
GO

IF COL_LENGTH('dbo.order_lines', 'price_includes_tax') IS NULL
    ALTER TABLE dbo.order_lines ADD price_includes_tax bit NOT NULL CONSTRAINT df_order_lines_price_includes_tax DEFAULT 0;
GO

IF COL_LENGTH('dbo.order_lines', 'line_net') IS NULL
    ALTER TABLE dbo.order_lines ADD line_net decimal(18,2) NULL;
GO

IF COL_LENGTH('dbo.order_lines', 'line_tax') IS NULL
    ALTER TABLE dbo.order_lines ADD line_tax decimal(18,2) NULL;
GO

IF COL_LENGTH('dbo.order_lines', 'line_total') IS NULL
    ALTER TABLE dbo.order_lines ADD line_total decimal(18,2) NULL;
GO

-- ── sp_order_create ─────────────────────────────────────────────────────────
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

    -- V026 (spec 003): every line is priced by its own rule (same rule as SagaSales.Shared LinePricing).
    --   PVP (price includes VAT): total = ROUND(price x qty, 2); base = ROUND(total / (1 + VAT), 2) when taxable.
    --   Previous rule: base = ROUND(price x qty, 2); VAT = ROUND(base x VAT, 2) when taxable.
    -- A line that does not send the flags keeps the previous rule and is taxable, exactly as before V026.
    DECLARE @lines TABLE
    (
        product_id         uniqueidentifier,
        product_name       nvarchar(255),
        unit_price         decimal(18,2),
        quantity           int,
        price_source       smallint,
        stock_at_purchase  int,
        is_taxable         bit,
        price_includes_tax bit,
        line_net           decimal(18,2) NULL,
        line_tax           decimal(18,2) NULL,
        line_total         decimal(18,2) NULL
    );

    INSERT INTO @lines (product_id, product_name, unit_price, quantity, price_source, stock_at_purchase,
                        is_taxable, price_includes_tax)
    SELECT CAST(JSON_VALUE(value, '$.productId') AS uniqueidentifier),
           CAST(JSON_VALUE(value, '$.productName') AS nvarchar(255)),
           CAST(JSON_VALUE(value, '$.unitPrice') AS decimal(18,2)),
           CAST(JSON_VALUE(value, '$.quantity') AS int),
           ISNULL(CAST(JSON_VALUE(value, '$.priceSource') AS smallint), 1),
           CAST(JSON_VALUE(value, '$.stockAtPurchase') AS int),
           ISNULL(CAST(JSON_VALUE(value, '$.isTaxable') AS bit), 1),
           ISNULL(CAST(JSON_VALUE(value, '$.priceIncludesTax') AS bit), 0)
    FROM OPENJSON(@lines_json);

    -- PVP lines: total first, then the base is taken back out of it.
    UPDATE @lines
    SET line_total = ROUND(unit_price * quantity, 2)
    WHERE price_includes_tax = 1;

    UPDATE @lines
    SET line_net = CASE WHEN is_taxable = 1 THEN ROUND(line_total / (1 + @tax_rate), 2) ELSE line_total END
    WHERE price_includes_tax = 1;

    UPDATE @lines
    SET line_tax = line_total - line_net
    WHERE price_includes_tax = 1;

    -- Previous rule: base first, then the VAT on top.
    UPDATE @lines
    SET line_net = ROUND(unit_price * quantity, 2)
    WHERE price_includes_tax = 0;

    UPDATE @lines
    SET line_tax = CASE WHEN is_taxable = 1 THEN ROUND(line_net * @tax_rate, 2) ELSE 0 END
    WHERE price_includes_tax = 0;

    UPDATE @lines
    SET line_total = line_net + line_tax
    WHERE price_includes_tax = 0;

    DECLARE @subtotal decimal(18,2) = (SELECT SUM(line_net) FROM @lines);
    DECLARE @tax_amount decimal(18,2) = (SELECT SUM(line_tax) FROM @lines);

    SET @order_id = NEWID();

    BEGIN TRAN;

    -- Order number: one sequence per store (V021). Advanced with a row lock only (V025).
    DECLARE @store_code nvarchar(6) = UPPER(LEFT(REPLACE(CAST(@p_tenant_id AS varchar(36)), '-', ''), 6));
    DECLARE @sequence int;

    UPDATE dbo.order_number_sequences
    SET @sequence = next_value,
        next_value = next_value + 1
    WHERE tenant_id = @p_tenant_id;

    IF @@ROWCOUNT = 0
    BEGIN
        -- First sale of this store: its sequence starts at 1; the next value is 2.
        INSERT INTO dbo.order_number_sequences (tenant_id, store_code, next_value)
        VALUES (@p_tenant_id, @store_code, 2);

        SET @sequence = 1;
    END

    DECLARE @order_number nvarchar(20) =
        N'PED-' + @store_code + N'-' + RIGHT(N'000000' + CAST(@sequence AS nvarchar(10)), 6);

    INSERT INTO dbo.orders (id, tenant_id, customer_id, status, subtotal, paid_amount,
                            shipping_address, notes, order_date, created_by, tax_amount,
                            order_number, created_by_name)
    VALUES (@order_id, @p_tenant_id, @customer_id, 1, ISNULL(@subtotal, 0), 0,
            @shipping_address, @notes, SYSUTCDATETIME(), @created_by, ISNULL(@tax_amount, 0),
            @order_number, @created_by_name);

    INSERT INTO dbo.order_lines (id, tenant_id, order_id, product_id, product_name,
                                 unit_price, quantity, price_source, stock_at_purchase, tax_rate,
                                 is_taxable, price_includes_tax, line_net, line_tax, line_total)
    SELECT NEWID(), @p_tenant_id, @order_id, product_id, product_name,
           unit_price, quantity, price_source, stock_at_purchase,
           CASE WHEN is_taxable = 1 THEN @tax_rate ELSE 0 END,
           is_taxable, price_includes_tax, line_net, line_tax, line_total
    FROM @lines;

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

-- ── sp_order_lines ──────────────────────────────────────────────────────────
-- Lines stored before V026 have no amounts: they are recomputed with the previous rule.
CREATE OR ALTER PROCEDURE dbo.sp_order_lines
    @p_tenant_id uniqueidentifier,
    @order_id    uniqueidentifier
AS
BEGIN
    SET NOCOUNT ON;

    SELECT l.product_id         AS ProductId,
           l.product_name       AS ProductName,
           l.quantity           AS Quantity,
           l.unit_price         AS UnitPrice,
           l.price_source       AS PriceSource,
           l.is_taxable         AS IsTaxable,
           l.price_includes_tax AS PriceIncludesTax,
           a.line_net           AS LineNet,
           a.line_tax           AS LineTax,
           ISNULL(l.line_total, a.line_net + a.line_tax) AS LineTotal,
           CASE WHEN l.price_includes_tax = 1 THEN l.unit_price
                WHEN l.is_taxable = 1 THEN l.unit_price + ROUND(l.unit_price * ISNULL(l.tax_rate, 0), 2)
                ELSE l.unit_price
           END                  AS UnitPriceWithTax
    FROM dbo.order_lines l
    CROSS APPLY
    (
        SELECT ISNULL(l.line_net, ROUND(l.unit_price * l.quantity, 2)) AS line_net,
               ISNULL(l.line_tax,
                      CASE WHEN l.is_taxable = 1
                           THEN ROUND(l.unit_price * l.quantity * ISNULL(l.tax_rate, 0), 2)
                           ELSE 0 END) AS line_tax
    ) a
    WHERE l.order_id = @order_id AND l.tenant_id = @p_tenant_id
    ORDER BY l.product_name;
END
GO

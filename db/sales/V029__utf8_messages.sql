-- V029__utf8_messages.sql
-- Sales: los mensajes con tildes ("Configuración Fiscal", "número de guía") llegaban como "??" porque apply-db
-- enviaba los scripts a sqlcmd en ASCII. apply-db ya los envía en UTF-8 (-f 65001); aquí se recrean, sin
-- cambios de lógica, los procedures cuyos mensajes llevan tildes, con literales N'' (nvarchar).

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
        ;THROW 50000, N'VALIDATION_ERROR: the shipping address is required', 1;
    END

    IF @lines_json IS NULL OR ISJSON(@lines_json) <> 1
    BEGIN
        ;THROW 50000, N'VALIDATION_ERROR: the order needs at least one valid line', 1;
    END

    IF NOT EXISTS (SELECT 1 FROM OPENJSON(@lines_json))
    BEGIN
        ;THROW 50000, N'VALIDATION_ERROR: the order needs at least one line', 1;
    END

    IF EXISTS (SELECT 1 FROM OPENJSON(@lines_json)
               WHERE CAST(JSON_VALUE(value, '$.quantity') AS int) <= 0)
    BEGIN
        ;THROW 50000, N'VALIDATION_ERROR: every line needs a positive quantity', 1;
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
        ;THROW 50000, N'VALIDATION_ERROR: no VAT rate is in force for the order date; configure it in Configuración Fiscal', 1;
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

CREATE OR ALTER PROCEDURE dbo.sp_order_change_status
    @p_tenant_id uniqueidentifier,
    @order_id    uniqueidentifier,
    @new_status  smallint
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @current smallint;

    SELECT @current = status
    FROM dbo.orders WITH (UPDLOCK, ROWLOCK)
    WHERE id = @order_id AND tenant_id = @p_tenant_id;

    IF @current IS NULL
    BEGIN
        ;THROW 50000, N'NOT_FOUND: order does not exist for this tenant', 1;
    END

    IF NOT EXISTS
    (
        SELECT 1
        FROM (VALUES (1, 2), (1, 6),
                     (2, 3), (2, 6),
                     (3, 4), (3, 6),
                     (4, 5),
                     (5, 7)) AS allowed (from_status, to_status)
        WHERE allowed.from_status = @current AND allowed.to_status = @new_status
    )
    BEGIN
        ;THROW 50000, N'INVALID_TRANSITION: that status change is not allowed', 1;
    END

    -- Spec 007: enviar exige registrar cómo sale el pedido; solo sp_order_ship hace ese paso.
    IF @new_status = 4
    BEGIN
        ;THROW 50000, N'VALIDATION_ERROR: para marcar el pedido como enviado registra el método de envío', 1;
    END

    UPDATE dbo.orders
    SET status    = @new_status,
        paid_date = CASE WHEN @new_status = 3 AND paid_date IS NULL THEN SYSUTCDATETIME() ELSE paid_date END
    WHERE id = @order_id AND tenant_id = @p_tenant_id;

    SELECT id               AS Id,
           tenant_id        AS TenantId,
           customer_id      AS CustomerId,
           status           AS Status,
           subtotal         AS Subtotal,
           paid_amount      AS PaidAmount,
           shipping_address AS ShippingAddress,
           notes            AS Notes,
           order_date       AS OrderDate,
           paid_date        AS PaidDate,
           created_by       AS CreatedBy
    FROM dbo.orders
    WHERE id = @order_id AND tenant_id = @p_tenant_id;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_order_ship
    @p_tenant_id       uniqueidentifier,
    @order_id          uniqueidentifier,
    @shipping_method   smallint,
    @courier_company   nvarchar(60) = NULL,
    @tracking_number   nvarchar(60) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    SET @courier_company = NULLIF(LTRIM(RTRIM(@courier_company)), N'');
    SET @tracking_number = NULLIF(LTRIM(RTRIM(@tracking_number)), N'');

    -- 1 courier · 2 entrega propia o retiro
    IF @shipping_method IS NULL OR @shipping_method NOT IN (1, 2)
    BEGIN
        ;THROW 50000, N'VALIDATION_ERROR: elige courier o entrega propia', 1;
    END

    IF @shipping_method = 1 AND (@courier_company IS NULL OR LEN(@courier_company) < 2)
    BEGIN
        ;THROW 50000, N'VALIDATION_ERROR: indica la empresa de courier', 1;
    END

    IF @shipping_method = 1 AND (@tracking_number IS NULL OR LEN(@tracking_number) < 3)
    BEGIN
        ;THROW 50000, N'VALIDATION_ERROR: indica el número de guía del courier', 1;
    END

    BEGIN TRAN;

    DECLARE @current smallint;

    SELECT @current = status
    FROM dbo.orders WITH (UPDLOCK, ROWLOCK)
    WHERE id = @order_id AND tenant_id = @p_tenant_id;

    IF @current IS NULL
    BEGIN
        ROLLBACK;
        ;THROW 50000, N'NOT_FOUND: order does not exist for this tenant', 1;
    END

    -- Solo desde En preparación (3), igual que la máquina de estados.
    IF @current <> 3
    BEGIN
        ROLLBACK;
        ;THROW 50000, N'INVALID_TRANSITION: that status change is not allowed', 1;
    END

    UPDATE dbo.orders
    SET status          = 4,
        shipping_method = @shipping_method,
        courier_company = CASE WHEN @shipping_method = 1 THEN @courier_company END,
        tracking_number = CASE WHEN @shipping_method = 1 THEN @tracking_number END,
        shipped_at      = SYSUTCDATETIME()
    WHERE id = @order_id AND tenant_id = @p_tenant_id;

    COMMIT;

    SELECT id               AS Id,
           tenant_id        AS TenantId,
           customer_id      AS CustomerId,
           status           AS Status,
           subtotal         AS Subtotal,
           paid_amount      AS PaidAmount,
           shipping_address AS ShippingAddress,
           notes            AS Notes,
           order_date       AS OrderDate,
           paid_date        AS PaidDate,
           created_by       AS CreatedBy
    FROM dbo.orders
    WHERE id = @order_id AND tenant_id = @p_tenant_id;
END
GO

-- ============================================================================
-- V002__functions.sql — Sales: orders, the state machine and payment registration
--
-- The lines travel as a JSON array, so the header and its lines are written in ONE
-- transaction without needing a table-valued parameter.
-- ============================================================================

-- ── sp_order_create ─────────────────────────────────────────────────────────
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

    DECLARE @subtotal decimal(18,2) =
    (
        SELECT SUM(CAST(JSON_VALUE(value, '$.unitPrice') AS decimal(18,2))
                   * CAST(JSON_VALUE(value, '$.quantity') AS int))
        FROM OPENJSON(@lines_json)
    );

    SET @order_id = NEWID();

    BEGIN TRAN;

    INSERT INTO dbo.orders (id, tenant_id, customer_id, status, subtotal, paid_amount,
                            shipping_address, notes, order_date, created_by)
    VALUES (@order_id, @p_tenant_id, @customer_id, 1, ISNULL(@subtotal, 0), 0,
            @shipping_address, @notes, SYSUTCDATETIME(), @created_by);

    INSERT INTO dbo.order_lines (id, tenant_id, order_id, product_id, product_name,
                                 unit_price, quantity, price_source, stock_at_purchase)
    SELECT NEWID(),
           @p_tenant_id,
           @order_id,
           CAST(JSON_VALUE(value, '$.productId') AS uniqueidentifier),
           CAST(JSON_VALUE(value, '$.productName') AS nvarchar(255)),
           CAST(JSON_VALUE(value, '$.unitPrice') AS decimal(18,2)),
           CAST(JSON_VALUE(value, '$.quantity') AS int),
           ISNULL(CAST(JSON_VALUE(value, '$.priceSource') AS smallint), 1),
           CAST(JSON_VALUE(value, '$.stockAtPurchase') AS int)
    FROM OPENJSON(@lines_json);

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
    WHERE id = @order_id;
END
GO

-- ── sp_order_get ────────────────────────────────────────────────────────────
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

-- ── sp_order_lines ──────────────────────────────────────────────────────────
CREATE OR ALTER PROCEDURE dbo.sp_order_lines
    @p_tenant_id uniqueidentifier,
    @order_id    uniqueidentifier
AS
BEGIN
    SET NOCOUNT ON;

    SELECT product_id        AS ProductId,
           product_name      AS ProductName,
           quantity          AS Quantity,
           unit_price        AS UnitPrice,
           price_source      AS PriceSource
    FROM dbo.order_lines
    WHERE order_id = @order_id AND tenant_id = @p_tenant_id
    ORDER BY product_name;
END
GO

-- ── sp_order_list ───────────────────────────────────────────────────────────
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
           paid_amount      AS PaidAmount,
           shipping_address AS ShippingAddress,
           notes            AS Notes,
           order_date       AS OrderDate,
           paid_date        AS PaidDate,
           created_by       AS CreatedBy,
           COUNT(*) OVER () AS TotalCount
    FROM dbo.orders
    WHERE tenant_id = @p_tenant_id
      AND (@status IS NULL OR status = @status)
    ORDER BY order_date DESC
    OFFSET (@page_number - 1) * @page_size ROWS
    FETCH NEXT @page_size ROWS ONLY;
END
GO

-- ── sp_order_change_status ──────────────────────────────────────────────────
-- The state machine lives here, so no caller can skip a step (FR-013).
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
        ;THROW 50000, 'NOT_FOUND: order does not exist for this tenant', 1;
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
        ;THROW 50000, 'INVALID_TRANSITION: that status change is not allowed', 1;
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

-- ── sp_order_register_payment ───────────────────────────────────────────────
-- Adds money received. A pending order is confirmed automatically once it is fully paid.
CREATE OR ALTER PROCEDURE dbo.sp_order_register_payment
    @p_tenant_id uniqueidentifier,
    @order_id    uniqueidentifier,
    @amount      decimal(18,2)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @amount <= 0
    BEGIN
        ;THROW 50000, 'VALIDATION_ERROR: the amount must be greater than zero', 1;
    END

    IF NOT EXISTS (SELECT 1 FROM dbo.orders WHERE id = @order_id AND tenant_id = @p_tenant_id)
    BEGIN
        ;THROW 50000, 'NOT_FOUND: order does not exist for this tenant', 1;
    END

    UPDATE dbo.orders
    SET paid_amount = paid_amount + @amount,
        status      = CASE WHEN status = 1 AND paid_amount + @amount >= subtotal THEN 3 ELSE status END,
        paid_date   = CASE WHEN status = 1 AND paid_amount + @amount >= subtotal AND paid_date IS NULL
                           THEN SYSUTCDATETIME() ELSE paid_date END
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

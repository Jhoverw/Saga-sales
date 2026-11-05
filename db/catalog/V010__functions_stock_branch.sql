-- ============================================================================
-- V010__functions_stock_branch.sql — Catalog: stock per branch, FEFO and lots
--
-- This is the version that makes product_stock the truth:
--   * products.stock_quantity is kept as the SUM of the branches (the catalogue keeps working),
--   * reserving stock takes a branch (defaults to the store's default branch),
--   * a product with tracks_batches consumes lots FEFO (first expired, first out),
--   * an expired lot is never sold,
--   * receiving a purchase writes the branch, the lot and the kardex in one transaction.
-- ============================================================================

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

-- ── purchases and lines learn about branch and lot ──────────────────────────
IF COL_LENGTH('dbo.purchases', 'branch_id') IS NULL
    ALTER TABLE dbo.purchases ADD branch_id uniqueidentifier NULL;
GO

IF COL_LENGTH('dbo.purchase_lines', 'lot_code') IS NULL
    ALTER TABLE dbo.purchase_lines ADD lot_code nvarchar(60) NULL;
GO

IF COL_LENGTH('dbo.purchase_lines', 'expires_at') IS NULL
    ALTER TABLE dbo.purchase_lines ADD expires_at date NULL;
GO

-- Existing purchases belong to the default branch of their store.
UPDATE p
SET branch_id = b.id
FROM dbo.purchases AS p
INNER JOIN dbo.branches AS b ON b.tenant_id = p.tenant_id AND b.is_default = 1
WHERE p.branch_id IS NULL;
GO

-- ── helper: the branch to use (given, or the store's default) ───────────────
-- Kept as a scalar function so the three procedures agree on the fallback.
CREATE OR ALTER FUNCTION dbo.fn_default_branch (@p_tenant_id uniqueidentifier)
RETURNS uniqueidentifier
AS
BEGIN
    RETURN (SELECT TOP 1 id
            FROM dbo.branches
            WHERE tenant_id = @p_tenant_id AND status = 1
            ORDER BY is_default DESC, name);
END
GO

-- ── sp_stock_reserve: per branch, FEFO, no expired lots ─────────────────────
CREATE OR ALTER PROCEDURE dbo.sp_stock_reserve
    @p_tenant_id uniqueidentifier,
    @product_id  uniqueidentifier,
    @quantity    int,
    @branch_id   uniqueidentifier = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @quantity <= 0
    BEGIN
        ;THROW 50000, 'VALIDATION_ERROR: the quantity must be greater than zero', 1;
    END

    BEGIN TRAN;

    DECLARE @tracks_batches bit, @status smallint;

    SELECT @tracks_batches = tracks_batches, @status = status
    FROM dbo.products WITH (UPDLOCK, ROWLOCK)
    WHERE id = @product_id AND tenant_id = @p_tenant_id;

    IF @status IS NULL
    BEGIN
        ROLLBACK;
        ;THROW 50000, 'NOT_FOUND: product does not exist for this tenant', 1;
    END

    IF @status <> 2
    BEGIN
        ROLLBACK;
        ;THROW 50000, 'CONFLICT: the product is not available for sale', 1;
    END

    SET @branch_id = ISNULL(@branch_id, dbo.fn_default_branch(@p_tenant_id));

    IF @branch_id IS NULL OR NOT EXISTS (
        SELECT 1 FROM dbo.branches WHERE id = @branch_id AND tenant_id = @p_tenant_id AND status = 1)
    BEGIN
        ROLLBACK;
        ;THROW 50000, 'NOT_FOUND: branch does not exist for this tenant', 1;
    END

    DECLARE @in_branch int =
        (SELECT ISNULL(quantity, 0) FROM dbo.product_stock
         WHERE product_id = @product_id AND branch_id = @branch_id);

    IF @in_branch < @quantity
    BEGIN
        ROLLBACK;
        ;THROW 50000, 'INSUFFICIENT_STOCK: not enough units in that branch', 1;
    END

    IF @tracks_batches = 0
    BEGIN
        UPDATE dbo.product_stock
        SET quantity = quantity - @quantity, updated_at = SYSUTCDATETIME()
        WHERE product_id = @product_id AND branch_id = @branch_id;

        INSERT INTO dbo.stock_movements (tenant_id, product_id, kind, quantity, unit_cost,
                                         stock_after, reference_type, reference_id, created_by,
                                         branch_id, batch_id)
        VALUES (@p_tenant_id, @product_id, 2, -@quantity, NULL,
                @in_branch - @quantity, N'order', NULL, NULL, @branch_id, NULL);
    END
    ELSE
    BEGIN
        -- Only lots that are still valid count towards what can be sold.
        DECLARE @available int =
            (SELECT ISNULL(SUM(quantity), 0) FROM dbo.stock_batches
             WHERE tenant_id = @p_tenant_id AND branch_id = @branch_id AND product_id = @product_id
               AND quantity > 0
               AND (expires_at IS NULL OR expires_at >= CAST(SYSUTCDATETIME() AS date)));

        IF @available < @quantity
        BEGIN
            ROLLBACK;

            DECLARE @expired int =
                (SELECT ISNULL(SUM(quantity), 0) FROM dbo.stock_batches
                 WHERE tenant_id = @p_tenant_id AND branch_id = @branch_id AND product_id = @product_id
                   AND quantity > 0 AND expires_at < CAST(SYSUTCDATETIME() AS date));

            IF @expired > 0
            BEGIN
                ;THROW 50000, 'INSUFFICIENT_STOCK: the only remaining units are expired', 1;
            END

            ;THROW 50000, 'INSUFFICIENT_STOCK: not enough units in that branch', 1;
        END

        DECLARE @remaining int = @quantity;
        DECLARE @batch_id uniqueidentifier, @batch_qty int, @batch_after int;
        DECLARE @consumed int = 0;

        -- FEFO: earliest expiry first, lots without expiry last.
        DECLARE fefo CURSOR LOCAL FAST_FORWARD FOR
            SELECT id, quantity
            FROM dbo.stock_batches WITH (UPDLOCK, ROWLOCK)
            WHERE tenant_id = @p_tenant_id AND branch_id = @branch_id AND product_id = @product_id
              AND quantity > 0
              AND (expires_at IS NULL OR expires_at >= CAST(SYSUTCDATETIME() AS date))
            ORDER BY CASE WHEN expires_at IS NULL THEN 1 ELSE 0 END, expires_at, received_at;

        OPEN fefo;
        FETCH NEXT FROM fefo INTO @batch_id, @batch_qty;

        WHILE @@FETCH_STATUS = 0 AND @remaining > 0
        BEGIN
            SET @consumed = CASE WHEN @batch_qty <= @remaining THEN @batch_qty ELSE @remaining END;
            SET @batch_after = @batch_qty - @consumed;

            UPDATE dbo.stock_batches
            SET quantity = @batch_after
            WHERE id = @batch_id;

            INSERT INTO dbo.stock_movements (tenant_id, product_id, kind, quantity, unit_cost,
                                             stock_after, reference_type, reference_id, created_by,
                                             branch_id, batch_id)
            VALUES (@p_tenant_id, @product_id, 2, -@consumed, NULL,
                    @batch_after, N'order', NULL, NULL, @branch_id, @batch_id);

            SET @remaining = @remaining - @consumed;
            FETCH NEXT FROM fefo INTO @batch_id, @batch_qty;
        END

        CLOSE fefo;
        DEALLOCATE fefo;

        UPDATE dbo.product_stock
        SET quantity = quantity - @quantity, updated_at = SYSUTCDATETIME()
        WHERE product_id = @product_id AND branch_id = @branch_id;
    END

    -- products.stock_quantity stays as the store-wide total, so the catalogue and the
    -- reports that already read it keep being correct.
    UPDATE dbo.products
    SET stock_quantity = (SELECT ISNULL(SUM(quantity), 0) FROM dbo.product_stock WHERE product_id = @product_id),
        updated_at = SYSUTCDATETIME()
    WHERE id = @product_id AND tenant_id = @p_tenant_id;

    COMMIT;

    SELECT id AS Id, name AS Name, base_price AS Price, currency AS Currency,
           stock_quantity AS StockQuantity, cost AS Cost
    FROM dbo.products
    WHERE id = @product_id AND tenant_id = @p_tenant_id;
END
GO

-- ── sp_stock_release: compensation, back into the branch ────────────────────
CREATE OR ALTER PROCEDURE dbo.sp_stock_release
    @p_tenant_id uniqueidentifier,
    @product_id  uniqueidentifier,
    @quantity    int,
    @branch_id   uniqueidentifier = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @quantity <= 0
    BEGIN
        ;THROW 50000, 'VALIDATION_ERROR: the quantity must be greater than zero', 1;
    END

    SET @branch_id = ISNULL(@branch_id, dbo.fn_default_branch(@p_tenant_id));

    BEGIN TRAN;

    IF NOT EXISTS (SELECT 1 FROM dbo.product_stock WHERE product_id = @product_id AND branch_id = @branch_id)
    BEGIN
        INSERT INTO dbo.product_stock (product_id, branch_id, tenant_id, quantity)
        VALUES (@product_id, @branch_id, @p_tenant_id, 0);
    END

    DECLARE @after int;

    UPDATE dbo.product_stock
    SET quantity = quantity + @quantity, updated_at = SYSUTCDATETIME()
    WHERE product_id = @product_id AND branch_id = @branch_id;

    SET @after = (SELECT quantity FROM dbo.product_stock WHERE product_id = @product_id AND branch_id = @branch_id);

    INSERT INTO dbo.stock_movements (tenant_id, product_id, kind, quantity, unit_cost,
                                     stock_after, reference_type, reference_id, created_by,
                                     branch_id, batch_id)
    VALUES (@p_tenant_id, @product_id, 4, @quantity, NULL, @after, N'order', NULL, NULL, @branch_id, NULL);

    UPDATE dbo.products
    SET stock_quantity = (SELECT ISNULL(SUM(quantity), 0) FROM dbo.product_stock WHERE product_id = @product_id),
        updated_at = SYSUTCDATETIME()
    WHERE id = @product_id AND tenant_id = @p_tenant_id;

    COMMIT;
END
GO

-- ── sp_purchase_receive: branch, lot, weighted cost, kardex ─────────────────
CREATE OR ALTER PROCEDURE dbo.sp_purchase_receive
    @p_tenant_id uniqueidentifier,
    @purchase_id uniqueidentifier,
    @received_by uniqueidentifier = NULL,
    @branch_id   uniqueidentifier = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRAN;

    DECLARE @status smallint, @purchase_branch uniqueidentifier;

    SELECT @status = status, @purchase_branch = branch_id
    FROM dbo.purchases WITH (UPDLOCK, ROWLOCK)
    WHERE id = @purchase_id AND tenant_id = @p_tenant_id;

    IF @status IS NULL
    BEGIN
        ROLLBACK;
        ;THROW 50000, 'NOT_FOUND: purchase does not exist for this tenant', 1;
    END

    IF @status = 2
    BEGIN
        ROLLBACK;
        ;THROW 50000, 'INVALID_TRANSITION: this purchase was already received', 1;
    END

    IF @status = 3
    BEGIN
        ROLLBACK;
        ;THROW 50000, 'INVALID_TRANSITION: this purchase was cancelled', 1;
    END

    SET @branch_id = ISNULL(@branch_id, ISNULL(@purchase_branch, dbo.fn_default_branch(@p_tenant_id)));

    IF @branch_id IS NULL OR NOT EXISTS (
        SELECT 1 FROM dbo.branches WHERE id = @branch_id AND tenant_id = @p_tenant_id)
    BEGIN
        ROLLBACK;
        ;THROW 50000, 'NOT_FOUND: branch does not exist for this tenant', 1;
    END

    DECLARE @product_id uniqueidentifier, @quantity int, @unit_cost decimal(18,4);
    DECLARE @lot_code nvarchar(60), @expires_at date;

    DECLARE purchase_cursor CURSOR LOCAL FAST_FORWARD FOR
        SELECT product_id, quantity, effective_unit_cost, lot_code, expires_at
        FROM dbo.purchase_lines
        WHERE purchase_id = @purchase_id AND tenant_id = @p_tenant_id;

    OPEN purchase_cursor;
    FETCH NEXT FROM purchase_cursor INTO @product_id, @quantity, @unit_cost, @lot_code, @expires_at;

    WHILE @@FETCH_STATUS = 0
    BEGIN
        DECLARE @tracks bit, @current_cost decimal(18,4), @stock_before int, @stock_after int;

        SELECT @tracks = tracks_batches, @current_cost = cost,
               @stock_before = ISNULL((SELECT quantity FROM dbo.product_stock
                                       WHERE product_id = @product_id AND branch_id = @branch_id), 0)
        FROM dbo.products WITH (UPDLOCK, ROWLOCK)
        WHERE id = @product_id AND tenant_id = @p_tenant_id;

        IF @tracks IS NULL
        BEGIN
            CLOSE purchase_cursor;
            DEALLOCATE purchase_cursor;
            ROLLBACK;
            ;THROW 50000, 'NOT_FOUND: product does not exist for this tenant', 1;
        END

        IF @tracks = 1 AND (@lot_code IS NULL OR LEN(LTRIM(RTRIM(@lot_code))) = 0)
        BEGIN
            CLOSE purchase_cursor;
            DEALLOCATE purchase_cursor;
            ROLLBACK;
            ;THROW 50000, 'VALIDATION_ERROR: this product requires a lot code', 1;
        END

        SET @stock_after = @stock_before + @quantity;

        IF NOT EXISTS (SELECT 1 FROM dbo.product_stock WHERE product_id = @product_id AND branch_id = @branch_id)
            INSERT INTO dbo.product_stock (product_id, branch_id, tenant_id, quantity)
            VALUES (@product_id, @branch_id, @p_tenant_id, @quantity);
        ELSE
            UPDATE dbo.product_stock
            SET quantity = quantity + @quantity, updated_at = SYSUTCDATETIME()
            WHERE product_id = @product_id AND branch_id = @branch_id;

        IF @tracks = 1
        BEGIN
            IF EXISTS (SELECT 1 FROM dbo.stock_batches
                       WHERE tenant_id = @p_tenant_id AND branch_id = @branch_id
                         AND product_id = @product_id AND lot_code = @lot_code)
                UPDATE dbo.stock_batches
                SET quantity = quantity + @quantity,
                    unit_cost = @unit_cost,
                    expires_at = ISNULL(@expires_at, expires_at)
                WHERE tenant_id = @p_tenant_id AND branch_id = @branch_id
                  AND product_id = @product_id AND lot_code = @lot_code;
            ELSE
                INSERT INTO dbo.stock_batches (tenant_id, branch_id, product_id, lot_code,
                                               expires_at, quantity, unit_cost)
                VALUES (@p_tenant_id, @branch_id, @product_id, @lot_code,
                        @expires_at, @quantity, @unit_cost);
        END

        -- Weighted average: unknown cost or empty stock simply takes the new one.
        DECLARE @new_cost decimal(18,4) =
            CASE WHEN @current_cost IS NULL OR @stock_before <= 0
                 THEN @unit_cost
                 ELSE CAST(ROUND(((@stock_before * @current_cost) + (@quantity * @unit_cost))
                                 / (@stock_before + @quantity), 4) AS decimal(18,4))
            END;

        UPDATE dbo.products
        SET cost = @new_cost,
            stock_quantity = (SELECT ISNULL(SUM(quantity), 0) FROM dbo.product_stock WHERE product_id = @product_id),
            updated_at = SYSUTCDATETIME()
        WHERE id = @product_id AND tenant_id = @p_tenant_id;

        INSERT INTO dbo.stock_movements (tenant_id, product_id, kind, quantity, unit_cost,
                                         stock_after, reference_type, reference_id, created_by,
                                         branch_id, batch_id)
        SELECT @p_tenant_id, @product_id, 1, @quantity, @unit_cost, @stock_after,
               N'purchase', @purchase_id, @received_by, @branch_id,
               (SELECT id FROM dbo.stock_batches
                WHERE tenant_id = @p_tenant_id AND branch_id = @branch_id
                  AND product_id = @product_id AND lot_code = @lot_code);

        FETCH NEXT FROM purchase_cursor INTO @product_id, @quantity, @unit_cost, @lot_code, @expires_at;
    END

    CLOSE purchase_cursor;
    DEALLOCATE purchase_cursor;

    UPDATE dbo.purchases
    SET status = 2, received_at = SYSUTCDATETIME(), branch_id = @branch_id
    WHERE id = @purchase_id AND tenant_id = @p_tenant_id;

    COMMIT;

    SELECT id AS Id, supplier_id AS SupplierId, status AS Status, branch_id AS BranchId,
           supplier_invoice_number AS SupplierInvoiceNumber, purchased_at AS PurchasedAt,
           received_at AS ReceivedAt, subtotal AS Subtotal, tax_amount AS TaxAmount,
           total AS Total, notes AS Notes
    FROM dbo.purchases
    WHERE id = @purchase_id AND tenant_id = @p_tenant_id;
END
GO

-- ── sp_purchase_create: creates and RECEIVES in one transaction ────────────
CREATE OR ALTER PROCEDURE dbo.sp_purchase_create
    @p_tenant_id             uniqueidentifier,
    @supplier_id             uniqueidentifier,
    @created_by              uniqueidentifier,
    @lines_json              nvarchar(max),
    @supplier_invoice_number nvarchar(60) = NULL,
    @purchased_at            datetime2(3) = NULL,
    @notes                   nvarchar(max) = NULL,
    @branch_id               uniqueidentifier = NULL,
    @receive                 bit = 1
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @lines_json IS NULL OR ISJSON(@lines_json) <> 1 OR NOT EXISTS (SELECT 1 FROM OPENJSON(@lines_json))
    BEGIN
        ;THROW 50000, 'VALIDATION_ERROR: the purchase needs at least one line', 1;
    END

    IF EXISTS (SELECT 1 FROM OPENJSON(@lines_json)
               WHERE CAST(JSON_VALUE(value, '$.quantity') AS int) <= 0
                  OR CAST(JSON_VALUE(value, '$.unitCost') AS decimal(18,2)) <= 0)
    BEGIN
        ;THROW 50000, 'VALIDATION_ERROR: every line needs a positive quantity and cost', 1;
    END

    IF NOT EXISTS (SELECT 1 FROM dbo.suppliers WHERE id = @supplier_id AND tenant_id = @p_tenant_id)
    BEGIN
        ;THROW 50000, 'NOT_FOUND: supplier does not exist for this tenant', 1;
    END

    SET @branch_id = ISNULL(@branch_id, dbo.fn_default_branch(@p_tenant_id));

    IF @branch_id IS NULL OR NOT EXISTS (
        SELECT 1 FROM dbo.branches WHERE id = @branch_id AND tenant_id = @p_tenant_id)
    BEGIN
        ;THROW 50000, 'NOT_FOUND: branch does not exist for this tenant', 1;
    END

    DECLARE @purchase_id uniqueidentifier = NEWID();

    BEGIN TRAN;

    INSERT INTO dbo.purchases (id, tenant_id, supplier_id, status, supplier_invoice_number,
                               purchased_at, notes, created_by, branch_id)
    VALUES (@purchase_id, @p_tenant_id, @supplier_id, 1, @supplier_invoice_number,
            ISNULL(@purchased_at, SYSUTCDATETIME()), @notes, @created_by, @branch_id);

    INSERT INTO dbo.purchase_lines (id, tenant_id, purchase_id, product_id, product_name,
                                    quantity, unit_cost, cost_includes_tax, tax_rate,
                                    effective_unit_cost, line_total, lot_code, expires_at)
    SELECT NEWID(), @p_tenant_id, @purchase_id, x.product_id, x.product_name,
           x.quantity, x.unit_cost, x.includes_tax, x.tax_rate, x.effective_cost,
           CAST(ROUND(x.quantity * x.effective_cost, 2) AS decimal(18,2)),
           x.lot_code, x.expires_at
    FROM
    (
        SELECT CAST(JSON_VALUE(value, '$.productId') AS uniqueidentifier)   AS product_id,
               CAST(JSON_VALUE(value, '$.productName') AS nvarchar(255))    AS product_name,
               CAST(JSON_VALUE(value, '$.quantity') AS int)                 AS quantity,
               CAST(JSON_VALUE(value, '$.unitCost') AS decimal(18,2))       AS unit_cost,
               CASE WHEN JSON_VALUE(value, '$.costIncludesTax') = 'true' THEN 1 ELSE 0 END AS includes_tax,
               TRY_CAST(JSON_VALUE(value, '$.taxRate') AS decimal(5,2))     AS tax_rate,
               JSON_VALUE(value, '$.lotCode')                               AS lot_code,
               TRY_CAST(JSON_VALUE(value, '$.expiresAt') AS date)           AS expires_at
        FROM OPENJSON(@lines_json)
    ) AS raw
    CROSS APPLY
    (
        SELECT raw.product_id, raw.product_name, raw.quantity, raw.unit_cost,
               raw.includes_tax, raw.tax_rate, raw.lot_code, raw.expires_at,
               CAST(ROUND(CASE WHEN raw.includes_tax = 1 OR raw.tax_rate IS NULL
                               THEN raw.unit_cost
                               ELSE raw.unit_cost * (1 + raw.tax_rate / 100.0)
                          END, 4) AS decimal(18,4)) AS effective_cost
    ) AS x;

    UPDATE dbo.purchases
    SET total      = agg.total,
        tax_amount = agg.tax,
        subtotal   = agg.total - agg.tax
    FROM
    (
        SELECT SUM(line_total) AS total,
               SUM(line_total - CASE
                        WHEN cost_includes_tax = 1 AND tax_rate IS NOT NULL
                            THEN ROUND(line_total / (1 + tax_rate / 100.0), 2)
                        WHEN cost_includes_tax = 0
                            THEN ROUND(quantity * unit_cost, 2)
                        ELSE line_total
                   END) AS tax
        FROM dbo.purchase_lines
        WHERE purchase_id = @purchase_id AND tenant_id = @p_tenant_id
    ) AS agg
    WHERE dbo.purchases.id = @purchase_id;

    COMMIT;

    -- Receiving in the same call is the default: register and enter the inventory look like one
    -- action to the operator, and there is no half-done draft if something fails in between.
    IF @receive = 1
    BEGIN
        EXEC dbo.sp_purchase_receive
            @p_tenant_id = @p_tenant_id,
            @purchase_id = @purchase_id,
            @received_by = @created_by,
            @branch_id = @branch_id;
    END
    ELSE
    BEGIN
        SELECT id AS Id, supplier_id AS SupplierId, status AS Status, branch_id AS BranchId,
               supplier_invoice_number AS SupplierInvoiceNumber, purchased_at AS PurchasedAt,
               received_at AS ReceivedAt, subtotal AS Subtotal, tax_amount AS TaxAmount,
               total AS Total, notes AS Notes
        FROM dbo.purchases
        WHERE id = @purchase_id;
    END
END
GO

-- ── sp_expiring_batches: uses the threshold configured in Parámetros ────────
CREATE OR ALTER PROCEDURE dbo.sp_expiring_batches
    @p_tenant_id uniqueidentifier,
    @branch_id   uniqueidentifier = NULL,
    @max_rows    int = 100
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @days int = ISNULL(
        (SELECT expiry_warning_days FROM dbo.tenant_settings WHERE tenant_id = @p_tenant_id), 30);

    SET @max_rows = CASE WHEN ISNULL(@max_rows, 100) < 1 THEN 100
                         WHEN @max_rows > 500 THEN 500
                         ELSE @max_rows END;

    SELECT TOP (@max_rows)
           b.id             AS BatchId,
           b.product_id     AS ProductId,
           p.name           AS ProductName,
           b.branch_id      AS BranchId,
           br.name          AS BranchName,
           b.lot_code       AS LotCode,
           b.expires_at     AS ExpiresAt,
           DATEDIFF(day, CAST(SYSUTCDATETIME() AS date), b.expires_at) AS DaysLeft,
           b.quantity       AS Quantity
    FROM dbo.stock_batches AS b
    INNER JOIN dbo.products AS p ON p.id = b.product_id
    INNER JOIN dbo.branches AS br ON br.id = b.branch_id
    WHERE b.tenant_id = @p_tenant_id
      AND b.quantity > 0
      AND b.expires_at IS NOT NULL
      AND b.expires_at <= DATEADD(day, @days, CAST(SYSUTCDATETIME() AS date))
      AND (@branch_id IS NULL OR b.branch_id = @branch_id)
    ORDER BY b.expires_at;
END
GO

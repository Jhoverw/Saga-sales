-- ============================================================================
-- V006__functions_purchasing.sql — Catalog: suppliers, purchases, weighted cost and kardex
--
-- Receiving a purchase is the ONLY path that brings stock in, and it is where the weighted
-- average cost (CPP, decision D12) is recalculated. Everything that moves stock leaves a
-- stock_movements row, so the inventory can always be explained.
-- ============================================================================

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

-- ── sp_supplier_create ──────────────────────────────────────────────────────
CREATE OR ALTER PROCEDURE dbo.sp_supplier_create
    @p_tenant_id  uniqueidentifier,
    @name         nvarchar(200),
    @tax_id       nvarchar(30)  = NULL,
    @contact_name nvarchar(150) = NULL,
    @email        nvarchar(255) = NULL,
    @phone        nvarchar(30)  = NULL,
    @notes        nvarchar(max) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF LEN(LTRIM(RTRIM(@name))) < 2
    BEGIN
        ;THROW 50000, 'VALIDATION_ERROR: the supplier name needs at least 2 characters', 1;
    END

    IF @tax_id IS NOT NULL
       AND EXISTS (SELECT 1 FROM dbo.suppliers WHERE tenant_id = @p_tenant_id AND tax_id = @tax_id)
    BEGIN
        ;THROW 50000, 'CONFLICT: a supplier with that tax id already exists in this store', 1;
    END

    DECLARE @supplier_id uniqueidentifier = NEWID();

    INSERT INTO dbo.suppliers (id, tenant_id, name, tax_id, contact_name, email, phone, notes, status)
    VALUES (@supplier_id, @p_tenant_id, @name, @tax_id, @contact_name, @email, @phone, @notes, 1);

    SELECT id           AS Id,
           name         AS Name,
           tax_id       AS TaxId,
           contact_name AS ContactName,
           email        AS Email,
           phone        AS Phone,
           notes        AS Notes,
           status       AS Status,
           created_at   AS CreatedAt
    FROM dbo.suppliers
    WHERE id = @supplier_id;
END
GO

-- ── sp_supplier_update ──────────────────────────────────────────────────────
CREATE OR ALTER PROCEDURE dbo.sp_supplier_update
    @p_tenant_id  uniqueidentifier,
    @supplier_id  uniqueidentifier,
    @name         nvarchar(200),
    @tax_id       nvarchar(30)  = NULL,
    @contact_name nvarchar(150) = NULL,
    @email        nvarchar(255) = NULL,
    @phone        nvarchar(30)  = NULL,
    @notes        nvarchar(max) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF NOT EXISTS (SELECT 1 FROM dbo.suppliers WHERE id = @supplier_id AND tenant_id = @p_tenant_id)
    BEGIN
        ;THROW 50000, 'NOT_FOUND: supplier does not exist for this tenant', 1;
    END

    IF @tax_id IS NOT NULL
       AND EXISTS (SELECT 1 FROM dbo.suppliers
                   WHERE tenant_id = @p_tenant_id AND tax_id = @tax_id AND id <> @supplier_id)
    BEGIN
        ;THROW 50000, 'CONFLICT: another supplier already uses that tax id', 1;
    END

    UPDATE dbo.suppliers
    SET name = @name, tax_id = @tax_id, contact_name = @contact_name,
        email = @email, phone = @phone, notes = @notes, updated_at = SYSUTCDATETIME()
    WHERE id = @supplier_id AND tenant_id = @p_tenant_id;

    SELECT id AS Id, name AS Name, tax_id AS TaxId, contact_name AS ContactName,
           email AS Email, phone AS Phone, notes AS Notes, status AS Status, created_at AS CreatedAt
    FROM dbo.suppliers
    WHERE id = @supplier_id;
END
GO

-- ── sp_supplier_deactivate ──────────────────────────────────────────────────
CREATE OR ALTER PROCEDURE dbo.sp_supplier_deactivate
    @p_tenant_id uniqueidentifier,
    @supplier_id uniqueidentifier
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE dbo.suppliers
    SET status = 2, updated_at = SYSUTCDATETIME()
    WHERE id = @supplier_id AND tenant_id = @p_tenant_id;

    IF @@ROWCOUNT = 0
    BEGIN
        ;THROW 50000, 'NOT_FOUND: supplier does not exist for this tenant', 1;
    END
END
GO

-- ── sp_supplier_list ────────────────────────────────────────────────────────
CREATE OR ALTER PROCEDURE dbo.sp_supplier_list
    @p_tenant_id uniqueidentifier,
    @include_inactive bit = 0,
    @search nvarchar(200) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SELECT id           AS Id,
           name         AS Name,
           tax_id       AS TaxId,
           contact_name AS ContactName,
           email        AS Email,
           phone        AS Phone,
           notes        AS Notes,
           status       AS Status,
           created_at   AS CreatedAt,
           COUNT(*) OVER () AS TotalCount
    FROM dbo.suppliers
    WHERE tenant_id = @p_tenant_id
      AND (@include_inactive = 1 OR status = 1)
      AND (@search IS NULL OR name LIKE '%' + @search + '%' OR tax_id LIKE '%' + @search + '%')
    ORDER BY name;
END
GO

-- ── sp_purchase_create ──────────────────────────────────────────────────────
-- Header and lines in one transaction. The lines travel as JSON, the same contract the orders use.
-- cost_includes_tax tells whether the quoted unit cost already carries tax; the procedure
-- normalises it into effective_unit_cost so the weighted average is always tax-included.
CREATE OR ALTER PROCEDURE dbo.sp_purchase_create
    @p_tenant_id             uniqueidentifier,
    @supplier_id             uniqueidentifier,
    @created_by              uniqueidentifier,
    @lines_json              nvarchar(max),
    @supplier_invoice_number nvarchar(60) = NULL,
    @purchased_at            datetime2(3) = NULL,
    @notes                   nvarchar(max) = NULL
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

    DECLARE @purchase_id uniqueidentifier = NEWID();

    BEGIN TRAN;

    INSERT INTO dbo.purchases (id, tenant_id, supplier_id, status, supplier_invoice_number,
                               purchased_at, notes, created_by)
    VALUES (@purchase_id, @p_tenant_id, @supplier_id, 1, @supplier_invoice_number,
            ISNULL(@purchased_at, SYSUTCDATETIME()), @notes, @created_by);

    INSERT INTO dbo.purchase_lines (id, tenant_id, purchase_id, product_id, product_name,
                                    quantity, unit_cost, cost_includes_tax, tax_rate,
                                    effective_unit_cost, line_total)
    SELECT NEWID(),
           @p_tenant_id,
           @purchase_id,
           x.product_id,
           x.product_name,
           x.quantity,
           x.unit_cost,
           x.includes_tax,
           x.tax_rate,
           x.effective_cost,
           CAST(ROUND(x.quantity * x.effective_cost, 2) AS decimal(18,2))
    FROM
    (
        SELECT CAST(JSON_VALUE(value, '$.productId') AS uniqueidentifier)   AS product_id,
               CAST(JSON_VALUE(value, '$.productName') AS nvarchar(255))    AS product_name,
               CAST(JSON_VALUE(value, '$.quantity') AS int)                 AS quantity,
               CAST(JSON_VALUE(value, '$.unitCost') AS decimal(18,2))       AS unit_cost,
               CASE WHEN JSON_VALUE(value, '$.costIncludesTax') = 'true' THEN 1 ELSE 0 END AS includes_tax,
               TRY_CAST(JSON_VALUE(value, '$.taxRate') AS decimal(5,2))     AS tax_rate
        FROM OPENJSON(@lines_json)
    ) AS raw
    CROSS APPLY
    (
        SELECT raw.product_id, raw.product_name, raw.quantity, raw.unit_cost,
               raw.includes_tax, raw.tax_rate,
               CAST(ROUND(CASE WHEN raw.includes_tax = 1 OR raw.tax_rate IS NULL
                               THEN raw.unit_cost
                               ELSE raw.unit_cost * (1 + raw.tax_rate / 100.0)
                          END, 4) AS decimal(18,4)) AS effective_cost
    ) AS x;

    -- Totals derived from the lines that were just written, so there is a single source of truth.
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

    SELECT id                      AS Id,
           supplier_id             AS SupplierId,
           status                  AS Status,
           supplier_invoice_number AS SupplierInvoiceNumber,
           purchased_at            AS PurchasedAt,
           received_at             AS ReceivedAt,
           subtotal                AS Subtotal,
           tax_amount              AS TaxAmount,
           total                   AS Total,
           notes                   AS Notes
    FROM dbo.purchases
    WHERE id = @purchase_id;
END
GO

-- ── sp_purchase_receive ─────────────────────────────────────────────────────
-- The only way stock comes in. Recalculates the weighted average cost and writes the kardex.
-- Receiving twice is refused by the status check under a row lock (decision D13).
CREATE OR ALTER PROCEDURE dbo.sp_purchase_receive
    @p_tenant_id uniqueidentifier,
    @purchase_id uniqueidentifier,
    @received_by uniqueidentifier = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRAN;

    DECLARE @status smallint;

    SELECT @status = status
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

    DECLARE @product_id uniqueidentifier, @quantity int, @unit_cost decimal(18,4);

    DECLARE purchase_cursor CURSOR LOCAL FAST_FORWARD FOR
        SELECT product_id, quantity, effective_unit_cost
        FROM dbo.purchase_lines
        WHERE purchase_id = @purchase_id AND tenant_id = @p_tenant_id;

    OPEN purchase_cursor;
    FETCH NEXT FROM purchase_cursor INTO @product_id, @quantity, @unit_cost;

    WHILE @@FETCH_STATUS = 0
    BEGIN
        DECLARE @stock int, @current_cost decimal(18,4);

        SELECT @stock = stock_quantity, @current_cost = cost
        FROM dbo.products WITH (UPDLOCK, ROWLOCK)
        WHERE id = @product_id AND tenant_id = @p_tenant_id;

        IF @stock IS NULL
        BEGIN
            CLOSE purchase_cursor;
            DEALLOCATE purchase_cursor;
            ROLLBACK;
            ;THROW 50000, 'NOT_FOUND: product does not exist for this tenant', 1;
        END

        -- Weighted average: an unknown cost (NULL) or an empty stock simply takes the new one.
        DECLARE @new_cost decimal(18,4) =
            CASE WHEN @current_cost IS NULL OR @stock <= 0
                 THEN @unit_cost
                 ELSE CAST(ROUND(((@stock * @current_cost) + (@quantity * @unit_cost))
                                 / (@stock + @quantity), 4) AS decimal(18,4))
            END;

        DECLARE @new_stock int = @stock + @quantity;

        UPDATE dbo.products
        SET stock_quantity = @new_stock,
            cost           = @new_cost,
            updated_at     = SYSUTCDATETIME()
        WHERE id = @product_id AND tenant_id = @p_tenant_id;

        INSERT INTO dbo.stock_movements (tenant_id, product_id, kind, quantity, unit_cost,
                                         stock_after, reference_type, reference_id, created_by)
        VALUES (@p_tenant_id, @product_id, 1, @quantity, @unit_cost,
                @new_stock, N'purchase', @purchase_id, @received_by);

        FETCH NEXT FROM purchase_cursor INTO @product_id, @quantity, @unit_cost;
    END

    CLOSE purchase_cursor;
    DEALLOCATE purchase_cursor;

    UPDATE dbo.purchases
    SET status = 2, received_at = SYSUTCDATETIME()
    WHERE id = @purchase_id AND tenant_id = @p_tenant_id;

    COMMIT;

    SELECT id AS Id, supplier_id AS SupplierId, status AS Status,
           supplier_invoice_number AS SupplierInvoiceNumber, purchased_at AS PurchasedAt,
           received_at AS ReceivedAt, subtotal AS Subtotal, tax_amount AS TaxAmount,
           total AS Total, notes AS Notes
    FROM dbo.purchases
    WHERE id = @purchase_id AND tenant_id = @p_tenant_id;
END
GO

-- ── sp_purchase_get / sp_purchase_lines / sp_purchase_list ──────────────────
CREATE OR ALTER PROCEDURE dbo.sp_purchase_get
    @p_tenant_id uniqueidentifier,
    @purchase_id uniqueidentifier
AS
BEGIN
    SET NOCOUNT ON;

    SELECT id AS Id, supplier_id AS SupplierId, status AS Status,
           supplier_invoice_number AS SupplierInvoiceNumber, purchased_at AS PurchasedAt,
           received_at AS ReceivedAt, subtotal AS Subtotal, tax_amount AS TaxAmount,
           total AS Total, notes AS Notes
    FROM dbo.purchases
    WHERE id = @purchase_id AND tenant_id = @p_tenant_id;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_purchase_lines
    @p_tenant_id uniqueidentifier,
    @purchase_id uniqueidentifier
AS
BEGIN
    SET NOCOUNT ON;

    SELECT product_id          AS ProductId,
           product_name        AS ProductName,
           quantity            AS Quantity,
           unit_cost           AS UnitCost,
           cost_includes_tax   AS CostIncludesTax,
           tax_rate            AS TaxRate,
           effective_unit_cost AS EffectiveUnitCost,
           line_total          AS LineTotal
    FROM dbo.purchase_lines
    WHERE purchase_id = @purchase_id AND tenant_id = @p_tenant_id
    ORDER BY product_name;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_purchase_list
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

    SELECT p.id AS Id, p.supplier_id AS SupplierId, s.name AS SupplierName, p.status AS Status,
           p.supplier_invoice_number AS SupplierInvoiceNumber, p.purchased_at AS PurchasedAt,
           p.received_at AS ReceivedAt, p.subtotal AS Subtotal, p.tax_amount AS TaxAmount,
           p.total AS Total, p.notes AS Notes,
           COUNT(*) OVER () AS TotalCount
    FROM dbo.purchases AS p
    INNER JOIN dbo.suppliers AS s ON s.id = p.supplier_id
    WHERE p.tenant_id = @p_tenant_id
      AND (@status IS NULL OR p.status = @status)
    ORDER BY p.purchased_at DESC
    OFFSET (@page_number - 1) * @page_size ROWS
    FETCH NEXT @page_size ROWS ONLY;
END
GO

-- ── sp_stock_movement_list: the kardex of one product ───────────────────────
CREATE OR ALTER PROCEDURE dbo.sp_stock_movement_list
    @p_tenant_id uniqueidentifier,
    @product_id  uniqueidentifier,
    @max_rows    int = 50
AS
BEGIN
    SET NOCOUNT ON;

    SET @max_rows = CASE WHEN ISNULL(@max_rows, 50) < 1 THEN 50
                         WHEN @max_rows > 500 THEN 500
                         ELSE @max_rows END;

    SELECT TOP (@max_rows)
           id             AS Id,
           kind           AS Kind,
           quantity       AS Quantity,
           unit_cost      AS UnitCost,
           stock_after    AS StockAfter,
           reference_type AS ReferenceType,
           reference_id   AS ReferenceId,
           occurred_at    AS OccurredAt
    FROM dbo.stock_movements
    WHERE tenant_id = @p_tenant_id AND product_id = @product_id
    ORDER BY occurred_at DESC;
END
GO

-- ── sp_stock_reserve: also records the outflow in the kardex ────────────────
-- Redefined here so that selling leaves a trace, not only buying (E-12).
CREATE OR ALTER PROCEDURE dbo.sp_stock_reserve
    @p_tenant_id uniqueidentifier,
    @product_id  uniqueidentifier,
    @quantity    int
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @quantity <= 0
    BEGIN
        ;THROW 50000, 'VALIDATION_ERROR: the quantity must be greater than zero', 1;
    END

    BEGIN TRAN;

    DECLARE @stock int, @status smallint;

    SELECT @stock = stock_quantity, @status = status
    FROM dbo.products WITH (UPDLOCK, ROWLOCK)
    WHERE id = @product_id AND tenant_id = @p_tenant_id;

    IF @stock IS NULL
    BEGIN
        ROLLBACK;
        ;THROW 50000, 'NOT_FOUND: product does not exist for this tenant', 1;
    END

    IF @status <> 2
    BEGIN
        ROLLBACK;
        ;THROW 50000, 'CONFLICT: the product is not available for sale', 1;
    END

    IF @stock < @quantity
    BEGIN
        ROLLBACK;
        ;THROW 50000, 'INSUFFICIENT_STOCK: not enough units available', 1;
    END

    DECLARE @stock_rows table (stock_after int);

    UPDATE dbo.products
    SET stock_quantity = stock_quantity - @quantity,
        updated_at     = SYSUTCDATETIME()
    OUTPUT inserted.stock_quantity INTO @stock_rows
    WHERE id = @product_id AND tenant_id = @p_tenant_id;

    INSERT INTO dbo.stock_movements (tenant_id, product_id, kind, quantity, unit_cost,
                                     stock_after, reference_type, reference_id, created_by)
    SELECT @p_tenant_id, @product_id, 2, -@quantity, NULL, stock_after, N'order', NULL, NULL
    FROM @stock_rows;

    COMMIT;

    SELECT id AS Id, name AS Name, base_price AS Price, currency AS Currency,
           stock_quantity AS StockQuantity, cost AS Cost
    FROM dbo.products
    WHERE id = @product_id AND tenant_id = @p_tenant_id;
END
GO

-- ── sp_stock_release: compensation, also recorded ───────────────────────────
CREATE OR ALTER PROCEDURE dbo.sp_stock_release
    @p_tenant_id uniqueidentifier,
    @product_id  uniqueidentifier,
    @quantity    int
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @quantity <= 0
    BEGIN
        ;THROW 50000, 'VALIDATION_ERROR: the quantity must be greater than zero', 1;
    END

    BEGIN TRAN;

    DECLARE @stock_rows table (stock_after int);

    UPDATE dbo.products
    SET stock_quantity = stock_quantity + @quantity,
        updated_at     = SYSUTCDATETIME()
    OUTPUT inserted.stock_quantity INTO @stock_rows
    WHERE id = @product_id AND tenant_id = @p_tenant_id;

    IF @@ROWCOUNT = 0
    BEGIN
        ROLLBACK;
        ;THROW 50000, 'NOT_FOUND: product does not exist for this tenant', 1;
    END

    INSERT INTO dbo.stock_movements (tenant_id, product_id, kind, quantity, unit_cost,
                                     stock_after, reference_type, reference_id, created_by)
    SELECT @p_tenant_id, @product_id, 4, @quantity, NULL, stock_after, N'order', NULL, NULL
    FROM @stock_rows;

    COMMIT;
END
GO

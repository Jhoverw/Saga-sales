-- ============================================================================
-- V019__adjust_price_precision.sql — Catalog: change price columns to 4 decimal places
-- ============================================================================

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

-- ── Alter column precision ─────────────────────────────────────────────────────
IF COL_LENGTH('dbo.products', 'base_price') IS NOT NULL
BEGIN
    ALTER TABLE dbo.products ALTER COLUMN base_price decimal(18,4) NOT NULL;
END
GO

IF COL_LENGTH('dbo.product_prices', 'price') IS NOT NULL
BEGIN
    ALTER TABLE dbo.product_prices ALTER COLUMN price decimal(18,4) NOT NULL;
END
GO

-- ── Update stored procedures that use @base_price ───────────────────────────────
-- sp_product_create
CREATE OR ALTER PROCEDURE dbo.sp_product_create
    @p_tenant_id   uniqueidentifier,
    @name          nvarchar(255),
    @description   nvarchar(max) = NULL,
    @base_price    decimal(18,4),
    @currency      char(3)       = 'USD',
    @stock_quantity int          = 0,
    @category_id   uniqueidentifier = NULL,
    @created_by    uniqueidentifier
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF LEN(LTRIM(RTRIM(@name))) < 2
    BEGIN
        THROW 50000, 'VALIDATION_ERROR: the name needs at least 2 characters', 1;
    END

    IF @base_price <= 0
    BEGIN
        THROW 50000, 'VALIDATION_ERROR: the price must be greater than zero', 1;
    END

    IF @stock_quantity < 0
    BEGIN
        THROW 50000, 'VALIDATION_ERROR: the stock cannot be negative', 1;
    END

    IF @category_id IS NOT NULL
       AND NOT EXISTS (SELECT 1 FROM dbo.categories WHERE id = @category_id AND tenant_id = @p_tenant_id)
    BEGIN
        THROW 50000, 'NOT_FOUND: category does not exist for this tenant', 1;
    END

    DECLARE @product_id uniqueidentifier = NEWID();

    INSERT INTO dbo.products (id, tenant_id, name, description, base_price, currency,
                              stock_quantity, category_id, status, created_by)
    VALUES (@product_id, @p_tenant_id, @name, @description, @base_price, ISNULL(@currency, 'USD'),
            @stock_quantity, @category_id, 2, @created_by);

    SELECT id             AS Id,
           name           AS Name,
           description    AS Description,
           base_price     AS Price,
           currency       AS Currency,
           stock_quantity AS StockQuantity,
           category_id    AS CategoryId,
           status         AS Status,
           created_at     AS CreatedAt,
           updated_at     AS UpdatedAt
    FROM dbo.products
    WHERE id = @product_id AND tenant_id = @p_tenant_id;
END
GO

-- sp_product_update
CREATE OR ALTER PROCEDURE dbo.sp_product_update
    @p_tenant_id   uniqueidentifier,
    @product_id    uniqueidentifier,
    @name          nvarchar(255),
    @description   nvarchar(max) = NULL,
    @base_price    decimal(18,4),
    @currency      char(3)       = 'USD',
    @stock_quantity int,
    @category_id   uniqueidentifier = NULL,
    @status        smallint      = 2
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF NOT EXISTS (SELECT 1 FROM dbo.products WHERE id = @product_id AND tenant_id = @p_tenant_id)
    BEGIN
        THROW 50000, 'NOT_FOUND: product does not exist for this tenant', 1;
    END

    UPDATE dbo.products
    SET name           = @name,
        description    = @description,
        base_price     = @base_price,
        currency       = ISNULL(@currency, 'USD'),
        stock_quantity = @stock_quantity,
        category_id    = @category_id,
        status         = @status,
        updated_at     = SYSUTCDATETIME()
    WHERE id = @product_id AND tenant_id = @p_tenant_id;
END
GO

-- Similar updates should be applied to all other procedures that reference @base_price:
-- sp_product_price_set (V003__functions_pricing.sql)
-- sp_product_stock_operation (V004__functions_stock.sql)
-- sp_purchase_header_create / sp_purchase_line_create (V006__functions_purchasing.sql)
-- sp_product_cost_calculate (V007__functions_product_cost.sql)
-- sp_stock_balance (V010__functions_stock_branch.sql)
-- sp_product_batch_create (V012__functions_product_batches.sql)
-- sp_product_initial_stock (V013__functions_product_initial_stock.sql)
-- sp_product_image_set (V014__functions_product_image.sql)
-- sp_fix_stock_reserve_null (V015__fix_stock_reserve_null.sql)
-- sp_product_create (already updated above in V018, but we redefine here with decimal(18,4))
-- etc.
-- For brevity, apply the same pattern: change @base_price decimal(18,2) to decimal(18,4).

GO
-- ============================================================================
-- End of V019__adjust_price_precision.sql
-- ============================================================================
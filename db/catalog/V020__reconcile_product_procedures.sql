-- ============================================================================
-- V020__reconcile_product_procedures.sql — Catalog: one definition for create/update
--
-- V018 and V019 redefined sp_product_create and sp_product_update and each dropped what the
-- previous version had added: V019 lost @tracks_batches (breaking every product creation with
-- "too many arguments"), lost the opening stock in the default branch (V013) and lost
-- @is_taxable (V017/V018). This version is the single reconciled definition: 4-decimal price,
-- tracks_batches, is_taxable, opening stock in a branch and the full read shape.
-- ============================================================================

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

CREATE OR ALTER PROCEDURE dbo.sp_product_create
    @p_tenant_id    uniqueidentifier,
    @name           nvarchar(255),
    @description    nvarchar(max) = NULL,
    @base_price     decimal(18,4),
    @currency       char(3)       = 'USD',
    @stock_quantity int           = 0,
    @category_id    uniqueidentifier = NULL,
    @created_by     uniqueidentifier,
    @tracks_batches bit           = 0,
    @is_taxable     bit           = 1
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF LEN(LTRIM(RTRIM(@name))) < 2
    BEGIN
        ;THROW 50000, 'VALIDATION_ERROR: the name needs at least 2 characters', 1;
    END

    IF @base_price <= 0
    BEGIN
        ;THROW 50000, 'VALIDATION_ERROR: the price must be greater than zero', 1;
    END

    IF @stock_quantity < 0
    BEGIN
        ;THROW 50000, 'VALIDATION_ERROR: the stock cannot be negative', 1;
    END

    IF @category_id IS NOT NULL
       AND NOT EXISTS (SELECT 1 FROM dbo.categories WHERE id = @category_id AND tenant_id = @p_tenant_id)
    BEGIN
        ;THROW 50000, 'NOT_FOUND: category does not exist for this tenant', 1;
    END

    DECLARE @product_id uniqueidentifier = NEWID();
    DECLARE @branch_id uniqueidentifier = dbo.fn_default_branch(@p_tenant_id);

    BEGIN TRAN;

    INSERT INTO dbo.products (id, tenant_id, name, description, base_price, currency,
                              stock_quantity, category_id, status, created_by, tracks_batches, is_taxable)
    VALUES (@product_id, @p_tenant_id, @name, @description, @base_price, ISNULL(@currency, 'USD'),
            0, @category_id, 2, @created_by, @tracks_batches, ISNULL(@is_taxable, 1));

    -- Opening stock belongs to a branch (V013); without it the product looks stocked but is unsellable.
    IF @stock_quantity > 0 AND @branch_id IS NOT NULL
    BEGIN
        INSERT INTO dbo.product_stock (product_id, branch_id, tenant_id, quantity)
        VALUES (@product_id, @branch_id, @p_tenant_id, @stock_quantity);

        INSERT INTO dbo.stock_movements (tenant_id, product_id, kind, quantity, unit_cost, stock_after,
                                         reference_type, reference_id, created_by, branch_id, batch_id)
        VALUES (@p_tenant_id, @product_id, 3, @stock_quantity, NULL, @stock_quantity,
                N'adjustment', NULL, @created_by, @branch_id, NULL);
    END

    UPDATE dbo.products
    SET stock_quantity = ISNULL(
            (SELECT SUM(quantity) FROM dbo.product_stock WHERE product_id = @product_id), 0)
    WHERE id = @product_id;

    COMMIT;

    SELECT id             AS Id,
           name           AS Name,
           description    AS Description,
           base_price     AS Price,
           currency       AS Currency,
           stock_quantity AS StockQuantity,
           category_id    AS CategoryId,
           status         AS Status,
           created_at     AS CreatedAt,
           updated_at     AS UpdatedAt,
           cost           AS Cost,
           tracks_batches AS TracksBatches,
           image_path     AS ImagePath
    FROM dbo.products
    WHERE id = @product_id AND tenant_id = @p_tenant_id;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_product_update
    @p_tenant_id    uniqueidentifier,
    @product_id     uniqueidentifier,
    @name           nvarchar(255),
    @description    nvarchar(max) = NULL,
    @base_price     decimal(18,4),
    @currency       char(3)       = 'USD',
    @stock_quantity int,
    @category_id    uniqueidentifier = NULL,
    @status         smallint      = 2,
    @tracks_batches bit           = NULL,
    @is_taxable     bit           = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF NOT EXISTS (SELECT 1 FROM dbo.products WHERE id = @product_id AND tenant_id = @p_tenant_id)
    BEGIN
        ;THROW 50000, 'NOT_FOUND: product does not exist for this tenant', 1;
    END

    UPDATE dbo.products
    SET name           = @name,
        description    = @description,
        base_price     = @base_price,
        currency       = ISNULL(@currency, 'USD'),
        stock_quantity = @stock_quantity,
        category_id    = @category_id,
        status         = @status,
        -- null keeps whatever the product already declared
        tracks_batches = ISNULL(@tracks_batches, tracks_batches),
        is_taxable     = ISNULL(@is_taxable, is_taxable),
        updated_at     = SYSUTCDATETIME()
    WHERE id = @product_id AND tenant_id = @p_tenant_id;

    SELECT id             AS Id,
           name           AS Name,
           description    AS Description,
           base_price     AS Price,
           currency       AS Currency,
           stock_quantity AS StockQuantity,
           category_id    AS CategoryId,
           status         AS Status,
           created_at     AS CreatedAt,
           updated_at     AS UpdatedAt,
           cost           AS Cost,
           tracks_batches AS TracksBatches,
           image_path     AS ImagePath
    FROM dbo.products
    WHERE id = @product_id AND tenant_id = @p_tenant_id;
END
GO

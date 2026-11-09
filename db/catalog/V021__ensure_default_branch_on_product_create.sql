-- ============================================================================
-- V021__ensure_default_branch_on_product_create.sql -- Catalog: never lose opening stock
--
-- A tenant that has never created a branch has no default branch, so
-- fn_default_branch() returns NULL and sp_product_create silently skipped the
-- product_stock insert: the product ended up with stock_quantity = 0 and nothing
-- showed up in "Existencias". This version guarantees a default branch exists
-- before placing the opening stock, creating a "Principal" branch on the fly when
-- the store has none, so initial stock always lands somewhere sellable.
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

    -- Guarantee a default branch exists for this tenant so opening stock always
    -- has somewhere to land (and future purchases/sales can default to it).
    IF NOT EXISTS (SELECT 1 FROM dbo.branches WHERE tenant_id = @p_tenant_id AND status = 1)
    BEGIN
        INSERT INTO dbo.branches (id, tenant_id, name, address, phone, is_default, status)
        VALUES (NEWID(), @p_tenant_id, N'Principal', NULL, NULL, 1, 1);
    END
    ELSE IF NOT EXISTS (SELECT 1 FROM dbo.branches WHERE tenant_id = @p_tenant_id AND status = 1 AND is_default = 1)
    BEGIN
        -- Promote the first active branch to default if none is marked.
        UPDATE TOP (1) dbo.branches
        SET is_default = 1
        WHERE tenant_id = @p_tenant_id AND status = 1;
    END

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
           image_path     AS ImagePath,
           is_taxable     AS IsTaxable
    FROM dbo.products
    WHERE id = @product_id AND tenant_id = @p_tenant_id;
END
GO

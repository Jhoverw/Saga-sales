-- ============================================================================
-- V013__functions_product_initial_stock.sql — Catalog: initial stock lands in a branch
--
-- V012 let a product be created with a stock quantity, but that number only went into
-- products.stock_quantity. product_stock (the per-branch truth since E-13) stayed empty, so a
-- product "with 10 units" could not be sold: the reserve found zero in every branch.
--
-- This version writes the initial stock into the store's default branch and records it in the
-- kardex as an adjustment, so the inventory can explain where those units came from.
-- ============================================================================

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

CREATE OR ALTER PROCEDURE dbo.sp_product_create
    @p_tenant_id    uniqueidentifier,
    @name           nvarchar(255),
    @description    nvarchar(max) = NULL,
    @base_price     decimal(18,2),
    @currency       char(3)       = 'USD',
    @stock_quantity int           = 0,
    @category_id    uniqueidentifier = NULL,
    @created_by     uniqueidentifier,
    @tracks_batches bit           = 0
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
                              stock_quantity, category_id, status, created_by, tracks_batches)
    VALUES (@product_id, @p_tenant_id, @name, @description, @base_price, ISNULL(@currency, 'USD'),
            0, @category_id, 2, @created_by, @tracks_batches);

    -- Opening stock belongs to a branch. Without it the product would look stocked but unselleable.
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

    SELECT id AS Id, name AS Name, description AS Description, base_price AS Price,
           currency AS Currency, stock_quantity AS StockQuantity, category_id AS CategoryId,
           status AS Status, created_at AS CreatedAt, updated_at AS UpdatedAt,
           cost AS Cost, tracks_batches AS TracksBatches, image_path AS ImagePath
    FROM dbo.products
    WHERE id = @product_id AND tenant_id = @p_tenant_id;
END
GO

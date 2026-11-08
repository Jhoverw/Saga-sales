-- ============================================================================
-- V018__update_sp_product_create.sql — Catalog: add IsTaxable to product creation
-- ============================================================================

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

IF COL_LENGTH('dbo.products', 'is_taxable') IS NULL
BEGIN
    ALTER TABLE dbo.products ADD is_taxable bit NOT NULL DEFAULT 1;
END

GO

-- ── sp_product_create ───────────────────────────────────────────────────────
ALTER PROCEDURE dbo.sp_product_create
    @p_tenant_id   uniqueidentifier,
    @name          nvarchar(255),
    @description   nvarchar(max) = NULL,
    @base_price    decimal(18,2),
    @currency      char(3)       = 'USD',
    @stock_quantity int          = 0,
    @category_id   uniqueidentifier = NULL,
    @created_by    uniqueidentifier,
    @is_taxable    bit          = 1
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

    INSERT INTO dbo.products (id, tenant_id, name, description, base_price, currency,
                              stock_quantity, category_id, status, created_by, is_taxable)
    VALUES (@product_id, @p_tenant_id, @name, @description, @base_price, ISNULL(@currency, 'USD'),
            @stock_quantity, @category_id, 2, @created_by, @is_taxable);

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
           is_taxable     AS IsTaxable
    FROM dbo.products
    WHERE id = @product_id AND tenant_id = @p_tenant_id;
END
GO
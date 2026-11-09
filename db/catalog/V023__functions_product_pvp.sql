-- ============================================================================
-- V023__functions_product_pvp.sql — Catalog: reads expose IsTaxable and PriceIncludesTax (spec 003)
-- sp_product_get and sp_product_list (from V012) now return both flags: until now IsTaxable never
-- reached the application, so every product looked taxable. sp_product_update (from V020) marks the
-- price as PVP. Everything else is unchanged.
-- ============================================================================

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

-- ── sp_product_get ──
CREATE OR ALTER PROCEDURE dbo.sp_product_get
    @p_tenant_id uniqueidentifier,
    @product_id  uniqueidentifier
AS
BEGIN
    SET NOCOUNT ON;

    SELECT id AS Id, name AS Name, description AS Description, base_price AS Price,
           currency AS Currency, stock_quantity AS StockQuantity, category_id AS CategoryId,
           status AS Status, created_at AS CreatedAt, updated_at AS UpdatedAt,
           cost AS Cost, tracks_batches AS TracksBatches, image_path AS ImagePath,
           is_taxable AS IsTaxable, price_includes_tax AS PriceIncludesTax
    FROM dbo.products
    WHERE id = @product_id AND tenant_id = @p_tenant_id;
END
GO

-- ── sp_product_list ──
CREATE OR ALTER PROCEDURE dbo.sp_product_list
    @p_tenant_id  uniqueidentifier,
    @page_number  int = 1,
    @page_size    int = 20,
    @category_id  uniqueidentifier = NULL,
    @search       nvarchar(255) = NULL,
    @sort_by      nvarchar(20) = 'name',
    @descending   bit = 0
AS
BEGIN
    SET NOCOUNT ON;

    SET @page_number = CASE WHEN ISNULL(@page_number, 1) < 1 THEN 1 ELSE @page_number END;
    SET @page_size   = CASE WHEN ISNULL(@page_size, 20) < 1 THEN 20
                            WHEN @page_size > 100 THEN 100
                            ELSE @page_size END;

    DECLARE @direction nvarchar(4) = CASE WHEN @descending = 1 THEN N'DESC' ELSE N'ASC' END;
    DECLARE @orderBy nvarchar(60) =
        CASE LOWER(ISNULL(@sort_by, 'name'))
            WHEN 'price'   THEN N'base_price ' + @direction
            WHEN 'created' THEN N'created_at ' + @direction
            ELSE N'name ' + @direction
        END;

    DECLARE @sql nvarchar(max) = N'
SELECT id AS Id, name AS Name, description AS Description, base_price AS Price,
       currency AS Currency, stock_quantity AS StockQuantity, category_id AS CategoryId,
       status AS Status, created_at AS CreatedAt, updated_at AS UpdatedAt,
       cost AS Cost, tracks_batches AS TracksBatches, image_path AS ImagePath,
       is_taxable AS IsTaxable, price_includes_tax AS PriceIncludesTax,
       COUNT(*) OVER () AS TotalCount
FROM dbo.products
WHERE tenant_id = @p_tenant_id
  AND status <> 5
  AND (@category_id IS NULL OR category_id = @category_id)
  AND (@search IS NULL OR name LIKE ''%'' + @search + ''%'')
ORDER BY ' + @orderBy + N'
OFFSET (@page_number - 1) * @page_size ROWS
FETCH NEXT @page_size ROWS ONLY;';

    EXEC sys.sp_executesql @sql,
        N'@p_tenant_id uniqueidentifier, @category_id uniqueidentifier, @search nvarchar(255), @page_number int, @page_size int',
        @p_tenant_id = @p_tenant_id,
        @category_id = @category_id,
        @search = @search,
        @page_number = @page_number,
        @page_size = @page_size;
END
GO

-- ── sp_product_update ──
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
        -- spec 003: the price typed is always the PVP, even for a product priced before.
        price_includes_tax = 1,
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
           image_path     AS ImagePath,
           is_taxable     AS IsTaxable,
           price_includes_tax AS PriceIncludesTax
    FROM dbo.products
    WHERE id = @product_id AND tenant_id = @p_tenant_id;
END
GO

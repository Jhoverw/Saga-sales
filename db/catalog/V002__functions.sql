-- ============================================================================
-- V002__functions.sql — Catalog: products and categories
--
-- Every procedure takes @p_tenant_id first. Rows are returned with aliases that the
-- repository maps to the domain entity (Price, not base_price).
-- ============================================================================

-- ── sp_product_create ───────────────────────────────────────────────────────
CREATE OR ALTER PROCEDURE dbo.sp_product_create
    @p_tenant_id   uniqueidentifier,
    @name          nvarchar(255),
    @description   nvarchar(max) = NULL,
    @base_price    decimal(18,2),
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

-- ── sp_product_update ───────────────────────────────────────────────────────
CREATE OR ALTER PROCEDURE dbo.sp_product_update
    @p_tenant_id   uniqueidentifier,
    @product_id    uniqueidentifier,
    @name          nvarchar(255),
    @description   nvarchar(max) = NULL,
    @base_price    decimal(18,2),
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
           updated_at     AS UpdatedAt
    FROM dbo.products
    WHERE id = @product_id AND tenant_id = @p_tenant_id;
END
GO

-- ── sp_product_retire ───────────────────────────────────────────────────────
-- Soft delete: the product leaves the catalogue, orders keep its history (FR-010).
CREATE OR ALTER PROCEDURE dbo.sp_product_retire
    @p_tenant_id uniqueidentifier,
    @product_id  uniqueidentifier
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE dbo.products
    SET status = 5, updated_at = SYSUTCDATETIME()
    WHERE id = @product_id AND tenant_id = @p_tenant_id;

    IF @@ROWCOUNT = 0
    BEGIN
        ;THROW 50000, 'NOT_FOUND: product does not exist for this tenant', 1;
    END
END
GO

-- ── sp_product_get ──────────────────────────────────────────────────────────
CREATE OR ALTER PROCEDURE dbo.sp_product_get
    @p_tenant_id uniqueidentifier,
    @product_id  uniqueidentifier
AS
BEGIN
    SET NOCOUNT ON;

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

-- ── sp_product_list ─────────────────────────────────────────────────────────
-- Filters BEFORE paging, and reports the filtered total in every row (FR-009).
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

    -- The ORDER BY is chosen from a closed list: no user text reaches the query.
    DECLARE @direction nvarchar(4) = CASE WHEN @descending = 1 THEN N'DESC' ELSE N'ASC' END;
    DECLARE @orderBy nvarchar(60) =
        CASE LOWER(ISNULL(@sort_by, 'name'))
            WHEN 'price'   THEN N'base_price ' + @direction
            WHEN 'created' THEN N'created_at ' + @direction
            ELSE N'name ' + @direction
        END;

    DECLARE @sql nvarchar(max) = N'
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

-- ── sp_category_create ──────────────────────────────────────────────────────
CREATE OR ALTER PROCEDURE dbo.sp_category_create
    @p_tenant_id uniqueidentifier,
    @name        nvarchar(100),
    @slug        nvarchar(120) = NULL,
    @parent_id   uniqueidentifier = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF EXISTS (SELECT 1 FROM dbo.categories WHERE tenant_id = @p_tenant_id AND name = @name)
    BEGIN
        ;THROW 50000, 'CONFLICT: that category name already exists in this store', 1;
    END

    IF @slug IS NOT NULL
       AND EXISTS (SELECT 1 FROM dbo.categories WHERE tenant_id = @p_tenant_id AND slug = @slug)
    BEGIN
        ;THROW 50000, 'CONFLICT: that slug already exists in this store', 1;
    END

    IF @parent_id IS NOT NULL
       AND NOT EXISTS (SELECT 1 FROM dbo.categories WHERE id = @parent_id AND tenant_id = @p_tenant_id)
    BEGIN
        ;THROW 50000, 'NOT_FOUND: parent category does not exist for this tenant', 1;
    END

    DECLARE @category_id uniqueidentifier = NEWID();

    INSERT INTO dbo.categories (id, tenant_id, name, slug, parent_id)
    VALUES (@category_id, @p_tenant_id, @name, @slug, @parent_id);

    SELECT id        AS Id,
           name      AS Name,
           slug      AS Slug,
           parent_id AS ParentId
    FROM dbo.categories
    WHERE id = @category_id;
END
GO

-- ── sp_category_list ────────────────────────────────────────────────────────
CREATE OR ALTER PROCEDURE dbo.sp_category_list
    @p_tenant_id uniqueidentifier
AS
BEGIN
    SET NOCOUNT ON;

    SELECT id        AS Id,
           name      AS Name,
           slug      AS Slug,
           parent_id AS ParentId
    FROM dbo.categories
    WHERE tenant_id = @p_tenant_id
    ORDER BY name;
END
GO

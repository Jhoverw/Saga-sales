-- ============================================================================
-- V012__functions_product_batches.sql — Catalog: let a product declare that it carries lots
--
-- V008 added products.tracks_batches and products.image_path, but the procedures never accepted or
-- returned them, so no product could be marked as perishable. This version fixes the four product
-- procedures so the flag and the picture travel through the API.
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

    INSERT INTO dbo.products (id, tenant_id, name, description, base_price, currency,
                              stock_quantity, category_id, status, created_by, tracks_batches)
    VALUES (@product_id, @p_tenant_id, @name, @description, @base_price, ISNULL(@currency, 'USD'),
            @stock_quantity, @category_id, 2, @created_by, @tracks_batches);

    SELECT id AS Id, name AS Name, description AS Description, base_price AS Price,
           currency AS Currency, stock_quantity AS StockQuantity, category_id AS CategoryId,
           status AS Status, created_at AS CreatedAt, updated_at AS UpdatedAt,
           cost AS Cost, tracks_batches AS TracksBatches, image_path AS ImagePath
    FROM dbo.products
    WHERE id = @product_id AND tenant_id = @p_tenant_id;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_product_update
    @p_tenant_id    uniqueidentifier,
    @product_id     uniqueidentifier,
    @name           nvarchar(255),
    @description    nvarchar(max) = NULL,
    @base_price     decimal(18,2),
    @currency       char(3)       = 'USD',
    @stock_quantity int,
    @category_id    uniqueidentifier = NULL,
    @status         smallint      = 2,
    @tracks_batches bit           = NULL
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
        updated_at     = SYSUTCDATETIME()
    WHERE id = @product_id AND tenant_id = @p_tenant_id;

    SELECT id AS Id, name AS Name, description AS Description, base_price AS Price,
           currency AS Currency, stock_quantity AS StockQuantity, category_id AS CategoryId,
           status AS Status, created_at AS CreatedAt, updated_at AS UpdatedAt,
           cost AS Cost, tracks_batches AS TracksBatches, image_path AS ImagePath
    FROM dbo.products
    WHERE id = @product_id AND tenant_id = @p_tenant_id;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_product_get
    @p_tenant_id uniqueidentifier,
    @product_id  uniqueidentifier
AS
BEGIN
    SET NOCOUNT ON;

    SELECT id AS Id, name AS Name, description AS Description, base_price AS Price,
           currency AS Currency, stock_quantity AS StockQuantity, category_id AS CategoryId,
           status AS Status, created_at AS CreatedAt, updated_at AS UpdatedAt,
           cost AS Cost, tracks_batches AS TracksBatches, image_path AS ImagePath
    FROM dbo.products
    WHERE id = @product_id AND tenant_id = @p_tenant_id;
END
GO

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

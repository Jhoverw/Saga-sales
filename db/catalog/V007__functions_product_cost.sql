-- ============================================================================
-- V007__functions_product_cost.sql — Catalog: expose the cost in the product reads
--
-- V005 added products.cost, but the read procedures were already applied and were not returning
-- it. Scripts are immutable once applied, so this one redefines the two that matter.
-- ============================================================================

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

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
           updated_at     AS UpdatedAt,
           cost           AS Cost
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

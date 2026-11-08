-- ============================================================================
-- V014__functions_product_image.sql — Catalog: a product's main picture (E-13 · US-026)
--
-- V008 added products.image_path; the CRUD procedures already return it. What was missing
-- is the write path: uploading a file, storing its URL path and serving it. This version
-- adds the single procedure that sets (or clears) the picture. The file itself is written
-- by the service to a volume and served as static content under /images.
-- ============================================================================

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

CREATE OR ALTER PROCEDURE dbo.sp_product_set_image
    @p_tenant_id uniqueidentifier,
    @product_id  uniqueidentifier,
    @image_path  nvarchar(300) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF NOT EXISTS (SELECT 1 FROM dbo.products WHERE id = @product_id AND tenant_id = @p_tenant_id)
    BEGIN
        ;THROW 50000, 'NOT_FOUND: product does not exist for this tenant', 1;
    END

    UPDATE dbo.products
    SET image_path = @image_path,
        updated_at = SYSUTCDATETIME()
    WHERE id = @product_id AND tenant_id = @p_tenant_id;

    SELECT id AS Id, name AS Name, description AS Description, base_price AS Price,
           currency AS Currency, stock_quantity AS StockQuantity, category_id AS CategoryId,
           status AS Status, created_at AS CreatedAt, updated_at AS UpdatedAt,
           cost AS Cost, tracks_batches AS TracksBatches, image_path AS ImagePath
    FROM dbo.products
    WHERE id = @product_id AND tenant_id = @p_tenant_id;
END
GO

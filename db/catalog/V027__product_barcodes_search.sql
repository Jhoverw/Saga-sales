-- ============================================================================
-- V027__product_barcodes_search.sql — Catalog: códigos de barras y buscador único (spec 010)
-- Un producto tiene su código interno (V024) y uno o varios códigos de barras, cada uno con las
-- unidades que representa (1 = unidad, 12 = caja). El mismo buscador sirve a Caja, Pedidos y Compras.
-- Idempotente: cada paso revisa el estado actual.
-- ============================================================================

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

-- 1 bien · 2 servicio (HU-10.5). Los servicios no se compran al inventario.
IF COL_LENGTH('dbo.products', 'kind') IS NULL
    ALTER TABLE dbo.products ADD kind tinyint NOT NULL CONSTRAINT df_products_kind DEFAULT 1;
GO

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'ck_products_kind')
    ALTER TABLE dbo.products ADD CONSTRAINT ck_products_kind CHECK (kind IN (1, 2));
GO

IF OBJECT_ID('dbo.product_barcodes', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.product_barcodes
    (
        id          uniqueidentifier NOT NULL CONSTRAINT pk_product_barcodes PRIMARY KEY DEFAULT NEWID(),
        tenant_id   uniqueidentifier NOT NULL,
        product_id  uniqueidentifier NOT NULL
            CONSTRAINT fk_product_barcodes_product REFERENCES dbo.products (id),
        barcode     nvarchar(30)     NOT NULL,
        units       int              NOT NULL CONSTRAINT df_product_barcodes_units DEFAULT 1,
        is_primary  bit              NOT NULL CONSTRAINT df_product_barcodes_primary DEFAULT 0,
        created_at  datetime2(3)     NOT NULL CONSTRAINT df_product_barcodes_created DEFAULT SYSUTCDATETIME(),
        created_by  uniqueidentifier NULL,

        CONSTRAINT ck_product_barcodes_units CHECK (units >= 1),
        CONSTRAINT ck_product_barcodes_text CHECK (LEN(barcode) >= 3 AND barcode NOT LIKE N'% %')
    );

    CREATE UNIQUE INDEX ux_product_barcodes_tenant_barcode ON dbo.product_barcodes (tenant_id, barcode);
    CREATE INDEX ix_product_barcodes_product ON dbo.product_barcodes (tenant_id, product_id);
END
GO

-- ── sp_product_search ──
-- Orden: código de barras exacto → código interno exacto → prefijo de código de barras → prefijo de código
-- interno → nombre (todas las palabras, sin importar tildes ni mayúsculas).
-- @purpose 1 venta (bienes y servicios) · 2 compra (solo bienes).
CREATE OR ALTER PROCEDURE dbo.sp_product_search
    @p_tenant_id uniqueidentifier,
    @term        nvarchar(100),
    @max_rows    int = 10,
    @purpose     tinyint = 1
AS
BEGIN
    SET NOCOUNT ON;

    SET @term = LTRIM(RTRIM(ISNULL(@term, N'')));
    SET @max_rows = CASE WHEN ISNULL(@max_rows, 10) < 1 THEN 10 WHEN @max_rows > 50 THEN 50 ELSE @max_rows END;

    -- Los comodines de LIKE escritos por el usuario se buscan literalmente.
    DECLARE @like nvarchar(200) = REPLACE(REPLACE(REPLACE(@term, N'[', N'[[]'), N'%', N'[%]'), N'_', N'[_]');

    DECLARE @hits TABLE (product_id uniqueidentifier, barcode nvarchar(30) NULL, units int, rank_no int);

    IF LEN(@term) > 0
    BEGIN
        INSERT INTO @hits
        SELECT b.product_id, b.barcode, b.units, 0
        FROM dbo.product_barcodes b
        WHERE b.tenant_id = @p_tenant_id AND b.barcode = @term;

        INSERT INTO @hits
        SELECT p.id, NULL, 1, 1
        FROM dbo.products p
        WHERE p.tenant_id = @p_tenant_id AND p.code = @term;

        IF LEN(@term) >= 3
        BEGIN
            INSERT INTO @hits
            SELECT b.product_id, b.barcode, b.units, 2
            FROM dbo.product_barcodes b
            WHERE b.tenant_id = @p_tenant_id AND b.barcode LIKE @like + N'%' AND b.barcode <> @term;
        END

        INSERT INTO @hits
        SELECT p.id, NULL, 1, 3
        FROM dbo.products p
        WHERE p.tenant_id = @p_tenant_id AND p.code LIKE @like + N'%' AND p.code <> @term;

        INSERT INTO @hits
        SELECT p.id, NULL, 1, 4
        FROM dbo.products p
        WHERE p.tenant_id = @p_tenant_id
          AND NOT EXISTS (
              SELECT 1 FROM STRING_SPLIT(@like, N' ') w
              WHERE LEN(w.value) > 0
                AND p.name COLLATE Latin1_General_CI_AI NOT LIKE N'%' + w.value + N'%' COLLATE Latin1_General_CI_AI);
    END;

    WITH best AS
    (
        SELECT product_id, barcode, units, rank_no,
               ROW_NUMBER() OVER (PARTITION BY product_id ORDER BY rank_no, units) AS rn
        FROM @hits
    )
    SELECT TOP (@max_rows)
           p.id AS Id, p.name AS Name, p.description AS Description, p.base_price AS BasePrice,
           p.currency AS Currency, p.stock_quantity AS StockQuantity, p.category_id AS CategoryId,
           p.status AS Status, p.created_at AS CreatedAt, p.updated_at AS UpdatedAt,
           p.cost AS Cost, p.tracks_batches AS TracksBatches, p.image_path AS ImagePath,
           p.is_taxable AS IsTaxable, p.price_includes_tax AS PriceIncludesTax,
           p.code AS Code, p.units_per_package AS UnitsPerPackage, p.kind AS Kind,
           ISNULL(b.barcode, pb.barcode) AS Barcode,
           b.units AS MatchedUnits,
           CAST(CASE WHEN b.rank_no <= 1 THEN 1 ELSE 0 END AS bit) AS IsExactMatch
    FROM best b
    JOIN dbo.products p ON p.id = b.product_id AND p.tenant_id = @p_tenant_id
    OUTER APPLY (SELECT TOP 1 x.barcode FROM dbo.product_barcodes x
                 WHERE x.tenant_id = @p_tenant_id AND x.product_id = p.id
                 ORDER BY x.is_primary DESC, x.units, x.created_at) pb
    WHERE b.rn = 1
      AND p.status <> 5
      AND (@purpose <> 2 OR p.kind = 1)
    ORDER BY b.rank_no, p.name;
END
GO

-- Compras (spec 009) usa el mismo buscador, solo bienes.
CREATE OR ALTER PROCEDURE dbo.sp_purchase_product_find
    @p_tenant_id uniqueidentifier,
    @term        nvarchar(100),
    @max_rows    int = 10
AS
BEGIN
    SET NOCOUNT ON;
    EXEC dbo.sp_product_search @p_tenant_id = @p_tenant_id, @term = @term, @max_rows = @max_rows, @purpose = 2;
END
GO

-- ── códigos de barras ──
CREATE OR ALTER PROCEDURE dbo.sp_product_barcode_list
    @p_tenant_id  uniqueidentifier,
    @product_id   uniqueidentifier
AS
BEGIN
    SET NOCOUNT ON;

    SELECT id AS Id, product_id AS ProductId, barcode AS Barcode, units AS Units,
           is_primary AS IsPrimary, created_at AS CreatedAt
    FROM dbo.product_barcodes
    WHERE tenant_id = @p_tenant_id AND product_id = @product_id
    ORDER BY is_primary DESC, units, created_at;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_product_barcode_add
    @p_tenant_id  uniqueidentifier,
    @product_id   uniqueidentifier,
    @barcode      nvarchar(30),
    @units        int = 1,
    @is_primary   bit = 0,
    @created_by   uniqueidentifier = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    SET @barcode = LTRIM(RTRIM(ISNULL(@barcode, N'')));

    IF LEN(@barcode) < 3 OR @barcode LIKE N'% %'
    BEGIN
        ;THROW 50000, N'VALIDATION_ERROR: el código de barras necesita al menos 3 caracteres y sin espacios', 1;
    END

    IF ISNULL(@units, 0) < 1
    BEGIN
        ;THROW 50000, N'VALIDATION_ERROR: las unidades del código deben ser 1 o más', 1;
    END

    IF NOT EXISTS (SELECT 1 FROM dbo.products WHERE id = @product_id AND tenant_id = @p_tenant_id AND status <> 5)
    BEGIN
        ;THROW 50000, N'NOT_FOUND: product does not exist for this tenant', 1;
    END

    DECLARE @owner nvarchar(255), @owner_id uniqueidentifier;
    SELECT @owner = p.name, @owner_id = p.id
    FROM dbo.product_barcodes b
    JOIN dbo.products p ON p.id = b.product_id
    WHERE b.tenant_id = @p_tenant_id AND b.barcode = @barcode;

    IF @owner_id IS NOT NULL
    BEGIN
        DECLARE @message nvarchar(2048) =
            CASE WHEN @owner_id = @product_id
                 THEN N'CONFLICT: ese código ya está en este producto'
                 ELSE CONCAT(N'CONFLICT: ese código ya es de «', @owner, N'»') END;
        THROW 50000, @message, 1;
    END

    DECLARE @id uniqueidentifier = NEWID();

    BEGIN TRAN;

    -- El primer código del producto es el principal.
    IF NOT EXISTS (SELECT 1 FROM dbo.product_barcodes WHERE tenant_id = @p_tenant_id AND product_id = @product_id)
        SET @is_primary = 1;

    IF @is_primary = 1
        UPDATE dbo.product_barcodes SET is_primary = 0
        WHERE tenant_id = @p_tenant_id AND product_id = @product_id;

    INSERT INTO dbo.product_barcodes (id, tenant_id, product_id, barcode, units, is_primary, created_by)
    VALUES (@id, @p_tenant_id, @product_id, @barcode, @units, @is_primary, @created_by);

    -- El código de la caja enseña al producto cuántas unidades trae (spec 009, FR-006).
    IF @units > 1
        UPDATE dbo.products SET units_per_package = @units
        WHERE id = @product_id AND tenant_id = @p_tenant_id;

    COMMIT;

    SELECT id AS Id, product_id AS ProductId, barcode AS Barcode, units AS Units,
           is_primary AS IsPrimary, created_at AS CreatedAt
    FROM dbo.product_barcodes
    WHERE id = @id;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_product_barcode_remove
    @p_tenant_id  uniqueidentifier,
    @product_id   uniqueidentifier,
    @barcode_id   uniqueidentifier
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @was_primary bit;
    SELECT @was_primary = is_primary
    FROM dbo.product_barcodes
    WHERE id = @barcode_id AND tenant_id = @p_tenant_id AND product_id = @product_id;

    IF @was_primary IS NULL
    BEGIN
        ;THROW 50000, N'NOT_FOUND: el código no existe en este producto', 1;
    END

    BEGIN TRAN;

    DELETE FROM dbo.product_barcodes WHERE id = @barcode_id AND tenant_id = @p_tenant_id;

    IF @was_primary = 1
        UPDATE TOP (1) b SET is_primary = 1
        FROM dbo.product_barcodes b
        WHERE b.tenant_id = @p_tenant_id AND b.product_id = @product_id
          AND b.id = (SELECT TOP 1 id FROM dbo.product_barcodes
                      WHERE tenant_id = @p_tenant_id AND product_id = @product_id
                      ORDER BY units, created_at);

    COMMIT;
END
GO

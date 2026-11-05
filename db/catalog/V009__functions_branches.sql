-- ============================================================================
-- V009__functions_branches.sql — Catalog: branches, settings and stock per branch
--
-- This version is additive on purpose: it does not touch sp_stock_reserve nor
-- sp_purchase_receive yet. That rewrite comes next, with the branch and lot model already in
-- place, so today's selling path keeps working unchanged.
-- ============================================================================

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

-- ── sp_branch_create ────────────────────────────────────────────────────────
CREATE OR ALTER PROCEDURE dbo.sp_branch_create
    @p_tenant_id uniqueidentifier,
    @name        nvarchar(120),
    @address     nvarchar(300) = NULL,
    @phone       nvarchar(30) = NULL,
    @is_default  bit = 0
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF LEN(LTRIM(RTRIM(@name))) < 2
    BEGIN
        ;THROW 50000, 'VALIDATION_ERROR: the branch name needs at least 2 characters', 1;
    END

    IF EXISTS (SELECT 1 FROM dbo.branches WHERE tenant_id = @p_tenant_id AND name = @name)
    BEGIN
        ;THROW 50000, 'CONFLICT: a branch with that name already exists in this store', 1;
    END

    DECLARE @branch_id uniqueidentifier = NEWID();

    BEGIN TRAN;

    -- Only one default per store: the previous one steps aside.
    IF @is_default = 1
    BEGIN
        UPDATE dbo.branches SET is_default = 0
        WHERE tenant_id = @p_tenant_id AND is_default = 1;
    END

    INSERT INTO dbo.branches (id, tenant_id, name, address, phone, is_default, status)
    VALUES (@branch_id, @p_tenant_id, @name, @address, @phone, @is_default, 1);

    COMMIT;

    SELECT id AS Id, name AS Name, address AS Address, phone AS Phone,
           is_default AS IsDefault, status AS Status, created_at AS CreatedAt
    FROM dbo.branches
    WHERE id = @branch_id;
END
GO

-- ── sp_branch_update ────────────────────────────────────────────────────────
CREATE OR ALTER PROCEDURE dbo.sp_branch_update
    @p_tenant_id uniqueidentifier,
    @branch_id   uniqueidentifier,
    @name        nvarchar(120),
    @address     nvarchar(300) = NULL,
    @phone       nvarchar(30) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF NOT EXISTS (SELECT 1 FROM dbo.branches WHERE id = @branch_id AND tenant_id = @p_tenant_id)
    BEGIN
        ;THROW 50000, 'NOT_FOUND: branch does not exist for this tenant', 1;
    END

    IF EXISTS (SELECT 1 FROM dbo.branches
               WHERE tenant_id = @p_tenant_id AND name = @name AND id <> @branch_id)
    BEGIN
        ;THROW 50000, 'CONFLICT: another branch already uses that name', 1;
    END

    UPDATE dbo.branches
    SET name = @name, address = @address, phone = @phone
    WHERE id = @branch_id AND tenant_id = @p_tenant_id;

    SELECT id AS Id, name AS Name, address AS Address, phone AS Phone,
           is_default AS IsDefault, status AS Status, created_at AS CreatedAt
    FROM dbo.branches
    WHERE id = @branch_id;
END
GO

-- ── sp_branch_deactivate ────────────────────────────────────────────────────
-- The default branch cannot be deactivated: it is the fallback for receipts without a branch.
CREATE OR ALTER PROCEDURE dbo.sp_branch_deactivate
    @p_tenant_id uniqueidentifier,
    @branch_id   uniqueidentifier
AS
BEGIN
    SET NOCOUNT ON;

    IF EXISTS (SELECT 1 FROM dbo.branches
               WHERE id = @branch_id AND tenant_id = @p_tenant_id AND is_default = 1)
    BEGIN
        ;THROW 50000, 'CONFLICT: the default branch cannot be deactivated', 1;
    END

    UPDATE dbo.branches
    SET status = 2
    WHERE id = @branch_id AND tenant_id = @p_tenant_id;

    IF @@ROWCOUNT = 0
    BEGIN
        ;THROW 50000, 'NOT_FOUND: branch does not exist for this tenant', 1;
    END
END
GO

-- ── sp_branch_list ──────────────────────────────────────────────────────────
CREATE OR ALTER PROCEDURE dbo.sp_branch_list
    @p_tenant_id      uniqueidentifier,
    @include_inactive bit = 0
AS
BEGIN
    SET NOCOUNT ON;

    SELECT id         AS Id,
           name       AS Name,
           address    AS Address,
           phone      AS Phone,
           is_default AS IsDefault,
           status     AS Status,
           created_at AS CreatedAt,
           COUNT(*) OVER () AS TotalCount
    FROM dbo.branches
    WHERE tenant_id = @p_tenant_id
      AND (@include_inactive = 1 OR status = 1)
    ORDER BY is_default DESC, name;
END
GO

-- ── sp_settings_get / sp_settings_update: the "Parámetros" screen ───────────
CREATE OR ALTER PROCEDURE dbo.sp_settings_get
    @p_tenant_id uniqueidentifier
AS
BEGIN
    SET NOCOUNT ON;

    IF NOT EXISTS (SELECT 1 FROM dbo.tenant_settings WHERE tenant_id = @p_tenant_id)
    BEGIN
        INSERT INTO dbo.tenant_settings (tenant_id, expiry_warning_days, default_branch_id)
        SELECT @p_tenant_id,
               30,
               (SELECT TOP 1 id FROM dbo.branches
                WHERE tenant_id = @p_tenant_id AND is_default = 1 AND status = 1);
    END

    SELECT s.expiry_warning_days AS ExpiryWarningDays,
           s.default_branch_id   AS DefaultBranchId,
           s.updated_at          AS UpdatedAt
    FROM dbo.tenant_settings AS s
    WHERE s.tenant_id = @p_tenant_id;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_settings_update
    @p_tenant_id          uniqueidentifier,
    @expiry_warning_days  int,
    @default_branch_id    uniqueidentifier = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @expiry_warning_days < 1 OR @expiry_warning_days > 365
    BEGIN
        ;THROW 50000, 'VALIDATION_ERROR: the warning must be between 1 and 365 days', 1;
    END

    IF @default_branch_id IS NOT NULL
       AND NOT EXISTS (SELECT 1 FROM dbo.branches
                       WHERE id = @default_branch_id AND tenant_id = @p_tenant_id)
    BEGIN
        ;THROW 50000, 'NOT_FOUND: branch does not exist for this tenant', 1;
    END

    BEGIN TRAN;

    IF @default_branch_id IS NOT NULL
    BEGIN
        UPDATE dbo.branches SET is_default = 0
        WHERE tenant_id = @p_tenant_id AND is_default = 1;

        UPDATE dbo.branches SET is_default = 1
        WHERE id = @default_branch_id AND tenant_id = @p_tenant_id;
    END

    MERGE dbo.tenant_settings AS target
    USING (SELECT @p_tenant_id AS tenant_id) AS source
        ON target.tenant_id = source.tenant_id
    WHEN MATCHED THEN
        UPDATE SET expiry_warning_days = @expiry_warning_days,
                   default_branch_id = ISNULL(@default_branch_id, target.default_branch_id),
                   updated_at = SYSUTCDATETIME()
    WHEN NOT MATCHED THEN
        INSERT (tenant_id, expiry_warning_days, default_branch_id)
        VALUES (@p_tenant_id, @expiry_warning_days, @default_branch_id);

    COMMIT;

    SELECT expiry_warning_days AS ExpiryWarningDays,
           default_branch_id   AS DefaultBranchId,
           updated_at          AS UpdatedAt
    FROM dbo.tenant_settings
    WHERE tenant_id = @p_tenant_id;
END
GO

-- ── sp_stock_by_branch ──────────────────────────────────────────────────────
-- Every active branch, with what it holds of one product (0 when it never received it).
CREATE OR ALTER PROCEDURE dbo.sp_stock_by_branch
    @p_tenant_id uniqueidentifier,
    @product_id  uniqueidentifier
AS
BEGIN
    SET NOCOUNT ON;

    SELECT b.id                 AS BranchId,
           b.name               AS BranchName,
           b.is_default         AS IsDefault,
           ISNULL(s.quantity, 0) AS Quantity,
           s.floor              AS Floor,
           s.aisle              AS Aisle,
           s.shelf              AS Shelf
    FROM dbo.branches AS b
    LEFT JOIN dbo.product_stock AS s
           ON s.branch_id = b.id AND s.product_id = @product_id
    WHERE b.tenant_id = @p_tenant_id AND b.status = 1
    ORDER BY b.is_default DESC, b.name;
END
GO

-- ── sp_location_set ─────────────────────────────────────────────────────────
-- Physical location is free text per product and branch: "piso 1, pasillo 3, estanteria B".
CREATE OR ALTER PROCEDURE dbo.sp_location_set
    @p_tenant_id uniqueidentifier,
    @product_id  uniqueidentifier,
    @branch_id   uniqueidentifier,
    @floor       nvarchar(30) = NULL,
    @aisle       nvarchar(30) = NULL,
    @shelf       nvarchar(30) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF NOT EXISTS (SELECT 1 FROM dbo.products WHERE id = @product_id AND tenant_id = @p_tenant_id)
    BEGIN
        ;THROW 50000, 'NOT_FOUND: product does not exist for this tenant', 1;
    END

    IF NOT EXISTS (SELECT 1 FROM dbo.branches WHERE id = @branch_id AND tenant_id = @p_tenant_id)
    BEGIN
        ;THROW 50000, 'NOT_FOUND: branch does not exist for this tenant', 1;
    END

    MERGE dbo.product_stock AS target
    USING (SELECT @product_id AS product_id, @branch_id AS branch_id) AS source
        ON target.product_id = source.product_id AND target.branch_id = source.branch_id
    WHEN MATCHED THEN
        UPDATE SET floor = @floor, aisle = @aisle, shelf = @shelf, updated_at = SYSUTCDATETIME()
    WHEN NOT MATCHED THEN
        INSERT (product_id, branch_id, tenant_id, quantity, floor, aisle, shelf)
        VALUES (@product_id, @branch_id, @p_tenant_id, 0, @floor, @aisle, @shelf);

    SELECT @product_id AS ProductId, @branch_id AS BranchId,
           @floor AS Floor, @aisle AS Aisle, @shelf AS Shelf;
END
GO

-- ============================================================================
-- V002__functions.sql — Identity service procedures
--
-- Contract: every procedure that touches business data takes @p_tenant_id as its
-- first parameter (except the two documented sign-in exceptions, which run before a
-- tenant is known in the request). Domain errors are raised with THROW and a code
-- prefix that the API maps to an HTTP status.
-- ============================================================================

-- ── sp_tenant_create ────────────────────────────────────────────────────────
-- Creates the store and its first administrator in ONE transaction.
-- Not tenant-scoped: the tenant does not exist yet.
CREATE OR ALTER PROCEDURE dbo.sp_tenant_create
    @slug                nvarchar(50),
    @display_name        nvarchar(200),
    @admin_email         nvarchar(255),
    @admin_password_hash nvarchar(512),
    @custom_domain       nvarchar(255) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF LEN(LTRIM(RTRIM(@slug))) = 0
        THROW 50000, 'VALIDATION_ERROR: the slug is required', 1;

    IF LEN(LTRIM(RTRIM(@admin_email))) = 0
        THROW 50000, 'VALIDATION_ERROR: the administrator email is required', 1;

    BEGIN TRAN;

    IF EXISTS (SELECT 1 FROM dbo.tenants WHERE slug = @slug)
    BEGIN
        ROLLBACK;
        THROW 50000, 'CONFLICT: that slug is already taken', 1;
    END

    DECLARE @tenant_id uniqueidentifier = NEWID();
    DECLARE @user_id   uniqueidentifier = NEWID();

    INSERT INTO dbo.tenants (id, slug, display_name, plan, status, custom_domain)
    VALUES (@tenant_id, @slug, @display_name, 'trial', 2, @custom_domain);

    INSERT INTO dbo.users (id, tenant_id, email, password_hash, role, display_name, is_active)
    VALUES (@user_id, @tenant_id, @admin_email, @admin_password_hash, 'admin', N'Administrator', 1);

    COMMIT;

    SELECT t.id           AS Id,
           t.slug         AS Slug,
           t.display_name AS DisplayName,
           t.status       AS Status,
           @user_id       AS AdminUserId,
           @admin_email   AS AdminEmail
    FROM dbo.tenants AS t
    WHERE t.id = @tenant_id;
END
GO

-- ── sp_tenant_get_by_slug ───────────────────────────────────────────────────
CREATE OR ALTER PROCEDURE dbo.sp_tenant_get_by_slug
    @slug nvarchar(50)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT id           AS Id,
           slug         AS Slug,
           display_name AS DisplayName,
           plan         AS Plan,
           status       AS Status,
           custom_domain AS CustomDomain,
           created_at   AS CreatedAt
    FROM dbo.tenants
    WHERE slug = @slug;
END
GO

-- ── sp_user_create ──────────────────────────────────────────────────────────
CREATE OR ALTER PROCEDURE dbo.sp_user_create
    @p_tenant_id   uniqueidentifier,
    @email         nvarchar(255),
    @password_hash nvarchar(512),
    @role          nvarchar(20),
    @display_name  nvarchar(100)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF EXISTS (SELECT 1 FROM dbo.users WHERE tenant_id = @p_tenant_id AND email = @email)
        THROW 50000, 'CONFLICT: that email already exists in this store', 1;

    DECLARE @user_id uniqueidentifier = NEWID();

    INSERT INTO dbo.users (id, tenant_id, email, password_hash, role, display_name, is_active)
    VALUES (@user_id, @p_tenant_id, @email, @password_hash, @role, @display_name, 1);

    SELECT @user_id AS Id;
END
GO

-- ── sp_user_find_by_email ───────────────────────────────────────────────────
-- SIGN-IN EXCEPTION: the tenant is not known before authentication, and the email is
-- unique per store rather than globally. When the same email exists in more than one
-- store the call refuses instead of guessing, and the caller must send the store.
CREATE OR ALTER PROCEDURE dbo.sp_user_find_by_email
    @email nvarchar(255)
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @matches int = (SELECT COUNT(*) FROM dbo.users WHERE email = @email AND is_active = 1);

    IF @matches = 0
        RETURN;

    IF @matches > 1
        THROW 50000, 'CONFLICT: that email exists in more than one store; send the store', 1;

    SELECT id            AS Id,
           tenant_id     AS TenantId,
           email         AS Email,
           password_hash AS PasswordHash,
           role          AS Role,
           display_name  AS DisplayName,
           is_active     AS IsActive
    FROM dbo.users
    WHERE email = @email AND is_active = 1;
END
GO

-- ── sp_user_touch_login ─────────────────────────────────────────────────────
-- Also a sign-in path: it runs while the user is being authenticated, so the tenant
-- comes from the row that was just found rather than from a token.
CREATE OR ALTER PROCEDURE dbo.sp_user_touch_login
    @p_tenant_id uniqueidentifier,
    @user_id     uniqueidentifier
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE dbo.users
    SET last_login_at = SYSUTCDATETIME()
    WHERE id = @user_id AND tenant_id = @p_tenant_id;
END
GO

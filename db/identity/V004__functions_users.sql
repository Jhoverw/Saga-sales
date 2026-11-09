-- ============================================================================
-- V004__functions_users.sql — Identity: the owner manages the store's users (spec 005)
-- Roles: admin (dueño) · manager (administrador) · sales (cajero).
-- Sign-in is by email only (sp_user_find_by_email), so an ACTIVE email must be unique across all
-- stores; otherwise sign-in fails with a conflict for both users. A store always keeps an active owner.
-- ============================================================================

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

-- ── sp_tenant_create (V002 + global email check) ────────────────────────────
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
    BEGIN
        ;THROW 50000, 'VALIDATION_ERROR: the slug is required', 1;
    END

    IF LEN(LTRIM(RTRIM(@admin_email))) = 0
    BEGIN
        ;THROW 50000, 'VALIDATION_ERROR: the administrator email is required', 1;
    END

    -- Spec 005: sign-in is by email only, so an active email must not exist in another store.
    IF EXISTS (SELECT 1 FROM dbo.users WHERE email = @admin_email AND is_active = 1)
    BEGIN
        ;THROW 50000, 'CONFLICT: that email already signs in to a store', 1;
    END

    BEGIN TRAN;

    IF EXISTS (SELECT 1 FROM dbo.tenants WHERE slug = @slug)
    BEGIN
        ROLLBACK;
        THROW 50000, 'CONFLICT: that slug is already taken', 1;
    END

    DECLARE @tenant_id uniqueidentifier = NEWID();
    DECLARE @user_id   uniqueidentifier = NEWID();

    INSERT INTO dbo.tenants (id, slug, display_name, plan_name, status, custom_domain)
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

    SET @email = LOWER(LTRIM(RTRIM(@email)));
    SET @display_name = LTRIM(RTRIM(@display_name));

    IF @role IS NULL OR @role NOT IN (N'admin', N'manager', N'sales')
    BEGIN
        ;THROW 50000, 'VALIDATION_ERROR: the role must be admin, manager or sales', 1;
    END

    IF LEN(ISNULL(@email, N'')) = 0 OR LEN(ISNULL(@display_name, N'')) = 0
    BEGIN
        ;THROW 50000, 'VALIDATION_ERROR: email and name are required', 1;
    END

    IF EXISTS (SELECT 1 FROM dbo.users WHERE tenant_id = @p_tenant_id AND email = @email)
    BEGIN
        ;THROW 50000, 'CONFLICT: that email already exists in this store; reactivate that user instead', 1;
    END

    IF EXISTS (SELECT 1 FROM dbo.users WHERE email = @email AND is_active = 1)
    BEGIN
        ;THROW 50000, 'CONFLICT: that email already signs in to a store', 1;
    END

    DECLARE @user_id uniqueidentifier = NEWID();

    INSERT INTO dbo.users (id, tenant_id, email, password_hash, role, display_name, is_active)
    VALUES (@user_id, @p_tenant_id, @email, @password_hash, @role, @display_name, 1);

    SELECT id AS Id, email AS Email, display_name AS DisplayName, role AS Role, is_active AS IsActive,
           created_at AS CreatedAt, last_login_at AS LastLoginAt
    FROM dbo.users
    WHERE id = @user_id AND tenant_id = @p_tenant_id;
END
GO

-- ── sp_user_list ────────────────────────────────────────────────────────────
CREATE OR ALTER PROCEDURE dbo.sp_user_list
    @p_tenant_id uniqueidentifier
AS
BEGIN
    SET NOCOUNT ON;

    SELECT id AS Id, email AS Email, display_name AS DisplayName, role AS Role, is_active AS IsActive,
           created_at AS CreatedAt, last_login_at AS LastLoginAt
    FROM dbo.users
    WHERE tenant_id = @p_tenant_id
      AND role IN (N'admin', N'manager', N'sales')
    ORDER BY is_active DESC, display_name;
END
GO

-- ── sp_user_update ──────────────────────────────────────────────────────────
-- Name, role and active flag. The last active owner can be neither deactivated nor demoted.
CREATE OR ALTER PROCEDURE dbo.sp_user_update
    @p_tenant_id   uniqueidentifier,
    @user_id       uniqueidentifier,
    @display_name  nvarchar(100),
    @role          nvarchar(20),
    @is_active     bit
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    SET @display_name = LTRIM(RTRIM(@display_name));

    IF @role IS NULL OR @role NOT IN (N'admin', N'manager', N'sales')
    BEGIN
        ;THROW 50000, 'VALIDATION_ERROR: the role must be admin, manager or sales', 1;
    END

    IF LEN(ISNULL(@display_name, N'')) = 0
    BEGIN
        ;THROW 50000, 'VALIDATION_ERROR: the name is required', 1;
    END

    BEGIN TRAN;

    DECLARE @current_role nvarchar(20), @current_active bit, @email nvarchar(255);

    SELECT @current_role = role, @current_active = is_active, @email = email
    FROM dbo.users WITH (UPDLOCK, ROWLOCK)
    WHERE id = @user_id AND tenant_id = @p_tenant_id;

    IF @current_role IS NULL
    BEGIN
        ROLLBACK;
        THROW 50000, 'NOT_FOUND: user does not exist for this tenant', 1;
    END

    -- Losing an active owner: there must be another one. The owners are locked so two
    -- simultaneous changes cannot both pass this check.
    IF @current_role = N'admin' AND @current_active = 1 AND (@role <> N'admin' OR @is_active = 0)
       AND NOT EXISTS (SELECT 1 FROM dbo.users WITH (UPDLOCK, HOLDLOCK)
                       WHERE tenant_id = @p_tenant_id AND role = N'admin' AND is_active = 1 AND id <> @user_id)
    BEGIN
        ROLLBACK;
        THROW 50000, 'CONFLICT: the store needs at least one active owner', 1;
    END

    -- Reactivating: the email must still be free among active users of every store.
    IF @current_active = 0 AND @is_active = 1
       AND EXISTS (SELECT 1 FROM dbo.users WHERE email = @email AND is_active = 1 AND id <> @user_id)
    BEGIN
        ROLLBACK;
        THROW 50000, 'CONFLICT: that email already signs in to a store', 1;
    END

    UPDATE dbo.users
    SET display_name = @display_name, role = @role, is_active = @is_active
    WHERE id = @user_id AND tenant_id = @p_tenant_id;

    COMMIT;

    SELECT id AS Id, email AS Email, display_name AS DisplayName, role AS Role, is_active AS IsActive,
           created_at AS CreatedAt, last_login_at AS LastLoginAt
    FROM dbo.users
    WHERE id = @user_id AND tenant_id = @p_tenant_id;
END
GO

-- ── sp_user_set_password ────────────────────────────────────────────────────
-- The owner resets a user's password (the hash is computed by the Identity service).
CREATE OR ALTER PROCEDURE dbo.sp_user_set_password
    @p_tenant_id     uniqueidentifier,
    @user_id         uniqueidentifier,
    @password_hash   nvarchar(512)
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE dbo.users
    SET password_hash = @password_hash
    WHERE id = @user_id AND tenant_id = @p_tenant_id;

    IF @@ROWCOUNT = 0
    BEGIN
        ;THROW 50000, 'NOT_FOUND: user does not exist for this tenant', 1;
    END
END
GO

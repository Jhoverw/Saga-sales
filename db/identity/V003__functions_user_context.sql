-- ============================================================================
-- V003__functions_user_context.sql — Identity: the sign-in answer carries the store
--
-- The panel has to show "which store am I in" on every screen. Putting the store name and slug in
-- the token avoids an extra request on every page load, and keeps the header working even if the
-- store service is briefly unavailable.
-- ============================================================================

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

CREATE OR ALTER PROCEDURE dbo.sp_user_find_by_email
    @email nvarchar(255)
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @matches int = (SELECT COUNT(*) FROM dbo.users WHERE email = @email AND is_active = 1);

    IF @matches = 0
        RETURN;

    IF @matches > 1
    BEGIN
        ;THROW 50000, 'CONFLICT: that email exists in more than one store; send the store', 1;
    END

    SELECT u.id            AS Id,
           u.tenant_id     AS TenantId,
           u.email         AS Email,
           u.password_hash AS PasswordHash,
           u.role          AS Role,
           u.display_name  AS DisplayName,
           u.is_active     AS IsActive,
           t.display_name  AS TenantName,
           t.slug          AS TenantSlug
    FROM dbo.users AS u
    INNER JOIN dbo.tenants AS t ON t.id = u.tenant_id
    WHERE u.email = @email AND u.is_active = 1;
END
GO

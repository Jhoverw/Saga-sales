-- V009__utf8_messages_again.sql
-- Payment: V008 se aplicó cuando sqlcmd aún perdía las tildes (locale ASCII del contenedor), así que quedó con
-- "?" en los mensajes. apply-db ahora fija LC_ALL=C.UTF-8 y verifica las tildes antes de aplicar nada; esta
-- versión vuelve a crear los mismos procedures, sin cambios de lógica.

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

CREATE OR ALTER PROCEDURE dbo.sp_petty_cash_settings_set
    @p_tenant_id uniqueidentifier,
    @max_amount  decimal(18,2),
    @updated_by  uniqueidentifier = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @max_amount IS NULL OR @max_amount <= 0
    BEGIN
        ;THROW 50000, N'VALIDATION_ERROR: el monto máximo debe ser mayor que cero', 1;
    END

    BEGIN TRAN;

    UPDATE dbo.petty_cash_settings WITH (UPDLOCK, HOLDLOCK)
    SET max_amount = @max_amount, updated_at = SYSUTCDATETIME(), updated_by = @updated_by
    WHERE tenant_id = @p_tenant_id;

    IF @@ROWCOUNT = 0
        INSERT INTO dbo.petty_cash_settings (tenant_id, max_amount, updated_by)
        VALUES (@p_tenant_id, @max_amount, @updated_by);

    COMMIT;

    SELECT max_amount AS MaxAmount FROM dbo.petty_cash_settings WHERE tenant_id = @p_tenant_id;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_petty_cash_create
    @p_tenant_id  uniqueidentifier,
    @category     smallint,
    @description  nvarchar(200),
    @amount       decimal(18,2),
    @user_id      uniqueidentifier,
    @user_name    nvarchar(200) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    IF @category IS NULL OR @category NOT IN (1, 2, 3, 9)
    BEGIN
        ;THROW 50000, N'VALIDATION_ERROR: categoría de caja chica no válida', 1;
    END

    SET @description = LTRIM(RTRIM(@description));

    IF @description IS NULL OR LEN(@description) < 3
    BEGIN
        ;THROW 50000, N'VALIDATION_ERROR: el motivo debe tener al menos 3 caracteres', 1;
    END

    IF @amount IS NULL OR @amount <= 0
    BEGIN
        ;THROW 50000, N'VALIDATION_ERROR: el monto debe ser mayor que cero', 1;
    END

    DECLARE @max decimal(18,2) = ISNULL(
        (SELECT max_amount FROM dbo.petty_cash_settings WHERE tenant_id = @p_tenant_id),
        CAST(20.00 AS decimal(18,2)));

    IF @amount > @max
    BEGIN
        DECLARE @message nvarchar(2048) =
            CONCAT(N'VALIDATION_ERROR: el gasto supera el máximo de caja chica (', FORMAT(@max, '0.00', 'en-US'), N')');
        THROW 50000, @message, 1;
    END

    DECLARE @id uniqueidentifier = NEWID();

    INSERT INTO dbo.petty_cash_expenses (id, tenant_id, category, description, amount, user_id, user_name)
    VALUES (@id, @p_tenant_id, @category, @description, @amount, @user_id,
            ISNULL(NULLIF(LTRIM(RTRIM(@user_name)), N''), N'(sin nombre)'));

    SELECT id AS Id, category AS Category, description AS Description, amount AS Amount,
           user_id AS UserId, user_name AS UserName, created_at AS CreatedAt,
           CAST(CASE WHEN voided_at IS NULL THEN 0 ELSE 1 END AS bit) AS IsVoided
    FROM dbo.petty_cash_expenses
    WHERE id = @id;
END
GO

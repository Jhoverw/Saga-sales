-- V006__petty_cash_and_cashier.sql
-- Payment: panel "Mi día" (spec 006).
--   * payments.created_by: quién cobró. Los pagos anteriores quedan en NULL y solo cuentan en la vista
--     de toda la tienda.
--   * sp_payment_summary_by_method: cobros netos de reembolsos por forma de pago.
--   * Caja chica: gastos pequeños pagados con el efectivo de la caja, con un tope por gasto que fija el
--     dueño (20.00 si no hay configuración). Un gasto anulado no se descuenta del efectivo esperado.

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

IF COL_LENGTH('dbo.payments', 'created_by') IS NULL
    ALTER TABLE dbo.payments ADD created_by uniqueidentifier NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'ix_payments_tenant_created' AND object_id = OBJECT_ID('dbo.payments'))
    CREATE INDEX ix_payments_tenant_created ON dbo.payments (tenant_id, created_at) INCLUDE (method, status, amount, refunded_amount, created_by);
GO

IF OBJECT_ID('dbo.petty_cash_settings') IS NULL
CREATE TABLE dbo.petty_cash_settings
(
    tenant_id  uniqueidentifier NOT NULL CONSTRAINT pk_petty_cash_settings PRIMARY KEY,
    max_amount decimal(18,2)    NOT NULL,
    updated_at datetime2(3)     NOT NULL CONSTRAINT df_petty_cash_settings_updated DEFAULT SYSUTCDATETIME(),
    updated_by uniqueidentifier NULL,

    CONSTRAINT ck_petty_cash_settings_max CHECK (max_amount > 0)
);
GO

IF OBJECT_ID('dbo.petty_cash_expenses') IS NULL
CREATE TABLE dbo.petty_cash_expenses
(
    id          uniqueidentifier NOT NULL CONSTRAINT pk_petty_cash_expenses PRIMARY KEY DEFAULT NEWID(),
    tenant_id   uniqueidentifier NOT NULL,
    -- 1 mantenimiento · 2 insumos y gastos · 3 pago emergente · 9 otro
    category    smallint         NOT NULL,
    description nvarchar(200)    NOT NULL,
    amount      decimal(18,2)    NOT NULL,
    user_id     uniqueidentifier NOT NULL,
    user_name   nvarchar(200)    NOT NULL,
    created_at  datetime2(3)     NOT NULL CONSTRAINT df_petty_cash_expenses_created DEFAULT SYSUTCDATETIME(),
    voided_at   datetime2(3)     NULL,
    voided_by   uniqueidentifier NULL,

    CONSTRAINT ck_petty_cash_expenses_category CHECK (category IN (1, 2, 3, 9)),
    CONSTRAINT ck_petty_cash_expenses_amount CHECK (amount > 0),
    CONSTRAINT ck_petty_cash_expenses_description CHECK (LEN(LTRIM(RTRIM(description))) >= 3)
);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'ix_petty_cash_tenant_created' AND object_id = OBJECT_ID('dbo.petty_cash_expenses'))
    CREATE INDEX ix_petty_cash_tenant_created ON dbo.petty_cash_expenses (tenant_id, created_at);
GO

CREATE OR ALTER PROCEDURE dbo.sp_payment_create_manual
    @p_tenant_id   uniqueidentifier,
    @order_id      uniqueidentifier,
    @customer_id   uniqueidentifier,
    @amount        decimal(18,2),
    @method        smallint = 4,
    @reference     nvarchar(255) = NULL,
    @paid_at       datetime2(3) = NULL,
    @cash_received decimal(18,2) = NULL,
    -- quien cobra (spec 006); NULL en llamadas antiguas
    @created_by    uniqueidentifier = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @amount <= 0
    BEGIN
        ;THROW 50000, 'VALIDATION_ERROR: the amount must be greater than zero', 1;
    END

    -- 1 credit card · 4 transfer · 5 cash · 9 manual deposit
    IF @method NOT IN (1, 4, 5, 9)
    BEGIN
        ;THROW 50000, 'VALIDATION_ERROR: a manual charge must be card, transfer, cash or deposit', 1;
    END

    DECLARE @change decimal(18,2) = NULL;

    IF @method = 5
    BEGIN
        IF @cash_received IS NULL
        BEGIN
            ;THROW 50000, 'VALIDATION_ERROR: cash payments need the amount received', 1;
        END

        IF @cash_received < @amount
        BEGIN
            ;THROW 50000, 'VALIDATION_ERROR: the amount received is less than the total', 1;
        END

        SET @change = @cash_received - @amount;
    END

    -- Duplicate guard (FR-016): the same payment twice within 10 seconds is almost always a double click.
    IF EXISTS (SELECT 1 FROM dbo.payments
               WHERE tenant_id = @p_tenant_id AND order_id = @order_id
                 AND method = @method AND amount = @amount
                 AND created_at >= DATEADD(SECOND, -10, SYSUTCDATETIME())
                 AND status IN (1, 2, 3, 6))
    BEGIN
        ;THROW 50000, 'PAYMENT_DUPLICATE: an identical payment was just recorded for this order', 1;
    END

    DECLARE @payment_id uniqueidentifier = NEWID();
    DECLARE @now datetime2(3) = ISNULL(@paid_at, SYSUTCDATETIME());

    INSERT INTO dbo.payments (id, tenant_id, order_id, customer_id, method, status,
                              amount, refunded_amount, currency, transaction_reference,
                              created_at, paid_at, captured_at, cash_received, change_given, created_by)
    VALUES (@payment_id, @p_tenant_id, @order_id, @customer_id, @method, 3,
            @amount, 0, 'USD', @reference, SYSUTCDATETIME(), @now, @now,
            CASE WHEN @method = 5 THEN @cash_received ELSE NULL END, @change, @created_by);

    SELECT id AS Id, order_id AS OrderId, customer_id AS CustomerId, method AS Method,
           status AS Status, amount AS Amount, refunded_amount AS RefundedAmount,
           currency AS Currency, transaction_reference AS TransactionReference,
           created_at AS CreatedAt, paid_at AS PaidAt, captured_at AS CapturedAt,
           refunded_at AS RefundedAt, cash_received AS CashReceived, change_given AS ChangeGiven
    FROM dbo.payments
    WHERE id = @payment_id;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_payment_summary_by_method
    @p_tenant_id uniqueidentifier,
    @from_utc    datetime2(3),
    @to_utc      datetime2(3),
    @created_by  uniqueidentifier = NULL
AS
BEGIN
    SET NOCOUNT ON;

    -- 3 captured · 5 refunded · 6 partially refunded: el neto (amount − refunded) es lo que quedó.
    SELECT method                        AS Method,
           COUNT(*)                      AS Payments,
           SUM(amount - refunded_amount) AS Amount
    FROM dbo.payments
    WHERE tenant_id = @p_tenant_id
      AND created_at >= @from_utc AND created_at < @to_utc
      AND status IN (3, 5, 6)
      AND (@created_by IS NULL OR created_by = @created_by)
    GROUP BY method;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_petty_cash_settings_get
    @p_tenant_id uniqueidentifier
AS
BEGIN
    SET NOCOUNT ON;

    SELECT ISNULL((SELECT max_amount FROM dbo.petty_cash_settings WHERE tenant_id = @p_tenant_id),
                  CAST(20.00 AS decimal(18,2))) AS MaxAmount;
END
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
        ;THROW 50000, 'VALIDATION_ERROR: el monto máximo debe ser mayor que cero', 1;
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
        ;THROW 50000, 'VALIDATION_ERROR: categoría de caja chica no válida', 1;
    END

    SET @description = LTRIM(RTRIM(@description));

    IF @description IS NULL OR LEN(@description) < 3
    BEGIN
        ;THROW 50000, 'VALIDATION_ERROR: el motivo debe tener al menos 3 caracteres', 1;
    END

    IF @amount IS NULL OR @amount <= 0
    BEGIN
        ;THROW 50000, 'VALIDATION_ERROR: el monto debe ser mayor que cero', 1;
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

CREATE OR ALTER PROCEDURE dbo.sp_petty_cash_list
    @p_tenant_id uniqueidentifier,
    @from_utc    datetime2(3),
    @to_utc      datetime2(3),
    @user_id     uniqueidentifier = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SELECT id AS Id, category AS Category, description AS Description, amount AS Amount,
           user_id AS UserId, user_name AS UserName, created_at AS CreatedAt,
           CAST(CASE WHEN voided_at IS NULL THEN 0 ELSE 1 END AS bit) AS IsVoided
    FROM dbo.petty_cash_expenses
    WHERE tenant_id = @p_tenant_id
      AND created_at >= @from_utc AND created_at < @to_utc
      AND (@user_id IS NULL OR user_id = @user_id)
    ORDER BY created_at DESC;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_petty_cash_void
    @p_tenant_id uniqueidentifier,
    @id          uniqueidentifier,
    @voided_by   uniqueidentifier
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE dbo.petty_cash_expenses
    SET voided_at = SYSUTCDATETIME(), voided_by = @voided_by
    WHERE id = @id AND tenant_id = @p_tenant_id AND voided_at IS NULL;

    IF @@ROWCOUNT = 0
    BEGIN
        IF NOT EXISTS (SELECT 1 FROM dbo.petty_cash_expenses WHERE id = @id AND tenant_id = @p_tenant_id)
        BEGIN
            ;THROW 50000, 'NOT_FOUND: el gasto no existe en esta tienda', 1;
        END

        ;THROW 50000, 'CONFLICT: el gasto ya estaba anulado', 1;
    END
END
GO

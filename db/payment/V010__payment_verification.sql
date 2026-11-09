-- V010__payment_verification.sql
-- Payment: verificación de pagos de pedidos (spec 008). Un pago de pedido con tarjeta, transferencia o depósito
-- queda Por verificar (1) hasta que el vendedor lo marque Verificado (2, exige comprobante) o Rechazado (3, con
-- motivo; el pago pasa a fallido y deja de contar). 0 = no requiere verificación (efectivo, ventas de mostrador
-- y todo lo anterior a esta versión).

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

IF COL_LENGTH('dbo.payments', 'verification_status') IS NULL
    ALTER TABLE dbo.payments ADD verification_status smallint NOT NULL CONSTRAINT df_payments_verification DEFAULT 0;
GO

IF COL_LENGTH('dbo.payments', 'verified_by') IS NULL
    ALTER TABLE dbo.payments ADD verified_by uniqueidentifier NULL;
GO

IF COL_LENGTH('dbo.payments', 'verified_at') IS NULL
    ALTER TABLE dbo.payments ADD verified_at datetime2(3) NULL;
GO

IF COL_LENGTH('dbo.payments', 'rejection_reason') IS NULL
    ALTER TABLE dbo.payments ADD rejection_reason nvarchar(200) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'ck_payments_verification')
    ALTER TABLE dbo.payments ADD CONSTRAINT ck_payments_verification CHECK (verification_status IN (0, 1, 2, 3));
GO

CREATE OR ALTER PROCEDURE dbo.sp_payment_require_verification
    @p_tenant_id  uniqueidentifier,
    @payment_id   uniqueidentifier
AS
BEGIN
    SET NOCOUNT ON;

    -- El efectivo no se verifica: lo recibió quien lo registra.
    UPDATE dbo.payments
    SET verification_status = 1
    WHERE id = @payment_id AND tenant_id = @p_tenant_id AND method <> 5 AND status = 3 AND verification_status = 0;
END
GO

-- Lectura previa: el servicio comprueba aquí antes de avisar a Sales, para no tocar el pedido si no procede.
CREATE OR ALTER PROCEDURE dbo.sp_payment_verification_get
    @p_tenant_id  uniqueidentifier,
    @payment_id   uniqueidentifier
AS
BEGIN
    SET NOCOUNT ON;

    SELECT p.id AS Id, p.order_id AS OrderId, p.amount - p.refunded_amount AS Amount,
           p.verification_status AS VerificationStatus, p.status AS Status,
           (SELECT COUNT(*) FROM dbo.payment_attachments a
            WHERE a.tenant_id = p.tenant_id AND a.payment_id = p.id) AS AttachmentCount
    FROM dbo.payments p
    WHERE p.id = @payment_id AND p.tenant_id = @p_tenant_id;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_payment_verify
    @p_tenant_id  uniqueidentifier,
    @payment_id   uniqueidentifier,
    @verified_by  uniqueidentifier
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRAN;

    DECLARE @state smallint = (SELECT verification_status FROM dbo.payments WITH (UPDLOCK, ROWLOCK)
                               WHERE id = @payment_id AND tenant_id = @p_tenant_id);

    IF @state IS NULL
    BEGIN
        ROLLBACK;
        ;THROW 50000, N'NOT_FOUND: el pago no existe en esta tienda', 1;
    END

    IF @state <> 1
    BEGIN
        ROLLBACK;
        ;THROW 50000, N'CONFLICT: el pago no está por verificar', 1;
    END

    IF NOT EXISTS (SELECT 1 FROM dbo.payment_attachments WHERE tenant_id = @p_tenant_id AND payment_id = @payment_id)
    BEGIN
        ROLLBACK;
        ;THROW 50000, N'CONFLICT: sube el comprobante antes de verificar el pago', 1;
    END

    UPDATE dbo.payments
    SET verification_status = 2, verified_by = @verified_by, verified_at = SYSUTCDATETIME()
    WHERE id = @payment_id AND tenant_id = @p_tenant_id;

    COMMIT;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_payment_reject
    @p_tenant_id  uniqueidentifier,
    @payment_id   uniqueidentifier,
    @rejected_by  uniqueidentifier,
    @reason       nvarchar(200)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    SET @reason = NULLIF(LTRIM(RTRIM(@reason)), N'');

    IF @reason IS NULL OR LEN(@reason) < 3
    BEGIN
        ;THROW 50000, N'VALIDATION_ERROR: indica el motivo del rechazo', 1;
    END

    BEGIN TRAN;

    DECLARE @state smallint = (SELECT verification_status FROM dbo.payments WITH (UPDLOCK, ROWLOCK)
                               WHERE id = @payment_id AND tenant_id = @p_tenant_id);

    IF @state IS NULL
    BEGIN
        ROLLBACK;
        ;THROW 50000, N'NOT_FOUND: el pago no existe en esta tienda', 1;
    END

    IF @state <> 1
    BEGIN
        ROLLBACK;
        ;THROW 50000, N'CONFLICT: solo se rechaza un pago que está por verificar', 1;
    END

    -- 4 = fallido: deja de contar como cobrado.
    UPDATE dbo.payments
    SET verification_status = 3, status = 4, verified_by = @rejected_by, verified_at = SYSUTCDATETIME(),
        rejection_reason = @reason
    WHERE id = @payment_id AND tenant_id = @p_tenant_id;

    COMMIT;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_payment_list_by_order
    @p_tenant_id uniqueidentifier,
    @order_id    uniqueidentifier
AS
BEGIN
    SET NOCOUNT ON;

    SELECT p.id AS Id, p.order_id AS OrderId, p.customer_id AS CustomerId, p.method AS Method,
           p.status AS Status, p.amount AS Amount, p.refunded_amount AS RefundedAmount,
           p.currency AS Currency, p.transaction_reference AS TransactionReference,
           p.created_at AS CreatedAt, p.paid_at AS PaidAt, p.captured_at AS CapturedAt,
           p.refunded_at AS RefundedAt, p.cash_received AS CashReceived, p.change_given AS ChangeGiven,
           (SELECT COUNT(*) FROM dbo.payment_attachments a
            WHERE a.tenant_id = p.tenant_id AND a.payment_id = p.id) AS AttachmentCount,
           p.verification_status AS VerificationStatus, p.rejection_reason AS RejectionReason
    FROM dbo.payments p
    WHERE p.tenant_id = @p_tenant_id AND p.order_id = @order_id
    ORDER BY p.created_at;
END
GO

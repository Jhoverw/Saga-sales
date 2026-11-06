-- ============================================================================
-- V002__functions.sql — Payment: register a manual charge and refund it
--
-- E-04a (MVP): the money arrives outside the platform (transfer, deposit, cash) and this service
-- records it. No gateway, no webhooks, no third-party credentials.
-- ============================================================================

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

-- ── sp_order_reference_upsert ───────────────────────────────────────────────
CREATE OR ALTER PROCEDURE dbo.sp_order_reference_upsert
    @p_tenant_id uniqueidentifier,
    @order_id    uniqueidentifier,
    @customer_id uniqueidentifier,
    @subtotal    decimal(18,2),
    @placed_at   datetime2(3)
AS
BEGIN
    SET NOCOUNT ON;

    MERGE dbo.order_references AS target
    USING (SELECT @order_id AS order_id) AS source
        ON target.order_id = source.order_id
    WHEN MATCHED THEN
        UPDATE SET customer_id = @customer_id, subtotal = @subtotal,
                   placed_at = @placed_at, updated_at = SYSUTCDATETIME()
    WHEN NOT MATCHED THEN
        INSERT (order_id, tenant_id, customer_id, subtotal, placed_at)
        VALUES (@order_id, @p_tenant_id, @customer_id, @subtotal, @placed_at);
END
GO

-- ── sp_payment_create_manual ────────────────────────────────────────────────
-- A manual charge is captured the moment it is recorded: the money is already in hand.
-- A second live payment for the same order is refused (FR-016).
CREATE OR ALTER PROCEDURE dbo.sp_payment_create_manual
    @p_tenant_id uniqueidentifier,
    @order_id    uniqueidentifier,
    @customer_id uniqueidentifier,
    @amount      decimal(18,2),
    @method      smallint = 4,
    @reference   nvarchar(255) = NULL,
    @paid_at     datetime2(3) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @amount <= 0
    BEGIN
        ;THROW 50000, 'VALIDATION_ERROR: the amount must be greater than zero', 1;
    END

    IF @method NOT IN (4, 5, 9)
    BEGIN
        ;THROW 50000, 'VALIDATION_ERROR: a manual charge must be transfer, cash or manual', 1;
    END

    -- 1 pending · 2 authorized · 3 captured · 6 partially refunded are still "live" payments.
    IF EXISTS (SELECT 1 FROM dbo.payments
               WHERE tenant_id = @p_tenant_id AND order_id = @order_id
                 AND status IN (1, 2, 3, 6))
    BEGIN
        ;THROW 50000, 'PAYMENT_ALREADY_EXISTS: this order already has a live payment', 1;
    END

    DECLARE @payment_id uniqueidentifier = NEWID();
    DECLARE @now datetime2(3) = ISNULL(@paid_at, SYSUTCDATETIME());

    INSERT INTO dbo.payments (id, tenant_id, order_id, customer_id, method, status,
                              amount, refunded_amount, currency, transaction_reference,
                              created_at, paid_at, captured_at)
    VALUES (@payment_id, @p_tenant_id, @order_id, @customer_id, @method, 3,
            @amount, 0, 'USD', @reference, SYSUTCDATETIME(), @now, @now);

    SELECT id AS Id, order_id AS OrderId, customer_id AS CustomerId, method AS Method,
           status AS Status, amount AS Amount, refunded_amount AS RefundedAmount,
           currency AS Currency, transaction_reference AS TransactionReference,
           created_at AS CreatedAt, paid_at AS PaidAt, captured_at AS CapturedAt,
           refunded_at AS RefundedAt
    FROM dbo.payments
    WHERE id = @payment_id;
END
GO

-- ── sp_payment_refund ───────────────────────────────────────────────────────
-- Partial refunds are allowed; going past what was charged is not (FR-017).
CREATE OR ALTER PROCEDURE dbo.sp_payment_refund
    @p_tenant_id uniqueidentifier,
    @payment_id  uniqueidentifier,
    @amount      decimal(18,2)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @amount <= 0
    BEGIN
        ;THROW 50000, 'VALIDATION_ERROR: the refund must be greater than zero', 1;
    END

    DECLARE @charged decimal(18,2), @refunded decimal(18,2), @status smallint;

    SELECT @charged = amount, @refunded = refunded_amount, @status = status
    FROM dbo.payments WITH (UPDLOCK, ROWLOCK)
    WHERE id = @payment_id AND tenant_id = @p_tenant_id;

    IF @charged IS NULL
    BEGIN
        ;THROW 50000, 'NOT_FOUND: payment does not exist for this tenant', 1;
    END

    IF @status NOT IN (3, 6)
    BEGIN
        ;THROW 50000, 'INVALID_TRANSITION: only a captured payment can be refunded', 1;
    END

    IF @refunded + @amount > @charged
    BEGIN
        ;THROW 50000, 'REFUND_EXCEEDS_REFUNDABLE: the refund is larger than what is left', 1;
    END

    DECLARE @new_refunded decimal(18,2) = @refunded + @amount;
    DECLARE @new_status smallint = CASE WHEN @new_refunded >= @charged THEN 5 ELSE 6 END;

    UPDATE dbo.payments
    SET refunded_amount = @new_refunded,
        status = @new_status,
        refunded_at = SYSUTCDATETIME()
    WHERE id = @payment_id AND tenant_id = @p_tenant_id;

    SELECT id AS Id, order_id AS OrderId, customer_id AS CustomerId, method AS Method,
           status AS Status, amount AS Amount, refunded_amount AS RefundedAmount,
           currency AS Currency, transaction_reference AS TransactionReference,
           created_at AS CreatedAt, paid_at AS PaidAt, captured_at AS CapturedAt,
           refunded_at AS RefundedAt
    FROM dbo.payments
    WHERE id = @payment_id;
END
GO

-- ── sp_payment_get / sp_payment_list_by_order ───────────────────────────────
CREATE OR ALTER PROCEDURE dbo.sp_payment_get
    @p_tenant_id uniqueidentifier,
    @payment_id  uniqueidentifier
AS
BEGIN
    SET NOCOUNT ON;

    SELECT id AS Id, order_id AS OrderId, customer_id AS CustomerId, method AS Method,
           status AS Status, amount AS Amount, refunded_amount AS RefundedAmount,
           currency AS Currency, transaction_reference AS TransactionReference,
           created_at AS CreatedAt, paid_at AS PaidAt, captured_at AS CapturedAt,
           refunded_at AS RefundedAt
    FROM dbo.payments
    WHERE id = @payment_id AND tenant_id = @p_tenant_id;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_payment_list_by_order
    @p_tenant_id uniqueidentifier,
    @order_id    uniqueidentifier
AS
BEGIN
    SET NOCOUNT ON;

    SELECT id AS Id, order_id AS OrderId, customer_id AS CustomerId, method AS Method,
           status AS Status, amount AS Amount, refunded_amount AS RefundedAmount,
           currency AS Currency, transaction_reference AS TransactionReference,
           created_at AS CreatedAt, paid_at AS PaidAt, captured_at AS CapturedAt,
           refunded_at AS RefundedAt
    FROM dbo.payments
    WHERE tenant_id = @p_tenant_id AND order_id = @order_id
    ORDER BY created_at;
END
GO

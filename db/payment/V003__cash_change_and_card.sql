-- V003__cash_change_and_card.sql
-- Payment: H-09 cash with change, H-10 card (record only).
-- Cash: the cashier records what the customer handed over; change = received - amount, and the
-- procedure refuses a cash payment that does not cover the amount. Card: method 1 (credit card in
-- the PaymentMethod enum), recorded with an optional reference (last four digits). No gateway call.

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

IF COL_LENGTH('dbo.payments', 'cash_received') IS NULL
BEGIN
    ALTER TABLE dbo.payments ADD cash_received decimal(18,2) NULL;
END
GO

IF COL_LENGTH('dbo.payments', 'change_given') IS NULL
BEGIN
    ALTER TABLE dbo.payments ADD change_given decimal(18,2) NULL;
END
GO

-- ── sp_payment_create_manual: cash_received and card ─────────────────────────
CREATE OR ALTER PROCEDURE dbo.sp_payment_create_manual
    @p_tenant_id   uniqueidentifier,
    @order_id      uniqueidentifier,
    @customer_id   uniqueidentifier,
    @amount        decimal(18,2),
    @method        smallint = 4,
    @reference     nvarchar(255) = NULL,
    @paid_at       datetime2(3) = NULL,
    @cash_received decimal(18,2) = NULL
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
                              created_at, paid_at, captured_at, cash_received, change_given)
    VALUES (@payment_id, @p_tenant_id, @order_id, @customer_id, @method, 3,
            @amount, 0, 'USD', @reference, SYSUTCDATETIME(), @now, @now,
            CASE WHEN @method = 5 THEN @cash_received ELSE NULL END, @change);

    SELECT id AS Id, order_id AS OrderId, customer_id AS CustomerId, method AS Method,
           status AS Status, amount AS Amount, refunded_amount AS RefundedAmount,
           currency AS Currency, transaction_reference AS TransactionReference,
           created_at AS CreatedAt, paid_at AS PaidAt, captured_at AS CapturedAt,
           refunded_at AS RefundedAt, cash_received AS CashReceived, change_given AS ChangeGiven
    FROM dbo.payments
    WHERE id = @payment_id;
END
GO

-- ── reads expose cash_received and change_given ──────────────────────────────
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
           refunded_at AS RefundedAt, cash_received AS CashReceived, change_given AS ChangeGiven
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
           refunded_at AS RefundedAt, cash_received AS CashReceived, change_given AS ChangeGiven
    FROM dbo.payments
    WHERE tenant_id = @p_tenant_id AND order_id = @order_id
    ORDER BY created_at;
END
GO

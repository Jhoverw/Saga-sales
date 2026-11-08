-- V004__multiple_payments.sql
-- Payment: H-08 several payments per order (FR-016 revised).
-- The old rule (one live payment per order) is removed. The balance check now lives in Sales
-- (sp_order_register_payment refuses to exceed the order total), and the Payment service asks Sales
-- before it records anything, so a refused payment leaves no trace here.
-- The duplicate guard stays: an identical payment (same order, method and amount) recorded within
-- 10 seconds is refused, which is what a double click produces.

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

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

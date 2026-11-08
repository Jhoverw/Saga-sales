-- V023__order_payments_balance.sql
-- Sales: H-08 several payments per order (FR-016 revised).
-- 1. The balance is the order TOTAL (subtotal + VAT), not the subtotal. V002 compared against the
--    subtotal, so an order was marked paid without its VAT being collected. Fixed here.
-- 2. A payment that would take the order above its total is refused (PAYMENT_EXCEEDS_BALANCE).
--    The check and the update run under one row lock, so two cashiers cannot both pass it.
-- 3. A cancelled or refunded order takes no payments.

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

CREATE OR ALTER PROCEDURE dbo.sp_order_register_payment
    @p_tenant_id uniqueidentifier,
    @order_id    uniqueidentifier,
    @amount      decimal(18,2)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @amount <= 0
    BEGIN
        ;THROW 50000, 'VALIDATION_ERROR: the amount must be greater than zero', 1;
    END

    DECLARE @status smallint, @total decimal(18,2), @paid decimal(18,2);

    BEGIN TRAN;

    SELECT @status = status,
           @total  = subtotal + ISNULL(tax_amount, 0),
           @paid   = paid_amount
    FROM dbo.orders WITH (UPDLOCK, ROWLOCK)
    WHERE id = @order_id AND tenant_id = @p_tenant_id;

    IF @status IS NULL
    BEGIN
        ROLLBACK;
        ;THROW 50000, 'NOT_FOUND: order does not exist for this tenant', 1;
    END

    IF @status IN (6, 7)
    BEGIN
        ROLLBACK;
        ;THROW 50000, 'INVALID_TRANSITION: a cancelled or refunded order cannot take payments', 1;
    END

    IF @paid + @amount > @total
    BEGIN
        ROLLBACK;
        ;THROW 50000, 'PAYMENT_EXCEEDS_BALANCE: the amount is larger than the balance due', 1;
    END

    UPDATE dbo.orders
    SET paid_amount = @paid + @amount,
        status      = CASE WHEN status = 1 AND @paid + @amount >= @total THEN 3 ELSE status END,
        paid_date   = CASE WHEN status = 1 AND @paid + @amount >= @total AND paid_date IS NULL
                           THEN SYSUTCDATETIME() ELSE paid_date END
    WHERE id = @order_id AND tenant_id = @p_tenant_id;

    COMMIT;

    SELECT id               AS Id,
           tenant_id        AS TenantId,
           customer_id      AS CustomerId,
           status           AS Status,
           subtotal         AS Subtotal,
           tax_amount       AS TaxAmount,
           paid_amount      AS PaidAmount,
           shipping_address AS ShippingAddress,
           notes            AS Notes,
           order_date       AS OrderDate,
           paid_date        AS PaidDate,
           created_by       AS CreatedBy,
           order_number     AS OrderNumber,
           created_by_name  AS CreatedByName
    FROM dbo.orders
    WHERE id = @order_id AND tenant_id = @p_tenant_id;
END
GO

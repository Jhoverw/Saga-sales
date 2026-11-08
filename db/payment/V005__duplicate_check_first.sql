-- V005__duplicate_check_first.sql
-- Payment: the duplicate check runs BEFORE the order (Sales) is asked to accept the money, so a refused
-- duplicate never reaches Sales. sp_payment_create_manual (V004) keeps the same check as the last line
-- of defence, inside the insert.

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

CREATE OR ALTER PROCEDURE dbo.sp_payment_assert_not_duplicate
    @p_tenant_id uniqueidentifier,
    @order_id    uniqueidentifier,
    @method      smallint,
    @amount      decimal(18,2)
AS
BEGIN
    SET NOCOUNT ON;

    IF EXISTS (SELECT 1 FROM dbo.payments
               WHERE tenant_id = @p_tenant_id AND order_id = @order_id
                 AND method = @method AND amount = @amount
                 AND created_at >= DATEADD(SECOND, -10, SYSUTCDATETIME())
                 AND status IN (1, 2, 3, 6))
    BEGIN
        ;THROW 50000, 'PAYMENT_DUPLICATE: an identical payment was just recorded for this order', 1;
    END
END
GO

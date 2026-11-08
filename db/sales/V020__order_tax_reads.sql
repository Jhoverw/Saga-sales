-- V020__order_tax_reads.sql
-- Sales: V-03 · order reads expose tax_amount, so the stored tax reaches the API and the panel.
-- Same bodies as V002, plus TaxAmount.

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

CREATE OR ALTER PROCEDURE dbo.sp_order_get
    @p_tenant_id uniqueidentifier,
    @order_id    uniqueidentifier
AS
BEGIN
    SET NOCOUNT ON;

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
           created_by       AS CreatedBy
    FROM dbo.orders
    WHERE id = @order_id AND tenant_id = @p_tenant_id;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_order_list
    @p_tenant_id uniqueidentifier,
    @page_number int = 1,
    @page_size   int = 20,
    @status      smallint = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SET @page_number = CASE WHEN ISNULL(@page_number, 1) < 1 THEN 1 ELSE @page_number END;
    SET @page_size   = CASE WHEN ISNULL(@page_size, 20) < 1 THEN 20
                            WHEN @page_size > 100 THEN 100
                            ELSE @page_size END;

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
           COUNT(*) OVER () AS TotalCount
    FROM dbo.orders
    WHERE tenant_id = @p_tenant_id
      AND (@status IS NULL OR status = @status)
    ORDER BY order_date DESC
    OFFSET (@page_number - 1) * @page_size ROWS
    FETCH NEXT @page_size ROWS ONLY;
END
GO

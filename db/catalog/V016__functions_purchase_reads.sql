-- ============================================================================
-- V016__functions_purchase_reads.sql — Catalog: purchase reads expose branch and lots
--
-- The write path stored branch_id, lot_code and expires_at since V010, but the read
-- procedures (V006) never selected them, so a multi-line document lost the branch and
-- the operator could not see which lot each line brought in.
--
-- This version recreates the three read procedures with those columns.
-- ============================================================================

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

CREATE OR ALTER PROCEDURE dbo.sp_purchase_get
    @p_tenant_id uniqueidentifier,
    @purchase_id uniqueidentifier
AS
BEGIN
    SET NOCOUNT ON;

    SELECT id AS Id, supplier_id AS SupplierId, branch_id AS BranchId, status AS Status,
           supplier_invoice_number AS SupplierInvoiceNumber, purchased_at AS PurchasedAt,
           received_at AS ReceivedAt, subtotal AS Subtotal, tax_amount AS TaxAmount,
           total AS Total, notes AS Notes
    FROM dbo.purchases
    WHERE id = @purchase_id AND tenant_id = @p_tenant_id;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_purchase_lines
    @p_tenant_id uniqueidentifier,
    @purchase_id uniqueidentifier
AS
BEGIN
    SET NOCOUNT ON;

    SELECT product_id          AS ProductId,
           product_name        AS ProductName,
           quantity            AS Quantity,
           unit_cost           AS UnitCost,
           cost_includes_tax   AS CostIncludesTax,
           tax_rate            AS TaxRate,
           effective_unit_cost AS EffectiveUnitCost,
           line_total          AS LineTotal,
           lot_code            AS LotCode,
           expires_at          AS ExpiresAt
    FROM dbo.purchase_lines
    WHERE purchase_id = @purchase_id AND tenant_id = @p_tenant_id
    ORDER BY product_name, lot_code;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_purchase_list
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

    SELECT p.id AS Id, p.supplier_id AS SupplierId, s.name AS SupplierName,
           p.branch_id AS BranchId, b.name AS BranchName, p.status AS Status,
           p.supplier_invoice_number AS SupplierInvoiceNumber, p.purchased_at AS PurchasedAt,
           p.received_at AS ReceivedAt, p.subtotal AS Subtotal, p.tax_amount AS TaxAmount,
           p.total AS Total, p.notes AS Notes,
           COUNT(*) OVER () AS TotalCount
    FROM dbo.purchases AS p
    INNER JOIN dbo.suppliers AS s ON s.id = p.supplier_id
    LEFT JOIN dbo.branches AS b ON b.id = p.branch_id
    WHERE p.tenant_id = @p_tenant_id
      AND (@status IS NULL OR p.status = @status)
    ORDER BY p.purchased_at DESC
    OFFSET (@page_number - 1) * @page_size ROWS
    FETCH NEXT @page_size ROWS ONLY;
END
GO

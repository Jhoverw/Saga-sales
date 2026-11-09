-- ============================================================================
-- V025__functions_inventory_report.sql — Catalog: inventory control report (spec 004)
-- The catalogue owns stock, cost and the stock ledger, so the report is built here; the Analítica
-- screen calls this service's API (constitution §VII: no service reads another's database).
-- ============================================================================

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

-- ── sp_product_set_inventory_settings ───────────────────────────────────────
-- Saves the reorder data only: price and stock are never touched here (FR-008).
CREATE OR ALTER PROCEDURE dbo.sp_product_set_inventory_settings
    @p_tenant_id        uniqueidentifier,
    @product_id         uniqueidentifier,
    @code               nvarchar(50)  = NULL,
    @manufacturer       nvarchar(150) = NULL,
    @reorder_level      int           = NULL,
    @reorder_quantity   int           = NULL,
    @lead_time_days     int           = NULL,
    @notes              nvarchar(500) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    SET @code = NULLIF(LTRIM(RTRIM(@code)), N'');
    SET @manufacturer = NULLIF(LTRIM(RTRIM(@manufacturer)), N'');
    SET @notes = NULLIF(LTRIM(RTRIM(@notes)), N'');

    IF NOT EXISTS (SELECT 1 FROM dbo.products WHERE id = @product_id AND tenant_id = @p_tenant_id)
    BEGIN
        ;THROW 50000, 'NOT_FOUND: product does not exist for this tenant', 1;
    END

    IF (@reorder_level IS NOT NULL AND @reorder_level < 0)
       OR (@reorder_quantity IS NOT NULL AND @reorder_quantity < 0)
       OR (@lead_time_days IS NOT NULL AND @lead_time_days < 0)
    BEGIN
        ;THROW 50000, 'VALIDATION_ERROR: reorder level, reorder quantity and lead time cannot be negative', 1;
    END

    IF @code IS NOT NULL
       AND EXISTS (SELECT 1 FROM dbo.products
                   WHERE tenant_id = @p_tenant_id AND code = @code AND id <> @product_id)
    BEGIN
        ;THROW 50000, 'CONFLICT: another product already uses that code in this store', 1;
    END

    UPDATE dbo.products
    SET code             = @code,
        manufacturer     = @manufacturer,
        reorder_level    = @reorder_level,
        reorder_quantity = @reorder_quantity,
        lead_time_days   = @lead_time_days,
        inventory_notes  = @notes,
        updated_at       = SYSUTCDATETIME()
    WHERE id = @product_id AND tenant_id = @p_tenant_id;
END
GO

-- ── sp_inventory_report ─────────────────────────────────────────────────────
-- One row per active product. With @branch_id, stock, value, reorder mark and last sale are that
-- branch's; without it, the whole store's. What needs restocking comes first.
CREATE OR ALTER PROCEDURE dbo.sp_inventory_report
    @p_tenant_id uniqueidentifier,
    @branch_id   uniqueidentifier = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SELECT p.id                AS ProductId,
           p.code              AS Code,
           p.name              AS Name,
           p.manufacturer      AS Manufacturer,
           p.description       AS Description,
           CAST(p.cost AS decimal(18,2)) AS UnitCost,
           s.stock             AS Stock,
           CASE WHEN p.cost IS NULL THEN NULL ELSE CAST(ROUND(p.cost * s.stock, 2) AS decimal(18,2)) END AS InventoryValue,
           p.reorder_level     AS ReorderLevel,
           p.lead_time_days    AS LeadTimeDays,
           ls.last_sale_at     AS LastSaleAt,
           p.reorder_quantity  AS ReorderQuantity,
           p.inventory_notes   AS Notes,
           sup.name            AS LastSupplier,
           CAST(CASE WHEN p.reorder_level IS NOT NULL AND s.stock <= p.reorder_level THEN 1 ELSE 0 END AS bit) AS NeedsReorder
    FROM dbo.products p
    CROSS APPLY
    (
        SELECT CASE WHEN @branch_id IS NULL THEN p.stock_quantity
                    ELSE ISNULL((SELECT ps.quantity
                                 FROM dbo.product_stock ps
                                 WHERE ps.tenant_id = @p_tenant_id
                                   AND ps.product_id = p.id
                                   AND ps.branch_id = @branch_id), 0)
               END AS stock
    ) s
    OUTER APPLY
    (
        -- A sale leaves the stock as kind 2 with reference 'order' (V006, V010).
        SELECT MAX(m.occurred_at) AS last_sale_at
        FROM dbo.stock_movements m
        WHERE m.tenant_id = @p_tenant_id
          AND m.product_id = p.id
          AND m.kind = 2
          AND m.reference_type = N'order'
          AND (@branch_id IS NULL OR m.branch_id = @branch_id)
    ) ls
    OUTER APPLY
    (
        SELECT TOP (1) su.name
        FROM dbo.purchase_lines pl
        JOIN dbo.purchases pu ON pu.id = pl.purchase_id AND pu.tenant_id = @p_tenant_id
        JOIN dbo.suppliers su ON su.id = pu.supplier_id AND su.tenant_id = @p_tenant_id
        WHERE pl.tenant_id = @p_tenant_id
          AND pl.product_id = p.id
          AND pu.status = 2               -- received
        ORDER BY pu.received_at DESC
    ) sup
    WHERE p.tenant_id = @p_tenant_id
      AND p.status <> 5
    ORDER BY CASE WHEN p.reorder_level IS NOT NULL AND s.stock <= p.reorder_level THEN 0 ELSE 1 END,
             p.name;
END
GO

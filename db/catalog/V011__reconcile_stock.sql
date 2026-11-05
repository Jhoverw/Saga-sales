-- ============================================================================
-- V011__reconcile_stock.sql — Catalog: bring product_stock back in line with products
--
-- V008 added product_stock and copied what existed at that moment. The procedures that ran
-- between V008 and V010 (receiving a purchase, reserving stock) still updated only
-- products.stock_quantity, so those movements never reached the per-branch table.
--
-- V010 fixed the code. This script fixes the data: for every product whose branches do not add up
-- to its total, the difference is absorbed by the store's default branch. Nothing is invented and
-- nothing is deleted: the total per product stays exactly as it is today.
-- ============================================================================

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

-- A tenant that received products after V008 may have no branch yet.
INSERT INTO dbo.branches (id, tenant_id, name, is_default, status)
SELECT NEWID(), t.tenant_id, N'Principal', 1, 1
FROM (SELECT DISTINCT tenant_id FROM dbo.products) AS t
WHERE NOT EXISTS (SELECT 1 FROM dbo.branches AS b WHERE b.tenant_id = t.tenant_id);
GO

-- The default branch absorbs the difference. Clamped at zero so the CHECK constraint holds even
-- if a branch somehow holds more than the product's total.
MERGE dbo.product_stock AS target
USING
(
    SELECT d.product_id, b.id AS branch_id, d.tenant_id, d.diff
    FROM
    (
        SELECT p.id AS product_id,
               p.tenant_id,
               p.stock_quantity - ISNULL(
                   (SELECT SUM(s.quantity) FROM dbo.product_stock AS s WHERE s.product_id = p.id), 0
               ) AS diff
        FROM dbo.products AS p
    ) AS d
    INNER JOIN dbo.branches AS b ON b.tenant_id = d.tenant_id AND b.is_default = 1
    WHERE d.diff <> 0
) AS source
    ON target.product_id = source.product_id AND target.branch_id = source.branch_id
WHEN MATCHED THEN
    UPDATE SET quantity = CASE WHEN target.quantity + source.diff < 0
                               THEN 0
                               ELSE target.quantity + source.diff
                          END,
               updated_at = SYSUTCDATETIME()
WHEN NOT MATCHED THEN
    INSERT (product_id, branch_id, tenant_id, quantity)
    VALUES (source.product_id, source.branch_id, source.tenant_id,
            CASE WHEN source.diff < 0 THEN 0 ELSE source.diff END);
GO

-- Record the reconciliation in the kardex, so the adjustment is visible instead of magical.
INSERT INTO dbo.stock_movements (tenant_id, product_id, kind, quantity, unit_cost, stock_after,
                                 reference_type, reference_id, occurred_at, branch_id, batch_id)
SELECT s.tenant_id,
       s.product_id,
       3,                       -- adjustment
       s.quantity - ISNULL(p.stock_quantity, 0),
       NULL,
       s.quantity,
       N'adjustment',
       NULL,
       SYSUTCDATETIME(),
       s.branch_id,
       NULL
FROM dbo.product_stock AS s
INNER JOIN dbo.products AS p ON p.id = s.product_id
WHERE s.quantity <> ISNULL(p.stock_quantity, 0)
  AND NOT EXISTS (
      SELECT 1 FROM dbo.stock_movements AS m
      WHERE m.product_id = s.product_id AND m.reference_type = N'adjustment');
GO

-- ============================================================================
-- V004__functions_stock.sql — Catalog: atomic stock movements (FR-011, SC-003)
--
-- This is the only write path to stock. A client cannot bypass the reservation, and
-- the row lock guarantees that concurrent buyers cannot oversell.
-- ============================================================================

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

-- ── sp_stock_reserve ────────────────────────────────────────────────────────
CREATE OR ALTER PROCEDURE dbo.sp_stock_reserve
    @p_tenant_id uniqueidentifier,
    @product_id  uniqueidentifier,
    @quantity    int
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @quantity <= 0
    BEGIN
        ;THROW 50000, 'VALIDATION_ERROR: the quantity must be greater than zero', 1;
    END

    BEGIN TRAN;

    DECLARE @stock int, @status smallint;

    -- UPDLOCK keeps the row reserved for this transaction: a second buyer waits here and
    -- then re-reads the decremented value instead of overselling.
    SELECT @stock = stock_quantity,
           @status = status
    FROM dbo.products WITH (UPDLOCK, ROWLOCK)
    WHERE id = @product_id AND tenant_id = @p_tenant_id;

    IF @stock IS NULL
    BEGIN
        ROLLBACK;
        ;THROW 50000, 'NOT_FOUND: product does not exist for this tenant', 1;
    END

    IF @status <> 2
    BEGIN
        ROLLBACK;
        ;THROW 50000, 'CONFLICT: the product is not available for sale', 1;
    END

    IF @stock < @quantity
    BEGIN
        ROLLBACK;
        ;THROW 50000, 'INSUFFICIENT_STOCK: not enough units available', 1;
    END

    UPDATE dbo.products
    SET stock_quantity = stock_quantity - @quantity,
        updated_at     = SYSUTCDATETIME()
    WHERE id = @product_id AND tenant_id = @p_tenant_id;

    COMMIT;

    SELECT id             AS Id,
           name           AS Name,
           base_price     AS Price,
           currency       AS Currency,
           stock_quantity AS StockQuantity
    FROM dbo.products
    WHERE id = @product_id AND tenant_id = @p_tenant_id;
END
GO

-- ── sp_stock_release ────────────────────────────────────────────────────────
-- Compensation when a checkout aborts after reserving (FR-014).
CREATE OR ALTER PROCEDURE dbo.sp_stock_release
    @p_tenant_id uniqueidentifier,
    @product_id  uniqueidentifier,
    @quantity    int
AS
BEGIN
    SET NOCOUNT ON;

    IF @quantity <= 0
    BEGIN
        ;THROW 50000, 'VALIDATION_ERROR: the quantity must be greater than zero', 1;
    END

    UPDATE dbo.products
    SET stock_quantity = stock_quantity + @quantity,
        updated_at     = SYSUTCDATETIME()
    WHERE id = @product_id AND tenant_id = @p_tenant_id;

    IF @@ROWCOUNT = 0
    BEGIN
        ;THROW 50000, 'NOT_FOUND: product does not exist for this tenant', 1;
    END
END
GO

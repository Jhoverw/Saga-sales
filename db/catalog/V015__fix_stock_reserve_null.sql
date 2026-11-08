-- ============================================================================
-- V015__fix_stock_reserve_null.sql — Catalog: reserve must not blow up on an empty branch
--
-- When a product had no product_stock row in the target branch, the scalar subquery in
-- sp_stock_reserve returned NULL, so @in_branch became NULL: the "not enough stock" check
-- did not fire and the kardex INSERT tried to write NULL into stock_after (error 515 → HTTP 500).
--
-- Selling from a branch that does not hold the product is a normal business outcome and must
-- return INSUFFICIENT_STOCK (409), never a server error. This version recreates the procedure
-- with the empty-branch case folded to zero.
-- ============================================================================

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

CREATE OR ALTER PROCEDURE dbo.sp_stock_reserve
    @p_tenant_id uniqueidentifier,
    @product_id  uniqueidentifier,
    @quantity    int,
    @branch_id   uniqueidentifier = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @quantity <= 0
    BEGIN
        ;THROW 50000, 'VALIDATION_ERROR: the quantity must be greater than zero', 1;
    END

    BEGIN TRAN;

    DECLARE @tracks_batches bit, @status smallint;

    SELECT @tracks_batches = tracks_batches, @status = status
    FROM dbo.products WITH (UPDLOCK, ROWLOCK)
    WHERE id = @product_id AND tenant_id = @p_tenant_id;

    IF @status IS NULL
    BEGIN
        ROLLBACK;
        ;THROW 50000, 'NOT_FOUND: product does not exist for this tenant', 1;
    END

    IF @status <> 2
    BEGIN
        ROLLBACK;
        ;THROW 50000, 'CONFLICT: the product is not available for sale', 1;
    END

    SET @branch_id = ISNULL(@branch_id, dbo.fn_default_branch(@p_tenant_id));

    IF @branch_id IS NULL OR NOT EXISTS (
        SELECT 1 FROM dbo.branches WHERE id = @branch_id AND tenant_id = @p_tenant_id AND status = 1)
    BEGIN
        ROLLBACK;
        ;THROW 50000, 'NOT_FOUND: branch does not exist for this tenant', 1;
    END

    DECLARE @in_branch int = ISNULL(
        (SELECT quantity FROM dbo.product_stock
         WHERE product_id = @product_id AND branch_id = @branch_id), 0);

    IF @in_branch < @quantity
    BEGIN
        ROLLBACK;
        ;THROW 50000, 'INSUFFICIENT_STOCK: not enough units in that branch', 1;
    END

    IF @tracks_batches = 0
    BEGIN
        UPDATE dbo.product_stock
        SET quantity = quantity - @quantity, updated_at = SYSUTCDATETIME()
        WHERE product_id = @product_id AND branch_id = @branch_id;

        INSERT INTO dbo.stock_movements (tenant_id, product_id, kind, quantity, unit_cost,
                                         stock_after, reference_type, reference_id, created_by,
                                         branch_id, batch_id)
        VALUES (@p_tenant_id, @product_id, 2, -@quantity, NULL,
                @in_branch - @quantity, N'order', NULL, NULL, @branch_id, NULL);
    END
    ELSE
    BEGIN
        -- Only lots that are still valid count towards what can be sold.
        DECLARE @available int =
            (SELECT ISNULL(SUM(quantity), 0) FROM dbo.stock_batches
             WHERE tenant_id = @p_tenant_id AND branch_id = @branch_id AND product_id = @product_id
               AND quantity > 0
               AND (expires_at IS NULL OR expires_at >= CAST(SYSUTCDATETIME() AS date)));

        IF @available < @quantity
        BEGIN
            ROLLBACK;

            DECLARE @expired int =
                (SELECT ISNULL(SUM(quantity), 0) FROM dbo.stock_batches
                 WHERE tenant_id = @p_tenant_id AND branch_id = @branch_id AND product_id = @product_id
                   AND quantity > 0 AND expires_at < CAST(SYSUTCDATETIME() AS date));

            IF @expired > 0
            BEGIN
                ;THROW 50000, 'INSUFFICIENT_STOCK: the only remaining units are expired', 1;
            END

            ;THROW 50000, 'INSUFFICIENT_STOCK: not enough units in that branch', 1;
        END

        DECLARE @remaining int = @quantity;
        DECLARE @batch_id uniqueidentifier, @batch_qty int, @batch_after int;
        DECLARE @consumed int = 0;

        -- FEFO: earliest expiry first, lots without expiry last.
        DECLARE fefo CURSOR LOCAL FAST_FORWARD FOR
            SELECT id, quantity
            FROM dbo.stock_batches WITH (UPDLOCK, ROWLOCK)
            WHERE tenant_id = @p_tenant_id AND branch_id = @branch_id AND product_id = @product_id
              AND quantity > 0
              AND (expires_at IS NULL OR expires_at >= CAST(SYSUTCDATETIME() AS date))
            ORDER BY CASE WHEN expires_at IS NULL THEN 1 ELSE 0 END, expires_at, received_at;

        OPEN fefo;
        FETCH NEXT FROM fefo INTO @batch_id, @batch_qty;

        WHILE @@FETCH_STATUS = 0 AND @remaining > 0
        BEGIN
            SET @consumed = CASE WHEN @batch_qty <= @remaining THEN @batch_qty ELSE @remaining END;
            SET @batch_after = @batch_qty - @consumed;

            UPDATE dbo.stock_batches
            SET quantity = @batch_after
            WHERE id = @batch_id;

            INSERT INTO dbo.stock_movements (tenant_id, product_id, kind, quantity, unit_cost,
                                             stock_after, reference_type, reference_id, created_by,
                                             branch_id, batch_id)
            VALUES (@p_tenant_id, @product_id, 2, -@consumed, NULL,
                    @batch_after, N'order', NULL, NULL, @branch_id, @batch_id);

            SET @remaining = @remaining - @consumed;
            FETCH NEXT FROM fefo INTO @batch_id, @batch_qty;
        END

        CLOSE fefo;
        DEALLOCATE fefo;

        UPDATE dbo.product_stock
        SET quantity = quantity - @quantity, updated_at = SYSUTCDATETIME()
        WHERE product_id = @product_id AND branch_id = @branch_id;
    END

    -- products.stock_quantity stays as the store-wide total, so the catalogue and the
    -- reports that already read it keep being correct.
    UPDATE dbo.products
    SET stock_quantity = (SELECT ISNULL(SUM(quantity), 0) FROM dbo.product_stock WHERE product_id = @product_id),
        updated_at = SYSUTCDATETIME()
    WHERE id = @product_id AND tenant_id = @p_tenant_id;

    COMMIT;

    SELECT id AS Id, name AS Name, base_price AS Price, currency AS Currency,
           stock_quantity AS StockQuantity, cost AS Cost
    FROM dbo.products
    WHERE id = @product_id AND tenant_id = @p_tenant_id;
END
GO

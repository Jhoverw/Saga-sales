-- ============================================================================
-- V003__functions_pricing.sql — Catalog: price list per customer type (D5, R9)
-- ============================================================================

-- ── sp_price_set ────────────────────────────────────────────────────────────
-- Upsert of one price for one customer type. Absence of a row means "no specific
-- price", so the base price applies.
CREATE OR ALTER PROCEDURE dbo.sp_price_set
    @p_tenant_id   uniqueidentifier,
    @product_id    uniqueidentifier,
    @customer_type smallint,
    @price         decimal(18,2)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF NOT EXISTS (SELECT 1 FROM dbo.products WHERE id = @product_id AND tenant_id = @p_tenant_id)
    BEGIN
        ;THROW 50000, 'NOT_FOUND: product does not exist for this tenant', 1;
    END

    IF @customer_type NOT IN (1, 2)
    BEGIN
        ;THROW 50000, 'VALIDATION_ERROR: unknown customer type', 1;
    END

    IF @price <= 0
    BEGIN
        ;THROW 50000, 'VALIDATION_ERROR: the price must be greater than zero', 1;
    END

    MERGE dbo.product_prices AS target
    USING (SELECT @p_tenant_id AS tenant_id, @product_id AS product_id, @customer_type AS customer_type) AS source
        ON target.tenant_id = source.tenant_id
       AND target.product_id = source.product_id
       AND target.customer_type = source.customer_type
    WHEN MATCHED THEN
        UPDATE SET price = @price
    WHEN NOT MATCHED THEN
        INSERT (id, tenant_id, product_id, customer_type, price)
        VALUES (NEWID(), @p_tenant_id, @product_id, @customer_type, @price);

    SELECT @product_id    AS ProductId,
           @customer_type AS CustomerType,
           @price         AS Price,
           2              AS Source;   -- 2 = list
END
GO

-- ── sp_price_resolve ────────────────────────────────────────────────────────
-- The single place that decides which price applies (FR-007b). When the customer type
-- has no specific price, the base price is returned with Source = 1 so the caller can
-- tell the buyer which price was used.
CREATE OR ALTER PROCEDURE dbo.sp_price_resolve
    @p_tenant_id   uniqueidentifier,
    @product_id    uniqueidentifier,
    @customer_type smallint
AS
BEGIN
    SET NOCOUNT ON;

    IF NOT EXISTS (SELECT 1 FROM dbo.products WHERE id = @product_id AND tenant_id = @p_tenant_id)
    BEGIN
        ;THROW 50000, 'NOT_FOUND: product does not exist for this tenant', 1;
    END

    SELECT p.id                       AS ProductId,
           @customer_type             AS CustomerType,
           ISNULL(pp.price, p.base_price) AS Price,
           CASE WHEN pp.price IS NULL THEN 1 ELSE 2 END AS Source
    FROM dbo.products AS p
    LEFT JOIN dbo.product_prices AS pp
           ON pp.tenant_id = p.tenant_id
          AND pp.product_id = p.id
          AND pp.customer_type = @customer_type
    WHERE p.id = @product_id
      AND p.tenant_id = @p_tenant_id;
END
GO

-- ── sp_price_list ───────────────────────────────────────────────────────────
-- Every specific price of a product, for the edit screen.
CREATE OR ALTER PROCEDURE dbo.sp_price_list
    @p_tenant_id uniqueidentifier,
    @product_id  uniqueidentifier
AS
BEGIN
    SET NOCOUNT ON;

    SELECT product_id    AS ProductId,
           customer_type AS CustomerType,
           price         AS Price,
           2             AS Source
    FROM dbo.product_prices
    WHERE tenant_id = @p_tenant_id AND product_id = @product_id
    ORDER BY customer_type;
END
GO

-- ============================================================================
-- V002__functions.sql — Analytics: upsert aggregates and read the summaries
--
-- The consumers run outside an HTTP request, so every procedure takes the tenant
-- explicitly as @p_tenant_id. Reads stay tenant-scoped: a summary for store A can
-- never include store B's rows (constitution §V).
-- ============================================================================

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

-- ── sp_report_order_upsert: an order placed adds revenue and one order ──────
CREATE OR ALTER PROCEDURE dbo.sp_report_order_upsert
    @p_tenant_id   uniqueidentifier,
    @report_date   date,
    @total_amount  decimal(18,2)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @total_amount < 0
    BEGIN
        ;THROW 50000, 'VALIDATION_ERROR: the amount cannot be negative', 1;
    END

    MERGE dbo.sales_report_daily AS target
    USING (SELECT @p_tenant_id AS tenant_id, @report_date AS report_date) AS source
        ON target.tenant_id = source.tenant_id AND target.report_date = source.report_date
    WHEN MATCHED THEN
        UPDATE SET total_revenue = total_revenue + @total_amount,
                   order_count   = order_count + 1,
                   generated_at  = SYSUTCDATETIME()
    WHEN NOT MATCHED THEN
        INSERT (tenant_id, report_date, total_revenue, order_count, payment_success_count)
        VALUES (@p_tenant_id, @report_date, @total_amount, 1, 0);
END
GO

-- ── sp_report_payment_upsert: one more successful payment on that day ───────
CREATE OR ALTER PROCEDURE dbo.sp_report_payment_upsert
    @p_tenant_id uniqueidentifier,
    @report_date date
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    MERGE dbo.sales_report_daily AS target
    USING (SELECT @p_tenant_id AS tenant_id, @report_date AS report_date) AS source
        ON target.tenant_id = source.tenant_id AND target.report_date = source.report_date
    WHEN MATCHED THEN
        UPDATE SET payment_success_count = payment_success_count + 1,
                   generated_at          = SYSUTCDATETIME()
    WHEN NOT MATCHED THEN
        INSERT (tenant_id, report_date, total_revenue, order_count, payment_success_count)
        VALUES (@p_tenant_id, @report_date, 0, 0, 1);
END
GO

-- ── sp_product_sales_upsert: units and revenue sold of one product ──────────
CREATE OR ALTER PROCEDURE dbo.sp_product_sales_upsert
    @p_tenant_id  uniqueidentifier,
    @report_date  date,
    @product_id   uniqueidentifier,
    @product_name nvarchar(255),
    @quantity     int,
    @revenue      decimal(18,2)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @quantity <= 0
    BEGIN
        ;THROW 50000, 'VALIDATION_ERROR: the quantity must be greater than zero', 1;
    END

    MERGE dbo.product_sales_daily AS target
    USING (SELECT @p_tenant_id AS tenant_id, @report_date AS report_date, @product_id AS product_id) AS source
        ON target.tenant_id = source.tenant_id
       AND target.report_date = source.report_date
       AND target.product_id = source.product_id
    WHEN MATCHED THEN
        UPDATE SET units_sold = units_sold + @quantity,
                   revenue    = revenue + @revenue,
                   product_name = @product_name
    WHEN NOT MATCHED THEN
        INSERT (tenant_id, report_date, product_id, product_name, units_sold, revenue)
        VALUES (@p_tenant_id, @report_date, @product_id, @product_name, @quantity, @revenue);
END
GO

-- ── sp_activity_add: append one customer event ──────────────────────────────
CREATE OR ALTER PROCEDURE dbo.sp_activity_add
    @p_tenant_id uniqueidentifier,
    @customer_id uniqueidentifier,
    @event_type  nvarchar(100),
    @occurred_at datetime2(3) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO dbo.customer_activity (tenant_id, customer_id, event_type, occurred_at)
    VALUES (@p_tenant_id, @customer_id, @event_type, ISNULL(@occurred_at, SYSUTCDATETIME()));
END
GO

-- ── sp_sales_summary_get: revenue, orders and payments for a range ──────────
CREATE OR ALTER PROCEDURE dbo.sp_sales_summary_get
    @p_tenant_id uniqueidentifier,
    @from_date   date,
    @to_date     date
AS
BEGIN
    SET NOCOUNT ON;

    SELECT ISNULL(SUM(total_revenue), 0)         AS TotalRevenue,
           ISNULL(SUM(order_count), 0)           AS OrderCount,
           ISNULL(SUM(payment_success_count), 0) AS PaymentSuccessCount
    FROM dbo.sales_report_daily
    WHERE tenant_id = @p_tenant_id
      AND report_date >= @from_date
      AND report_date <= @to_date;
END
GO

-- ── sp_top_products_get: best sellers by revenue for a range ────────────────
CREATE OR ALTER PROCEDURE dbo.sp_top_products_get
    @p_tenant_id uniqueidentifier,
    @from_date   date,
    @to_date     date,
    @top_n       int = 5
AS
BEGIN
    SET NOCOUNT ON;

    SET @top_n = CASE WHEN ISNULL(@top_n, 5) < 1 THEN 5
                      WHEN @top_n > 50 THEN 50
                      ELSE @top_n END;

    SELECT TOP (@top_n)
           product_id   AS ProductId,
           MAX(product_name) AS ProductName,
           SUM(units_sold)   AS UnitsSold,
           SUM(revenue)      AS Revenue
    FROM dbo.product_sales_daily
    WHERE tenant_id = @p_tenant_id
      AND report_date >= @from_date
      AND report_date <= @to_date
    GROUP BY product_id
    ORDER BY SUM(revenue) DESC, SUM(units_sold) DESC;
END
GO

-- ── sp_daily_series_get: one row per day for the chart ─────────────────────
CREATE OR ALTER PROCEDURE dbo.sp_daily_series_get
    @p_tenant_id uniqueidentifier,
    @from_date   date,
    @to_date     date
AS
BEGIN
    SET NOCOUNT ON;

    SELECT report_date           AS Date,
           total_revenue         AS Revenue,
           order_count           AS Orders
    FROM dbo.sales_report_daily
    WHERE tenant_id = @p_tenant_id
      AND report_date >= @from_date
      AND report_date <= @to_date
    ORDER BY report_date;
END
GO

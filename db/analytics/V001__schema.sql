-- ============================================================================
-- V001__schema.sql — Analytics service (saga_analytics)
-- Daily aggregates for the store panel (E-06 · US-013/US-014). Built exclusively
-- from integration events, never from another service's tables.
-- ============================================================================

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

IF OBJECT_ID('dbo.schema_version') IS NULL
CREATE TABLE dbo.schema_version
(
    version    int           NOT NULL CONSTRAINT pk_schema_version PRIMARY KEY,
    script     nvarchar(200) NOT NULL,
    applied_at datetime2(3)  NOT NULL CONSTRAINT df_schema_version_applied DEFAULT SYSUTCDATETIME()
);
GO

-- ── sales_report_daily: one row per tenant per day ─────────────────────────
IF OBJECT_ID('dbo.sales_report_daily') IS NULL
CREATE TABLE dbo.sales_report_daily
(
    id                    uniqueidentifier NOT NULL CONSTRAINT pk_sales_report_daily PRIMARY KEY DEFAULT NEWID(),
    tenant_id             uniqueidentifier NOT NULL,
    report_date           date              NOT NULL,
    total_revenue         decimal(18,2)     NOT NULL CONSTRAINT df_sales_report_revenue DEFAULT 0,
    order_count           int               NOT NULL CONSTRAINT df_sales_report_orders DEFAULT 0,
    payment_success_count int               NOT NULL CONSTRAINT df_sales_report_payments DEFAULT 0,
    generated_at          datetime2(3)      NOT NULL CONSTRAINT df_sales_report_generated DEFAULT SYSUTCDATETIME()
);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'ux_sales_report_daily' AND object_id = OBJECT_ID('dbo.sales_report_daily'))
    CREATE UNIQUE INDEX ux_sales_report_daily ON dbo.sales_report_daily (tenant_id, report_date);
GO

-- ── product_sales_daily: units and revenue per product per day ──────────────
IF OBJECT_ID('dbo.product_sales_daily') IS NULL
CREATE TABLE dbo.product_sales_daily
(
    id           uniqueidentifier NOT NULL CONSTRAINT pk_product_sales_daily PRIMARY KEY DEFAULT NEWID(),
    tenant_id    uniqueidentifier NOT NULL,
    report_date  date              NOT NULL,
    product_id   uniqueidentifier NOT NULL,
    product_name nvarchar(255)    NOT NULL,
    units_sold   int              NOT NULL CONSTRAINT df_product_sales_units DEFAULT 0,
    revenue      decimal(18,2)    NOT NULL CONSTRAINT df_product_sales_revenue DEFAULT 0
);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'ux_product_sales_daily' AND object_id = OBJECT_ID('dbo.product_sales_daily'))
    CREATE UNIQUE INDEX ux_product_sales_daily ON dbo.product_sales_daily (tenant_id, report_date, product_id);
GO

-- ── customer_activity: an append-only feed ─────────────────────────────────
IF OBJECT_ID('dbo.customer_activity') IS NULL
CREATE TABLE dbo.customer_activity
(
    id          uniqueidentifier NOT NULL CONSTRAINT pk_customer_activity PRIMARY KEY DEFAULT NEWID(),
    tenant_id   uniqueidentifier NOT NULL,
    customer_id uniqueidentifier NOT NULL,
    event_type  nvarchar(100)    NOT NULL,
    occurred_at datetime2(3)     NOT NULL CONSTRAINT df_customer_activity_occurred DEFAULT SYSUTCDATETIME()
);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'ix_customer_activity_tenant_occurred' AND object_id = OBJECT_ID('dbo.customer_activity'))
    CREATE INDEX ix_customer_activity_tenant_occurred ON dbo.customer_activity (tenant_id, occurred_at);
GO

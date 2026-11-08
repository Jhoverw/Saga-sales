-- ============================================================================
-- V018__tax_tenant.sql — Sales: tax rates are per store, not global
--
-- V017 created tax_rates without tenant_id and seeded two global rows. That breaks §V: every
-- store shared the same IVA periods. This version scopes the table to a tenant, removes the
-- global seed rows (they cannot belong to one store) and indexes the read path.
-- ============================================================================

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

IF COL_LENGTH('dbo.tax_rates', 'tenant_id') IS NULL
    ALTER TABLE dbo.tax_rates ADD tenant_id uniqueidentifier NULL;
GO

-- The V017 seeds are global; they cannot be assigned to a single store.
DELETE FROM dbo.tax_rates WHERE tenant_id IS NULL;
GO

ALTER TABLE dbo.tax_rates ALTER COLUMN tenant_id uniqueidentifier NOT NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'ix_tax_rates_tenant' AND object_id = OBJECT_ID('dbo.tax_rates'))
    CREATE INDEX ix_tax_rates_tenant ON dbo.tax_rates (tenant_id, start_date DESC);
GO

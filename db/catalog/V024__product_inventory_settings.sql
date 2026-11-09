-- ============================================================================
-- V024__product_inventory_settings.sql — Catalog: data for inventory control (spec 004, FR-001)
-- All optional. Each step checks the current state, so the script can run twice.
-- ============================================================================

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

-- Item number shown on the report; unique inside a store when present.
IF COL_LENGTH('dbo.products', 'code') IS NULL
    ALTER TABLE dbo.products ADD code nvarchar(50) NULL;
GO

IF COL_LENGTH('dbo.products', 'manufacturer') IS NULL
    ALTER TABLE dbo.products ADD manufacturer nvarchar(150) NULL;
GO

-- Restock when stock <= reorder_level; how much to order and how many days it takes to arrive.
IF COL_LENGTH('dbo.products', 'reorder_level') IS NULL
    ALTER TABLE dbo.products ADD reorder_level int NULL;
GO

IF COL_LENGTH('dbo.products', 'reorder_quantity') IS NULL
    ALTER TABLE dbo.products ADD reorder_quantity int NULL;
GO

IF COL_LENGTH('dbo.products', 'lead_time_days') IS NULL
    ALTER TABLE dbo.products ADD lead_time_days int NULL;
GO

IF COL_LENGTH('dbo.products', 'inventory_notes') IS NULL
    ALTER TABLE dbo.products ADD inventory_notes nvarchar(500) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'ck_products_inventory_settings')
    ALTER TABLE dbo.products ADD CONSTRAINT ck_products_inventory_settings
        CHECK ((reorder_level IS NULL OR reorder_level >= 0)
           AND (reorder_quantity IS NULL OR reorder_quantity >= 0)
           AND (lead_time_days IS NULL OR lead_time_days >= 0));
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'ux_products_tenant_code' AND object_id = OBJECT_ID('dbo.products'))
    CREATE UNIQUE INDEX ux_products_tenant_code ON dbo.products (tenant_id, code) WHERE code IS NOT NULL;
GO

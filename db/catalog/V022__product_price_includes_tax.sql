-- ============================================================================
-- V022__product_price_includes_tax.sql — Catalog: the price is the PVP (spec 003, FR-004)
-- Existing products keep price_includes_tax = 0 (price without VAT) until
-- scripts/convert-prices-to-pvp.ps1 converts them; new products are created with 1.
-- Each step checks the current state, so the script can run twice.
-- ============================================================================

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

-- Step 1: add the column; every existing row gets 0 through a temporary default.
IF COL_LENGTH('dbo.products', 'price_includes_tax') IS NULL
    ALTER TABLE dbo.products ADD price_includes_tax bit NOT NULL
        CONSTRAINT df_products_price_includes_tax_legacy DEFAULT 0;
GO

-- Step 2: drop the temporary default.
IF EXISTS (SELECT 1 FROM sys.default_constraints WHERE name = 'df_products_price_includes_tax_legacy')
    ALTER TABLE dbo.products DROP CONSTRAINT df_products_price_includes_tax_legacy;
GO

-- Step 3: from now on a new product's price is the PVP.
IF NOT EXISTS (SELECT 1 FROM sys.default_constraints WHERE name = 'df_products_price_includes_tax')
    ALTER TABLE dbo.products ADD CONSTRAINT df_products_price_includes_tax DEFAULT 1 FOR price_includes_tax;
GO

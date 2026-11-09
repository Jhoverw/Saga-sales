-- ============================================================================
-- V003__customer_identification.sql — Customer: identification, business name, optional email
-- Spec 002 (FR-003, FR-006, FR-007, FR-008). Every step checks the current state, so the script
-- can run twice.
-- ============================================================================

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

-- SRI identification type: 4 RUC · 5 cédula · 6 pasaporte · 7 consumidor final · 8 exterior.
IF COL_LENGTH('dbo.customers', 'identification_type') IS NULL
    ALTER TABLE dbo.customers ADD identification_type smallint NULL;
GO

IF COL_LENGTH('dbo.customers', 'identification_number') IS NULL
    ALTER TABLE dbo.customers ADD identification_number nvarchar(20) NULL;
GO

-- Companies (RUC whose third digit is 6 or 9) are stored by business name instead of person names.
IF COL_LENGTH('dbo.customers', 'business_name') IS NULL
    ALTER TABLE dbo.customers ADD business_name nvarchar(300) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'ck_customers_identification_type')
    ALTER TABLE dbo.customers ADD CONSTRAINT ck_customers_identification_type
        CHECK (identification_type IS NULL OR identification_type BETWEEN 4 AND 8);
GO

-- Type and number travel together: one without the other cannot be put on an invoice.
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'ck_customers_identification_pair')
    ALTER TABLE dbo.customers ADD CONSTRAINT ck_customers_identification_pair
        CHECK ((identification_type IS NULL AND identification_number IS NULL)
            OR (identification_type IS NOT NULL AND identification_number IS NOT NULL));
GO

-- A person needs first and last name; a company needs a business name.
IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'ck_customers_names')
    ALTER TABLE dbo.customers DROP CONSTRAINT ck_customers_names;
GO

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'ck_customers_names_or_business')
    ALTER TABLE dbo.customers ADD CONSTRAINT ck_customers_names_or_business
        CHECK ((business_name IS NOT NULL AND LEN(LTRIM(RTRIM(business_name))) > 0)
            OR (LEN(LTRIM(RTRIM(first_name))) > 0 AND LEN(LTRIM(RTRIM(last_name))) > 0));
GO

-- Email becomes optional (quick sign-up at the till). It stays unique per store when present.
IF EXISTS (SELECT 1 FROM sys.indexes
           WHERE name = 'ux_customers_tenant_email' AND object_id = OBJECT_ID('dbo.customers')
             AND has_filter = 0)
    DROP INDEX ux_customers_tenant_email ON dbo.customers;
GO

IF EXISTS (SELECT 1 FROM sys.columns
           WHERE object_id = OBJECT_ID('dbo.customers') AND name = 'email' AND is_nullable = 0)
    ALTER TABLE dbo.customers ALTER COLUMN email nvarchar(255) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'ux_customers_tenant_email' AND object_id = OBJECT_ID('dbo.customers'))
    CREATE UNIQUE INDEX ux_customers_tenant_email ON dbo.customers (tenant_id, email)
        WHERE email IS NOT NULL;
GO

-- One customer per identification inside a store (FR-006).
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'ux_customers_tenant_identification' AND object_id = OBJECT_ID('dbo.customers'))
    CREATE UNIQUE INDEX ux_customers_tenant_identification
        ON dbo.customers (tenant_id, identification_type, identification_number)
        WHERE identification_number IS NOT NULL;
GO

-- The till's "Consumidor final" (created by Caja with this email) gets the SRI identification.
UPDATE dbo.customers
SET identification_type = 7, identification_number = N'9999999999999', updated_at = SYSUTCDATETIME()
WHERE email = N'consumidor.final@caja.local' AND identification_number IS NULL;
GO

-- ============================================================================
-- V001__schema.sql — Catalog service (saga_catalog)
-- Seller's assortment: categories, products and the price list per customer type.
-- ============================================================================

IF OBJECT_ID('dbo.schema_version') IS NULL
CREATE TABLE dbo.schema_version
(
    version    int           NOT NULL CONSTRAINT pk_schema_version PRIMARY KEY,
    script     nvarchar(200) NOT NULL,
    applied_at datetime2(3)  NOT NULL CONSTRAINT df_schema_version_applied DEFAULT SYSUTCDATETIME()
);
GO

-- ── categories ──────────────────────────────────────────────────────────────
IF OBJECT_ID('dbo.categories') IS NULL
CREATE TABLE dbo.categories
(
    id         uniqueidentifier NOT NULL CONSTRAINT pk_categories PRIMARY KEY DEFAULT NEWID(),
    tenant_id  uniqueidentifier NOT NULL,
    name       nvarchar(100)    NOT NULL,
    slug       nvarchar(120)    NULL,
    parent_id  uniqueidentifier NULL,
    created_at datetime2(3)     NOT NULL CONSTRAINT df_categories_created DEFAULT SYSUTCDATETIME(),

    CONSTRAINT fk_categories_parent FOREIGN KEY (parent_id) REFERENCES dbo.categories (id),
    CONSTRAINT ck_categories_name CHECK (LEN(LTRIM(RTRIM(name))) BETWEEN 1 AND 100)
);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'ux_categories_tenant_name' AND object_id = OBJECT_ID('dbo.categories'))
    CREATE UNIQUE INDEX ux_categories_tenant_name ON dbo.categories (tenant_id, name);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'ux_categories_tenant_slug' AND object_id = OBJECT_ID('dbo.categories'))
    CREATE UNIQUE INDEX ux_categories_tenant_slug ON dbo.categories (tenant_id, slug) WHERE slug IS NOT NULL;
GO

-- ── products ────────────────────────────────────────────────────────────────
IF OBJECT_ID('dbo.products') IS NULL
CREATE TABLE dbo.products
(
    id             uniqueidentifier NOT NULL CONSTRAINT pk_products PRIMARY KEY DEFAULT NEWID(),
    tenant_id      uniqueidentifier NOT NULL,
    name           nvarchar(255)    NOT NULL,
    description    nvarchar(max)    NULL,
    base_price     decimal(18,2)    NOT NULL,
    currency       char(3)          NOT NULL CONSTRAINT df_products_currency DEFAULT 'USD',
    stock_quantity int              NOT NULL CONSTRAINT df_products_stock DEFAULT 0,
    category_id    uniqueidentifier NULL,
    status         smallint         NOT NULL CONSTRAINT df_products_status DEFAULT 2,
    fiscal_note    nvarchar(255)    NULL,   -- reserved for the separate invoicing project
    created_at     datetime2(3)     NOT NULL CONSTRAINT df_products_created DEFAULT SYSUTCDATETIME(),
    updated_at     datetime2(3)     NOT NULL CONSTRAINT df_products_updated DEFAULT SYSUTCDATETIME(),
    created_by     uniqueidentifier NOT NULL,

    -- the same rules the API validates, enforced by the database as a last line of defence
    CONSTRAINT ck_products_name CHECK (LEN(LTRIM(RTRIM(name))) >= 2),
    CONSTRAINT ck_products_price CHECK (base_price > 0),
    CONSTRAINT ck_products_stock CHECK (stock_quantity >= 0),
    CONSTRAINT ck_products_currency CHECK (currency = 'USD'),
    -- 1 draft · 2 active · 3 inactive · 4 out_of_stock · 5 discontinued
    CONSTRAINT ck_products_status CHECK (status IN (1, 2, 3, 4, 5)),

    CONSTRAINT fk_products_categories FOREIGN KEY (category_id) REFERENCES dbo.categories (id)
);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'ix_products_tenant_name' AND object_id = OBJECT_ID('dbo.products'))
    CREATE INDEX ix_products_tenant_name ON dbo.products (tenant_id, name);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'ix_products_tenant_status' AND object_id = OBJECT_ID('dbo.products'))
    CREATE INDEX ix_products_tenant_status ON dbo.products (tenant_id, status);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'ix_products_tenant_category' AND object_id = OBJECT_ID('dbo.products'))
    CREATE INDEX ix_products_tenant_category ON dbo.products (tenant_id, category_id);
GO

-- ── product_prices: one price per (product, customer type) ──────────────────
IF OBJECT_ID('dbo.product_prices') IS NULL
CREATE TABLE dbo.product_prices
(
    id            uniqueidentifier NOT NULL CONSTRAINT pk_product_prices PRIMARY KEY DEFAULT NEWID(),
    tenant_id     uniqueidentifier NOT NULL,
    product_id    uniqueidentifier NOT NULL,
    customer_type smallint         NOT NULL,
    price         decimal(18,2)    NOT NULL,

    CONSTRAINT fk_product_prices_products FOREIGN KEY (product_id)
        REFERENCES dbo.products (id) ON DELETE CASCADE,
    -- 1 retail · 2 wholesale
    CONSTRAINT ck_product_prices_type CHECK (customer_type IN (1, 2)),
    CONSTRAINT ck_product_prices_price CHECK (price > 0)
);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'ux_product_prices' AND object_id = OBJECT_ID('dbo.product_prices'))
    CREATE UNIQUE INDEX ux_product_prices ON dbo.product_prices (tenant_id, product_id, customer_type);
GO

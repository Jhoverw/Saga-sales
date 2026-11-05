-- ============================================================================
-- V008__schema_inventory.sql — Catalog: branches, per-branch stock, lots and location
--
-- Until now one number per product described the whole store. This adds the physical truth:
-- WHERE the stock is (branch, floor, aisle, shelf) and WHEN it expires (lot + date).
--
-- Migration is additive and lossless:
--   1. every tenant with products gets a "Principal" branch,
--   2. products.stock_quantity is copied into that branch's product_stock,
--   3. products.stock_quantity is left untouched for one more version, so nothing breaks if the
--      new procedures are not in place yet.
-- ============================================================================

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

-- ── branches ────────────────────────────────────────────────────────────────
IF OBJECT_ID('dbo.branches') IS NULL
CREATE TABLE dbo.branches
(
    id         uniqueidentifier NOT NULL CONSTRAINT pk_branches PRIMARY KEY DEFAULT NEWID(),
    tenant_id  uniqueidentifier NOT NULL,
    name       nvarchar(120)    NOT NULL,
    address    nvarchar(300)    NULL,
    phone      nvarchar(30)     NULL,
    is_default bit              NOT NULL CONSTRAINT df_branches_default DEFAULT 0,
    status     smallint         NOT NULL CONSTRAINT df_branches_status DEFAULT 1,
    created_at datetime2(3)     NOT NULL CONSTRAINT df_branches_created DEFAULT SYSUTCDATETIME(),

    CONSTRAINT ck_branches_name CHECK (LEN(LTRIM(RTRIM(name))) >= 2),
    -- 1 active · 2 inactive
    CONSTRAINT ck_branches_status CHECK (status IN (1, 2))
);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'ux_branches_tenant_name' AND object_id = OBJECT_ID('dbo.branches'))
    CREATE UNIQUE INDEX ux_branches_tenant_name ON dbo.branches (tenant_id, name);
GO

-- At most one default branch per store.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'ux_branches_tenant_default' AND object_id = OBJECT_ID('dbo.branches'))
    CREATE UNIQUE INDEX ux_branches_tenant_default ON dbo.branches (tenant_id) WHERE is_default = 1;
GO

-- ── tenant_settings: the "Parámetros" screen ────────────────────────────────
IF OBJECT_ID('dbo.tenant_settings') IS NULL
CREATE TABLE dbo.tenant_settings
(
    tenant_id           uniqueidentifier NOT NULL CONSTRAINT pk_tenant_settings PRIMARY KEY,
    -- how many days before expiry a lot starts appearing in the "expiring soon" list
    expiry_warning_days int              NOT NULL CONSTRAINT df_tenant_settings_expiry DEFAULT 30,
    default_branch_id   uniqueidentifier NULL,
    updated_at          datetime2(3)     NOT NULL CONSTRAINT df_tenant_settings_updated DEFAULT SYSUTCDATETIME(),

    CONSTRAINT fk_tenant_settings_branch FOREIGN KEY (default_branch_id) REFERENCES dbo.branches (id),
    CONSTRAINT ck_tenant_settings_expiry CHECK (expiry_warning_days BETWEEN 1 AND 365)
);
GO

-- ── products: does it carry lots, and does it have a picture? ───────────────
IF COL_LENGTH('dbo.products', 'tracks_batches') IS NULL
    ALTER TABLE dbo.products ADD tracks_batches bit NOT NULL CONSTRAINT df_products_tracks_batches DEFAULT 0;
GO

IF COL_LENGTH('dbo.products', 'image_path') IS NULL
    ALTER TABLE dbo.products ADD image_path nvarchar(300) NULL;
GO

-- ── product_stock: how much of each product sits in each branch ─────────────
-- The physical location lives here because it is per product AND per branch:
-- the same soda is in aisle 3 at the Centro branch and in aisle 1 at Norte.
IF OBJECT_ID('dbo.product_stock') IS NULL
CREATE TABLE dbo.product_stock
(
    product_id uniqueidentifier NOT NULL,
    branch_id  uniqueidentifier NOT NULL,
    tenant_id  uniqueidentifier NOT NULL,
    quantity   int              NOT NULL CONSTRAINT df_product_stock_quantity DEFAULT 0,
    floor      nvarchar(30)     NULL,   -- piso
    aisle      nvarchar(30)     NULL,   -- pasillo
    shelf      nvarchar(30)     NULL,   -- estanteria
    updated_at datetime2(3)     NOT NULL CONSTRAINT df_product_stock_updated DEFAULT SYSUTCDATETIME(),

    CONSTRAINT pk_product_stock PRIMARY KEY (product_id, branch_id),
    CONSTRAINT fk_product_stock_products FOREIGN KEY (product_id)
        REFERENCES dbo.products (id) ON DELETE CASCADE,
    CONSTRAINT fk_product_stock_branches FOREIGN KEY (branch_id)
        REFERENCES dbo.branches (id),
    CONSTRAINT ck_product_stock_quantity CHECK (quantity >= 0)
);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'ix_product_stock_branch' AND object_id = OBJECT_ID('dbo.product_stock'))
    CREATE INDEX ix_product_stock_branch ON dbo.product_stock (tenant_id, branch_id);
GO

-- ── stock_batches: the lot, its expiry and what is left of it ───────────────
IF OBJECT_ID('dbo.stock_batches') IS NULL
CREATE TABLE dbo.stock_batches
(
    id          uniqueidentifier NOT NULL CONSTRAINT pk_stock_batches PRIMARY KEY DEFAULT NEWID(),
    tenant_id   uniqueidentifier NOT NULL,
    branch_id   uniqueidentifier NOT NULL,
    product_id  uniqueidentifier NOT NULL,
    lot_code    nvarchar(60)     NOT NULL,
    expires_at  date             NULL,   -- null: the product does not expire
    quantity    int              NOT NULL CONSTRAINT df_stock_batches_quantity DEFAULT 0,
    unit_cost   decimal(18,4)    NULL,
    received_at datetime2(3)     NOT NULL CONSTRAINT df_stock_batches_received DEFAULT SYSUTCDATETIME(),

    CONSTRAINT fk_stock_batches_branches FOREIGN KEY (branch_id) REFERENCES dbo.branches (id),
    CONSTRAINT fk_stock_batches_products FOREIGN KEY (product_id)
        REFERENCES dbo.products (id) ON DELETE CASCADE,
    CONSTRAINT ck_stock_batches_quantity CHECK (quantity >= 0),
    CONSTRAINT ck_stock_batches_lot CHECK (LEN(LTRIM(RTRIM(lot_code))) > 0)
);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'ux_stock_batches_lot' AND object_id = OBJECT_ID('dbo.stock_batches'))
    CREATE UNIQUE INDEX ux_stock_batches_lot ON dbo.stock_batches (tenant_id, branch_id, product_id, lot_code);
GO

-- FEFO needs to find the oldest expiry first.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'ix_stock_batches_fefo' AND object_id = OBJECT_ID('dbo.stock_batches'))
    CREATE INDEX ix_stock_batches_fefo ON dbo.stock_batches (tenant_id, branch_id, product_id, expires_at)
        INCLUDE (quantity);
GO

-- ── stock_movements: the kardex now knows where and from which lot ──────────
IF COL_LENGTH('dbo.stock_movements', 'branch_id') IS NULL
    ALTER TABLE dbo.stock_movements ADD branch_id uniqueidentifier NULL;
GO

IF COL_LENGTH('dbo.stock_movements', 'batch_id') IS NULL
    ALTER TABLE dbo.stock_movements ADD batch_id uniqueidentifier NULL;
GO

-- ── migration: one default branch per tenant, current stock carried over ────
INSERT INTO dbo.branches (id, tenant_id, name, is_default, status)
SELECT NEWID(), t.tenant_id, N'Principal', 1, 1
FROM (SELECT DISTINCT tenant_id FROM dbo.products) AS t
WHERE NOT EXISTS (SELECT 1 FROM dbo.branches AS b WHERE b.tenant_id = t.tenant_id);
GO

INSERT INTO dbo.tenant_settings (tenant_id, expiry_warning_days, default_branch_id)
SELECT b.tenant_id, 30, b.id
FROM dbo.branches AS b
WHERE b.is_default = 1
  AND NOT EXISTS (SELECT 1 FROM dbo.tenant_settings AS s WHERE s.tenant_id = b.tenant_id);
GO

INSERT INTO dbo.product_stock (product_id, branch_id, tenant_id, quantity)
SELECT p.id, b.id, p.tenant_id, p.stock_quantity
FROM dbo.products AS p
INNER JOIN dbo.branches AS b ON b.tenant_id = p.tenant_id AND b.is_default = 1
WHERE NOT EXISTS (
    SELECT 1 FROM dbo.product_stock AS s WHERE s.product_id = p.id AND s.branch_id = b.id
);
GO

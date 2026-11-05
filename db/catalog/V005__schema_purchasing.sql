-- ============================================================================
-- V005__schema_purchasing.sql — Catalog: what the store buys from its suppliers
--
-- The product only knew its selling price (PVP). This adds the real cost and the document
-- that brings stock in: a purchase with its lines, plus a kardex that explains every move.
-- ============================================================================

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

-- ── products.cost ───────────────────────────────────────────────────────────
-- NULL means "cost unknown": the product has never been received through a purchase.
-- 4 decimals because a weighted average of 0.35 and 0.40 is 0.375.
IF COL_LENGTH('dbo.products', 'cost') IS NULL
    ALTER TABLE dbo.products ADD cost decimal(18,4) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'ck_products_cost')
    ALTER TABLE dbo.products ADD CONSTRAINT ck_products_cost CHECK (cost IS NULL OR cost >= 0);
GO

-- ── suppliers ───────────────────────────────────────────────────────────────
IF OBJECT_ID('dbo.suppliers') IS NULL
CREATE TABLE dbo.suppliers
(
    id           uniqueidentifier NOT NULL CONSTRAINT pk_suppliers PRIMARY KEY DEFAULT NEWID(),
    tenant_id    uniqueidentifier NOT NULL,
    name         nvarchar(200)    NOT NULL,
    tax_id       nvarchar(30)     NULL,   -- RUC / identification, unique per store when present
    contact_name nvarchar(150)    NULL,
    email        nvarchar(255)    NULL,
    phone        nvarchar(30)     NULL,
    notes        nvarchar(max)    NULL,
    status       smallint         NOT NULL CONSTRAINT df_suppliers_status DEFAULT 1,
    created_at   datetime2(3)     NOT NULL CONSTRAINT df_suppliers_created DEFAULT SYSUTCDATETIME(),
    updated_at   datetime2(3)     NOT NULL CONSTRAINT df_suppliers_updated DEFAULT SYSUTCDATETIME(),

    CONSTRAINT ck_suppliers_name CHECK (LEN(LTRIM(RTRIM(name))) >= 2),
    -- 1 active · 2 inactive
    CONSTRAINT ck_suppliers_status CHECK (status IN (1, 2))
);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'ix_suppliers_tenant_name' AND object_id = OBJECT_ID('dbo.suppliers'))
    CREATE INDEX ix_suppliers_tenant_name ON dbo.suppliers (tenant_id, name);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'ux_suppliers_tenant_taxid' AND object_id = OBJECT_ID('dbo.suppliers'))
    CREATE UNIQUE INDEX ux_suppliers_tenant_taxid ON dbo.suppliers (tenant_id, tax_id) WHERE tax_id IS NOT NULL;
GO

-- ── purchases ───────────────────────────────────────────────────────────────
IF OBJECT_ID('dbo.purchases') IS NULL
CREATE TABLE dbo.purchases
(
    id                      uniqueidentifier NOT NULL CONSTRAINT pk_purchases PRIMARY KEY DEFAULT NEWID(),
    tenant_id               uniqueidentifier NOT NULL,
    supplier_id             uniqueidentifier NOT NULL,
    status                  smallint         NOT NULL CONSTRAINT df_purchases_status DEFAULT 1,
    supplier_invoice_number nvarchar(60)     NULL,
    purchased_at            datetime2(3)     NOT NULL CONSTRAINT df_purchases_date DEFAULT SYSUTCDATETIME(),
    received_at             datetime2(3)     NULL,
    subtotal                decimal(18,2)    NOT NULL CONSTRAINT df_purchases_subtotal DEFAULT 0,
    tax_amount              decimal(18,2)    NOT NULL CONSTRAINT df_purchases_tax DEFAULT 0,
    total                   decimal(18,2)    NOT NULL CONSTRAINT df_purchases_total DEFAULT 0,
    notes                   nvarchar(max)    NULL,
    created_by              uniqueidentifier NOT NULL,

    CONSTRAINT fk_purchases_suppliers FOREIGN KEY (supplier_id) REFERENCES dbo.suppliers (id),
    -- 1 draft · 2 received · 3 cancelled
    CONSTRAINT ck_purchases_status CHECK (status IN (1, 2, 3)),
    CONSTRAINT ck_purchases_total CHECK (total >= 0)
);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'ix_purchases_tenant_date' AND object_id = OBJECT_ID('dbo.purchases'))
    CREATE INDEX ix_purchases_tenant_date ON dbo.purchases (tenant_id, purchased_at DESC);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'ix_purchases_tenant_supplier' AND object_id = OBJECT_ID('dbo.purchases'))
    CREATE INDEX ix_purchases_tenant_supplier ON dbo.purchases (tenant_id, supplier_id);
GO

-- ── purchase_lines ──────────────────────────────────────────────────────────
IF OBJECT_ID('dbo.purchase_lines') IS NULL
CREATE TABLE dbo.purchase_lines
(
    id                  uniqueidentifier NOT NULL CONSTRAINT pk_purchase_lines PRIMARY KEY DEFAULT NEWID(),
    tenant_id           uniqueidentifier NOT NULL,
    purchase_id         uniqueidentifier NOT NULL,
    product_id          uniqueidentifier NOT NULL,
    product_name        nvarchar(255)    NOT NULL,   -- snapshot
    quantity            int              NOT NULL,
    -- the cost exactly as the supplier quoted it
    unit_cost           decimal(18,2)    NOT NULL,
    -- true: 0.35 already includes tax · false: tax is added on top
    cost_includes_tax   bit              NOT NULL CONSTRAINT df_purchase_lines_incl DEFAULT 1,
    tax_rate            decimal(5,2)     NULL,
    -- normalised, always tax-included, used for the weighted average cost
    effective_unit_cost decimal(18,4)    NOT NULL,
    line_total          decimal(18,2)    NOT NULL,   -- quantity x effective_unit_cost

    CONSTRAINT fk_purchase_lines_purchases FOREIGN KEY (purchase_id)
        REFERENCES dbo.purchases (id) ON DELETE CASCADE,
    CONSTRAINT ck_purchase_lines_quantity CHECK (quantity > 0),
    CONSTRAINT ck_purchase_lines_cost CHECK (unit_cost > 0),
    CONSTRAINT ck_purchase_lines_effective CHECK (effective_unit_cost > 0)
);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'ix_purchase_lines_purchase' AND object_id = OBJECT_ID('dbo.purchase_lines'))
    CREATE INDEX ix_purchase_lines_purchase ON dbo.purchase_lines (purchase_id);
GO

-- ── stock_movements: the kardex ─────────────────────────────────────────────
IF OBJECT_ID('dbo.stock_movements') IS NULL
CREATE TABLE dbo.stock_movements
(
    id             uniqueidentifier NOT NULL CONSTRAINT pk_stock_movements PRIMARY KEY DEFAULT NEWID(),
    tenant_id      uniqueidentifier NOT NULL,
    product_id     uniqueidentifier NOT NULL,
    -- 1 purchase in · 2 sale out · 3 adjustment · 4 return in
    kind           smallint         NOT NULL,
    -- signed: positive brings stock in, negative takes it out
    quantity       int              NOT NULL,
    unit_cost      decimal(18,4)    NULL,   -- cost of the units that entered
    stock_after    int              NOT NULL,
    reference_type nvarchar(30)     NULL,   -- 'purchase' · 'order' · 'adjustment'
    reference_id   uniqueidentifier NULL,
    occurred_at    datetime2(3)     NOT NULL CONSTRAINT df_stock_movements_date DEFAULT SYSUTCDATETIME(),
    created_by     uniqueidentifier NULL,

    CONSTRAINT fk_stock_movements_products FOREIGN KEY (product_id)
        REFERENCES dbo.products (id),
    CONSTRAINT ck_stock_movements_kind CHECK (kind IN (1, 2, 3, 4)),
    CONSTRAINT ck_stock_movements_quantity CHECK (quantity <> 0),
    CONSTRAINT ck_stock_movements_after CHECK (stock_after >= 0)
);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'ix_stock_movements_product' AND object_id = OBJECT_ID('dbo.stock_movements'))
    CREATE INDEX ix_stock_movements_product ON dbo.stock_movements (tenant_id, product_id, occurred_at DESC);
GO

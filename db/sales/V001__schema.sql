-- ============================================================================
-- V001__schema.sql — Sales service (saga_sales)
-- Orders and their lines. The line keeps the product's name and price as they were at
-- purchase time: retiring a product must never rewrite history (FR-012).
-- ============================================================================

IF OBJECT_ID('dbo.schema_version') IS NULL
CREATE TABLE dbo.schema_version
(
    version    int           NOT NULL CONSTRAINT pk_schema_version PRIMARY KEY,
    script     nvarchar(200) NOT NULL,
    applied_at datetime2(3)  NOT NULL CONSTRAINT df_schema_version_applied DEFAULT SYSUTCDATETIME()
);
GO

IF OBJECT_ID('dbo.orders') IS NULL
CREATE TABLE dbo.orders
(
    id                     uniqueidentifier NOT NULL CONSTRAINT pk_orders PRIMARY KEY DEFAULT NEWID(),
    tenant_id              uniqueidentifier NOT NULL,
    -- the customer being served, not the staff user placing the order (D8)
    customer_id            uniqueidentifier NOT NULL,
    status                 smallint         NOT NULL CONSTRAINT df_orders_status DEFAULT 1,
    subtotal               decimal(18,2)    NOT NULL,
    paid_amount            decimal(18,2)    NOT NULL CONSTRAINT df_orders_paid DEFAULT 0,
    shipping_address       nvarchar(500)    NOT NULL,   -- always required (FR-028)
    notes                  nvarchar(max)    NULL,
    order_date             datetime2(3)     NOT NULL CONSTRAINT df_orders_date DEFAULT SYSUTCDATETIME(),
    paid_date              datetime2(3)     NULL,
    created_by             uniqueidentifier NOT NULL,
    -- reserved for the separate invoicing project (D3): never written by this platform
    fiscal_status          smallint         NULL,
    fiscal_document_number nvarchar(40)     NULL,
    tax_amount             decimal(18,2)    NULL,

    -- 1 pending · 2 confirmed · 3 processing · 4 shipped · 5 delivered · 6 cancelled · 7 refunded
    CONSTRAINT ck_orders_status CHECK (status IN (1, 2, 3, 4, 5, 6, 7)),
    CONSTRAINT ck_orders_subtotal CHECK (subtotal >= 0),
    CONSTRAINT ck_orders_paid CHECK (paid_amount >= 0),
    CONSTRAINT ck_orders_address CHECK (LEN(LTRIM(RTRIM(shipping_address))) > 0)
);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'ix_orders_tenant_date' AND object_id = OBJECT_ID('dbo.orders'))
    CREATE INDEX ix_orders_tenant_date ON dbo.orders (tenant_id, order_date DESC);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'ix_orders_tenant_status' AND object_id = OBJECT_ID('dbo.orders'))
    CREATE INDEX ix_orders_tenant_status ON dbo.orders (tenant_id, status);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'ix_orders_tenant_customer' AND object_id = OBJECT_ID('dbo.orders'))
    CREATE INDEX ix_orders_tenant_customer ON dbo.orders (tenant_id, customer_id);
GO

IF OBJECT_ID('dbo.order_lines') IS NULL
CREATE TABLE dbo.order_lines
(
    id                   uniqueidentifier NOT NULL CONSTRAINT pk_order_lines PRIMARY KEY DEFAULT NEWID(),
    tenant_id            uniqueidentifier NOT NULL,
    order_id             uniqueidentifier NOT NULL,
    product_id           uniqueidentifier NOT NULL,
    product_name         nvarchar(255)    NOT NULL,   -- snapshot
    unit_price           decimal(18,2)    NOT NULL,   -- snapshot
    quantity             int              NOT NULL,
    price_source         smallint         NOT NULL CONSTRAINT df_order_lines_source DEFAULT 1,
    stock_at_purchase    int              NULL,
    tax_rate             decimal(5,2)     NULL,       -- reserved for invoicing

    CONSTRAINT fk_order_lines_orders FOREIGN KEY (order_id)
        REFERENCES dbo.orders (id) ON DELETE CASCADE,
    CONSTRAINT ck_order_lines_quantity CHECK (quantity > 0),
    CONSTRAINT ck_order_lines_price CHECK (unit_price >= 0),
    -- 1 base · 2 list
    CONSTRAINT ck_order_lines_source CHECK (price_source IN (1, 2))
);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'ix_order_lines_order' AND object_id = OBJECT_ID('dbo.order_lines'))
    CREATE INDEX ix_order_lines_order ON dbo.order_lines (order_id);
GO

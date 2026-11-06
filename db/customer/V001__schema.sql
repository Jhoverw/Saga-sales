-- ============================================================================
-- V001__schema.sql — Customer service (saga_customer)
-- Who buys: the record itself and the addresses an order is shipped to.
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

IF OBJECT_ID('dbo.customers') IS NULL
CREATE TABLE dbo.customers
(
    id            uniqueidentifier NOT NULL CONSTRAINT pk_customers PRIMARY KEY DEFAULT NEWID(),
    tenant_id     uniqueidentifier NOT NULL,
    first_name    nvarchar(100)    NOT NULL,
    last_name     nvarchar(100)    NOT NULL,
    email         nvarchar(255)    NOT NULL,
    phone         nvarchar(30)     NULL,
    -- 1 retail · 2 wholesale: decides which price list applies (D5)
    customer_type smallint         NOT NULL CONSTRAINT df_customers_type DEFAULT 1,
    -- 1 active · 2 inactive
    status        smallint         NOT NULL CONSTRAINT df_customers_status DEFAULT 1,
    created_at    datetime2(3)     NOT NULL CONSTRAINT df_customers_created DEFAULT SYSUTCDATETIME(),
    updated_at    datetime2(3)     NOT NULL CONSTRAINT df_customers_updated DEFAULT SYSUTCDATETIME(),

    CONSTRAINT ck_customers_names CHECK (LEN(LTRIM(RTRIM(first_name))) > 0 AND LEN(LTRIM(RTRIM(last_name))) > 0),
    CONSTRAINT ck_customers_email CHECK (LEN(LTRIM(RTRIM(email))) >= 5 AND email LIKE '%_@_%._%'),
    CONSTRAINT ck_customers_type CHECK (customer_type IN (1, 2)),
    CONSTRAINT ck_customers_status CHECK (status IN (1, 2))
);
GO

-- The email identifies the buyer inside a store: two customers with one address would split
-- their order history.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'ux_customers_tenant_email' AND object_id = OBJECT_ID('dbo.customers'))
    CREATE UNIQUE INDEX ux_customers_tenant_email ON dbo.customers (tenant_id, email);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'ix_customers_tenant_name' AND object_id = OBJECT_ID('dbo.customers'))
    CREATE INDEX ix_customers_tenant_name ON dbo.customers (tenant_id, last_name, first_name);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'ix_customers_tenant_phone' AND object_id = OBJECT_ID('dbo.customers'))
    CREATE INDEX ix_customers_tenant_phone ON dbo.customers (tenant_id, phone);
GO

IF OBJECT_ID('dbo.addresses') IS NULL
CREATE TABLE dbo.addresses
(
    id             uniqueidentifier NOT NULL CONSTRAINT pk_addresses PRIMARY KEY DEFAULT NEWID(),
    tenant_id      uniqueidentifier NOT NULL,
    customer_id    uniqueidentifier NOT NULL,
    line1          nvarchar(255)    NOT NULL,
    city           nvarchar(100)    NOT NULL,
    state_province nvarchar(100)    NOT NULL,
    postal_code    nvarchar(20)     NOT NULL,
    country        nvarchar(100)    NOT NULL,
    is_default     bit              NOT NULL CONSTRAINT df_addresses_default DEFAULT 0,
    created_at     datetime2(3)     NOT NULL CONSTRAINT df_addresses_created DEFAULT SYSUTCDATETIME(),

    CONSTRAINT fk_addresses_customers FOREIGN KEY (customer_id)
        REFERENCES dbo.customers (id) ON DELETE CASCADE,
    CONSTRAINT ck_addresses_line1 CHECK (LEN(LTRIM(RTRIM(line1))) > 0)
);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'ix_addresses_customer' AND object_id = OBJECT_ID('dbo.addresses'))
    CREATE INDEX ix_addresses_customer ON dbo.addresses (customer_id);
GO

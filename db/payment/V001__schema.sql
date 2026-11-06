-- ============================================================================
-- V001__schema.sql — Payment service (saga_payment)
-- Money received. A payment is never deleted: a refund is a new amount on the same record.
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

-- Read model of the orders this service knows about, fed by OrderPlacedEvent (R4).
-- It is a copy on purpose: Payment must never read the Sales database.
IF OBJECT_ID('dbo.order_references') IS NULL
CREATE TABLE dbo.order_references
(
    order_id    uniqueidentifier NOT NULL CONSTRAINT pk_order_references PRIMARY KEY,
    tenant_id   uniqueidentifier NOT NULL,
    customer_id uniqueidentifier NOT NULL,
    subtotal    decimal(18,2)    NOT NULL,
    placed_at   datetime2(3)     NOT NULL,
    updated_at  datetime2(3)     NOT NULL CONSTRAINT df_order_references_updated DEFAULT SYSUTCDATETIME()
);
GO

IF OBJECT_ID('dbo.payments') IS NULL
CREATE TABLE dbo.payments
(
    id                    uniqueidentifier NOT NULL CONSTRAINT pk_payments PRIMARY KEY DEFAULT NEWID(),
    tenant_id             uniqueidentifier NOT NULL,
    order_id              uniqueidentifier NOT NULL,
    customer_id           uniqueidentifier NOT NULL,
    -- 1 credit · 2 debit · 3 paypal · 4 transfer · 5 cash · 6 stripe · 9 manual
    method                smallint         NOT NULL,
    -- 1 pending · 2 authorized · 3 captured · 4 failed · 5 refunded · 6 partially_refunded
    status                smallint         NOT NULL CONSTRAINT df_payments_status DEFAULT 1,
    amount                decimal(18,2)    NOT NULL,
    refunded_amount       decimal(18,2)    NOT NULL CONSTRAINT df_payments_refunded DEFAULT 0,
    currency              char(3)          NOT NULL CONSTRAINT df_payments_currency DEFAULT 'USD',
    transaction_reference nvarchar(255)    NULL,
    gateway_response      nvarchar(max)    NULL,
    created_at            datetime2(3)     NOT NULL CONSTRAINT df_payments_created DEFAULT SYSUTCDATETIME(),
    paid_at               datetime2(3)     NULL,
    captured_at           datetime2(3)     NULL,
    refunded_at           datetime2(3)     NULL,

    CONSTRAINT ck_payments_amount CHECK (amount > 0),
    CONSTRAINT ck_payments_refunded CHECK (refunded_amount >= 0 AND refunded_amount <= amount),
    CONSTRAINT ck_payments_currency CHECK (currency = 'USD'),
    CONSTRAINT ck_payments_method CHECK (method IN (1, 2, 3, 4, 5, 6, 9)),
    CONSTRAINT ck_payments_status CHECK (status IN (1, 2, 3, 4, 5, 6)),
    CONSTRAINT ck_payments_gateway_json CHECK (gateway_response IS NULL OR ISJSON(gateway_response) = 1)
);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'ix_payments_tenant_order' AND object_id = OBJECT_ID('dbo.payments'))
    CREATE INDEX ix_payments_tenant_order ON dbo.payments (tenant_id, order_id);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'ux_payments_reference' AND object_id = OBJECT_ID('dbo.payments'))
    CREATE UNIQUE INDEX ux_payments_reference ON dbo.payments (tenant_id, transaction_reference)
        WHERE transaction_reference IS NOT NULL;
GO

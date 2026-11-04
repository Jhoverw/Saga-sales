-- ============================================================================
-- V001__schema.sql — Identity service (saga_identity)
-- SQL Server 2022 / T-SQL. Applied by scripts/apply-db.ps1.
-- ============================================================================

IF OBJECT_ID('dbo.schema_version') IS NULL
CREATE TABLE dbo.schema_version
(
    version    int           NOT NULL CONSTRAINT pk_schema_version PRIMARY KEY,
    script     nvarchar(200) NOT NULL,
    applied_at datetime2(3)  NOT NULL CONSTRAINT df_schema_version_applied DEFAULT SYSUTCDATETIME()
);
GO

-- ── tenants ─────────────────────────────────────────────────────────────────
IF OBJECT_ID('dbo.tenants') IS NULL
CREATE TABLE dbo.tenants
(
    id            uniqueidentifier NOT NULL CONSTRAINT pk_tenants PRIMARY KEY DEFAULT NEWID(),
    slug          nvarchar(50)     NOT NULL,
    display_name  nvarchar(200)    NOT NULL,
    plan          nvarchar(30)     NOT NULL CONSTRAINT df_tenants_plan DEFAULT 'trial',
    status        smallint         NOT NULL CONSTRAINT df_tenants_status DEFAULT 2,
    custom_domain nvarchar(255)    NULL,
    created_at    datetime2(3)     NOT NULL CONSTRAINT df_tenants_created DEFAULT SYSUTCDATETIME(),
    suspended_at  datetime2(3)     NULL,

    -- slug: lowercase letters, digits and hyphens only (validated again in the API)
    CONSTRAINT ck_tenants_slug CHECK (LEN(slug) BETWEEN 1 AND 50 AND slug NOT LIKE '%[^a-z0-9-]%'),
    -- 1 active · 2 trial · 3 suspended · 4 cancelled
    CONSTRAINT ck_tenants_status CHECK (status IN (1, 2, 3, 4))
);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'ux_tenants_slug' AND object_id = OBJECT_ID('dbo.tenants'))
    CREATE UNIQUE INDEX ux_tenants_slug ON dbo.tenants (slug);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'ux_tenants_custom_domain' AND object_id = OBJECT_ID('dbo.tenants'))
    CREATE UNIQUE INDEX ux_tenants_custom_domain ON dbo.tenants (custom_domain) WHERE custom_domain IS NOT NULL;
GO

-- ── users ───────────────────────────────────────────────────────────────────
IF OBJECT_ID('dbo.users') IS NULL
CREATE TABLE dbo.users
(
    id            uniqueidentifier NOT NULL CONSTRAINT pk_users PRIMARY KEY DEFAULT NEWID(),
    tenant_id     uniqueidentifier NOT NULL,
    email         nvarchar(255)    NOT NULL,
    password_hash nvarchar(512)    NOT NULL,
    role          nvarchar(20)     NOT NULL,
    display_name  nvarchar(100)    NOT NULL,
    is_active     bit              NOT NULL CONSTRAINT df_users_active DEFAULT 1,
    created_at    datetime2(3)     NOT NULL CONSTRAINT df_users_created DEFAULT SYSUTCDATETIME(),
    last_login_at datetime2(3)     NULL,

    CONSTRAINT fk_users_tenants FOREIGN KEY (tenant_id) REFERENCES dbo.tenants (id),
    CONSTRAINT ck_users_role CHECK (role IN ('admin', 'manager', 'sales', 'customer'))
);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'ux_users_tenant_email' AND object_id = OBJECT_ID('dbo.users'))
    CREATE UNIQUE INDEX ux_users_tenant_email ON dbo.users (tenant_id, email);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'ix_users_email' AND object_id = OBJECT_ID('dbo.users'))
    CREATE INDEX ix_users_email ON dbo.users (email) INCLUDE (tenant_id);
GO

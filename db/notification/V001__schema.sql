-- V001__schema.sql
-- Servicio: Notification
-- Descripción: Tablas base para registro de notificaciones, plantillas y control de versiones

SET NOCOUNT ON;
-- No `USE [saga_notification]` here: apply-db.ps1 already selects the database with -d, and a
-- hardcoded USE breaks the _test database (and any other deployment).
GO

-- 1. TABLA DE CONTROL DE VERSIONES (Requerida por apply-db.ps1)
-- Sin esta tabla, el aplicador marca el script como FALLIDO aunque el SQL sea correcto.
IF OBJECT_ID(N'[dbo].[schema_version]', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.schema_version (
        version INT PRIMARY KEY,
        script NVARCHAR(255) NOT NULL,
        applied_at DATETIME2 DEFAULT GETDATE()
    );
END
GO

-- 2. LIMPIEZA DE TABLAS DE NEGOCIO
IF OBJECT_ID(N'[dbo].[notification_logs]', 'U') IS NOT NULL DROP TABLE [dbo].[notification_logs];
IF OBJECT_ID(N'[dbo].[notification_templates]', 'U') IS NOT NULL DROP TABLE [dbo].[notification_templates];
GO

-- 3. TABLA DE PLANTILLAS
CREATE TABLE dbo.notification_templates (
    id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    tenant_id UNIQUEIDENTIFIER NOT NULL,
    template_key VARCHAR(100) NOT NULL,
    subject NVARCHAR(255) NOT NULL,
    body NVARCHAR(MAX) NOT NULL,
    variables_json NVARCHAR(MAX), 
    created_at DATETIME2 DEFAULT GETDATE(),
    updated_at DATETIME2 DEFAULT GETDATE(),
    CONSTRAINT UC_Template_Tenant UNIQUE (tenant_id, template_key)
);
GO

-- 4. TABLA DE LOGS
CREATE TABLE dbo.notification_logs (
    id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    tenant_id UNIQUEIDENTIFIER NOT NULL,
    customer_id UNIQUEIDENTIFIER NULL,
    notification_type INT NOT NULL, 
    channel VARCHAR(50) NOT NULL,
    subject NVARCHAR(255) NOT NULL,
    body NVARCHAR(MAX) NOT NULL,
    status INT NOT NULL, 
    error_message NVARCHAR(MAX) NULL,
    sent_at DATETIME2 NULL,
    created_at DATETIME2 DEFAULT GETDATE(),
    INDEX IX_Tenant_Status (tenant_id, status),
    INDEX IX_Customer (customer_id)
);
GO

-- 5. SEED DE PLANTILLAS BÁSICAS
INSERT INTO dbo.notification_templates (tenant_id, template_key, subject, body)
VALUES 
('00000000-0000-0000-0000-000000000000', 'order_confirmation', 'Confirmación de Pedido', 'Hola, tu pedido {orderId} ha sido confirmado.'),
('00000000-0000-0000-0000-000000000000', 'high_value_alert', 'ALERTA: Pedido de Alto Valor', 'Se ha registrado un pedido de ${amount} en la tienda.');
GO

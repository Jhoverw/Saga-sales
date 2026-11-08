-- V002__functions.sql
-- Servicio: Notification
-- Descripción: Procedimientos almacenados alineados con la Constitución v2.2.0

-- Limpiar procedimientos existentes para evitar errores de recreación
IF OBJECT_ID(N'dbo.sp_notification_create', 'P') IS NOT NULL DROP PROCEDURE dbo.sp_notification_create;
IF OBJECT_ID(N'dbo.sp_notification_mark_sent', 'P') IS NOT NULL DROP PROCEDURE dbo.sp_notification_mark_sent;
IF OBJECT_ID(N'dbo.sp_notification_mark_failed', 'P') IS NOT NULL DROP PROCEDURE dbo.sp_notification_mark_failed;
IF OBJECT_ID(N'dbo.sp_notification_list', 'P') IS NOT NULL DROP PROCEDURE dbo.sp_notification_list;
GO

CREATE PROCEDURE dbo.sp_notification_create
    @p_tenant_id UNIQUEIDENTIFIER,
    @p_customer_id UNIQUEIDENTIFIER,
    @p_subject NVARCHAR(255),
    @p_body NVARCHAR(MAX),
    @p_type INT,
    @p_channel VARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO dbo.notification_logs (tenant_id, customer_id, notification_type, channel, subject, body, status)
    VALUES (@p_tenant_id, @p_customer_id, @p_type, @p_channel, @p_subject, @p_body, 1); -- 1 = Pending
END;
GO

CREATE PROCEDURE dbo.sp_notification_mark_sent
    @p_id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.notification_logs 
    SET status = 2, sent_at = SYSDATETIME() -- 2 = Sent
    WHERE id = @p_id;
END;
GO

CREATE PROCEDURE dbo.sp_notification_mark_failed
    @p_id UNIQUEIDENTIFIER,
    @p_error NVARCHAR(MAX)
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.notification_logs 
    SET status = 3, error_message = @p_error -- 3 = Failed
    WHERE id = @p_id;
END;
GO

CREATE PROCEDURE dbo.sp_notification_list
    @p_tenant_id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SELECT id, customer_id, subject, status, sent_at, error_message, created_at
    FROM dbo.notification_logs
    WHERE tenant_id = @p_tenant_id
    ORDER BY created_at DESC;
END;
GO

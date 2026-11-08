-- ============================================================================
-- V003__functions_fix.sql — Notification: the create procedure returns the log id
--
-- V002's sp_notification_create inserted the row but returned nothing, so the dispatcher had
-- no id to hand back to the API and to mark the attempt as sent/failed. This version emits
-- the generated id, which the repository reads as a single value.
-- ============================================================================

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

CREATE OR ALTER PROCEDURE dbo.sp_notification_create
    @p_tenant_id   UNIQUEIDENTIFIER,
    @p_customer_id UNIQUEIDENTIFIER = NULL,
    @p_subject     NVARCHAR(255),
    @p_body        NVARCHAR(MAX),
    @p_type        INT,
    @p_channel     VARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO dbo.notification_logs (tenant_id, customer_id, notification_type, channel, subject, body, status)
    OUTPUT inserted.id
    VALUES (@p_tenant_id, @p_customer_id, @p_type, @p_channel, @p_subject, @p_body, 1);
END
GO

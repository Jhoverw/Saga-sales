-- V007__payment_attachments.sql
-- Payment: comprobantes de pago (spec 007). Foto del recibo, captura de la transferencia o PDF del depósito.
-- El archivo vive en disco (volumen payment_receipts); aquí solo su registro. Un pedido no puede pasar a
-- Enviado si un pago vigente con tarjeta (1), transferencia (4) o depósito (9) no tiene comprobante.

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

IF OBJECT_ID('dbo.payment_attachments') IS NULL
CREATE TABLE dbo.payment_attachments
(
    id           uniqueidentifier NOT NULL CONSTRAINT pk_payment_attachments PRIMARY KEY,
    tenant_id    uniqueidentifier NOT NULL,
    payment_id   uniqueidentifier NOT NULL,
    file_name    nvarchar(255)    NOT NULL,
    content_type nvarchar(100)    NOT NULL,
    size_bytes   int              NOT NULL,
    storage_key  nvarchar(300)    NOT NULL,
    uploaded_by  uniqueidentifier NOT NULL,
    uploaded_at  datetime2(3)     NOT NULL CONSTRAINT df_payment_attachments_uploaded DEFAULT SYSUTCDATETIME(),

    CONSTRAINT fk_payment_attachments_payments FOREIGN KEY (payment_id) REFERENCES dbo.payments (id),
    CONSTRAINT ck_payment_attachments_size CHECK (size_bytes > 0 AND size_bytes <= 5242880),
    CONSTRAINT ck_payment_attachments_type CHECK (content_type IN ('image/jpeg', 'image/png', 'image/webp', 'application/pdf'))
);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'ix_payment_attachments_payment' AND object_id = OBJECT_ID('dbo.payment_attachments'))
    CREATE INDEX ix_payment_attachments_payment ON dbo.payment_attachments (tenant_id, payment_id);
GO

CREATE OR ALTER PROCEDURE dbo.sp_payment_attachment_add
    @p_tenant_id    uniqueidentifier,
    @id             uniqueidentifier,
    @payment_id     uniqueidentifier,
    @file_name      nvarchar(255),
    @content_type   nvarchar(100),
    @size_bytes     int,
    @storage_key    nvarchar(300),
    @uploaded_by    uniqueidentifier
AS
BEGIN
    SET NOCOUNT ON;

    IF NOT EXISTS (SELECT 1 FROM dbo.payments WHERE id = @payment_id AND tenant_id = @p_tenant_id)
    BEGIN
        ;THROW 50000, 'NOT_FOUND: el pago no existe en esta tienda', 1;
    END

    IF @content_type NOT IN ('image/jpeg', 'image/png', 'image/webp', 'application/pdf')
    BEGIN
        ;THROW 50000, 'VALIDATION_ERROR: el comprobante debe ser JPG, PNG, WEBP o PDF', 1;
    END

    IF @size_bytes IS NULL OR @size_bytes <= 0 OR @size_bytes > 5242880
    BEGIN
        ;THROW 50000, 'VALIDATION_ERROR: el comprobante no puede superar los 5 MB', 1;
    END

    INSERT INTO dbo.payment_attachments (id, tenant_id, payment_id, file_name, content_type, size_bytes, storage_key, uploaded_by)
    VALUES (@id, @p_tenant_id, @payment_id, LEFT(ISNULL(NULLIF(LTRIM(RTRIM(@file_name)), N''), N'comprobante'), 255),
            @content_type, @size_bytes, @storage_key, @uploaded_by);

    SELECT id AS Id, payment_id AS PaymentId, file_name AS FileName, content_type AS ContentType,
           size_bytes AS SizeBytes, uploaded_at AS UploadedAt
    FROM dbo.payment_attachments
    WHERE id = @id;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_payment_attachment_list
    @p_tenant_id uniqueidentifier,
    @payment_id  uniqueidentifier
AS
BEGIN
    SET NOCOUNT ON;

    SELECT id AS Id, payment_id AS PaymentId, file_name AS FileName, content_type AS ContentType,
           size_bytes AS SizeBytes, uploaded_at AS UploadedAt
    FROM dbo.payment_attachments
    WHERE tenant_id = @p_tenant_id AND payment_id = @payment_id
    ORDER BY uploaded_at;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_payment_attachment_get
    @p_tenant_id uniqueidentifier,
    @id          uniqueidentifier
AS
BEGIN
    SET NOCOUNT ON;

    SELECT id AS Id, payment_id AS PaymentId, file_name AS FileName, content_type AS ContentType,
           size_bytes AS SizeBytes, uploaded_at AS UploadedAt, storage_key AS StorageKey
    FROM dbo.payment_attachments
    WHERE tenant_id = @p_tenant_id AND id = @id;
END
GO

-- Lo que Sales pregunta antes de enviar un pedido (FR-003).
CREATE OR ALTER PROCEDURE dbo.sp_order_payment_proof_status
    @p_tenant_id uniqueidentifier,
    @order_id    uniqueidentifier
AS
BEGIN
    SET NOCOUNT ON;

    -- Vigente: capturado (3) o reembolsado en parte (6). SQL Server no admite una subconsulta dentro de
    -- SUM, así que los comprobantes se cuentan antes en una tabla derivada.
    SELECT COUNT(*) AS Payments,
           ISNULL(SUM(CASE WHEN p.method IN (1, 4, 9) AND a.files IS NULL THEN 1 ELSE 0 END), 0) AS WithoutProof
    FROM dbo.payments p
    LEFT JOIN (SELECT payment_id, COUNT(*) AS files
               FROM dbo.payment_attachments
               WHERE tenant_id = @p_tenant_id
               GROUP BY payment_id) a ON a.payment_id = p.id
    WHERE p.tenant_id = @p_tenant_id AND p.order_id = @order_id AND p.status IN (3, 6);
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_payment_list_by_order
    @p_tenant_id uniqueidentifier,
    @order_id    uniqueidentifier
AS
BEGIN
    SET NOCOUNT ON;

    SELECT p.id AS Id, p.order_id AS OrderId, p.customer_id AS CustomerId, p.method AS Method,
           p.status AS Status, p.amount AS Amount, p.refunded_amount AS RefundedAmount,
           p.currency AS Currency, p.transaction_reference AS TransactionReference,
           p.created_at AS CreatedAt, p.paid_at AS PaidAt, p.captured_at AS CapturedAt,
           p.refunded_at AS RefundedAt, p.cash_received AS CashReceived, p.change_given AS ChangeGiven,
           (SELECT COUNT(*) FROM dbo.payment_attachments a
            WHERE a.tenant_id = p.tenant_id AND a.payment_id = p.id) AS AttachmentCount
    FROM dbo.payments p
    WHERE p.tenant_id = @p_tenant_id AND p.order_id = @order_id
    ORDER BY p.created_at;
END
GO

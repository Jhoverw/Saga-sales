-- V028__order_shipment.sql
-- Sales: envío de pedidos (spec 007). Cómo sale el pedido (courier o entrega propia/retiro), la empresa de
-- courier y el número de guía. El paso a Enviado solo existe en sp_order_ship; el comprobante de los pagos lo
-- verifica el servicio antes de llamar a este procedure, porque los pagos viven en la base de Payment.

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

IF COL_LENGTH('dbo.orders', 'shipping_method') IS NULL
    ALTER TABLE dbo.orders ADD shipping_method smallint NULL;
GO

IF COL_LENGTH('dbo.orders', 'courier_company') IS NULL
    ALTER TABLE dbo.orders ADD courier_company nvarchar(60) NULL;
GO

IF COL_LENGTH('dbo.orders', 'tracking_number') IS NULL
    ALTER TABLE dbo.orders ADD tracking_number nvarchar(60) NULL;
GO

IF COL_LENGTH('dbo.orders', 'shipped_at') IS NULL
    ALTER TABLE dbo.orders ADD shipped_at datetime2(3) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'ck_orders_shipping_method')
    ALTER TABLE dbo.orders ADD CONSTRAINT ck_orders_shipping_method CHECK (shipping_method IS NULL OR shipping_method IN (1, 2));
GO

CREATE OR ALTER PROCEDURE dbo.sp_order_change_status
    @p_tenant_id uniqueidentifier,
    @order_id    uniqueidentifier,
    @new_status  smallint
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @current smallint;

    SELECT @current = status
    FROM dbo.orders WITH (UPDLOCK, ROWLOCK)
    WHERE id = @order_id AND tenant_id = @p_tenant_id;

    IF @current IS NULL
    BEGIN
        ;THROW 50000, 'NOT_FOUND: order does not exist for this tenant', 1;
    END

    IF NOT EXISTS
    (
        SELECT 1
        FROM (VALUES (1, 2), (1, 6),
                     (2, 3), (2, 6),
                     (3, 4), (3, 6),
                     (4, 5),
                     (5, 7)) AS allowed (from_status, to_status)
        WHERE allowed.from_status = @current AND allowed.to_status = @new_status
    )
    BEGIN
        ;THROW 50000, 'INVALID_TRANSITION: that status change is not allowed', 1;
    END

    -- Spec 007: enviar exige registrar cómo sale el pedido; solo sp_order_ship hace ese paso.
    IF @new_status = 4
    BEGIN
        ;THROW 50000, 'VALIDATION_ERROR: para marcar el pedido como enviado registra el método de envío', 1;
    END

    UPDATE dbo.orders
    SET status    = @new_status,
        paid_date = CASE WHEN @new_status = 3 AND paid_date IS NULL THEN SYSUTCDATETIME() ELSE paid_date END
    WHERE id = @order_id AND tenant_id = @p_tenant_id;

    SELECT id               AS Id,
           tenant_id        AS TenantId,
           customer_id      AS CustomerId,
           status           AS Status,
           subtotal         AS Subtotal,
           paid_amount      AS PaidAmount,
           shipping_address AS ShippingAddress,
           notes            AS Notes,
           order_date       AS OrderDate,
           paid_date        AS PaidDate,
           created_by       AS CreatedBy
    FROM dbo.orders
    WHERE id = @order_id AND tenant_id = @p_tenant_id;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_order_ship
    @p_tenant_id       uniqueidentifier,
    @order_id          uniqueidentifier,
    @shipping_method   smallint,
    @courier_company   nvarchar(60) = NULL,
    @tracking_number   nvarchar(60) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    SET @courier_company = NULLIF(LTRIM(RTRIM(@courier_company)), N'');
    SET @tracking_number = NULLIF(LTRIM(RTRIM(@tracking_number)), N'');

    -- 1 courier · 2 entrega propia o retiro
    IF @shipping_method IS NULL OR @shipping_method NOT IN (1, 2)
    BEGIN
        ;THROW 50000, 'VALIDATION_ERROR: elige courier o entrega propia', 1;
    END

    IF @shipping_method = 1 AND (@courier_company IS NULL OR LEN(@courier_company) < 2)
    BEGIN
        ;THROW 50000, 'VALIDATION_ERROR: indica la empresa de courier', 1;
    END

    IF @shipping_method = 1 AND (@tracking_number IS NULL OR LEN(@tracking_number) < 3)
    BEGIN
        ;THROW 50000, 'VALIDATION_ERROR: indica el número de guía del courier', 1;
    END

    BEGIN TRAN;

    DECLARE @current smallint;

    SELECT @current = status
    FROM dbo.orders WITH (UPDLOCK, ROWLOCK)
    WHERE id = @order_id AND tenant_id = @p_tenant_id;

    IF @current IS NULL
    BEGIN
        ROLLBACK;
        ;THROW 50000, 'NOT_FOUND: order does not exist for this tenant', 1;
    END

    -- Solo desde En preparación (3), igual que la máquina de estados.
    IF @current <> 3
    BEGIN
        ROLLBACK;
        ;THROW 50000, 'INVALID_TRANSITION: that status change is not allowed', 1;
    END

    UPDATE dbo.orders
    SET status          = 4,
        shipping_method = @shipping_method,
        courier_company = CASE WHEN @shipping_method = 1 THEN @courier_company END,
        tracking_number = CASE WHEN @shipping_method = 1 THEN @tracking_number END,
        shipped_at      = SYSUTCDATETIME()
    WHERE id = @order_id AND tenant_id = @p_tenant_id;

    COMMIT;

    SELECT id               AS Id,
           tenant_id        AS TenantId,
           customer_id      AS CustomerId,
           status           AS Status,
           subtotal         AS Subtotal,
           paid_amount      AS PaidAmount,
           shipping_address AS ShippingAddress,
           notes            AS Notes,
           order_date       AS OrderDate,
           paid_date        AS PaidDate,
           created_by       AS CreatedBy
    FROM dbo.orders
    WHERE id = @order_id AND tenant_id = @p_tenant_id;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_order_shipment_get
    @p_tenant_id uniqueidentifier,
    @order_id    uniqueidentifier
AS
BEGIN
    SET NOCOUNT ON;

    SELECT shipping_method AS ShippingMethod,
           courier_company AS CourierCompany,
           tracking_number AS TrackingNumber,
           shipped_at      AS ShippedAt
    FROM dbo.orders
    WHERE id = @order_id AND tenant_id = @p_tenant_id;
END
GO

-- V031__remote_orders.sql
-- Sales: pedidos (spec 008). Canal (1 mostrador, 2 pedido), contra entrega, stock apartado con vencimiento
-- configurable por tienda y monto verificado. Un pedido no contra entrega se prepara y se envía solo cuando lo
-- verificado cubre el total. Los vencidos los cancela el servicio de vencimiento (HoldExpiryService).

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

IF COL_LENGTH('dbo.orders', 'channel') IS NULL
    ALTER TABLE dbo.orders ADD channel smallint NOT NULL CONSTRAINT df_orders_channel DEFAULT 1;
GO

IF COL_LENGTH('dbo.orders', 'cash_on_delivery') IS NULL
    ALTER TABLE dbo.orders ADD cash_on_delivery bit NOT NULL CONSTRAINT df_orders_cod DEFAULT 0;
GO

IF COL_LENGTH('dbo.orders', 'hold_until') IS NULL
    ALTER TABLE dbo.orders ADD hold_until datetime2(3) NULL;
GO

IF COL_LENGTH('dbo.orders', 'expired_at') IS NULL
    ALTER TABLE dbo.orders ADD expired_at datetime2(3) NULL;
GO

-- Lo pagado antes de esta versión se da por verificado: ningún pedido existente queda bloqueado.
IF COL_LENGTH('dbo.orders', 'verified_amount') IS NULL
BEGIN
    ALTER TABLE dbo.orders ADD verified_amount decimal(18,2) NOT NULL CONSTRAINT df_orders_verified DEFAULT 0;
    EXEC (N'UPDATE dbo.orders SET verified_amount = paid_amount;');
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'ck_orders_channel')
    ALTER TABLE dbo.orders ADD CONSTRAINT ck_orders_channel CHECK (channel IN (1, 2));
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'ix_orders_holds' AND object_id = OBJECT_ID('dbo.orders'))
    CREATE INDEX ix_orders_holds ON dbo.orders (hold_until) INCLUDE (tenant_id, status, channel, cash_on_delivery)
    WHERE hold_until IS NOT NULL AND expired_at IS NULL;
GO

IF OBJECT_ID('dbo.sales_settings') IS NULL
CREATE TABLE dbo.sales_settings
(
    tenant_id        uniqueidentifier NOT NULL CONSTRAINT pk_sales_settings PRIMARY KEY,
    order_hold_hours int              NOT NULL,
    updated_at       datetime2(3)     NOT NULL CONSTRAINT df_sales_settings_updated DEFAULT SYSUTCDATETIME(),

    CONSTRAINT ck_sales_settings_hold CHECK (order_hold_hours BETWEEN 1 AND 720)
);
GO

CREATE OR ALTER PROCEDURE dbo.sp_order_change_status
    @p_tenant_id uniqueidentifier,
    @order_id    uniqueidentifier,
    @new_status  smallint
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @current smallint, @channel smallint, @cod bit, @total decimal(18,2), @paid decimal(18,2), @verified decimal(18,2);

    SELECT @current  = status,
           @channel  = channel,
           @cod      = cash_on_delivery,
           @total    = subtotal + ISNULL(tax_amount, 0),
           @paid     = paid_amount,
           @verified = verified_amount
    FROM dbo.orders WITH (UPDLOCK, ROWLOCK)
    WHERE id = @order_id AND tenant_id = @p_tenant_id;

    IF @current IS NULL
    BEGIN
        ;THROW 50000, N'NOT_FOUND: order does not exist for this tenant', 1;
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
        ;THROW 50000, N'INVALID_TRANSITION: that status change is not allowed', 1;
    END

    -- Spec 008: un pedido (canal 2) que no es contra entrega se prepara solo con el pago verificado.
    IF @new_status = 3 AND @channel = 2 AND @cod = 0 AND @verified < @total
    BEGIN
        ;THROW 50000, N'CONFLICT: el pago del pedido aún no está verificado', 1;
    END

    -- Spec 008: contra entrega se marca entregado solo cuando ya se cobró.
    IF @new_status = 5 AND @channel = 2 AND @cod = 1 AND @paid < @total
    BEGIN
        ;THROW 50000, N'CONFLICT: registra el cobro contra entrega antes de marcarlo entregado', 1;
    END

    -- Spec 007: enviar exige registrar cómo sale el pedido; solo sp_order_ship hace ese paso.
    IF @new_status = 4
    BEGIN
        ;THROW 50000, N'VALIDATION_ERROR: para marcar el pedido como enviado registra el método de envío', 1;
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
        ;THROW 50000, N'VALIDATION_ERROR: elige courier o entrega propia', 1;
    END

    IF @shipping_method = 1 AND (@courier_company IS NULL OR LEN(@courier_company) < 2)
    BEGIN
        ;THROW 50000, N'VALIDATION_ERROR: indica la empresa de courier', 1;
    END

    IF @shipping_method = 1 AND (@tracking_number IS NULL OR LEN(@tracking_number) < 3)
    BEGIN
        ;THROW 50000, N'VALIDATION_ERROR: indica el número de guía del courier', 1;
    END

    BEGIN TRAN;

    DECLARE @current smallint, @channel smallint, @cod bit, @total decimal(18,2), @verified decimal(18,2);

    SELECT @current  = status,
           @channel  = channel,
           @cod      = cash_on_delivery,
           @total    = subtotal + ISNULL(tax_amount, 0),
           @verified = verified_amount
    FROM dbo.orders WITH (UPDLOCK, ROWLOCK)
    WHERE id = @order_id AND tenant_id = @p_tenant_id;

    IF @current IS NULL
    BEGIN
        ROLLBACK;
        ;THROW 50000, N'NOT_FOUND: order does not exist for this tenant', 1;
    END

    -- Solo desde En preparación (3), igual que la máquina de estados.
    IF @current <> 3
    BEGIN
        ROLLBACK;
        ;THROW 50000, N'INVALID_TRANSITION: that status change is not allowed', 1;
    END

    -- Spec 008: tampoco sale un pedido no contra entrega con el pago sin verificar.
    IF @channel = 2 AND @cod = 0 AND @verified < @total
    BEGIN
        ROLLBACK;
        ;THROW 50000, N'CONFLICT: el pago del pedido aún no está verificado', 1;
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

CREATE OR ALTER PROCEDURE dbo.sp_order_register_payment
    @p_tenant_id uniqueidentifier,
    @order_id    uniqueidentifier,
    @amount      decimal(18,2),
    -- Spec 008: efectivo y ventas de mostrador llegan verificados; transferencias de pedidos, no.
    @verified    bit = 1
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @amount <= 0
    BEGIN
        ;THROW 50000, N'VALIDATION_ERROR: the amount must be greater than zero', 1;
    END

    DECLARE @status smallint, @total decimal(18,2), @paid decimal(18,2), @channel smallint, @cod bit, @checked decimal(18,2);

    BEGIN TRAN;

    SELECT @status  = status,
           @total   = subtotal + ISNULL(tax_amount, 0),
           @paid    = paid_amount,
           @channel = channel,
           @cod     = cash_on_delivery,
           @checked = verified_amount
    FROM dbo.orders WITH (UPDLOCK, ROWLOCK)
    WHERE id = @order_id AND tenant_id = @p_tenant_id;

    IF @status IS NULL
    BEGIN
        ROLLBACK;
        ;THROW 50000, N'NOT_FOUND: order does not exist for this tenant', 1;
    END

    IF @status IN (6, 7)
    BEGIN
        ROLLBACK;
        ;THROW 50000, N'INVALID_TRANSITION: a cancelled or refunded order cannot take payments', 1;
    END

    IF @paid + @amount > @total
    BEGIN
        ROLLBACK;
        ;THROW 50000, N'PAYMENT_EXCEEDS_BALANCE: the amount is larger than the balance due', 1;
    END

    DECLARE @new_checked decimal(18,2) = @checked + CASE WHEN @verified = 1 THEN @amount ELSE 0 END;

    -- Mostrador: pagado completo pasa a preparación (como antes, desde Pendiente). Pedido: solo cuando lo
    -- verificado cubre el total; un contra entrega nunca avanza solo (se cobra al entregar).
    DECLARE @ready bit = CASE
        WHEN @channel = 2 AND @cod = 0 THEN CASE WHEN @new_checked >= @total THEN 1 ELSE 0 END
        WHEN @channel = 2 THEN 0
        ELSE CASE WHEN @paid + @amount >= @total THEN 1 ELSE 0 END END;

    UPDATE dbo.orders
    SET paid_amount     = @paid + @amount,
        verified_amount = @new_checked,
        status          = CASE WHEN (status = 1 OR (status = 2 AND @channel = 2)) AND @ready = 1 THEN 3 ELSE status END,
        paid_date       = CASE WHEN (status = 1 OR (status = 2 AND @channel = 2)) AND @ready = 1 AND paid_date IS NULL
                               THEN SYSUTCDATETIME() ELSE paid_date END
    WHERE id = @order_id AND tenant_id = @p_tenant_id;

    COMMIT;

    SELECT id               AS Id,
           tenant_id        AS TenantId,
           customer_id      AS CustomerId,
           status           AS Status,
           subtotal         AS Subtotal,
           tax_amount       AS TaxAmount,
           paid_amount      AS PaidAmount,
           shipping_address AS ShippingAddress,
           notes            AS Notes,
           order_date       AS OrderDate,
           paid_date        AS PaidDate,
           created_by       AS CreatedBy,
           order_number     AS OrderNumber,
           created_by_name  AS CreatedByName
    FROM dbo.orders
    WHERE id = @order_id AND tenant_id = @p_tenant_id;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_sales_settings_get
    @p_tenant_id uniqueidentifier
AS
BEGIN
    SET NOCOUNT ON;

    SELECT ISNULL((SELECT order_hold_hours FROM dbo.sales_settings WHERE tenant_id = @p_tenant_id), 48) AS OrderHoldHours;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_sales_settings_set
    @p_tenant_id      uniqueidentifier,
    @order_hold_hours int
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @order_hold_hours IS NULL OR @order_hold_hours NOT BETWEEN 1 AND 720
    BEGIN
        ;THROW 50000, N'VALIDATION_ERROR: las horas de apartado van de 1 a 720', 1;
    END

    BEGIN TRAN;

    UPDATE dbo.sales_settings WITH (UPDLOCK, HOLDLOCK)
    SET order_hold_hours = @order_hold_hours, updated_at = SYSUTCDATETIME()
    WHERE tenant_id = @p_tenant_id;

    IF @@ROWCOUNT = 0
        INSERT INTO dbo.sales_settings (tenant_id, order_hold_hours) VALUES (@p_tenant_id, @order_hold_hours);

    COMMIT;

    SELECT order_hold_hours AS OrderHoldHours FROM dbo.sales_settings WHERE tenant_id = @p_tenant_id;
END
GO

-- Se llama justo después de sp_order_create: marca el canal y, si no es contra entrega, hasta cuándo se aparta.
CREATE OR ALTER PROCEDURE dbo.sp_order_set_channel
    @p_tenant_id       uniqueidentifier,
    @order_id          uniqueidentifier,
    @channel           smallint,
    @cash_on_delivery  bit = 0
AS
BEGIN
    SET NOCOUNT ON;

    IF @channel IS NULL OR @channel NOT IN (1, 2)
    BEGIN
        ;THROW 50000, N'VALIDATION_ERROR: canal de venta no válido', 1;
    END

    DECLARE @hours int = ISNULL((SELECT order_hold_hours FROM dbo.sales_settings WHERE tenant_id = @p_tenant_id), 48);

    UPDATE dbo.orders
    SET channel          = @channel,
        cash_on_delivery = CASE WHEN @channel = 2 THEN ISNULL(@cash_on_delivery, 0) ELSE 0 END,
        hold_until       = CASE WHEN @channel = 2 AND ISNULL(@cash_on_delivery, 0) = 0
                                THEN DATEADD(HOUR, @hours, order_date) END
    WHERE id = @order_id AND tenant_id = @p_tenant_id AND status = 1;

    IF @@ROWCOUNT = 0
    BEGIN
        ;THROW 50000, N'NOT_FOUND: order does not exist for this tenant', 1;
    END
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_order_payment_verified
    @p_tenant_id uniqueidentifier,
    @order_id    uniqueidentifier,
    @amount      decimal(18,2)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @amount IS NULL OR @amount <= 0
    BEGIN
        ;THROW 50000, N'VALIDATION_ERROR: the amount must be greater than zero', 1;
    END

    DECLARE @status smallint, @total decimal(18,2), @paid decimal(18,2), @checked decimal(18,2), @channel smallint, @cod bit;

    BEGIN TRAN;

    SELECT @status = status, @total = subtotal + ISNULL(tax_amount, 0), @paid = paid_amount,
           @checked = verified_amount, @channel = channel, @cod = cash_on_delivery
    FROM dbo.orders WITH (UPDLOCK, ROWLOCK)
    WHERE id = @order_id AND tenant_id = @p_tenant_id;

    IF @status IS NULL
    BEGIN
        ROLLBACK;
        ;THROW 50000, N'NOT_FOUND: order does not exist for this tenant', 1;
    END

    IF @status IN (6, 7)
    BEGIN
        ROLLBACK;
        ;THROW 50000, N'INVALID_TRANSITION: el pedido está cancelado o reembolsado', 1;
    END

    IF @checked + @amount > @paid
    BEGIN
        ROLLBACK;
        ;THROW 50000, N'CONFLICT: se verificaría más de lo pagado', 1;
    END

    UPDATE dbo.orders
    SET verified_amount = @checked + @amount,
        status          = CASE WHEN status IN (1, 2) AND @channel = 2 AND @cod = 0 AND @checked + @amount >= @total
                               THEN 3 ELSE status END,
        paid_date       = CASE WHEN status IN (1, 2) AND @channel = 2 AND @cod = 0 AND @checked + @amount >= @total
                                    AND paid_date IS NULL THEN SYSUTCDATETIME() ELSE paid_date END
    WHERE id = @order_id AND tenant_id = @p_tenant_id;

    COMMIT;

    SELECT id               AS Id,
           tenant_id        AS TenantId,
           customer_id      AS CustomerId,
           status           AS Status,
           subtotal         AS Subtotal,
           tax_amount       AS TaxAmount,
           paid_amount      AS PaidAmount,
           shipping_address AS ShippingAddress,
           notes            AS Notes,
           order_date       AS OrderDate,
           paid_date        AS PaidDate,
           created_by       AS CreatedBy,
           order_number     AS OrderNumber,
           created_by_name  AS CreatedByName
    FROM dbo.orders
    WHERE id = @order_id AND tenant_id = @p_tenant_id;
END
GO

-- Un pago rechazado deja de contar (spec 008, FR-004): solo se descuenta lo que aún no estaba verificado.
CREATE OR ALTER PROCEDURE dbo.sp_order_payment_rejected
    @p_tenant_id uniqueidentifier,
    @order_id    uniqueidentifier,
    @amount      decimal(18,2)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @status smallint, @paid decimal(18,2), @checked decimal(18,2);

    BEGIN TRAN;

    SELECT @status = status, @paid = paid_amount, @checked = verified_amount
    FROM dbo.orders WITH (UPDLOCK, ROWLOCK)
    WHERE id = @order_id AND tenant_id = @p_tenant_id;

    IF @status IS NULL
    BEGIN
        ROLLBACK;
        ;THROW 50000, N'NOT_FOUND: order does not exist for this tenant', 1;
    END

    IF @amount IS NULL OR @amount <= 0 OR @amount > @paid - @checked
    BEGIN
        ROLLBACK;
        ;THROW 50000, N'CONFLICT: solo se rechaza un pago que aún no está verificado', 1;
    END

    UPDATE dbo.orders
    SET paid_amount = @paid - @amount
    WHERE id = @order_id AND tenant_id = @p_tenant_id;

    COMMIT;

    SELECT id               AS Id,
           tenant_id        AS TenantId,
           customer_id      AS CustomerId,
           status           AS Status,
           subtotal         AS Subtotal,
           tax_amount       AS TaxAmount,
           paid_amount      AS PaidAmount,
           shipping_address AS ShippingAddress,
           notes            AS Notes,
           order_date       AS OrderDate,
           paid_date        AS PaidDate,
           created_by       AS CreatedBy,
           order_number     AS OrderNumber,
           created_by_name  AS CreatedByName
    FROM dbo.orders
    WHERE id = @order_id AND tenant_id = @p_tenant_id;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_order_fulfillment_get
    @p_tenant_id uniqueidentifier,
    @order_id    uniqueidentifier
AS
BEGIN
    SET NOCOUNT ON;

    SELECT channel          AS Channel,
           cash_on_delivery AS CashOnDelivery,
           hold_until       AS HoldUntil,
           expired_at       AS ExpiredAt,
           verified_amount  AS VerifiedAmount,
           paid_amount      AS PaidAmount,
           subtotal + ISNULL(tax_amount, 0) AS Total,
           shipping_method  AS ShippingMethod,
           courier_company  AS CourierCompany,
           tracking_number  AS TrackingNumber,
           shipped_at       AS ShippedAt
    FROM dbo.orders
    WHERE id = @order_id AND tenant_id = @p_tenant_id;
END
GO

-- Pedidos en curso para el tablero: canal 2, más los cerrados en los últimos 7 días.
CREATE OR ALTER PROCEDURE dbo.sp_order_remote_list
    @p_tenant_id uniqueidentifier
AS
BEGIN
    SET NOCOUNT ON;

    SELECT o.id AS Id, o.order_number AS OrderNumber, o.customer_id AS CustomerId, o.status AS Status,
           o.subtotal + ISNULL(o.tax_amount, 0) AS Total, o.paid_amount AS PaidAmount,
           o.verified_amount AS VerifiedAmount, o.cash_on_delivery AS CashOnDelivery,
           o.hold_until AS HoldUntil, o.expired_at AS ExpiredAt, o.order_date AS OrderDate,
           o.created_by_name AS CreatedByName
    FROM dbo.orders o
    WHERE o.tenant_id = @p_tenant_id AND o.channel = 2
      AND (o.status IN (1, 2, 3, 4) OR o.order_date >= DATEADD(DAY, -7, SYSUTCDATETIME()))
    ORDER BY CASE WHEN o.status IN (1, 2) THEN 0 ELSE 1 END, o.hold_until, o.order_date DESC;
END
GO

-- Vencidos de todas las tiendas para el servicio de vencimiento. Cada fila nombra su tienda y la cancelación
-- se hace después con esa tienda: este es el único procedure de Sales sin @p_tenant_id (constitución §V).
CREATE OR ALTER PROCEDURE dbo.sp_order_holds_due
    @max_rows int = 100
AS
BEGIN
    SET NOCOUNT ON;

    SELECT TOP (@max_rows) tenant_id AS TenantId, id AS OrderId
    FROM dbo.orders
    WHERE hold_until IS NOT NULL AND expired_at IS NULL AND hold_until < SYSUTCDATETIME()
      AND channel = 2 AND cash_on_delivery = 0 AND status IN (1, 2)
      AND verified_amount < subtotal + ISNULL(tax_amount, 0)
    ORDER BY hold_until;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_order_mark_expired
    @p_tenant_id uniqueidentifier,
    @order_id    uniqueidentifier
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE dbo.orders SET expired_at = SYSUTCDATETIME()
    WHERE id = @order_id AND tenant_id = @p_tenant_id AND expired_at IS NULL;
END
GO

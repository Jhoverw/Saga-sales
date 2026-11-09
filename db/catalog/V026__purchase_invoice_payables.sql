-- V026__purchase_invoice_payables.sql
-- Catalog: compra ingresada como la factura del proveedor y cuentas por pagar (spec 009).
--   * purchases: datos del comprobante (tipo, número, autorización SRI, fechas, forma de pago, crédito) y el
--     desglose de totales tal como trae el papel (subtotal 0 %, con IVA, descuentos, ICE, otros).
--   * purchase_lines: cantidad y precio como vienen en la factura, empaque y unidades por empaque. La columna
--     quantity sigue siendo la cantidad en unidades de stock, y effective_unit_cost el costo por unidad que usa
--     el promedio ponderado; el servicio los calcula (PurchaseInvoiceCalculator) y aquí solo se guardan.
--   * products.units_per_package: unidades que trae la caja de ese producto.
--   * payables / payable_payments: lo que se debe a cada proveedor por compras a crédito, y sus abonos.

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

IF COL_LENGTH('dbo.products', 'units_per_package') IS NULL
    ALTER TABLE dbo.products ADD units_per_package int NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'ck_products_units_per_package')
    ALTER TABLE dbo.products ADD CONSTRAINT ck_products_units_per_package CHECK (units_per_package IS NULL OR units_per_package >= 1);
GO

IF COL_LENGTH('dbo.purchases', 'document_type') IS NULL
BEGIN
    -- 1 factura · 2 nota de venta · 3 liquidación de compra (códigos 01, 02, 03 del SRI)
    ALTER TABLE dbo.purchases ADD document_type smallint NULL,
                                  authorization_number nvarchar(49) NULL,
                                  issued_at date NULL,
                                  document_expires_at date NULL,
                                  -- código de forma de pago del SRI: 1, 16, 17, 18, 19, 20, 21
                                  payment_method smallint NULL,
                                  is_credit bit NOT NULL CONSTRAINT df_purchases_credit DEFAULT 0,
                                  credit_due_at date NULL,
                                  vat_rate decimal(5,4) NULL,
                                  subtotal_zero decimal(18,2) NULL,
                                  subtotal_taxed decimal(18,2) NULL,
                                  discount_zero decimal(18,2) NULL,
                                  discount_taxed decimal(18,2) NULL,
                                  ice_amount decimal(18,2) NULL,
                                  other_charges decimal(18,2) NULL;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'ux_purchases_supplier_document' AND object_id = OBJECT_ID('dbo.purchases'))
    CREATE UNIQUE INDEX ux_purchases_supplier_document
        ON dbo.purchases (tenant_id, supplier_id, document_type, supplier_invoice_number)
        WHERE document_type IS NOT NULL AND supplier_invoice_number IS NOT NULL AND status <> 3;
GO

IF COL_LENGTH('dbo.purchase_lines', 'invoice_quantity') IS NULL
    ALTER TABLE dbo.purchase_lines ADD invoice_quantity decimal(18,3) NULL,
                                       package_label nvarchar(20) NULL,
                                       units_per_package int NULL,
                                       invoice_unit_price decimal(18,4) NULL,
                                       line_discount decimal(18,2) NULL,
                                       taxable bit NULL;
GO

IF OBJECT_ID('dbo.payables') IS NULL
CREATE TABLE dbo.payables
(
    id          uniqueidentifier NOT NULL CONSTRAINT pk_payables PRIMARY KEY,
    tenant_id   uniqueidentifier NOT NULL,
    purchase_id uniqueidentifier NOT NULL,
    supplier_id uniqueidentifier NOT NULL,
    amount      decimal(18,2)    NOT NULL,
    balance     decimal(18,2)    NOT NULL,
    due_at      date             NOT NULL,
    -- 1 abierta · 2 pagada · 3 anulada
    status      smallint         NOT NULL CONSTRAINT df_payables_status DEFAULT 1,
    created_at  datetime2(3)     NOT NULL CONSTRAINT df_payables_created DEFAULT SYSUTCDATETIME(),

    CONSTRAINT fk_payables_purchases FOREIGN KEY (purchase_id) REFERENCES dbo.purchases (id),
    CONSTRAINT fk_payables_suppliers FOREIGN KEY (supplier_id) REFERENCES dbo.suppliers (id),
    CONSTRAINT ck_payables_amount CHECK (amount > 0 AND balance >= 0 AND balance <= amount),
    CONSTRAINT ck_payables_status CHECK (status IN (1, 2, 3))
);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'ux_payables_purchase' AND object_id = OBJECT_ID('dbo.payables'))
    CREATE UNIQUE INDEX ux_payables_purchase ON dbo.payables (purchase_id);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'ix_payables_open' AND object_id = OBJECT_ID('dbo.payables'))
    CREATE INDEX ix_payables_open ON dbo.payables (tenant_id, status, due_at);
GO

IF OBJECT_ID('dbo.payable_payments') IS NULL
CREATE TABLE dbo.payable_payments
(
    id          uniqueidentifier NOT NULL CONSTRAINT pk_payable_payments PRIMARY KEY DEFAULT NEWID(),
    tenant_id   uniqueidentifier NOT NULL,
    payable_id  uniqueidentifier NOT NULL,
    amount      decimal(18,2)    NOT NULL,
    paid_at     date             NOT NULL,
    -- 1 efectivo · 2 transferencia · 3 cheque · 9 otro
    method      smallint         NOT NULL,
    reference   nvarchar(100)    NULL,
    created_by  uniqueidentifier NOT NULL,
    created_at  datetime2(3)     NOT NULL CONSTRAINT df_payable_payments_created DEFAULT SYSUTCDATETIME(),

    CONSTRAINT fk_payable_payments_payables FOREIGN KEY (payable_id) REFERENCES dbo.payables (id),
    CONSTRAINT ck_payable_payments_amount CHECK (amount > 0),
    CONSTRAINT ck_payable_payments_method CHECK (method IN (1, 2, 3, 9))
);
GO

-- Búsqueda para el detalle de la compra: código exacto primero, después por nombre.
CREATE OR ALTER PROCEDURE dbo.sp_purchase_product_find
    @p_tenant_id uniqueidentifier,
    @term        nvarchar(100),
    @max_rows    int = 10
AS
BEGIN
    SET NOCOUNT ON;

    SET @term = LTRIM(RTRIM(@term));

    SELECT TOP (@max_rows)
           p.id AS Id, p.code AS Code, p.name AS Name, p.is_taxable AS IsTaxable,
           p.units_per_package AS UnitsPerPackage, p.cost AS Cost, p.tracks_batches AS TracksBatches,
           p.base_price AS BasePrice
    FROM dbo.products p
    WHERE p.tenant_id = @p_tenant_id AND p.status <> 5
      AND (p.code = @term OR p.name LIKE N'%' + @term + N'%' OR p.code LIKE @term + N'%')
    ORDER BY CASE WHEN p.code = @term THEN 0 WHEN p.code LIKE @term + N'%' THEN 1 ELSE 2 END, p.name;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_product_set_purchase_info
    @p_tenant_id        uniqueidentifier,
    @product_id         uniqueidentifier,
    @code               nvarchar(50) = NULL,
    @units_per_package  int = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SET @code = NULLIF(LTRIM(RTRIM(@code)), N'');

    IF @units_per_package IS NOT NULL AND @units_per_package < 1
    BEGIN
        ;THROW 50000, N'VALIDATION_ERROR: las unidades por caja deben ser 1 o más', 1;
    END

    IF @code IS NOT NULL AND EXISTS (SELECT 1 FROM dbo.products
                                     WHERE tenant_id = @p_tenant_id AND code = @code AND id <> @product_id)
    BEGIN
        ;THROW 50000, N'CONFLICT: ese código ya lo usa otro producto', 1;
    END

    UPDATE dbo.products
    SET code              = ISNULL(@code, code),
        units_per_package = ISNULL(@units_per_package, units_per_package)
    WHERE id = @product_id AND tenant_id = @p_tenant_id;

    IF @@ROWCOUNT = 0
    BEGIN
        ;THROW 50000, N'NOT_FOUND: product does not exist for this tenant', 1;
    END
END
GO

-- Compra completa como la factura: cabecera, líneas ya calculadas y, si es a crédito, la cuenta por pagar.
-- Todo en una transacción, y la recepción (stock, costo promedio, kardex, lotes) es la de siempre.
CREATE OR ALTER PROCEDURE dbo.sp_purchase_create_invoice
    @p_tenant_id            uniqueidentifier,
    @supplier_id            uniqueidentifier,
    @created_by             uniqueidentifier,
    @branch_id              uniqueidentifier = NULL,
    @receive                bit = 1,
    @document_type          smallint,
    @document_number        nvarchar(60),
    @authorization_number   nvarchar(49) = NULL,
    @issued_at              date,
    @received_at            datetime2(3) = NULL,
    @document_expires_at    date = NULL,
    @payment_method         smallint = NULL,
    @is_credit              bit = 0,
    @credit_due_at          date = NULL,
    @vat_rate               decimal(5,4),
    @subtotal_zero          decimal(18,2),
    @subtotal_taxed         decimal(18,2),
    @discount_zero          decimal(18,2) = 0,
    @discount_taxed         decimal(18,2) = 0,
    @ice_amount             decimal(18,2) = 0,
    @tax_amount             decimal(18,2),
    @other_charges          decimal(18,2) = 0,
    @total                  decimal(18,2),
    @notes                  nvarchar(max) = NULL,
    @lines_json             nvarchar(max)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @document_type IS NULL OR @document_type NOT IN (1, 2, 3)
    BEGIN
        ;THROW 50000, N'VALIDATION_ERROR: elige el tipo de comprobante', 1;
    END

    SET @document_number = NULLIF(LTRIM(RTRIM(@document_number)), N'');
    IF @document_number IS NULL
    BEGIN
        ;THROW 50000, N'VALIDATION_ERROR: escribe el número del comprobante', 1;
    END

    IF @is_credit = 1 AND (@credit_due_at IS NULL OR @credit_due_at < @issued_at)
    BEGIN
        ;THROW 50000, N'VALIDATION_ERROR: una compra a crédito necesita su vencimiento, igual o posterior a la emisión', 1;
    END

    IF @lines_json IS NULL OR ISJSON(@lines_json) <> 1 OR NOT EXISTS (SELECT 1 FROM OPENJSON(@lines_json))
    BEGIN
        ;THROW 50000, N'VALIDATION_ERROR: la compra necesita al menos una línea', 1;
    END

    IF NOT EXISTS (SELECT 1 FROM dbo.suppliers WHERE id = @supplier_id AND tenant_id = @p_tenant_id)
    BEGIN
        ;THROW 50000, N'NOT_FOUND: supplier does not exist for this tenant', 1;
    END

    IF EXISTS (SELECT 1 FROM dbo.purchases
               WHERE tenant_id = @p_tenant_id AND supplier_id = @supplier_id AND document_type = @document_type
                 AND supplier_invoice_number = @document_number AND status <> 3)
    BEGIN
        ;THROW 50000, N'CONFLICT: ese comprobante del proveedor ya está registrado', 1;
    END

    SET @branch_id = ISNULL(@branch_id, dbo.fn_default_branch(@p_tenant_id));

    IF @branch_id IS NULL OR NOT EXISTS (SELECT 1 FROM dbo.branches WHERE id = @branch_id AND tenant_id = @p_tenant_id)
    BEGIN
        ;THROW 50000, N'NOT_FOUND: branch does not exist for this tenant', 1;
    END

    DECLARE @lines TABLE
    (
        product_id          uniqueidentifier NOT NULL,
        product_name        nvarchar(255)    NOT NULL,
        quantity            int              NOT NULL,
        invoice_quantity    decimal(18,3)    NOT NULL,
        package_label       nvarchar(20)     NULL,
        units_per_package   int              NOT NULL,
        invoice_unit_price  decimal(18,4)    NOT NULL,
        line_discount       decimal(18,2)    NOT NULL,
        taxable             bit              NOT NULL,
        effective_unit_cost decimal(18,4)    NOT NULL,
        line_total          decimal(18,2)    NOT NULL,
        lot_code            nvarchar(60)     NULL,
        expires_at          date             NULL,
        save_package        bit              NOT NULL
    );

    INSERT INTO @lines
    SELECT CAST(JSON_VALUE(value, '$.productId') AS uniqueidentifier),
           CAST(JSON_VALUE(value, '$.productName') AS nvarchar(255)),
           CAST(JSON_VALUE(value, '$.stockQuantity') AS int),
           CAST(JSON_VALUE(value, '$.invoiceQuantity') AS decimal(18,3)),
           JSON_VALUE(value, '$.packageLabel'),
           CAST(JSON_VALUE(value, '$.unitsPerPackage') AS int),
           CAST(JSON_VALUE(value, '$.invoiceUnitPrice') AS decimal(18,4)),
           CAST(JSON_VALUE(value, '$.discount') AS decimal(18,2)),
           CASE WHEN JSON_VALUE(value, '$.taxable') = 'true' THEN 1 ELSE 0 END,
           CAST(JSON_VALUE(value, '$.effectiveUnitCost') AS decimal(18,4)),
           CAST(JSON_VALUE(value, '$.lineTotal') AS decimal(18,2)),
           JSON_VALUE(value, '$.lotCode'),
           TRY_CAST(JSON_VALUE(value, '$.expiresAt') AS date),
           CASE WHEN JSON_VALUE(value, '$.savePackage') = 'true' THEN 1 ELSE 0 END
    FROM OPENJSON(@lines_json);

    IF EXISTS (SELECT 1 FROM @lines WHERE quantity <= 0 OR effective_unit_cost <= 0 OR invoice_unit_price <= 0 OR units_per_package < 1)
    BEGIN
        ;THROW 50000, N'VALIDATION_ERROR: cada línea necesita cantidad y precio mayores que cero', 1;
    END

    IF EXISTS (SELECT 1 FROM @lines l WHERE NOT EXISTS (SELECT 1 FROM dbo.products p WHERE p.id = l.product_id AND p.tenant_id = @p_tenant_id))
    BEGIN
        ;THROW 50000, N'NOT_FOUND: un producto de la compra no existe en esta tienda', 1;
    END

    DECLARE @purchase_id uniqueidentifier = NEWID();

    BEGIN TRAN;

    INSERT INTO dbo.purchases (id, tenant_id, supplier_id, status, supplier_invoice_number, purchased_at, notes,
                               created_by, branch_id, subtotal, tax_amount, total,
                               document_type, authorization_number, issued_at, document_expires_at, payment_method,
                               is_credit, credit_due_at, vat_rate, subtotal_zero, subtotal_taxed, discount_zero,
                               discount_taxed, ice_amount, other_charges)
    VALUES (@purchase_id, @p_tenant_id, @supplier_id, 1, @document_number, ISNULL(@received_at, SYSUTCDATETIME()), @notes,
            @created_by, @branch_id, @total - @tax_amount, @tax_amount, @total,
            @document_type, NULLIF(LTRIM(RTRIM(@authorization_number)), N''), @issued_at, @document_expires_at, @payment_method,
            @is_credit, CASE WHEN @is_credit = 1 THEN @credit_due_at END, @vat_rate, @subtotal_zero, @subtotal_taxed,
            @discount_zero, @discount_taxed, @ice_amount, @other_charges);

    INSERT INTO dbo.purchase_lines (id, tenant_id, purchase_id, product_id, product_name, quantity, unit_cost,
                                    cost_includes_tax, tax_rate, effective_unit_cost, line_total, lot_code, expires_at,
                                    invoice_quantity, package_label, units_per_package, invoice_unit_price,
                                    line_discount, taxable)
    SELECT NEWID(), @p_tenant_id, @purchase_id, product_id, product_name, quantity,
           -- unit_cost keeps its old meaning (cost of one stock unit, 2 decimals) for the screens that read it
           CASE WHEN ROUND(effective_unit_cost, 2) < 0.01 THEN 0.01 ELSE ROUND(effective_unit_cost, 2) END,
           1, CASE WHEN taxable = 1 THEN @vat_rate * 100 END, effective_unit_cost, line_total, lot_code, expires_at,
           invoice_quantity, package_label, units_per_package, invoice_unit_price, line_discount, taxable
    FROM @lines;

    -- FR-006: the box size typed on the invoice becomes the product's.
    UPDATE p SET units_per_package = l.units_per_package
    FROM dbo.products p
    JOIN (SELECT product_id, MAX(units_per_package) AS units_per_package FROM @lines WHERE save_package = 1 GROUP BY product_id) l
      ON l.product_id = p.id
    WHERE p.tenant_id = @p_tenant_id;

    IF @is_credit = 1
        INSERT INTO dbo.payables (id, tenant_id, purchase_id, supplier_id, amount, balance, due_at)
        VALUES (NEWID(), @p_tenant_id, @purchase_id, @supplier_id, @total, @total, @credit_due_at);

    IF @receive = 1
    BEGIN
        EXEC dbo.sp_purchase_receive
            @p_tenant_id = @p_tenant_id,
            @purchase_id = @purchase_id,
            @received_by = @created_by,
            @branch_id = @branch_id;
    END

    COMMIT;

    -- sp_purchase_receive already returned the received row; a single result set either way.
    IF @receive = 1
        RETURN;

    SELECT id AS Id, supplier_id AS SupplierId, status AS Status, branch_id AS BranchId,
           supplier_invoice_number AS SupplierInvoiceNumber, purchased_at AS PurchasedAt,
           received_at AS ReceivedAt, subtotal AS Subtotal, tax_amount AS TaxAmount,
           total AS Total, notes AS Notes
    FROM dbo.purchases
    WHERE id = @purchase_id;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_payable_list
    @p_tenant_id   uniqueidentifier,
    @include_paid  bit = 0
AS
BEGIN
    SET NOCOUNT ON;

    SELECT y.id AS Id, y.purchase_id AS PurchaseId, y.supplier_id AS SupplierId, s.name AS SupplierName,
           p.document_type AS DocumentType, p.supplier_invoice_number AS DocumentNumber, p.issued_at AS IssuedAt,
           y.amount AS Amount, y.balance AS Balance, y.due_at AS DueAt, y.status AS Status
    FROM dbo.payables y
    JOIN dbo.suppliers s ON s.id = y.supplier_id
    JOIN dbo.purchases p ON p.id = y.purchase_id
    WHERE y.tenant_id = @p_tenant_id AND (y.status = 1 OR (@include_paid = 1 AND y.status = 2))
    ORDER BY CASE WHEN y.status = 1 THEN 0 ELSE 1 END, y.due_at, s.name;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_payable_pay
    @p_tenant_id  uniqueidentifier,
    @payable_id   uniqueidentifier,
    @amount       decimal(18,2),
    @paid_at      date,
    @method       smallint,
    @reference    nvarchar(100) = NULL,
    @created_by   uniqueidentifier
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @amount IS NULL OR @amount <= 0
    BEGIN
        ;THROW 50000, N'VALIDATION_ERROR: el abono debe ser mayor que cero', 1;
    END

    IF @method IS NULL OR @method NOT IN (1, 2, 3, 9)
    BEGIN
        ;THROW 50000, N'VALIDATION_ERROR: forma de pago no válida', 1;
    END

    BEGIN TRAN;

    DECLARE @balance decimal(18,2), @status smallint;

    SELECT @balance = balance, @status = status
    FROM dbo.payables WITH (UPDLOCK, ROWLOCK)
    WHERE id = @payable_id AND tenant_id = @p_tenant_id;

    IF @status IS NULL
    BEGIN
        ROLLBACK;
        ;THROW 50000, N'NOT_FOUND: la cuenta por pagar no existe en esta tienda', 1;
    END

    IF @status <> 1
    BEGIN
        ROLLBACK;
        ;THROW 50000, N'CONFLICT: esa cuenta ya está pagada', 1;
    END

    IF @amount > @balance
    BEGIN
        ROLLBACK;
        DECLARE @message nvarchar(2048) =
            CONCAT(N'CONFLICT: el abono supera el saldo (', FORMAT(@balance, '0.00', 'en-US'), N')');
        THROW 50000, @message, 1;
    END

    INSERT INTO dbo.payable_payments (tenant_id, payable_id, amount, paid_at, method, reference, created_by)
    VALUES (@p_tenant_id, @payable_id, @amount, @paid_at, @method, NULLIF(LTRIM(RTRIM(@reference)), N''), @created_by);

    UPDATE dbo.payables
    SET balance = @balance - @amount,
        status  = CASE WHEN @balance - @amount = 0 THEN 2 ELSE 1 END
    WHERE id = @payable_id AND tenant_id = @p_tenant_id;

    COMMIT;

    SELECT id AS Id, balance AS Balance, status AS Status FROM dbo.payables WHERE id = @payable_id;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_payable_payments_list
    @p_tenant_id  uniqueidentifier,
    @payable_id   uniqueidentifier
AS
BEGIN
    SET NOCOUNT ON;

    SELECT id AS Id, amount AS Amount, paid_at AS PaidAt, method AS Method, reference AS Reference, created_at AS CreatedAt
    FROM dbo.payable_payments
    WHERE tenant_id = @p_tenant_id AND payable_id = @payable_id
    ORDER BY paid_at, created_at;
END
GO

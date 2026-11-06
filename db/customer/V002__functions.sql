-- ============================================================================
-- V002__functions.sql — Customer: register a buyer, edit it, search it
-- ============================================================================

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

-- ── sp_customer_create ──────────────────────────────────────────────────────
-- The first address arrives with the customer: an order always needs a shipping address (D7).
CREATE OR ALTER PROCEDURE dbo.sp_customer_create
    @p_tenant_id   uniqueidentifier,
    @first_name    nvarchar(100),
    @last_name     nvarchar(100),
    @email         nvarchar(255),
    @phone         nvarchar(30)  = NULL,
    @customer_type smallint      = 1,
    @line1         nvarchar(255) = NULL,
    @city          nvarchar(100) = NULL,
    @state_province nvarchar(100) = NULL,
    @postal_code   nvarchar(20)  = NULL,
    @country       nvarchar(100) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF LEN(LTRIM(RTRIM(@first_name))) = 0 OR LEN(LTRIM(RTRIM(@last_name))) = 0
    BEGIN
        ;THROW 50000, 'VALIDATION_ERROR: first and last name are required', 1;
    END

    IF @email IS NULL OR LEN(LTRIM(RTRIM(@email))) < 5 OR @email NOT LIKE '%_@_%._%'
    BEGIN
        ;THROW 50000, 'VALIDATION_ERROR: a valid email is required', 1;
    END

    IF EXISTS (SELECT 1 FROM dbo.customers WHERE tenant_id = @p_tenant_id AND email = @email)
    BEGIN
        ;THROW 50000, 'CONFLICT: another customer already uses that email in this store', 1;
    END

    DECLARE @customer_id uniqueidentifier = NEWID();

    BEGIN TRAN;

    INSERT INTO dbo.customers (id, tenant_id, first_name, last_name, email, phone, customer_type, status)
    VALUES (@customer_id, @p_tenant_id, @first_name, @last_name, @email, @phone, @customer_type, 1);

    IF @line1 IS NOT NULL AND LEN(LTRIM(RTRIM(@line1))) > 0
    BEGIN
        INSERT INTO dbo.addresses (id, tenant_id, customer_id, line1, city, state_province,
                                   postal_code, country, is_default)
        VALUES (NEWID(), @p_tenant_id, @customer_id, @line1,
                ISNULL(@city, ''), ISNULL(@state_province, ''), ISNULL(@postal_code, ''),
                ISNULL(@country, ''), 1);
    END

    COMMIT;

    SELECT id AS Id, first_name AS FirstName, last_name AS LastName, email AS Email,
           phone AS Phone, customer_type AS CustomerType, status AS Status, created_at AS CreatedAt
    FROM dbo.customers
    WHERE id = @customer_id;
END
GO

-- ── sp_customer_update ──────────────────────────────────────────────────────
CREATE OR ALTER PROCEDURE dbo.sp_customer_update
    @p_tenant_id   uniqueidentifier,
    @customer_id   uniqueidentifier,
    @first_name    nvarchar(100),
    @last_name     nvarchar(100),
    @phone         nvarchar(30) = NULL,
    @customer_type smallint     = 1
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF NOT EXISTS (SELECT 1 FROM dbo.customers WHERE id = @customer_id AND tenant_id = @p_tenant_id)
    BEGIN
        ;THROW 50000, 'NOT_FOUND: customer does not exist for this tenant', 1;
    END

    UPDATE dbo.customers
    SET first_name = @first_name, last_name = @last_name, phone = @phone,
        customer_type = @customer_type, updated_at = SYSUTCDATETIME()
    WHERE id = @customer_id AND tenant_id = @p_tenant_id;

    SELECT id AS Id, first_name AS FirstName, last_name AS LastName, email AS Email,
           phone AS Phone, customer_type AS CustomerType, status AS Status, created_at AS CreatedAt
    FROM dbo.customers
    WHERE id = @customer_id;
END
GO

-- ── sp_customer_soft_delete ─────────────────────────────────────────────────
-- Never deleted: the orders that point at this customer must keep resolving.
CREATE OR ALTER PROCEDURE dbo.sp_customer_soft_delete
    @p_tenant_id uniqueidentifier,
    @customer_id uniqueidentifier
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE dbo.customers
    SET status = 2, updated_at = SYSUTCDATETIME()
    WHERE id = @customer_id AND tenant_id = @p_tenant_id;

    IF @@ROWCOUNT = 0
    BEGIN
        ;THROW 50000, 'NOT_FOUND: customer does not exist for this tenant', 1;
    END
END
GO

-- ── sp_customer_get / sp_customer_list ──────────────────────────────────────
CREATE OR ALTER PROCEDURE dbo.sp_customer_get
    @p_tenant_id uniqueidentifier,
    @customer_id uniqueidentifier
AS
BEGIN
    SET NOCOUNT ON;

    SELECT id AS Id, first_name AS FirstName, last_name AS LastName, email AS Email,
           phone AS Phone, customer_type AS CustomerType, status AS Status, created_at AS CreatedAt
    FROM dbo.customers
    WHERE id = @customer_id AND tenant_id = @p_tenant_id;
END
GO

-- Search by name, email or phone: the counter needs to find a buyer while they are talking.
CREATE OR ALTER PROCEDURE dbo.sp_customer_list
    @p_tenant_id uniqueidentifier,
    @page_number int = 1,
    @page_size   int = 20,
    @search      nvarchar(255) = NULL,
    @include_inactive bit = 0
AS
BEGIN
    SET NOCOUNT ON;

    SET @page_number = CASE WHEN ISNULL(@page_number, 1) < 1 THEN 1 ELSE @page_number END;
    SET @page_size   = CASE WHEN ISNULL(@page_size, 20) < 1 THEN 20
                            WHEN @page_size > 100 THEN 100
                            ELSE @page_size END;

    SELECT id AS Id, first_name AS FirstName, last_name AS LastName, email AS Email,
           phone AS Phone, customer_type AS CustomerType, status AS Status, created_at AS CreatedAt,
           COUNT(*) OVER () AS TotalCount
    FROM dbo.customers
    WHERE tenant_id = @p_tenant_id
      AND (@include_inactive = 1 OR status = 1)
      AND (@search IS NULL
           OR first_name LIKE '%' + @search + '%'
           OR last_name LIKE '%' + @search + '%'
           OR email LIKE '%' + @search + '%'
           OR phone LIKE '%' + @search + '%')
    ORDER BY last_name, first_name
    OFFSET (@page_number - 1) * @page_size ROWS
    FETCH NEXT @page_size ROWS ONLY;
END
GO

-- ── sp_address_list / sp_address_add ────────────────────────────────────────
CREATE OR ALTER PROCEDURE dbo.sp_address_list
    @p_tenant_id uniqueidentifier,
    @customer_id uniqueidentifier
AS
BEGIN
    SET NOCOUNT ON;

    SELECT id AS Id, line1 AS Line1, city AS City, state_province AS StateProvince,
           postal_code AS PostalCode, country AS Country, is_default AS IsDefault
    FROM dbo.addresses
    WHERE customer_id = @customer_id AND tenant_id = @p_tenant_id
    ORDER BY is_default DESC, created_at;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_address_add
    @p_tenant_id   uniqueidentifier,
    @customer_id   uniqueidentifier,
    @line1         nvarchar(255),
    @city          nvarchar(100),
    @state_province nvarchar(100),
    @postal_code   nvarchar(20),
    @country       nvarchar(100),
    @is_default    bit = 0
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF NOT EXISTS (SELECT 1 FROM dbo.customers WHERE id = @customer_id AND tenant_id = @p_tenant_id)
    BEGIN
        ;THROW 50000, 'NOT_FOUND: customer does not exist for this tenant', 1;
    END

    IF @line1 IS NULL OR LEN(LTRIM(RTRIM(@line1))) = 0
    BEGIN
        ;THROW 50000, 'VALIDATION_ERROR: the address line is required', 1;
    END

    DECLARE @address_id uniqueidentifier = NEWID();

    BEGIN TRAN;

    IF @is_default = 1
    BEGIN
        UPDATE dbo.addresses SET is_default = 0
        WHERE customer_id = @customer_id AND tenant_id = @p_tenant_id AND is_default = 1;
    END

    INSERT INTO dbo.addresses (id, tenant_id, customer_id, line1, city, state_province,
                               postal_code, country, is_default)
    VALUES (@address_id, @p_tenant_id, @customer_id, @line1, @city, @state_province,
            @postal_code, @country, @is_default);

    COMMIT;

    SELECT id AS Id, line1 AS Line1, city AS City, state_province AS StateProvince,
           postal_code AS PostalCode, country AS Country, is_default AS IsDefault
    FROM dbo.addresses
    WHERE id = @address_id;
END
GO

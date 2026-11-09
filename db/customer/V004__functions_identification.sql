-- ============================================================================
-- V004__functions_identification.sql — Customer procedures with identification and business name
-- Spec 002 (FR-002, FR-006, FR-007, FR-008). Replaces the V002 versions of create, update, get
-- and list; the rest of V002 is unchanged. Format rules for cédula/RUC live in the shared C#
-- library (FR-009); the database enforces tenant isolation and uniqueness.
-- ============================================================================

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

-- ── sp_customer_create ──────────────────────────────────────────────────────
CREATE OR ALTER PROCEDURE dbo.sp_customer_create
    @p_tenant_id             uniqueidentifier,
    @first_name              nvarchar(100),
    @last_name               nvarchar(100),
    @email                   nvarchar(255) = NULL,
    @phone                   nvarchar(30)  = NULL,
    @customer_type           smallint      = 1,
    @line1                   nvarchar(255) = NULL,
    @city                    nvarchar(100) = NULL,
    @state_province          nvarchar(100) = NULL,
    @postal_code             nvarchar(20)  = NULL,
    @country                 nvarchar(100) = NULL,
    @identification_type     smallint      = NULL,
    @identification_number   nvarchar(20)  = NULL,
    @business_name           nvarchar(300) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    SET @first_name = LTRIM(RTRIM(ISNULL(@first_name, N'')));
    SET @last_name = LTRIM(RTRIM(ISNULL(@last_name, N'')));
    SET @business_name = NULLIF(LTRIM(RTRIM(@business_name)), N'');
    SET @email = NULLIF(LTRIM(RTRIM(@email)), N'');
    SET @identification_number = NULLIF(LTRIM(RTRIM(@identification_number)), N'');

    IF @business_name IS NULL AND (LEN(@first_name) = 0 OR LEN(@last_name) = 0)
    BEGIN
        ;THROW 50000, 'VALIDATION_ERROR: first and last name, or a business name, are required', 1;
    END

    IF @email IS NOT NULL AND (LEN(@email) < 5 OR @email NOT LIKE '%_@_%._%')
    BEGIN
        ;THROW 50000, 'VALIDATION_ERROR: the email is not valid', 1;
    END

    IF (@identification_type IS NULL AND @identification_number IS NOT NULL)
       OR (@identification_type IS NOT NULL AND @identification_number IS NULL)
       OR (@identification_type IS NOT NULL AND @identification_type NOT BETWEEN 4 AND 8)
    BEGIN
        ;THROW 50000, 'VALIDATION_ERROR: identification type and number must be given together', 1;
    END

    IF @email IS NOT NULL
       AND EXISTS (SELECT 1 FROM dbo.customers WHERE tenant_id = @p_tenant_id AND email = @email)
    BEGIN
        ;THROW 50000, 'CONFLICT: another customer already uses that email in this store', 1;
    END

    IF @identification_number IS NOT NULL
       AND EXISTS (SELECT 1 FROM dbo.customers
                   WHERE tenant_id = @p_tenant_id
                     AND identification_type = @identification_type
                     AND identification_number = @identification_number)
    BEGIN
        ;THROW 50000, 'CONFLICT: another customer already uses that identification in this store', 1;
    END

    DECLARE @customer_id uniqueidentifier = NEWID();

    BEGIN TRAN;

    INSERT INTO dbo.customers (id, tenant_id, first_name, last_name, email, phone, customer_type, status,
                               identification_type, identification_number, business_name)
    VALUES (@customer_id, @p_tenant_id, @first_name, @last_name, @email, @phone, @customer_type, 1,
            @identification_type, @identification_number, @business_name);

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
           phone AS Phone, customer_type AS CustomerType, status AS Status, created_at AS CreatedAt,
           identification_type AS IdentificationType, identification_number AS IdentificationNumber,
           business_name AS BusinessName
    FROM dbo.customers
    WHERE id = @customer_id AND tenant_id = @p_tenant_id;
END
GO

-- ── sp_customer_update ──────────────────────────────────────────────────────
-- Identification and business name can be corrected; uniqueness ignores the customer itself.
CREATE OR ALTER PROCEDURE dbo.sp_customer_update
    @p_tenant_id             uniqueidentifier,
    @customer_id             uniqueidentifier,
    @first_name              nvarchar(100),
    @last_name               nvarchar(100),
    @phone                   nvarchar(30)  = NULL,
    @customer_type           smallint      = 1,
    @identification_type     smallint      = NULL,
    @identification_number   nvarchar(20)  = NULL,
    @business_name           nvarchar(300) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    SET @first_name = LTRIM(RTRIM(ISNULL(@first_name, N'')));
    SET @last_name = LTRIM(RTRIM(ISNULL(@last_name, N'')));
    SET @business_name = NULLIF(LTRIM(RTRIM(@business_name)), N'');
    SET @identification_number = NULLIF(LTRIM(RTRIM(@identification_number)), N'');

    IF NOT EXISTS (SELECT 1 FROM dbo.customers WHERE id = @customer_id AND tenant_id = @p_tenant_id)
    BEGIN
        ;THROW 50000, 'NOT_FOUND: customer does not exist for this tenant', 1;
    END

    IF @business_name IS NULL AND (LEN(@first_name) = 0 OR LEN(@last_name) = 0)
    BEGIN
        ;THROW 50000, 'VALIDATION_ERROR: first and last name, or a business name, are required', 1;
    END

    IF (@identification_type IS NULL AND @identification_number IS NOT NULL)
       OR (@identification_type IS NOT NULL AND @identification_number IS NULL)
       OR (@identification_type IS NOT NULL AND @identification_type NOT BETWEEN 4 AND 8)
    BEGIN
        ;THROW 50000, 'VALIDATION_ERROR: identification type and number must be given together', 1;
    END

    IF @identification_number IS NOT NULL
       AND EXISTS (SELECT 1 FROM dbo.customers
                   WHERE tenant_id = @p_tenant_id
                     AND id <> @customer_id
                     AND identification_type = @identification_type
                     AND identification_number = @identification_number)
    BEGIN
        ;THROW 50000, 'CONFLICT: another customer already uses that identification in this store', 1;
    END

    UPDATE dbo.customers
    SET first_name = @first_name, last_name = @last_name, phone = @phone,
        customer_type = @customer_type,
        identification_type = @identification_type,
        identification_number = @identification_number,
        business_name = @business_name,
        updated_at = SYSUTCDATETIME()
    WHERE id = @customer_id AND tenant_id = @p_tenant_id;

    SELECT id AS Id, first_name AS FirstName, last_name AS LastName, email AS Email,
           phone AS Phone, customer_type AS CustomerType, status AS Status, created_at AS CreatedAt,
           identification_type AS IdentificationType, identification_number AS IdentificationNumber,
           business_name AS BusinessName
    FROM dbo.customers
    WHERE id = @customer_id AND tenant_id = @p_tenant_id;
END
GO

-- ── sp_customer_get ─────────────────────────────────────────────────────────
CREATE OR ALTER PROCEDURE dbo.sp_customer_get
    @p_tenant_id uniqueidentifier,
    @customer_id uniqueidentifier
AS
BEGIN
    SET NOCOUNT ON;

    SELECT id AS Id, first_name AS FirstName, last_name AS LastName, email AS Email,
           phone AS Phone, customer_type AS CustomerType, status AS Status, created_at AS CreatedAt,
           identification_type AS IdentificationType, identification_number AS IdentificationNumber,
           business_name AS BusinessName
    FROM dbo.customers
    WHERE id = @customer_id AND tenant_id = @p_tenant_id;
END
GO

-- ── sp_customer_list ────────────────────────────────────────────────────────
-- Searches names, business name, identification, email and phone (FR-002).
CREATE OR ALTER PROCEDURE dbo.sp_customer_list
    @p_tenant_id      uniqueidentifier,
    @page_number      int = 1,
    @page_size        int = 20,
    @search           nvarchar(255) = NULL,
    @include_inactive bit = 0
AS
BEGIN
    SET NOCOUNT ON;

    SET @page_number = CASE WHEN ISNULL(@page_number, 1) < 1 THEN 1 ELSE @page_number END;
    SET @page_size   = CASE WHEN ISNULL(@page_size, 20) < 1 THEN 20
                            WHEN @page_size > 100 THEN 100
                            ELSE @page_size END;
    SET @search = NULLIF(LTRIM(RTRIM(@search)), N'');

    SELECT id AS Id, first_name AS FirstName, last_name AS LastName, email AS Email,
           phone AS Phone, customer_type AS CustomerType, status AS Status, created_at AS CreatedAt,
           identification_type AS IdentificationType, identification_number AS IdentificationNumber,
           business_name AS BusinessName,
           COUNT(*) OVER () AS TotalCount
    FROM dbo.customers
    WHERE tenant_id = @p_tenant_id
      AND (@include_inactive = 1 OR status = 1)
      AND (@search IS NULL
           OR first_name LIKE '%' + @search + '%'
           OR last_name LIKE '%' + @search + '%'
           OR business_name LIKE '%' + @search + '%'
           OR identification_number LIKE '%' + @search + '%'
           OR email LIKE '%' + @search + '%'
           OR phone LIKE '%' + @search + '%')
    ORDER BY COALESCE(business_name, last_name + N' ' + first_name)
    OFFSET (@page_number - 1) * @page_size ROWS
    FETCH NEXT @page_size ROWS ONLY;
END
GO

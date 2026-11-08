-- Migration V017: Tax Configuration System
CREATE TABLE tax_rates (
    id INT IDENTITY(1,1) PRIMARY KEY,
    rate_percentage DECIMAL(5,4) NOT NULL,
    start_date DATE NOT NULL,
    end_date DATE NULL,
    description NVARCHAR(100) NULL,
    created_at DATETIME2 DEFAULT GETDATE()
);
GO

-- Seed: Configuración inicial solicitada
-- Periodo de rebaja (ejemplo: Enero)
INSERT INTO tax_rates (rate_percentage, start_date, end_date, description)
VALUES (0.08, '2026-01-01', '2026-01-31', 'IVA Rebajado Enero');

-- Periodo estándar (actual)
INSERT INTO tax_rates (rate_percentage, start_date, end_date, description)
VALUES (0.15, '2026-02-01', NULL, 'IVA Estándar');
GO
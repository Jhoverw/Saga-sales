-- Migration V017: Add IsTaxable to products
ALTER TABLE products ADD IsTaxable BIT NOT NULL DEFAULT 1;
GO
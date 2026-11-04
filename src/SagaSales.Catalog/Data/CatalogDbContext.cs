// RETIRED — the Catalog service no longer uses EF Core.
//
// This file used to hold CatalogDbContext. Catalog now persists through Data/ProductRepository.cs,
// Data/CategoryRepository.cs and Data/PriceListRepository.cs, over the procedures in
// db/catalog/V001..V004 (constitution §VIII: stored procedures, no ORM).
//
// Kept as a tombstone because this session cannot delete files. T102 of tasks.md removes it
// together with every other *DbContext.cs and the EF Core package references.

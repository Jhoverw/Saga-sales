// RETIRED — the Sales service no longer uses EF Core.
//
// This file used to hold SalesDbContext. Sales now persists through Data/OrderRepository.cs and
// the procedures in db/sales/V001..V002 (constitution §VIII: stored procedures, no ORM).
//
// Kept as a tombstone because this session cannot delete files. T102 of tasks.md removes it
// together with every other *DbContext.cs and the EF Core package references.

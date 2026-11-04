// RETIRED — the Identity service no longer uses EF Core.
//
// This file used to hold TenantDbContext. Identity now persists through
// Data/TenantRepository.cs and the procedures in db/identity/V002__functions.sql
// (constitution §VIII: stored procedures, no ORM).
//
// Kept as a tombstone because this session cannot delete files. Once the remaining services are
// migrated, T102 of tasks.md removes this file together with every other *DbContext.cs and the
// EF Core package references.

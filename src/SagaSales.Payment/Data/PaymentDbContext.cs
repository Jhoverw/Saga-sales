// RETIRED — the Payment service no longer uses EF Core.
//
// This file used to hold PaymentDbContext. Payment now persists through Data/PaymentRepository.cs
// and the procedures in db/payment/V001..V002 (constitution §VIII: stored procedures, no ORM).
//
// Kept as a tombstone because this session cannot delete files. T102 of tasks.md removes it with
// every other *DbContext.cs and the EF Core package references.

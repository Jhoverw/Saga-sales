// Analytics Service — REST contract.
// Declarative contract: this file documents the API, it is not compiled.
// Added in this revision: the CSV export (D4) had no contract.
// Procedures in data-model.md §8.

namespace SagaSales.Contracts;

public class SalesSummaryDto
{
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    public decimal TotalRevenue { get; set; }
    public int OrderCount { get; set; }
    public int PaymentSuccessCount { get; set; }
    public IReadOnlyList<TopProductDto> TopProducts { get; set; } = [];
}

public class TopProductDto
{
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = "";
    public int UnitsSold { get; set; }
    public decimal Revenue { get; set; }
}

public class DailyPointDto
{
    public DateOnly Date { get; set; }
    public decimal Revenue { get; set; }
    public int Orders { get; set; }
}

// HTTP Endpoints — bearer token + tenant required.
//
// GET /api/analytics/sales-summary      → 200 SalesSummaryDto    ?from&to  (ISO dates, inclusive)
// GET /api/analytics/daily              → 200 DailyPointDto[]    ?from&to
// GET /api/analytics/sales-summary.csv  → 200 text/csv           ?from&to
//
// Defaults: to = today, from = to - 6 days (a week). An inverted range → 400 INVALID_RANGE.
//
// CSV export format (D4, FR-026) — must open in Excel with no import wizard:
//   UTF-8 with BOM · separator ';' · decimal comma · one row per day · header row
//   columns: fecha;ingresos;pedidos;pagos_aprobados
//   A range with no movements returns the header only, never an error.
//   Content-Disposition: attachment; filename="reporte-YYYYMMDD-YYYYMMDD.csv"
//
// Invariants
//  - Figures come only from this service's aggregates, never from the Sales database (R11).
//  - A store can only read its own figures (FR-005, SC-008).
//  - The previous day's numbers must be complete before 06:00 (SC-009).

// Errors: 400 VALIDATION_ERROR | INVALID_RANGE · 401 · 403

// Domain events consumed
//   OrderPlacedEvent (revenue, order count, product sales)
//   PaymentAuthorizedEvent (successful payments)
//   CustomerCreatedEvent (customer activity)

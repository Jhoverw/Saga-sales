namespace SagaSales.Analytics.Domain;

/// <summary>Daily sales aggregate per tenant (US-013).</summary>
public class SalesReportDaily
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid TenantId { get; set; }

    public DateOnly ReportDate { get; set; }

    public decimal TotalRevenue { get; set; }

    public int OrderCount { get; set; }

    public int PaymentSuccessCount { get; set; }

    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>Units and revenue sold per product per day, used for the top-products panel.</summary>
public class ProductSalesDaily
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid TenantId { get; set; }

    public DateOnly ReportDate { get; set; }

    public Guid ProductId { get; set; }

    public string ProductName { get; set; } = string.Empty;

    public int UnitsSold { get; set; }

    public decimal Revenue { get; set; }
}

/// <summary>Customer activity feed.</summary>
public class CustomerActivity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid TenantId { get; set; }

    public Guid CustomerId { get; set; }

    public string EventType { get; set; } = string.Empty;

    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
}

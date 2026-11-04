namespace SagaSales.Analytics.Data;

using SagaSales.Analytics.Domain;

/// <summary>
/// Analytics database. It only holds aggregates built from integration events; the previous
/// version referenced the Sales, Customer and Catalog DbContexts directly.
/// </summary>
public class AnalyticsDbContext(DbContextOptions<AnalyticsDbContext> options) : DbContext(options)
{
    public DbSet<SalesReportDaily> SalesReports => Set<SalesReportDaily>();

    public DbSet<ProductSalesDaily> ProductSales => Set<ProductSalesDaily>();

    public DbSet<CustomerActivity> CustomerActivities => Set<CustomerActivity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<SalesReportDaily>(b =>
        {
            b.ToTable("sales_report_daily");
            b.HasKey(r => r.Id);
            b.Property(r => r.TotalRevenue).HasPrecision(18, 2);
            b.HasIndex(r => new { r.TenantId, r.ReportDate }).IsUnique();
        });

        modelBuilder.Entity<ProductSalesDaily>(b =>
        {
            b.ToTable("product_sales_daily");
            b.HasKey(p => p.Id);
            b.Property(p => p.ProductName).IsRequired().HasMaxLength(255);
            b.Property(p => p.Revenue).HasPrecision(18, 2);
            b.HasIndex(p => new { p.TenantId, p.ReportDate, p.ProductId }).IsUnique();
        });

        modelBuilder.Entity<CustomerActivity>(b =>
        {
            b.ToTable("customer_activity");
            b.HasKey(c => c.Id);
            b.Property(c => c.EventType).IsRequired().HasMaxLength(100);
            b.HasIndex(c => new { c.TenantId, c.OccurredAt });
        });
    }
}

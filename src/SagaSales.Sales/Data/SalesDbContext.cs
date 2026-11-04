namespace SagaSales.Sales.Data;

using SagaSales.Sales.Domain;

/// <summary>
/// Sales database. Owns orders only: product data arrives through the Catalog API,
/// never through a shared database connection.
/// </summary>
public class SalesDbContext(DbContextOptions<SalesDbContext> options) : DbContext(options)
{
    public DbSet<Order> Orders => Set<Order>();

    public DbSet<OrderItem> OrderItems => Set<OrderItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Order>(b =>
        {
            b.ToTable("orders");
            b.HasKey(o => o.Id);
            b.Property(o => o.Status).HasConversion<int>().HasDefaultValue(OrderStatus.Pending);
            b.Property(o => o.Subtotal).HasPrecision(18, 2);
            b.Property(o => o.PaidAmount).HasPrecision(18, 2);
            b.Property(o => o.ShippingAddress).HasMaxLength(500);
            b.Property(o => o.Notes).HasColumnType("text");
            b.HasMany(o => o.Items).WithOne(i => i.Order).HasForeignKey(i => i.OrderId);
            b.HasIndex(o => new { o.TenantId, o.OrderDate });
            b.HasIndex(o => new { o.TenantId, o.Status });
        });

        modelBuilder.Entity<OrderItem>(b =>
        {
            b.ToTable("order_items");
            b.HasKey(i => i.Id);
            b.Property(i => i.UnitPrice).HasPrecision(18, 2);
            b.Property(i => i.ProductSnapshotName).IsRequired().HasMaxLength(255);
            b.Property(i => i.ProductSnapshotPrice).HasPrecision(18, 2);
        });
    }
}

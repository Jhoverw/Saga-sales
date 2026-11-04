namespace SagaSales.Payment.Data;

using SagaSales.Payment.Domain;

/// <summary>Payment database. Entity types live in <c>Domain/</c>.</summary>
public class PaymentDbContext(DbContextOptions<PaymentDbContext> options) : DbContext(options)
{
    public DbSet<Payment> Payments => Set<Payment>();

    public DbSet<OrderReference> OrderReferences => Set<OrderReference>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Payment>(b =>
        {
            b.ToTable("payments");
            b.HasKey(p => p.Id);
            b.Property(p => p.Method).HasConversion<int>();
            b.Property(p => p.Status).HasConversion<int>();
            b.Property(p => p.Amount).HasPrecision(18, 2);
            b.Property(p => p.RefundedAmount).HasPrecision(18, 2);
            b.Property(p => p.Currency).IsRequired().HasMaxLength(3);
            b.Property(p => p.TransactionReference).HasMaxLength(255);
            b.Property(p => p.GatewayResponse).HasColumnType("nvarchar(max)");
            b.HasIndex(p => p.OrderId);
            b.HasIndex(p => new { p.TenantId, p.Status });
        });

        modelBuilder.Entity<OrderReference>(b =>
        {
            b.ToTable("order_references");
            b.HasKey(o => o.OrderId);
            b.Property(o => o.Subtotal).HasPrecision(18, 2);
            b.Property(o => o.Currency).IsRequired().HasMaxLength(3);
            b.HasIndex(o => o.TenantId);
        });
    }
}

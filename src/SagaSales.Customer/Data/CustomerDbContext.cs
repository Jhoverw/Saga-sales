namespace SagaSales.Customer.Data;

using SagaSales.Customer.Domain;

/// <summary>
/// Customer database. Entity types live in <c>Domain/</c>.
/// The previous model stored a <c>PasswordHash</c> on the customer: authentication belongs
/// to the Identity service.
/// </summary>
public class CustomerDbContext(DbContextOptions<CustomerDbContext> options) : DbContext(options)
{
    public DbSet<Customer> Customers => Set<Customer>();

    public DbSet<Address> Addresses => Set<Address>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Customer>(b =>
        {
            b.ToTable("customers");
            b.HasKey(c => c.Id);
            b.Property(c => c.FirstName).IsRequired().HasMaxLength(100);
            b.Property(c => c.LastName).IsRequired().HasMaxLength(100);
            b.Property(c => c.Email).IsRequired().HasMaxLength(255);
            b.Property(c => c.Phone).HasMaxLength(30);
            b.Property(c => c.Status).HasConversion<int>().HasDefaultValue(CustomerStatus.Active);
            b.HasIndex(c => new { c.TenantId, c.Email }).IsUnique();
            b.HasIndex(c => new { c.TenantId, c.Phone });
        });

        modelBuilder.Entity<Address>(b =>
        {
            b.ToTable("addresses");
            b.HasKey(a => a.Id);
            b.Property(a => a.Line1).IsRequired().HasMaxLength(255);
            b.Property(a => a.City).IsRequired().HasMaxLength(100);
            b.Property(a => a.StateProvince).HasMaxLength(100);
            b.Property(a => a.PostalCode).HasMaxLength(20);
            b.Property(a => a.Country).HasMaxLength(100);
            b.HasOne(a => a.Customer).WithMany(c => c.Addresses).HasForeignKey(a => a.CustomerId);
        });
    }
}

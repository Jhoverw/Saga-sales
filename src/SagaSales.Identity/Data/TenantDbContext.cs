namespace SagaSales.Identity.Data;

using SagaSales.Identity.Domain;

/// <summary>
/// Identity database: tenants and users. Entity types live in <c>Domain/</c>.
/// </summary>
public class TenantDbContext(DbContextOptions<TenantDbContext> options) : DbContext(options)
{
    public DbSet<Tenant> Tenants => Set<Tenant>();

    public DbSet<User> Users => Set<User>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Tenant>(b =>
        {
            b.ToTable("tenants");
            b.HasKey(t => t.Id);
            b.Property(t => t.Slug).IsRequired().HasMaxLength(50);
            b.Property(t => t.DisplayName).IsRequired().HasMaxLength(200);
            b.Property(t => t.Plan).IsRequired().HasMaxLength(30).HasDefaultValue("trial");
            b.Property(t => t.Status).HasConversion<int>().HasDefaultValue(TenantStatus.Trial);
            b.Property(t => t.CustomDomain).HasMaxLength(255);
            b.HasIndex(t => t.Slug).IsUnique();
        });

        modelBuilder.Entity<User>(b =>
        {
            b.ToTable("users");
            b.HasKey(u => u.Id);
            b.Property(u => u.Email).IsRequired().HasMaxLength(255);
            b.Property(u => u.PasswordHash).IsRequired().HasMaxLength(512);
            b.Property(u => u.Role).IsRequired().HasMaxLength(50);
            b.Property(u => u.DisplayName).IsRequired().HasMaxLength(100);
            b.HasOne(u => u.Tenant).WithMany(t => t.Users).HasForeignKey(u => u.TenantId);
            b.HasIndex(u => new { u.TenantId, u.Email }).IsUnique();
        });
    }
}

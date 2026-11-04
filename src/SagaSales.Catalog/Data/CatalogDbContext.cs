namespace SagaSales.Catalog.Data;

using SagaSales.Catalog.Domain;

/// <summary>
/// Catalog database. Entity types live in <c>Domain/</c>.
/// </summary>
public class CatalogDbContext(DbContextOptions<CatalogDbContext> options) : DbContext(options)
{
    public DbSet<Product> Products => Set<Product>();

    public DbSet<Category> Categories => Set<Category>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Product>(b =>
        {
            b.ToTable("products");
            b.HasKey(p => p.Id);
            b.Property(p => p.Name).IsRequired().HasMaxLength(255);
            b.Property(p => p.Description).HasColumnType("text");
            b.Property(p => p.Price).HasPrecision(18, 2);
            b.Property(p => p.Currency).IsRequired().HasMaxLength(3).HasDefaultValue("USD");
            b.Property(p => p.Status).HasConversion<int>().HasDefaultValue(ProductStatus.Active);
            b.HasOne(p => p.Category).WithMany(c => c.Products).HasForeignKey(p => p.CategoryId);
            b.HasIndex(p => new { p.TenantId, p.Name });
            b.HasIndex(p => new { p.TenantId, p.Status });
        });

        modelBuilder.Entity<Category>(b =>
        {
            b.ToTable("categories");
            b.HasKey(c => c.Id);
            b.Property(c => c.Name).IsRequired().HasMaxLength(100);
            b.Property(c => c.Slug).HasMaxLength(120);
            b.HasOne(c => c.Parent).WithMany(c => c.Children).HasForeignKey(c => c.ParentId);

            // Unique per tenant: the previous index was global and collided across tenants.
            b.HasIndex(c => new { c.TenantId, c.Slug }).IsUnique();
            b.HasIndex(c => new { c.TenantId, c.Name }).IsUnique();
        });
    }
}

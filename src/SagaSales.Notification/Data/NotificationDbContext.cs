namespace SagaSales.Notification.Data;

using SagaSales.Notification.Domain;

/// <summary>
/// Notification database. The previous model declared <c>Channel</c> as a string and then
/// configured it with <c>HasConversion&lt;int&gt;()</c>, which does not compile.
/// </summary>
public class NotificationDbContext(DbContextOptions<NotificationDbContext> options) : DbContext(options)
{
    public DbSet<NotificationLog> NotificationLogs => Set<NotificationLog>();

    public DbSet<NotificationTemplate> Templates => Set<NotificationTemplate>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<NotificationLog>(b =>
        {
            b.ToTable("notification_logs");
            b.HasKey(n => n.Id);
            b.Property(n => n.Type).HasConversion<int>();
            b.Property(n => n.Status).HasConversion<int>();
            b.Property(n => n.Recipient).IsRequired().HasMaxLength(320);
            b.Property(n => n.Subject).IsRequired().HasMaxLength(255);
            b.Property(n => n.Body).HasColumnType("text");
            b.Property(n => n.Error).HasColumnType("text");
            b.HasIndex(n => new { n.TenantId, n.Status });
            b.HasIndex(n => n.CustomerId);
        });

        modelBuilder.Entity<NotificationTemplate>(b =>
        {
            b.ToTable("notification_templates");
            b.HasKey(t => t.Id);
            b.Property(t => t.Key).IsRequired().HasMaxLength(100);
            b.Property(t => t.Subject).IsRequired().HasMaxLength(255);
            b.Property(t => t.Body).IsRequired().HasColumnType("text");
            b.Property(t => t.Variables).HasColumnType("nvarchar(max)");
            b.HasIndex(t => new { t.TenantId, t.Key }).IsUnique();
        });
    }
}

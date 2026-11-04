namespace SagaSales.Identity.Domain;

public class Tenant
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Slug { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string Plan { get; set; } = "trial";

    public TenantStatus Status { get; set; } = TenantStatus.Trial;

    public string? CustomDomain { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? SuspendedAt { get; set; }

    public IList<User> Users { get; set; } = [];
}

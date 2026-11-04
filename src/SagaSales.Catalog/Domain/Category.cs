namespace SagaSales.Catalog.Domain;

public class Category
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid TenantId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Slug { get; set; }

    public Guid? ParentId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Category? Parent { get; set; }

    public IList<Category> Children { get; set; } = [];

    public IList<Product> Products { get; set; } = [];
}

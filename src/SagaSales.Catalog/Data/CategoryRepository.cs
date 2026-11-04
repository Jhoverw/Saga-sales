namespace SagaSales.Catalog.Data;

using Dapper;
using SagaSales.BuildingBlocks.Data;
using SagaSales.Catalog.Domain;

/// <summary>Categories, through <c>db/catalog/V002__functions.sql</c>.</summary>
public interface ICategoryRepository
{
    Task<Category> CreateAsync(string name, string? slug, Guid? parentId, CancellationToken cancellationToken);

    Task<IReadOnlyList<Category>> ListAsync(CancellationToken cancellationToken);

    Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken);
}

public sealed class CategoryRepository(IStoredProcedureExecutor executor) : ICategoryRepository
{
    public async Task<Category> CreateAsync(
        string name,
        string? slug,
        Guid? parentId,
        CancellationToken cancellationToken)
    {
        var arguments = SqlArgs.New()
            .With("name", name)
            .With("slug", slug)
            .With("parent_id", parentId);

        var row = await executor.QueryProcSingleAsync<CategoryRow>(
            "dbo.sp_category_create", arguments, cancellationToken: cancellationToken);

        return new Category
        {
            Id = row!.Id,
            Name = row.Name,
            Slug = row.Slug,
            ParentId = row.ParentId,
        };
    }

    public async Task<IReadOnlyList<Category>> ListAsync(CancellationToken cancellationToken)
    {
        var rows = await executor.QueryProcAsync<CategoryRow>(
            "dbo.sp_category_list", cancellationToken: cancellationToken);

        return rows
            .Select(r => new Category { Id = r.Id, Name = r.Name, Slug = r.Slug, ParentId = r.ParentId })
            .ToList();
    }

    public async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken)
    {
        var categories = await ListAsync(cancellationToken);
        return categories.Any(c => c.Id == id);
    }

    private sealed class CategoryRow
    {
        public Guid Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public string? Slug { get; set; }

        public Guid? ParentId { get; set; }
    }
}

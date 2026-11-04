namespace SagaSales.Catalog.Queries;

public record GetCategoriesQuery : IQuery<IReadOnlyList<CategoryDto>>;

public sealed class GetCategoriesQueryHandler(
    CatalogDbContext dbContext,
    ICurrentUser currentUser) : IQueryHandler<GetCategoriesQuery, IReadOnlyList<CategoryDto>>
{
    public async Task<Result<IReadOnlyList<CategoryDto>>> Handle(
        GetCategoriesQuery request,
        CancellationToken cancellationToken)
    {
        var tenantId = currentUser.RequireTenantId();

        var categories = await dbContext.Categories
            .AsNoTracking()
            .Where(c => c.TenantId == tenantId)
            .OrderBy(c => c.Name)
            .ToListAsync(cancellationToken);

        IReadOnlyList<CategoryDto> result = categories.Select(c => c.ToDto()).ToList();
        return Result<IReadOnlyList<CategoryDto>>.Success(result);
    }
}

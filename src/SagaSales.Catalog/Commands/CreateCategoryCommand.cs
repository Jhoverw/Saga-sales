namespace SagaSales.Catalog.Commands;

public record CreateCategoryCommand(string Name, string? Slug = null, Guid? ParentId = null)
    : ICommand<CategoryDto>;

public sealed class CreateCategoryCommandHandler(
    CatalogDbContext dbContext,
    ICurrentUser currentUser) : ICommandHandler<CreateCategoryCommand, CategoryDto>
{
    public async Task<Result<CategoryDto>> Handle(CreateCategoryCommand request, CancellationToken cancellationToken)
    {
        Guard.AgainstNullOrEmpty(request.Name, nameof(request.Name));

        var tenantId = currentUser.RequireTenantId();
        var name = request.Name.Trim();
        var slug = string.IsNullOrWhiteSpace(request.Slug) ? Slugify(name) : Slugify(request.Slug);

        var duplicate = await dbContext.Categories
            .AnyAsync(c => c.TenantId == tenantId && (c.Name == name || c.Slug == slug), cancellationToken);

        if (duplicate)
        {
            throw new ConflictException($"Category '{name}' already exists for this tenant.");
        }

        if (request.ParentId is { } parentId)
        {
            var parentExists = await dbContext.Categories
                .AnyAsync(c => c.Id == parentId && c.TenantId == tenantId, cancellationToken);

            if (!parentExists)
            {
                throw new NotFoundException("Category", parentId);
            }
        }

        var category = new Domain.Category
        {
            TenantId = tenantId,
            Name = name,
            Slug = slug,
            ParentId = request.ParentId,
        };

        dbContext.Categories.Add(category);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result<CategoryDto>.Success(category.ToDto());
    }

    private static string Slugify(string value) => string.Join(
        '-',
        value.ToLowerInvariant()
            .Split([' ', '_', '/'], StringSplitOptions.RemoveEmptyEntries));
}

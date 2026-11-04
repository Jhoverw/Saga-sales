namespace SagaSales.Catalog.Commands;

public record CreateCategoryCommand(string Name, string? Slug = null, Guid? ParentId = null)
    : ICommand<CategoryDto>;

public sealed class CreateCategoryCommandHandler(ICategoryRepository repository)
    : ICommandHandler<CreateCategoryCommand, CategoryDto>
{
    public async Task<Result<CategoryDto>> Handle(
        CreateCategoryCommand request,
        CancellationToken cancellationToken)
    {
        Guard.AgainstNullOrEmpty(request.Name, nameof(request.Name));

        var name = request.Name.Trim();
        var slug = Slugify(string.IsNullOrWhiteSpace(request.Slug) ? name : request.Slug);

        // A duplicate name or slug surfaces from the procedure as CONFLICT → 409.
        var category = await repository.CreateAsync(name, slug, request.ParentId, cancellationToken);

        return Result<CategoryDto>.Success(category.ToDto());
    }

    private static string Slugify(string value) => string.Join(
        '-',
        value.ToLowerInvariant().Split([' ', '_', '/'], StringSplitOptions.RemoveEmptyEntries));
}

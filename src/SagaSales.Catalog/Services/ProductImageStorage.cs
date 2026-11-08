namespace SagaSales.Catalog.Services;

/// <summary>
/// Writes a product's picture to the image volume and returns the URL path it is served under.
/// The path is deterministic (<c>products/{productId}.{ext}</c>), so uploading again replaces the
/// previous file. Serving is done by the static-files middleware mapped to <c>/images</c>.
/// </summary>
public interface IProductImageStorage
{
    /// <summary>Absolute root directory that is served under <c>/images</c>.</summary>
    string RootPath { get; }

    Task<string> SaveAsync(Guid productId, IFormFile file, CancellationToken cancellationToken);

    void Delete(Guid productId);
}

public sealed class ProductImageStorage : IProductImageStorage
{
    private const long MaxBytes = 5 * 1024 * 1024;

    private static readonly Dictionary<string, string> ExtensionsByContentType =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["image/jpeg"] = ".jpg",
            ["image/png"] = ".png",
            ["image/webp"] = ".webp",
            ["image/gif"] = ".gif",
        };

    private readonly string _productsDirectory;

    public ProductImageStorage(IConfiguration configuration, IHostEnvironment environment)
    {
        var configured = configuration["Storage:ImageRoot"];
        RootPath = string.IsNullOrWhiteSpace(configured)
            ? Path.Combine(environment.ContentRootPath, "uploads")
            : configured;

        _productsDirectory = Path.Combine(RootPath, "products");
        Directory.CreateDirectory(_productsDirectory);
    }

    public string RootPath { get; }

    public async Task<string> SaveAsync(Guid productId, IFormFile file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            throw new ValidationException("Selecciona una imagen para subir.");
        }

        if (file.Length > MaxBytes)
        {
            throw new ValidationException("La imagen no puede superar los 5 MB.");
        }

        if (!ExtensionsByContentType.TryGetValue(file.ContentType, out var extension))
        {
            throw new ValidationException(
                "Formato no admitido. Usa JPEG, PNG, WebP o GIF.",
                new Dictionary<string, string[]> { ["file"] = ["Formato de imagen no admitido."] });
        }

        Delete(productId);

        var fileName = $"{productId}{extension}";
        var fullPath = Path.Combine(_productsDirectory, fileName);

        await using (var stream = File.Create(fullPath))
        {
            await file.CopyToAsync(stream, cancellationToken);
        }

        return $"images/products/{fileName}";
    }

    public void Delete(Guid productId)
    {
        foreach (var extension in ExtensionsByContentType.Values.Distinct())
        {
            var path = Path.Combine(_productsDirectory, $"{productId}{extension}");
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}

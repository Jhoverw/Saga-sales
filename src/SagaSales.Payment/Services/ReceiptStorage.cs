namespace SagaSales.Payment.Services;

/// <summary>
/// Tipo real de un comprobante según sus primeros bytes (spec 007, FR-001). El Content-Type y la extensión
/// los pone el navegador; la firma del archivo no se puede cambiar sin romperlo.
/// </summary>
public static class ReceiptFileType
{
    public const long MaxBytes = 5 * 1024 * 1024;

    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    /// <summary>image/jpeg, image/png, image/webp o application/pdf; null si no es ninguno.</summary>
    public static string? Detect(ReadOnlySpan<byte> head)
    {
        if (head.Length >= 3 && head[0] == 0xFF && head[1] == 0xD8 && head[2] == 0xFF)
        {
            return "image/jpeg";
        }

        if (head.Length >= 8 && MemoryExtensions.SequenceEqual<byte>(head[..8], PngSignature))
        {
            return "image/png";
        }

        if (head.Length >= 12 && head[..4].SequenceEqual("RIFF"u8) && head[8..12].SequenceEqual("WEBP"u8))
        {
            return "image/webp";
        }

        if (head.Length >= 5 && head[..5].SequenceEqual("%PDF-"u8))
        {
            return "application/pdf";
        }

        return null;
    }

    public static string Extension(string contentType) => contentType switch
    {
        "image/jpeg" => ".jpg",
        "image/png" => ".png",
        "image/webp" => ".webp",
        _ => ".pdf",
    };
}

/// <summary>Comprobantes en disco, fuera de cualquier carpeta servida en público (FR-002).</summary>
public interface IReceiptStorage
{
    Task SaveAsync(string storageKey, byte[] content, CancellationToken cancellationToken);

    Task<byte[]?> ReadAsync(string storageKey, CancellationToken cancellationToken);
}

public sealed class ReceiptStorage : IReceiptStorage
{
    private readonly string _root;

    public ReceiptStorage(IConfiguration configuration, IHostEnvironment environment)
    {
        var configured = configuration["Storage:ReceiptRoot"];
        _root = Path.GetFullPath(string.IsNullOrWhiteSpace(configured)
            ? Path.Combine(environment.ContentRootPath, "receipts")
            : configured);

        Directory.CreateDirectory(_root);
    }

    /// <summary>Clave relativa: <c>{tienda}/{id}{extensión}</c>. La arma el servidor, nunca el cliente.</summary>
    public static string KeyFor(Guid tenantId, Guid attachmentId, string contentType) =>
        $"{tenantId:N}/{attachmentId:N}{ReceiptFileType.Extension(contentType)}";

    public async Task SaveAsync(string storageKey, byte[] content, CancellationToken cancellationToken)
    {
        var path = Resolve(storageKey);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllBytesAsync(path, content, cancellationToken);
    }

    public async Task<byte[]?> ReadAsync(string storageKey, CancellationToken cancellationToken)
    {
        var path = Resolve(storageKey);
        return File.Exists(path) ? await File.ReadAllBytesAsync(path, cancellationToken) : null;
    }

    private string Resolve(string storageKey)
    {
        var path = Path.GetFullPath(Path.Combine(_root, storageKey));

        // Defensa en profundidad: la clave nunca debe salir de la carpeta de comprobantes.
        if (!path.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Ruta de comprobante fuera de la carpeta permitida.");
        }

        return path;
    }
}

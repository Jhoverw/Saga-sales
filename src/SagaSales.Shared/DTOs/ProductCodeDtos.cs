namespace SagaSales.Shared.DTOs;

/// <summary>Un producto encontrado por el buscador único (spec 010): por código de barras, código interno o nombre.</summary>
public class ProductSearchResultDto : ProductDto
{
    /// <summary>Código interno (N.º de artículo).</summary>
    public string? Code { get; set; }

    /// <summary>El código de barras que coincidió, o el principal del producto.</summary>
    public string? Barcode { get; set; }

    /// <summary>Unidades del código que coincidió: 12 si se escaneó el código de la caja; 1 en otro caso.</summary>
    public int? MatchedUnits { get; set; }

    /// <summary>Coincidencia exacta de código de barras o código interno: se agrega sin elegir.</summary>
    public bool IsExactMatch { get; set; }

    public int? UnitsPerPackage { get; set; }

    /// <summary>1 bien · 2 servicio.</summary>
    public int Kind { get; set; } = 1;
}

public class ProductBarcodeDto
{
    public Guid Id { get; set; }

    public Guid ProductId { get; set; }

    public string Barcode { get; set; } = string.Empty;

    /// <summary>1 = unidad; más de 1 = caja o fardo con esas unidades.</summary>
    public int Units { get; set; } = 1;

    public bool IsPrimary { get; set; }

    public DateTime CreatedAt { get; set; }

    /// <summary>Advertencia (dígito verificador) al guardarlo; no impide guardar.</summary>
    public string? Warning { get; set; }
}

public class AddProductBarcodeRequest
{
    public string Barcode { get; set; } = string.Empty;

    public int Units { get; set; } = 1;

    public bool IsPrimary { get; set; }
}

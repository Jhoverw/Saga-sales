namespace SagaSales.Shared.Purchasing;

/// <summary>Una línea de la factura del proveedor, como viene en el papel (spec 009).</summary>
/// <param name="Quantity">Cantidad de la factura, en su empaque (cajas o unidades).</param>
/// <param name="UnitsPerPackage">Unidades de stock por empaque; 1 si se compra por unidad.</param>
/// <param name="UnitPrice">Precio de un empaque, sin IVA.</param>
/// <param name="Taxable">Si la línea grava IVA.</param>
public readonly record struct InvoiceLineInput(decimal Quantity, int UnitsPerPackage, decimal UnitPrice, bool Taxable);

/// <summary>Lo que se guarda de cada línea: su parte del descuento, del ICE, de otros y del IVA, y el costo por unidad.</summary>
public readonly record struct InvoiceLineResult(
    decimal Net,
    decimal Discount,
    decimal Ice,
    decimal Others,
    decimal Tax,
    decimal LineTotal,
    int StockQuantity,
    decimal EffectiveUnitCost);

public sealed record InvoiceTotals(
    decimal SubtotalZero,
    decimal SubtotalTaxed,
    decimal DiscountZero,
    decimal DiscountTaxed,
    decimal Ice,
    decimal Tax,
    decimal Others,
    decimal Total,
    IReadOnlyList<InvoiceLineResult> Lines);

/// <summary>
/// Totales de una factura de compra y el costo real de cada unidad que entra al inventario (spec 009, FR-004 y
/// FR-005). Lo usan el servidor al guardar y el panel para mostrar los totales mientras se escribe, así que
/// ambos dan exactamente lo mismo.
/// <list type="bullet">
/// <item>Descuento de cada grupo (0 % y con IVA) repartido entre sus líneas por su valor.</item>
/// <item>ICE repartido entre las líneas con IVA; el IVA se calcula sobre (subtotal con IVA − descuento + ICE).</item>
/// <item>Otros cargos (flete, etc.) repartidos entre todas las líneas.</item>
/// <item>Cada reparto se redondea a centavos y el último centavo va a la línea mayor: la suma da el total exacto.</item>
/// </list>
/// </summary>
public static class PurchaseInvoiceCalculator
{
    public static InvoiceTotals Compute(
        IReadOnlyList<InvoiceLineInput> lines,
        decimal vatRate,
        decimal discountZero = 0,
        decimal discountTaxed = 0,
        decimal ice = 0,
        decimal others = 0)
    {
        if (lines.Count == 0)
        {
            throw new ArgumentException("La compra necesita al menos una línea.", nameof(lines));
        }

        if (vatRate is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(vatRate), "La tasa de IVA va como fracción: 0.15 = 15 %.");
        }

        if (discountZero < 0 || discountTaxed < 0 || ice < 0 || others < 0)
        {
            throw new ArgumentException("Descuentos, ICE y otros no pueden ser negativos.");
        }

        var net = lines.Select(l => Round(l.Quantity * l.UnitPrice)).ToArray();
        var taxable = lines.Select(l => l.Taxable).ToArray();

        var subtotalZero = net.Where((_, i) => !taxable[i]).Sum();
        var subtotalTaxed = net.Where((_, i) => taxable[i]).Sum();

        if (discountZero > subtotalZero || discountTaxed > subtotalTaxed)
        {
            throw new ArgumentException("El descuento no puede superar su subtotal.");
        }

        var discount = new decimal[lines.Count];
        Spread(discountZero, net, i => !taxable[i], discount);
        Spread(discountTaxed, net, i => taxable[i], discount);

        var bases = net.Select((n, i) => n - discount[i]).ToArray();

        // ICE goes on the taxed lines; with none, on every line.
        var iceShare = new decimal[lines.Count];
        var anyTaxed = taxable.Any(t => t);
        Spread(ice, bases, i => !anyTaxed || taxable[i], iceShare);

        var taxBase = bases.Select((b, i) => taxable[i] ? b + iceShare[i] : 0m).ToArray();
        var tax = Round(taxBase.Sum() * vatRate);
        var taxShare = new decimal[lines.Count];
        Spread(tax, taxBase, i => taxable[i], taxShare);

        var otherShare = new decimal[lines.Count];
        Spread(others, bases, _ => true, otherShare);

        var results = new InvoiceLineResult[lines.Count];
        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            if (line.Quantity <= 0 || line.UnitPrice <= 0 || line.UnitsPerPackage < 1)
            {
                throw new ArgumentException($"La línea {i + 1} necesita cantidad, precio y unidades por empaque mayores que cero.");
            }

            var units = line.Quantity * line.UnitsPerPackage;
            if (units != decimal.Truncate(units))
            {
                throw new ArgumentException(
                    $"La línea {i + 1} da {units} unidades: el inventario cuenta unidades enteras.");
            }

            var lineTotal = bases[i] + iceShare[i] + otherShare[i] + taxShare[i];
            var stock = (int)units;
            results[i] = new InvoiceLineResult(
                net[i],
                discount[i],
                iceShare[i],
                otherShare[i],
                taxShare[i],
                lineTotal,
                stock,
                Math.Round(lineTotal / stock, 4, MidpointRounding.AwayFromZero));
        }

        var total = subtotalZero - discountZero + subtotalTaxed - discountTaxed + ice + tax + others;

        return new InvoiceTotals(subtotalZero, subtotalTaxed, discountZero, discountTaxed, ice, tax, others, total, results);
    }

    private static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    /// <summary>Spreads <paramref name="amount"/> over the selected lines by weight; the rounding rest goes to the largest.</summary>
    private static void Spread(decimal amount, decimal[] weights, Func<int, bool> selected, decimal[] into)
    {
        if (amount == 0)
        {
            return;
        }

        var indexes = Enumerable.Range(0, weights.Length).Where(selected).ToList();
        if (indexes.Count == 0)
        {
            return;
        }

        var totalWeight = indexes.Sum(i => weights[i]);
        var given = 0m;
        foreach (var i in indexes)
        {
            var share = totalWeight == 0 ? amount / indexes.Count : amount * weights[i] / totalWeight;
            into[i] += Round(share);
            given += Round(share);
        }

        var largest = indexes.OrderByDescending(i => weights[i]).First();
        into[largest] += amount - given;
    }
}

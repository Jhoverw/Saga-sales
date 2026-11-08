using System.Text;
using SagaSales.Analytics.Data;
using SagaSales.Analytics.Queries;

namespace SagaSales.Analytics.Services;

public interface ISalesSummaryCsvExporter
{
    Task<byte[]> ExportAsync(
        Guid tenantId, 
        DateOnly from, 
        DateOnly to, 
        CancellationToken cancellationToken);
}

public sealed class SalesSummaryCsvExporter(IAnalyticsRepository repository) : ISalesSummaryCsvExporter
{
    public async Task<byte[]> ExportAsync(
        Guid tenantId, 
        DateOnly from, 
        DateOnly to, 
        CancellationToken cancellationToken)
    {
        var topProducts = await repository.GetTopProductsAsync(tenantId, from, to, cancellationToken);
        
        var sb = new StringBuilder();
        // BOM for Excel
        sb.Append('\uFEFF');
        
        // Headers
        sb.AppendLine("Producto;Unidades Vendidas;Ingresos");
        
        foreach (var product in topProducts)
        {
            sb.AppendLine($"{product.ProductName};{product.UnitsSold};{product.Revenue.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)}");
        }
        
        return Encoding.UTF8.GetBytes(sb.ToString());
    }
}

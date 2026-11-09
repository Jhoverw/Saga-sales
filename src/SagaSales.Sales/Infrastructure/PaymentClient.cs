namespace SagaSales.Sales.Infrastructure;

/// <summary>
/// Pregunta a Payment si los pagos de un pedido tienen respaldo (spec 007). Sales nunca lee la base de
/// Payment; la llamada viaja con el token y la tienda del usuario.
/// </summary>
public interface IPaymentClient
{
    Task<OrderProofStatusDto> GetProofStatusAsync(Guid orderId, CancellationToken cancellationToken);
}

public sealed class PaymentClient(HttpClient http) : IPaymentClient
{
    public async Task<OrderProofStatusDto> GetProofStatusAsync(Guid orderId, CancellationToken cancellationToken)
    {
        using var response = await http.GetAsync($"api/payments/proof-status?orderId={orderId}", cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<OrderProofStatusDto>(cancellationToken)
               ?? new OrderProofStatusDto();
    }
}

namespace SagaSales.Sales.Infrastructure;

using SagaSales.BuildingBlocks.Extensions;

/// <summary>
/// Talks to the Catalog service over HTTP. Sales used to reference Catalog's
/// <c>DbContext</c> directly, which made the two "microservices" impossible to deploy apart.
/// </summary>
public interface ICatalogClient
{
    Task<ProductDto> GetProductAsync(Guid productId, CancellationToken cancellationToken);

    Task ReserveStockAsync(Guid productId, int quantity, CancellationToken cancellationToken);

    Task ReleaseStockAsync(Guid productId, int quantity, CancellationToken cancellationToken);
}

public sealed class CatalogClient(HttpClient http) : ICatalogClient
{
    public async Task<ProductDto> GetProductAsync(Guid productId, CancellationToken cancellationToken)
    {
        using var response = await http.GetAsync($"api/products/{productId}", cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            throw new NotFoundException("Product", productId);
        }

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<ProductDto>(cancellationToken)
               ?? throw new NotFoundException("Product", productId);
    }

    public async Task ReserveStockAsync(Guid productId, int quantity, CancellationToken cancellationToken)
    {
        using var response = await http.PostAsJsonAsync(
            $"api/products/{productId}/stock/reserve",
            new ReserveStockRequest { Quantity = quantity },
            cancellationToken);

        await EnsureReservedAsync(response, productId, cancellationToken);
    }

    public async Task ReleaseStockAsync(Guid productId, int quantity, CancellationToken cancellationToken)
    {
        using var response = await http.PostAsJsonAsync(
            $"api/products/{productId}/stock/release",
            new ReserveStockRequest { Quantity = quantity },
            cancellationToken);

        response.EnsureSuccessStatusCode();
    }

    private static async Task EnsureReservedAsync(
        HttpResponseMessage response,
        Guid productId,
        CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        if (response.StatusCode is HttpStatusCode.Conflict or HttpStatusCode.BadRequest)
        {
            var error = await response.Content.ReadFromJsonAsync<ApiError>(cancellationToken);
            throw new ConflictException(error?.Message ?? $"Could not reserve stock for product {productId}.");
        }

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            throw new NotFoundException("Product", productId);
        }

        response.EnsureSuccessStatusCode();
    }
}

/// <summary>
/// Forwards the caller's bearer token and tenant to downstream services, so the
/// catalog sees the same identity the gateway authenticated.
/// </summary>
public sealed class ForwardCredentialsHandler(IHttpContextAccessor accessor, ICurrentUser currentUser)
    : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var context = accessor.HttpContext;

        if (context is not null &&
            context.Request.Headers.TryGetValue("Authorization", out var authorization))
        {
            request.Headers.TryAddWithoutValidation("Authorization", authorization.ToString());
        }

        if (currentUser.TenantId is { } tenantId)
        {
            request.Headers.TryAddWithoutValidation(SagaSalesConstants.Headers.TenantId, tenantId.ToString());
        }

        return base.SendAsync(request, cancellationToken);
    }
}

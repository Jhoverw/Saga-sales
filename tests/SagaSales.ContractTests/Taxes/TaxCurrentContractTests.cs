using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using SagaSales.ContractTests.Catalog;
using SagaSales.Shared.DTOs;
using Xunit;

namespace SagaSales.ContractTests.Taxes;

/// <summary>
/// V-03 contract: <c>GET /api/taxes/current?date=</c> returns the VAT rate in force on that date, and
/// 404 when no rate covers it. Requires <c>SAGASALES_GATEWAY_URL</c> and a running stack.
/// </summary>
public class TaxCurrentContractTests
{
    [Fact]
    public async Task Current_rate_is_the_one_in_force_on_the_requested_date()
    {
        if (Gateway.TryAnnounceSkip(nameof(Current_rate_is_the_one_in_force_on_the_requested_date)))
        {
            return;
        }

        using var client = Gateway.CreateClient();
        var session = await SignedInStore.CreateAsync(client);

        using var response = await client.SendAsync(Authorized(session, "api/taxes/current?date=2026-11-08"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var rate = await response.Content.ReadFromJsonAsync<TaxRateDto>();
        Assert.NotNull(rate);
        Assert.Equal(0.15m, rate!.RatePercentage);
    }

    [Fact]
    public async Task Current_rate_returns_404_when_no_rate_covers_the_date()
    {
        if (Gateway.TryAnnounceSkip(nameof(Current_rate_returns_404_when_no_rate_covers_the_date)))
        {
            return;
        }

        using var client = Gateway.CreateClient();
        var session = await SignedInStore.CreateAsync(client);

        // The store's rate starts in 2000: 1999 is not covered.
        using var response = await client.SendAsync(Authorized(session, "api/taxes/current?date=1999-06-30"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static HttpRequestMessage Authorized(SignedInStore session, string path)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session.Token);
        request.Headers.TryAddWithoutValidation("X-Tenant-Id", session.TenantId.ToString());
        return request;
    }
}

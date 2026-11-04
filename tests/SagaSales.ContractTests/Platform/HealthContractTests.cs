using Xunit;

namespace SagaSales.ContractTests.Platform;

/// <summary>
/// The first contract every deployment must satisfy: the gateway is up and reports healthy.
/// Story-level contract tests (auth, catalogue, orders, payments) live in their own folders.
/// </summary>
public class HealthContractTests
{
    [Fact]
    public async Task Gateway_reports_healthy()
    {
        if (Gateway.TryAnnounceSkip(nameof(Gateway_reports_healthy)))
        {
            return;
        }

        using var client = Gateway.CreateClient();
        using var response = await client.GetAsync("health");

        Assert.True(
            response.IsSuccessStatusCode,
            $"GET /health returned {(int)response.StatusCode} {response.ReasonPhrase}");
    }
}

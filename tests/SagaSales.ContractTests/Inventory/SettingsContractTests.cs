using System.Net;
using System.Net.Http.Json;
using SagaSales.Shared.DTOs;
using Xunit;

namespace SagaSales.ContractTests.Inventory;

/// <summary>
/// Contract of the store parameters ("Parámetros"): expiry warning between 1 and 365 days and a
/// default branch that must exist. Requires a running stack.
/// </summary>
public class SettingsContractTests
{
    [Fact]
    public async Task The_defaults_are_read_and_the_warning_can_be_updated()
    {
        if (Gateway.TryAnnounceSkip(nameof(The_defaults_are_read_and_the_warning_can_be_updated)))
        {
            return;
        }

        using var session = await ContractSession.CreateAsync(withBranch: true);

        using var initial = await session.SendAsync(HttpMethod.Get, "api/settings");
        Assert.Equal(HttpStatusCode.OK, initial.StatusCode);
        var settings = await initial.Content.ReadFromJsonAsync<SettingsDto>();
        Assert.Equal(30, settings!.ExpiryWarningDays);

        using var updated = await session.SendAsync(HttpMethod.Put, "api/settings", new UpdateSettingsRequest
        {
            ExpiryWarningDays = 45,
        });
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        var after = await updated.Content.ReadFromJsonAsync<SettingsDto>();
        Assert.Equal(45, after!.ExpiryWarningDays);

        using var reread = await session.SendAsync(HttpMethod.Get, "api/settings");
        var stored = await reread.Content.ReadFromJsonAsync<SettingsDto>();
        Assert.Equal(45, stored!.ExpiryWarningDays);
    }

    [Fact]
    public async Task An_out_of_range_warning_returns_400()
    {
        if (Gateway.TryAnnounceSkip(nameof(An_out_of_range_warning_returns_400)))
        {
            return;
        }

        using var session = await ContractSession.CreateAsync(withBranch: true);

        using var zero = await session.SendAsync(HttpMethod.Put, "api/settings", new UpdateSettingsRequest
        {
            ExpiryWarningDays = 0,
        });
        Assert.Equal(HttpStatusCode.BadRequest, zero.StatusCode);

        using var tooMany = await session.SendAsync(HttpMethod.Put, "api/settings", new UpdateSettingsRequest
        {
            ExpiryWarningDays = 400,
        });
        Assert.Equal(HttpStatusCode.BadRequest, tooMany.StatusCode);
    }
}

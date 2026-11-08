using System.Net;
using System.Net.Http.Json;
using SagaSales.Shared.DTOs;
using Xunit;

namespace SagaSales.ContractTests.Notification;

/// <summary>
/// Contract of the Notification service (E-07): an attempt is recorded and listed with its state.
/// Requires <c>SAGASALES_GATEWAY_URL</c> and a running stack.
/// </summary>
public class NotificationContractTests
{
    [Fact]
    public async Task Listing_notifications_without_a_token_returns_401()
    {
        if (Gateway.TryAnnounceSkip(nameof(Listing_notifications_without_a_token_returns_401)))
        {
            return;
        }

        using var client = Gateway.CreateClient();
        using var response = await client.GetAsync("api/notifications");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task A_sent_notification_is_listed_with_its_subject()
    {
        if (Gateway.TryAnnounceSkip(nameof(A_sent_notification_is_listed_with_its_subject)))
        {
            return;
        }

        using var session = await ContractSession.CreateAsync();
        var subject = "Contract notice " + Guid.NewGuid().ToString("N")[..8];

        using var created = await session.SendAsync(HttpMethod.Post, "api/notifications", new SendNotificationRequest
        {
            Type = SagaSales.Shared.Enums.NotificationType.Email,
            Recipient = "buyer@contract.local",
            Subject = subject,
            Body = "<p>hello</p>",
        });

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        using var list = await session.SendAsync(HttpMethod.Get, "api/notifications");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);

        var page = await list.Content.ReadFromJsonAsync<PagedResult<NotificationItem>>();
        Assert.NotNull(page);
        Assert.Contains(page!.Items, i => i.Subject == subject);
    }

    private sealed class NotificationItem
    {
        public Guid Id { get; set; }

        public string Subject { get; set; } = string.Empty;
    }
}

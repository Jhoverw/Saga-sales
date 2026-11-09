using Moq;
using SagaSales.Notification.Data;
using SagaSales.Notification.Infrastructure;
using SagaSales.Shared.Enums;
using Xunit;

namespace SagaSales.UnitTests.Notification;

/// <summary>
/// The dispatcher is the single write path for notifications: it records the attempt and then
/// hands the message to the sender. A failing sender must never break the caller (FR-023).
/// </summary>
public class NotificationDispatcherTests
{
    private static readonly Guid Tenant = Guid.NewGuid();
    private static readonly Guid Customer = Guid.NewGuid();

    [Fact]
    public async Task Dispatch_records_the_attempt_and_calls_the_sender()
    {
        var repository = new Mock<INotificationRepository>();
        var sender = new Mock<INotificationSender>();
        var dispatcher = new NotificationDispatcher(repository.Object, sender.Object);

        await dispatcher.DispatchAsync(
            Tenant,
            Customer,
            NotificationType.Email,
            "buyer@shop.local",
            "Order confirmation",
            "<p>Thanks</p>",
            CancellationToken.None);

        repository.Verify(
            r => r.CreateNotificationAsync(
                Tenant,
                Customer,
                "Order confirmation",
                "<p>Thanks</p>",
                (int)NotificationType.Email,
                "email",
                It.IsAny<CancellationToken>()),
            Times.Once);

        sender.Verify(
            s => s.SendAsync(
                NotificationType.Email,
                "buyer@shop.local",
                "Order confirmation",
                "<p>Thanks</p>",
                It.IsAny<CancellationToken>()),
            Times.Once);

        repository.Verify(
            r => r.MarkSentAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task A_failing_sender_does_not_throw_into_the_caller()
    {
        var repository = new Mock<INotificationRepository>();
        var sender = new Mock<INotificationSender>();
        sender
            .Setup(s => s.SendAsync(
                It.IsAny<NotificationType>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("provider is down"));

        var logId = Guid.NewGuid();
        repository
            .Setup(r => r.CreateNotificationAsync(
                It.IsAny<Guid>(),
                It.IsAny<Guid?>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(logId);

        var dispatcher = new NotificationDispatcher(repository.Object, sender.Object);

        // The order flow must survive a notification failure: no exception expected.
        var result = await dispatcher.DispatchAsync(
            Tenant,
            Customer,
            NotificationType.Email,
            "buyer@shop.local",
            "Order confirmation",
            "<p>Thanks</p>",
            CancellationToken.None);

        // The caller still gets the id of the recorded attempt, so the failure can be traced.
        Assert.Equal(logId, result);
        repository.Verify(
            r => r.CreateNotificationAsync(
                It.IsAny<Guid>(),
                It.IsAny<Guid?>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
        repository.Verify(
            r => r.MarkSentAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}

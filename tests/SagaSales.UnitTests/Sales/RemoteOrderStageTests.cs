using SagaSales.Shared.DTOs;
using Xunit;

namespace SagaSales.UnitTests.Sales;

/// <summary>Spec 008: cada pedido cae en la columna del tablero que dice qué hacer con él.</summary>
public class RemoteOrderStageTests
{
    [Theory]
    [InlineData("Pending", 20, 0, false, "PorVerificar")]
    [InlineData("Confirmed", 20, 10, false, "PorVerificar")]
    [InlineData("Confirmed", 20, 20, false, "PorPreparar")]
    [InlineData("Pending", 20, 0, true, "PorPreparar")]
    [InlineData("Processing", 20, 20, false, "EnPreparacion")]
    [InlineData("Shipped", 20, 20, false, "Enviado")]
    [InlineData("Delivered", 20, 20, true, "Entregado")]
    [InlineData("Cancelled", 20, 0, false, "Anulado")]
    public void The_stage_follows_status_and_verification(string status, int total, int verified, bool cod, string expected)
    {
        var order = new RemoteOrderDto { Status = status, Total = total, VerifiedAmount = verified, CashOnDelivery = cod };

        Assert.Equal(expected, order.Stage);
    }
}

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using SagaSales.Shared.DTOs;
using SagaSales.Shared.Enums;
using Xunit;

namespace SagaSales.ContractTests.Sales;

/// <summary>
/// Spec 007 a través del gateway: un pedido cobrado por transferencia no sale sin comprobante; con la foto
/// y la guía del courier, sí. Requiere <c>SAGASALES_GATEWAY_URL</c> y el stack levantado.
/// </summary>
public class OrderShipmentContractTests
{
    private static readonly byte[] TinyJpeg = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00, 0x01, 0xFF, 0xD9];

    [Fact]
    public async Task A_transfer_without_receipt_blocks_shipping_until_it_is_uploaded()
    {
        if (Gateway.TryAnnounceSkip(nameof(A_transfer_without_receipt_blocks_shipping_until_it_is_uploaded)))
        {
            return;
        }

        using var owner = await ContractSession.CreateAsync(withBranch: true);
        var order = await PlaceOrderAsync(owner);

        using var paid = await owner.SendAsync(HttpMethod.Post, "api/payments", new ManualPaymentRequest
        {
            OrderId = order.Id,
            Amount = order.Total,
            Method = ManualPaymentMethod.Transfer,
            Reference = "BP-778",
        });
        Assert.Equal(HttpStatusCode.Created, paid.StatusCode);
        var payment = (await paid.Content.ReadFromJsonAsync<PaymentDto>())!;

        await AdvanceToProcessingAsync(owner, order.Id);

        var courier = new ShipmentRequest { ShippingMethod = 1, CourierCompany = "Servientrega", TrackingNumber = "SV-555001" };

        using (var blocked = await owner.SendAsync(HttpMethod.Put, $"api/orders/{order.Id}/status",
                   new UpdateOrderStatusRequest { NewStatus = (int)OrderStatus.Shipped, Shipment = courier }))
        {
            Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);
        }

        // Un archivo que no es imagen ni PDF se rechaza aunque diga ser JPG.
        using (var fake = await UploadAsync(owner, payment.Id, "<html></html>"u8.ToArray(), "image/jpeg"))
        {
            Assert.Equal(HttpStatusCode.BadRequest, fake.StatusCode);
        }

        using (var uploaded = await UploadAsync(owner, payment.Id, TinyJpeg, "image/jpeg"))
        {
            Assert.Equal(HttpStatusCode.Created, uploaded.StatusCode);
        }

        using (var noTracking = await owner.SendAsync(HttpMethod.Put, $"api/orders/{order.Id}/status",
                   new UpdateOrderStatusRequest { NewStatus = (int)OrderStatus.Shipped, Shipment = new ShipmentRequest { ShippingMethod = 1, CourierCompany = "Servientrega" } }))
        {
            Assert.Equal(HttpStatusCode.BadRequest, noTracking.StatusCode);
        }

        using (var shipped = await owner.SendAsync(HttpMethod.Put, $"api/orders/{order.Id}/status",
                   new UpdateOrderStatusRequest { NewStatus = (int)OrderStatus.Shipped, Shipment = courier }))
        {
            Assert.True(shipped.StatusCode == HttpStatusCode.OK, await shipped.Content.ReadAsStringAsync());
        }

        using var shipment = await owner.SendAsync(HttpMethod.Get, $"api/orders/{order.Id}/shipment");
        var stored = await shipment.Content.ReadFromJsonAsync<OrderShipmentDto>();
        Assert.Equal("SV-555001", stored!.TrackingNumber);

        using var list = await owner.SendAsync(HttpMethod.Get, $"api/payments/{payment.Id}/attachments");
        var files = await list.Content.ReadFromJsonAsync<List<PaymentAttachmentDto>>();
        var file = Assert.Single(files!);

        using var download = await owner.SendAsync(HttpMethod.Get, $"api/payments/attachments/{file.Id}");
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Equal(TinyJpeg, await download.Content.ReadAsByteArrayAsync());
    }

    private static async Task<OrderDto> PlaceOrderAsync(ContractSession owner)
    {
        using var product = await owner.SendAsync(HttpMethod.Post, "api/products", new CreateProductRequest
        {
            Name = "Producto para envío",
            Price = 12.50m,
            StockQuantity = 5,
        });
        Assert.Equal(HttpStatusCode.Created, product.StatusCode);
        var created = (await product.Content.ReadFromJsonAsync<ProductDto>())!;

        using var order = await owner.SendAsync(HttpMethod.Post, "api/orders", new CreateOrderRequest
        {
            CustomerId = Guid.NewGuid(),
            ShippingAddress = "Av. 10 de Agosto, Quito",
            Items = [new OrderItemRequest { ProductId = created.Id, Quantity = 1 }],
        });
        Assert.Equal(HttpStatusCode.Created, order.StatusCode);
        return (await order.Content.ReadFromJsonAsync<OrderDto>())!;
    }

    private static async Task AdvanceToProcessingAsync(ContractSession owner, Guid orderId)
    {
        for (var step = 0; step < 3; step++)
        {
            using var current = await owner.SendAsync(HttpMethod.Get, $"api/orders/{orderId}");
            var order = (await current.Content.ReadFromJsonAsync<OrderDto>())!;

            var next = order.Status switch
            {
                "Pending" => OrderStatus.Confirmed,
                "Confirmed" => OrderStatus.Processing,
                _ => (OrderStatus?)null,
            };

            if (next is null)
            {
                Assert.Equal("Processing", order.Status);
                return;
            }

            using var moved = await owner.SendAsync(HttpMethod.Put, $"api/orders/{orderId}/status",
                new UpdateOrderStatusRequest { NewStatus = (int)next.Value });
            Assert.Equal(HttpStatusCode.OK, moved.StatusCode);
        }
    }

    private static async Task<HttpResponseMessage> UploadAsync(ContractSession owner, Guid paymentId, byte[] bytes, string contentType)
    {
        var request = owner.Request(HttpMethod.Post, $"api/payments/{paymentId}/attachments");
        var content = new MultipartFormDataContent();
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        content.Add(file, "file", "transferencia.jpg");
        request.Content = content;
        return await owner.Client.SendAsync(request);
    }
}

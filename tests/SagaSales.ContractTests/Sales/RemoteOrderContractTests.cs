using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using SagaSales.Shared.DTOs;
using SagaSales.Shared.Enums;
using Xunit;

namespace SagaSales.ContractTests.Sales;

/// <summary>
/// Spec 008 a través del gateway: el vendedor anota un pedido, registra la transferencia, no puede prepararlo
/// sin verificar, lo verifica con el comprobante y lo envía; un pago rechazado deja de contar.
/// Requiere <c>SAGASALES_GATEWAY_URL</c> y el stack levantado.
/// </summary>
public class RemoteOrderContractTests
{
    private const string Password = "Vendedor12345!";
    private static readonly byte[] TinyJpeg = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00, 0x01, 0xFF, 0xD9];

    [Fact]
    public async Task The_seller_verifies_the_transfer_and_ships_the_pedido()
    {
        if (Gateway.TryAnnounceSkip(nameof(The_seller_verifies_the_transfer_and_ships_the_pedido)))
        {
            return;
        }

        using var owner = await ContractSession.CreateAsync(withBranch: true);
        using var seller = await SellerAsync(owner);
        var order = await PedidoAsync(owner, seller);

        using (var fulfillment = await Send(seller, owner, HttpMethod.Get, $"api/orders/{order.Id}/fulfillment"))
        {
            var state = await fulfillment.Content.ReadFromJsonAsync<OrderFulfillmentDto>();
            Assert.Equal(2, state!.Channel);
            Assert.NotNull(state.HoldUntil);
        }

        var payment = await PayAsync(seller, owner, order);
        Assert.Equal(1, payment.VerificationStatus);

        Assert.Equal(HttpStatusCode.OK, (await Status(seller, owner, order.Id, OrderStatus.Confirmed)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Status(seller, owner, order.Id, OrderStatus.Processing)).StatusCode);

        using (var noProof = await Send(seller, owner, HttpMethod.Post, $"api/payments/{payment.Id}/verify"))
        {
            Assert.Equal(HttpStatusCode.Conflict, noProof.StatusCode);
        }

        using (var upload = await UploadAsync(seller, owner, payment.Id))
        {
            Assert.Equal(HttpStatusCode.Created, upload.StatusCode);
        }

        using (var verify = await Send(seller, owner, HttpMethod.Post, $"api/payments/{payment.Id}/verify"))
        {
            Assert.True(verify.StatusCode == HttpStatusCode.NoContent, await verify.Content.ReadAsStringAsync());
        }

        using (var current = await Send(seller, owner, HttpMethod.Get, $"api/orders/{order.Id}"))
        {
            Assert.Equal("Processing", (await current.Content.ReadFromJsonAsync<OrderDto>())!.Status);
        }

        using var shipped = await Send(seller, owner, HttpMethod.Put, $"api/orders/{order.Id}/status", new UpdateOrderStatusRequest
        {
            NewStatus = (int)OrderStatus.Shipped,
            Shipment = new ShipmentRequest { ShippingMethod = 1, CourierCompany = "Laar Courier", TrackingNumber = "LAAR-90210" },
        });
        Assert.True(shipped.StatusCode == HttpStatusCode.OK, await shipped.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task A_rejected_transfer_no_longer_counts_as_paid()
    {
        if (Gateway.TryAnnounceSkip(nameof(A_rejected_transfer_no_longer_counts_as_paid)))
        {
            return;
        }

        using var owner = await ContractSession.CreateAsync(withBranch: true);
        using var seller = await SellerAsync(owner);
        var order = await PedidoAsync(owner, seller);
        var payment = await PayAsync(seller, owner, order);

        using (var noReason = await Send(seller, owner, HttpMethod.Post, $"api/payments/{payment.Id}/reject", new RejectPaymentRequest { Reason = "" }))
        {
            Assert.Equal(HttpStatusCode.BadRequest, noReason.StatusCode);
        }

        using (var reject = await Send(seller, owner, HttpMethod.Post, $"api/payments/{payment.Id}/reject", new RejectPaymentRequest { Reason = "No llegó al banco" }))
        {
            Assert.True(reject.StatusCode == HttpStatusCode.NoContent, await reject.Content.ReadAsStringAsync());
        }

        using var current = await Send(seller, owner, HttpMethod.Get, $"api/orders/{order.Id}");
        Assert.Equal(0m, (await current.Content.ReadFromJsonAsync<OrderDto>())!.PaidAmount);
    }

    private static async Task<OrderDto> PedidoAsync(ContractSession owner, HttpClient seller)
    {
        using var product = await owner.SendAsync(HttpMethod.Post, "api/products", new CreateProductRequest
        {
            Name = "Producto del en vivo",
            Price = 8.00m,
            StockQuantity = 5,
        });
        Assert.Equal(HttpStatusCode.Created, product.StatusCode);
        var created = (await product.Content.ReadFromJsonAsync<ProductDto>())!;

        using var order = await Send(seller, owner, HttpMethod.Post, "api/orders", new CreateOrderRequest
        {
            CustomerId = Guid.NewGuid(),
            ShippingAddress = "Cuenca, Av. Solano",
            Items = [new OrderItemRequest { ProductId = created.Id, Quantity = 1 }],
            Channel = 2,
        });
        Assert.True(order.StatusCode == HttpStatusCode.Created, await order.Content.ReadAsStringAsync());
        return (await order.Content.ReadFromJsonAsync<OrderDto>())!;
    }

    private static async Task<PaymentDto> PayAsync(HttpClient seller, ContractSession owner, OrderDto order)
    {
        using var paid = await Send(seller, owner, HttpMethod.Post, "api/payments", new ManualPaymentRequest
        {
            OrderId = order.Id,
            Amount = order.Total,
            Method = ManualPaymentMethod.Transfer,
            RequiresVerification = true,
        });
        Assert.True(paid.StatusCode == HttpStatusCode.Created, await paid.Content.ReadAsStringAsync());
        return (await paid.Content.ReadFromJsonAsync<PaymentDto>())!;
    }

    private static async Task<HttpClient> SellerAsync(ContractSession owner)
    {
        var email = $"vendedor-{Guid.NewGuid():N}@contract.local";
        using var created = await owner.SendAsync(HttpMethod.Post, "api/users", new CreateUserRequest
        {
            Email = email,
            DisplayName = "Ana Vendedora",
            Role = "sales",
            Password = Password,
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var client = Gateway.CreateClient();
        using var login = await client.PostAsJsonAsync("api/auth/login", new LoginRequest { Email = email, Password = Password });
        login.EnsureSuccessStatusCode();
        var body = await login.Content.ReadFromJsonAsync<LoginResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body!.AccessToken);
        return client;
    }

    private static Task<HttpResponseMessage> Status(HttpClient seller, ContractSession owner, Guid orderId, OrderStatus status) =>
        Send(seller, owner, HttpMethod.Put, $"api/orders/{orderId}/status", new UpdateOrderStatusRequest { NewStatus = (int)status });

    private static Task<HttpResponseMessage> Send(HttpClient client, ContractSession owner, HttpMethod method, string path, object? body = null)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.TryAddWithoutValidation("X-Tenant-Id", owner.TenantId.ToString());
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return client.SendAsync(request);
    }

    private static Task<HttpResponseMessage> UploadAsync(HttpClient seller, ContractSession owner, Guid paymentId)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"api/payments/{paymentId}/attachments");
        request.Headers.TryAddWithoutValidation("X-Tenant-Id", owner.TenantId.ToString());
        var content = new MultipartFormDataContent();
        var file = new ByteArrayContent(TinyJpeg);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        content.Add(file, "file", "transferencia.jpg");
        request.Content = content;
        return seller.SendAsync(request);
    }
}

using System.Net;
using System.Net.Http.Json;
using SagaSales.ContractTests.Catalog;
using SagaSales.Shared.DTOs;
using SagaSales.Shared.Enums;
using Xunit;

namespace SagaSales.ContractTests.Sales;

/// <summary>
/// Contract of the Sales service (contracts/SalesApi.cs), through the gateway: an order carries the
/// price snapshot and cannot jump states. Requires <c>SAGASALES_GATEWAY_URL</c> and a running stack.
/// </summary>
public class OrderContractTests
{
    [Fact]
    public async Task Creating_an_order_returns_201_with_the_snapshot_of_the_price()
    {
        if (Gateway.TryAnnounceSkip(nameof(Creating_an_order_returns_201_with_the_snapshot_of_the_price)))
        {
            return;
        }

        using var client = Gateway.CreateClient();
        var session = await SignedInStore.CreateAsync(client);

        var product = await CreateProductAsync(client, session, "Snapshot product", 25m);
        var customerId = Guid.NewGuid();

        using var response = await client.SendAsync(
            Authorized(HttpMethod.Post, "api/orders", session, new CreateOrderRequest
            {
                CustomerId = customerId,
                ShippingAddress = "Av. Amazonas 123, Quito",
                Items = [new OrderItemRequest { ProductId = product.Id, Quantity = 2 }],
            }));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var order = await response.Content.ReadFromJsonAsync<OrderDto>();
        Assert.NotNull(order);
        // Spec 003: 25 is the PVP. 2 × 25 = 50.00 with VAT; base 50 / 1.15 = 43.48; VAT 6.52 (15 % in force).
        Assert.Equal(43.48m, order!.Subtotal);
        Assert.Equal(6.52m, order.TaxAmount);
        Assert.Equal(50.00m, order.Total);
        Assert.Matches(@"^PED-[0-9A-F]{6}-\d{6}$", order.OrderNumber);   // V021: receipt number
        Assert.False(string.IsNullOrWhiteSpace(order.CreatedByName));     // H-06: seller on the receipt
        Assert.Equal(customerId, order.CustomerId);
        Assert.Single(order.Items);
        Assert.Equal(25m, order.Items[0].UnitPrice);
        Assert.Equal("Snapshot product", order.Items[0].ProductName);
    }

    [Fact]
    public async Task Two_units_of_a_pvp_of_0_40_are_charged_0_80()
    {
        if (Gateway.TryAnnounceSkip(nameof(Two_units_of_a_pvp_of_0_40_are_charged_0_80)))
        {
            return;
        }

        // The case reported on 2026-11-08: 2 Coca-Cola at 0.40 were charged 0.81 (spec 003, SC-001).
        using var client = Gateway.CreateClient();
        var session = await SignedInStore.CreateAsync(client);
        var product = await CreateProductAsync(client, session, "Coca-Cola 500 ml", 0.40m);

        using var response = await client.SendAsync(
            Authorized(HttpMethod.Post, "api/orders", session, new CreateOrderRequest
            {
                CustomerId = Guid.NewGuid(),
                ShippingAddress = "Mostrador",
                Items = [new OrderItemRequest { ProductId = product.Id, Quantity = 2 }],
            }));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var order = await response.Content.ReadFromJsonAsync<OrderDto>();
        Assert.NotNull(order);
        Assert.Equal(0.80m, order!.Total);
        Assert.Equal(0.70m, order.Subtotal);
        Assert.Equal(0.10m, order.TaxAmount);
        Assert.Equal(0.40m, order.Items[0].UnitPriceWithTax);
        Assert.Equal(0.80m, order.Items[0].LineTotal);
    }

    [Fact]
    public async Task Skipping_a_status_step_returns_409()
    {
        if (Gateway.TryAnnounceSkip(nameof(Skipping_a_status_step_returns_409)))
        {
            return;
        }

        using var client = Gateway.CreateClient();
        var session = await SignedInStore.CreateAsync(client);

        var product = await CreateProductAsync(client, session, "State product", 10m);

        using var created = await client.SendAsync(
            Authorized(HttpMethod.Post, "api/orders", session, new CreateOrderRequest
            {
                CustomerId = Guid.NewGuid(),
                ShippingAddress = "Av. Amazonas 123, Quito",
                Items = [new OrderItemRequest { ProductId = product.Id, Quantity = 1 }],
            }));

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var order = await created.Content.ReadFromJsonAsync<OrderDto>();

        // pending → shipped is not allowed.
        using var skipped = await client.SendAsync(
            Authorized(
                HttpMethod.Put,
                $"api/orders/{order!.Id}/status",
                session,
                new UpdateOrderStatusRequest { NewStatus = (int)OrderStatus.Shipped }));

        Assert.Equal(HttpStatusCode.Conflict, skipped.StatusCode);
    }

    [Fact]
    public async Task Ordering_without_a_shipping_address_returns_400()
    {
        if (Gateway.TryAnnounceSkip(nameof(Ordering_without_a_shipping_address_returns_400)))
        {
            return;
        }

        using var client = Gateway.CreateClient();
        var session = await SignedInStore.CreateAsync(client);

        var product = await CreateProductAsync(client, session, "No address product", 10m);

        using var response = await client.SendAsync(
            Authorized(HttpMethod.Post, "api/orders", session, new CreateOrderRequest
            {
                CustomerId = Guid.NewGuid(),
                ShippingAddress = "   ",
                Items = [new OrderItemRequest { ProductId = product.Id, Quantity = 1 }],
            }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task An_order_can_be_paid_in_parts_but_not_beyond_its_total()
    {
        if (Gateway.TryAnnounceSkip(nameof(An_order_can_be_paid_in_parts_but_not_beyond_its_total)))
        {
            return;
        }

        using var client = Gateway.CreateClient();
        var session = await SignedInStore.CreateAsync(client);
        var product = await CreateProductAsync(client, session, "Split payment product", 28.75m);

        // Spec 003: the price is the PVP, so one unit totals 28.75 (H-08).
        using var created = await client.SendAsync(
            Authorized(HttpMethod.Post, "api/orders", session, new CreateOrderRequest
            {
                CustomerId = Guid.NewGuid(),
                ShippingAddress = "Av. Amazonas 123, Quito",
                Items = [new OrderItemRequest { ProductId = product.Id, Quantity = 1 }],
            }));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var order = await created.Content.ReadFromJsonAsync<OrderDto>();
        Assert.NotNull(order);
        Assert.Equal(28.75m, order!.Total);

        // First part in cash-equivalent transfer, then more than the balance is refused.
        using var first = await client.SendAsync(
            Authorized(HttpMethod.Post, "api/payments", session, new ManualPaymentRequest
            {
                OrderId = order.Id,
                Amount = 10m,
                Method = ManualPaymentMethod.Transfer,
            }));
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        using var tooMuch = await client.SendAsync(
            Authorized(HttpMethod.Post, "api/payments", session, new ManualPaymentRequest
            {
                OrderId = order.Id,
                Amount = 30m,
                Method = ManualPaymentMethod.Deposit,
            }));
        Assert.Equal(HttpStatusCode.Conflict, tooMuch.StatusCode);

        // The rest of the balance closes the order.
        using var rest = await client.SendAsync(
            Authorized(HttpMethod.Post, "api/payments", session, new ManualPaymentRequest
            {
                OrderId = order.Id,
                Amount = 18.75m,
                Method = ManualPaymentMethod.Deposit,
            }));
        Assert.Equal(HttpStatusCode.Created, rest.StatusCode);

        using var getRequest = new HttpRequestMessage(HttpMethod.Get, $"api/orders/{order.Id}");
        getRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", session.Token);
        getRequest.Headers.TryAddWithoutValidation("X-Tenant-Id", session.TenantId.ToString());
        using var reread = await client.SendAsync(getRequest);
        var paid = await reread.Content.ReadFromJsonAsync<OrderDto>();
        Assert.NotNull(paid);
        Assert.Equal(28.75m, paid!.PaidAmount);
        Assert.Equal("Processing", paid.Status);
    }

    [Fact]
    public async Task An_identical_payment_twice_in_a_row_is_refused_as_a_duplicate()
    {
        if (Gateway.TryAnnounceSkip(nameof(An_identical_payment_twice_in_a_row_is_refused_as_a_duplicate)))
        {
            return;
        }

        using var client = Gateway.CreateClient();
        var session = await SignedInStore.CreateAsync(client);
        var product = await CreateProductAsync(client, session, "Duplicate guard product", 25m);

        using var created = await client.SendAsync(
            Authorized(HttpMethod.Post, "api/orders", session, new CreateOrderRequest
            {
                CustomerId = Guid.NewGuid(),
                ShippingAddress = "Av. Amazonas 123, Quito",
                Items = [new OrderItemRequest { ProductId = product.Id, Quantity = 1 }],
            }));
        var order = await created.Content.ReadFromJsonAsync<OrderDto>();

        var payment = new ManualPaymentRequest { OrderId = order!.Id, Amount = 5m, Method = ManualPaymentMethod.Transfer };

        using var once = await client.SendAsync(Authorized(HttpMethod.Post, "api/payments", session, payment));
        Assert.Equal(HttpStatusCode.Created, once.StatusCode);

        using var twice = await client.SendAsync(Authorized(HttpMethod.Post, "api/payments", session, payment));
        Assert.Equal(HttpStatusCode.Conflict, twice.StatusCode);
    }

    private static async Task<ProductDto> CreateProductAsync(
        HttpClient client,
        SignedInStore session,
        string name,
        decimal price)
    {
        using var response = await client.SendAsync(
            Authorized(HttpMethod.Post, "api/products", session, new CreateProductRequest
            {
                Name = name,
                Price = price,
                StockQuantity = 50,
                Currency = "USD",
            }));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var product = await response.Content.ReadFromJsonAsync<ProductDto>();
        return product!;
    }

    private static HttpRequestMessage Authorized<T>(HttpMethod method, string path, SignedInStore session, T body)
    {
        var request = new HttpRequestMessage(method, path) { Content = JsonContent.Create(body) };
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", session.Token);
        request.Headers.TryAddWithoutValidation("X-Tenant-Id", session.TenantId.ToString());
        return request;
    }
}

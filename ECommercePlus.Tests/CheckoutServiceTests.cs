using ECommercePlus.Domain;
using ECommercePlus.Services.Checkout;
using ECommercePlus.Services.Payments;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace ECommercePlus.Tests;

public class CheckoutServiceTests : IDisposable
{
    private const string GoodCard = "4242 4242 4242 4242";
    private readonly TestDatabase _database = new();

    public void Dispose() => _database.Dispose();

    private CheckoutService CreateService() => new(
        _database.CreateContext(),
        new FakePaymentGateway(TimeProvider.System, NullLogger<FakePaymentGateway>.Instance),
        TimeProvider.System,
        NullLogger<CheckoutService>.Instance);

    private static CheckoutRequest Request(Dictionary<int, int> lines, string card = GoodCard, string? token = null) => new(
        lines,
        new CustomerDetails("Jane Doe", "jane@example.com", "1 Main St"),
        new CardDetails("Jane Doe", card, 12, DateTime.UtcNow.Year + 2, "123"),
        token ?? Guid.NewGuid().ToString("N"));

    private async Task<int> StockOfAsync(int id)
    {
        await using var db = _database.CreateContext();
        return (await db.Products.SingleAsync(p => p.Id == id)).Stock;
    }

    [Fact]
    public async Task Successful_purchase_creates_order_and_decrements_stock()
    {
        var a = await _database.AddProductAsync("A-1", "A", 10.50m, 5);
        var b = await _database.AddProductAsync("B-1", "B", 2m, 3);

        var result = await CreateService().PlaceOrderAsync(Request(new() { [a.Id] = 2, [b.Id] = 3 }));

        Assert.True(result.Succeeded);
        var order = result.Value!;
        Assert.Equal(27m, order.Total);
        Assert.Equal("4242", order.CardLast4);
        Assert.Equal(OrderStatus.Paid, order.Status);
        Assert.StartsWith("fake_", order.PaymentReference);
        Assert.Equal(3, await StockOfAsync(a.Id));
        Assert.Equal(0, await StockOfAsync(b.Id));

        var stored = await CreateService().GetOrderAsync(order.OrderNumber);
        Assert.Equal(2, stored!.Items.Count);
    }

    [Fact]
    public async Task Insufficient_stock_fails_without_side_effects()
    {
        var a = await _database.AddProductAsync("A-1", "A", 1m, 5);
        var b = await _database.AddProductAsync("B-1", "B", 1m, 1);

        var result = await CreateService().PlaceOrderAsync(Request(new() { [a.Id] = 2, [b.Id] = 2 }));

        Assert.Equal(FailureKind.Conflict, result.Failure);
        Assert.Equal(5, await StockOfAsync(a.Id));
        Assert.Equal(1, await StockOfAsync(b.Id));
        await using var db = _database.CreateContext();
        Assert.Equal(0, await db.Orders.CountAsync());
    }

    [Theory]
    [InlineData(FakePaymentGateway.DeclinedTestCard)]
    [InlineData(FakePaymentGateway.InsufficientFundsTestCard)]
    [InlineData("1234 5678 9012 3456")]
    public async Task Declined_payment_rolls_back_stock(string card)
    {
        var a = await _database.AddProductAsync("A-1", "A", 1m, 5);

        var result = await CreateService().PlaceOrderAsync(Request(new() { [a.Id] = 2 }, card));

        Assert.False(result.Succeeded);
        Assert.Contains("Payment declined", result.Errors[0].Message);
        Assert.Equal(5, await StockOfAsync(a.Id));
    }

    [Fact]
    public async Task Resubmitting_the_same_checkout_token_does_not_charge_twice()
    {
        var a = await _database.AddProductAsync("A-1", "A", 1m, 5);
        var token = Guid.NewGuid().ToString("N");

        var first = await CreateService().PlaceOrderAsync(Request(new() { [a.Id] = 1 }, token: token));
        var second = await CreateService().PlaceOrderAsync(Request(new() { [a.Id] = 1 }, token: token));

        Assert.Equal(first.Value!.OrderNumber, second.Value!.OrderNumber);
        Assert.Equal(4, await StockOfAsync(a.Id));
    }

    [Fact]
    public async Task Deleting_a_product_keeps_order_history()
    {
        var a = await _database.AddProductAsync("A-1", "Gadget", 4m, 5);
        var order = (await CreateService().PlaceOrderAsync(Request(new() { [a.Id] = 1 }))).Value!;

        await using (var db = _database.CreateContext())
            await db.Products.Where(p => p.Id == a.Id).ExecuteDeleteAsync();

        var stored = await CreateService().GetOrderAsync(order.OrderNumber);
        Assert.Null(stored!.Items[0].ProductId);
        Assert.Equal("Gadget", stored.Items[0].ProductName);
    }

    [Fact]
    public async Task Empty_cart_is_rejected()
    {
        var result = await CreateService().PlaceOrderAsync(Request([]));

        Assert.Equal(FailureKind.Validation, result.Failure);
    }
}

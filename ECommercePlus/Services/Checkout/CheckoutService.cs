using System.Security.Cryptography;
using ECommercePlus.Data;
using ECommercePlus.Domain;
using ECommercePlus.Services.Payments;
using Microsoft.EntityFrameworkCore;

namespace ECommercePlus.Services.Checkout;

public sealed record CustomerDetails(string Name, string Email, string ShippingAddress);

public sealed record CardDetails(string CardholderName, string Number, int ExpiryMonth, int ExpiryYear, string Cvv);

public sealed record CheckoutRequest(
    IReadOnlyDictionary<int, int> Lines,
    CustomerDetails Customer,
    CardDetails Card,
    string IdempotencyKey);

public interface ICheckoutService
{
    Task<OperationResult<Order>> PlaceOrderAsync(CheckoutRequest request, CancellationToken cancellationToken = default);
    Task<Order?> GetOrderAsync(string orderNumber, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Order>> GetRecentOrdersAsync(int take, CancellationToken cancellationToken = default);
}

public sealed class CheckoutService(
    AppDbContext db,
    IPaymentGateway paymentGateway,
    TimeProvider timeProvider,
    ILogger<CheckoutService> logger) : ICheckoutService
{
    public const string Currency = "USD";

    public async Task<OperationResult<Order>> PlaceOrderAsync(CheckoutRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Lines.Count == 0)
            return OperationResult<Order>.Fail(FailureKind.Validation, "Your cart is empty.");

        if (request.Lines.Values.Any(q => q <= 0))
            return OperationResult<Order>.Fail(FailureKind.Validation, "Every item must have a quantity of at least 1.");

        var existingOrder = await db.Orders.AsNoTracking().Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.CheckoutToken == request.IdempotencyKey, cancellationToken);
        if (existingOrder is not null)
            return OperationResult<Order>.Success(existingOrder);

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var ids = request.Lines.Keys.ToList();
        var products = await db.Products.AsNoTracking().Where(p => ids.Contains(p.Id)).ToDictionaryAsync(p => p.Id, cancellationToken);

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var order = new Order
        {
            OrderNumber = NewOrderNumber(now),
            CustomerName = request.Customer.Name.Trim(),
            CustomerEmail = request.Customer.Email.Trim(),
            ShippingAddress = request.Customer.ShippingAddress.Trim(),
            CreatedAtUtc = now,
            Status = OrderStatus.Paid
        };

        foreach (var (productId, quantity) in request.Lines)
        {
            if (!products.TryGetValue(productId, out var product))
                return OperationResult<Order>.Fail(FailureKind.NotFound, "One of the products in your cart is no longer available.");

            var reserved = await db.Products
                .Where(p => p.Id == productId && p.Stock >= quantity)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(p => p.Stock, p => p.Stock - quantity)
                    .SetProperty(p => p.Version, Guid.NewGuid())
                    .SetProperty(p => p.UpdatedAtUtc, now), cancellationToken);

            if (reserved == 0)
                return OperationResult<Order>.Fail(FailureKind.Conflict, $"Not enough stock for '{product.Name}'. Please update your cart.");

            order.Items.Add(new OrderItem
            {
                ProductId = product.Id,
                ProductName = product.Name,
                Sku = product.Sku,
                UnitPrice = product.Price,
                Quantity = quantity
            });
        }

        order.Total = order.Items.Sum(i => i.LineTotal);

        var digits = CardNumbers.DigitsOnly(request.Card.Number);
        var payment = await paymentGateway.ChargeAsync(new PaymentRequest(
            order.Total,
            Currency,
            request.Card.CardholderName.Trim(),
            digits,
            request.Card.ExpiryMonth,
            request.Card.ExpiryYear,
            request.Card.Cvv.Trim(),
            request.IdempotencyKey), cancellationToken);

        if (!payment.Approved)
            return OperationResult<Order>.Fail(FailureKind.Validation, $"Payment declined: {payment.DeclineReason}");

        order.CheckoutToken = request.IdempotencyKey;
        order.PaymentReference = payment.TransactionId ?? string.Empty;
        order.CardLast4 = CardNumbers.Last4(digits);
        db.Orders.Add(order);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        logger.LogInformation("Order {OrderNumber} placed for {Total} {Currency} (transaction {TransactionId})",
            order.OrderNumber, order.Total, Currency, payment.TransactionId);

        return OperationResult<Order>.Success(order);
    }

    public Task<Order?> GetOrderAsync(string orderNumber, CancellationToken cancellationToken = default) =>
        db.Orders.AsNoTracking().Include(o => o.Items).FirstOrDefaultAsync(o => o.OrderNumber == orderNumber, cancellationToken);

    public async Task<IReadOnlyList<Order>> GetRecentOrdersAsync(int take, CancellationToken cancellationToken = default) =>
        await db.Orders.AsNoTracking().Include(o => o.Items)
            .OrderByDescending(o => o.CreatedAtUtc).ThenByDescending(o => o.Id)
            .Take(take).ToListAsync(cancellationToken);

    private static string NewOrderNumber(DateTime now) =>
        $"ORD-{now:yyyyMMdd}-{Convert.ToHexString(RandomNumberGenerator.GetBytes(4))}";
}

namespace ECommercePlus.Services.Payments;

public sealed record PaymentRequest(
    decimal Amount,
    string Currency,
    string CardholderName,
    string CardNumber,
    int ExpiryMonth,
    int ExpiryYear,
    string Cvv,
    string IdempotencyKey);

public sealed record PaymentResult(bool Approved, string? TransactionId, string? DeclineReason)
{
    public static PaymentResult Approve(string transactionId) => new(true, transactionId, null);
    public static PaymentResult Decline(string reason) => new(false, null, reason);
}

public interface IPaymentGateway
{
    Task<PaymentResult> ChargeAsync(PaymentRequest request, CancellationToken cancellationToken = default);
}

namespace ECommercePlus.Services.Payments;

public sealed class FakePaymentGateway(TimeProvider timeProvider, ILogger<FakePaymentGateway> logger) : IPaymentGateway
{
    public const string DeclinedTestCard = "4000000000000002";
    public const string InsufficientFundsTestCard = "4000000000009995";

    public Task<PaymentResult> ChargeAsync(PaymentRequest request, CancellationToken cancellationToken = default)
    {
        var digits = CardNumbers.DigitsOnly(request.CardNumber);
        var result = Evaluate(request, digits);

        logger.LogInformation(
            "Fake payment for {Amount} {Currency} with card ending {Last4}: {Outcome}",
            request.Amount, request.Currency, CardNumbers.Last4(digits), result.Approved ? "approved" : result.DeclineReason);

        return Task.FromResult(result);
    }

    private PaymentResult Evaluate(PaymentRequest request, string digits)
    {
        if (request.Amount < 0)
            return PaymentResult.Decline("Invalid amount.");

        if (!CardNumbers.IsValid(digits))
            return PaymentResult.Decline("The card number is invalid.");

        if (request.Cvv.Length is < 3 or > 4 || !request.Cvv.All(char.IsAsciiDigit))
            return PaymentResult.Decline("The security code is invalid.");

        var now = timeProvider.GetUtcNow();
        if (request.ExpiryMonth is < 1 or > 12 || request.ExpiryYear < now.Year || (request.ExpiryYear == now.Year && request.ExpiryMonth < now.Month))
            return PaymentResult.Decline("The card has expired.");

        return digits switch
        {
            DeclinedTestCard => PaymentResult.Decline("The card was declined."),
            InsufficientFundsTestCard => PaymentResult.Decline("Insufficient funds."),
            _ => PaymentResult.Approve($"fake_{Guid.NewGuid():N}")
        };
    }
}

using ECommercePlus.Services.Payments;
using Microsoft.Extensions.Logging.Abstractions;

namespace ECommercePlus.Tests;

public class PaymentGatewayTests
{
    [Theory]
    [InlineData("4242424242424242", true)]
    [InlineData("5555555555554444", true)]
    [InlineData("4242424242424241", false)]
    [InlineData("1234", false)]
    public void Luhn_check(string number, bool valid) => Assert.Equal(valid, CardNumbers.IsValid(number));

    [Fact]
    public async Task Expired_card_is_declined()
    {
        var gateway = new FakePaymentGateway(TimeProvider.System, NullLogger<FakePaymentGateway>.Instance);

        var result = await gateway.ChargeAsync(new PaymentRequest(10m, "USD", "J", "4242424242424242", 1, 2020, "123", "k"));

        Assert.False(result.Approved);
        Assert.Contains("expired", result.DeclineReason);
    }
}

using ECommercePlus.Services.Checkout;
using Microsoft.AspNetCore.Mvc;

namespace ECommercePlus.Controllers;

public class OrdersController(ICheckoutService checkout) : Controller
{
    public async Task<IActionResult> Index(CancellationToken cancellationToken) =>
        View(await checkout.GetRecentOrdersAsync(100, cancellationToken));

    [Route("orders/{orderNumber}")]
    public async Task<IActionResult> Details(string orderNumber, CancellationToken cancellationToken)
    {
        var order = await checkout.GetOrderAsync(orderNumber, cancellationToken);
        return order is null ? NotFound() : View(order);
    }
}

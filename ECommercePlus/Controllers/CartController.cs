using ECommercePlus.Infrastructure;
using ECommercePlus.Services.Cart;
using Microsoft.AspNetCore.Mvc;

namespace ECommercePlus.Controllers;

public class CartController(ICartService cart) : Controller
{
    public async Task<IActionResult> Index(CancellationToken cancellationToken) =>
        View(await cart.GetAsync(cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Add(int productId, int quantity = 1, string? returnUrl = null, CancellationToken cancellationToken = default)
    {
        var result = await cart.AddAsync(productId, quantity, cancellationToken);
        if (result.Succeeded)
            TempData[TempDataKeys.Success] = "Added to cart.";
        else
            TempData[TempDataKeys.Error] = result.Errors[0].Message;

        return Url.IsLocalUrl(returnUrl) ? LocalRedirect(returnUrl) : RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Update(int productId, int quantity, CancellationToken cancellationToken)
    {
        var result = await cart.UpdateAsync(productId, quantity, cancellationToken);
        if (!result.Succeeded)
            TempData[TempDataKeys.Error] = result.Errors[0].Message;

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public IActionResult Remove(int productId)
    {
        cart.Remove(productId);
        return RedirectToAction(nameof(Index));
    }
}

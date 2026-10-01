using ECommercePlus.Domain;
using ECommercePlus.Infrastructure;
using ECommercePlus.Services.Cart;
using ECommercePlus.Services.Checkout;
using ECommercePlus.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace ECommercePlus.Controllers;

public class CheckoutController(ICartService cart, ICheckoutService checkout, IOrderHistory orderHistory) : Controller
{
    private const string FormPrefix = nameof(CheckoutViewModel.Form);

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var view = await cart.GetAsync(cancellationToken);
        if (view.IsEmpty)
            return RedirectToAction("Index", "Cart");

        return View(new CheckoutViewModel(new CheckoutFormModel { CheckoutToken = Guid.NewGuid().ToString("N") }, view));
    }

    [HttpPost]
    public async Task<IActionResult> Index([Bind(Prefix = FormPrefix)] CheckoutFormModel form, CancellationToken cancellationToken)
    {
        var view = await cart.GetAsync(cancellationToken);
        if (view.IsEmpty)
            return RedirectToAction("Index", "Cart");

        if (!ModelState.IsValid)
            return View(Redact(form, view));

        var request = new CheckoutRequest(
            view.Lines.ToDictionary(l => l.Product.Id, l => l.Quantity),
            new CustomerDetails(form.Name, form.Email, form.ShippingAddress),
            new CardDetails(form.CardholderName, form.CardNumber, form.ExpiryMonth!.Value, form.ExpiryYear!.Value, form.Cvv),
            form.CheckoutToken);

        var result = await checkout.PlaceOrderAsync(request, cancellationToken);
        if (!result.Succeeded)
        {
            ModelState.AddErrors(result, FormPrefix + ".");
            return View(Redact(form, await cart.GetAsync(cancellationToken)));
        }

        cart.Clear();
        orderHistory.Add(result.Value!.OrderNumber);
        TempData[TempDataKeys.Success] = "Thank you! Your order has been placed.";
        return RedirectToAction("Details", "Orders", new { orderNumber = result.Value!.OrderNumber });
    }

    private CheckoutViewModel Redact(CheckoutFormModel form, CartView view)
    {
        foreach (var key in new[] { nameof(CheckoutFormModel.Cvv), nameof(CheckoutFormModel.CardNumber) })
            ModelState.SetModelValue($"{FormPrefix}.{key}", string.Empty, string.Empty);
        form.Cvv = string.Empty;
        form.CardNumber = string.Empty;
        return new CheckoutViewModel(form, view);
    }
}

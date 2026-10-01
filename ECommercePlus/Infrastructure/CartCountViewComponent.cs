using ECommercePlus.Services.Cart;
using Microsoft.AspNetCore.Mvc;

namespace ECommercePlus.Infrastructure;

public sealed class CartCountViewComponent(ICartService cart) : ViewComponent
{
    public IViewComponentResult Invoke() => Content(cart.Count().ToString());
}

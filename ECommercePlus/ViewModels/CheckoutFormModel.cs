using System.ComponentModel.DataAnnotations;

namespace ECommercePlus.ViewModels;

public sealed class CheckoutFormModel
{
    [Required]
    public string CheckoutToken { get; set; } = string.Empty;

    [Required, StringLength(200), Display(Name = "Full name")]
    public string Name { get; set; } = string.Empty;

    [Required, EmailAddress, StringLength(254)]
    public string Email { get; set; } = string.Empty;

    [Required, StringLength(500), Display(Name = "Shipping address")]
    public string ShippingAddress { get; set; } = string.Empty;

    [Required, StringLength(200), Display(Name = "Name on card")]
    public string CardholderName { get; set; } = string.Empty;

    [Required, Display(Name = "Card number"), RegularExpression(@"^[\d ]{12,23}$", ErrorMessage = "Enter a valid card number.")]
    public string CardNumber { get; set; } = string.Empty;

    [Required, Range(1, 12), Display(Name = "Exp. month")]
    public int? ExpiryMonth { get; set; }

    [Required, Range(2000, 2100), Display(Name = "Exp. year")]
    public int? ExpiryYear { get; set; }

    [Required, RegularExpression(@"^\d{3,4}$", ErrorMessage = "Enter a 3 or 4 digit code."), Display(Name = "CVV")]
    public string Cvv { get; set; } = string.Empty;
}

public sealed record CheckoutViewModel(CheckoutFormModel Form, Services.Cart.CartView Cart);

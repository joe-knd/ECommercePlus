namespace ECommercePlus.Domain;

public class Order
{
    public int Id { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerEmail { get; set; } = string.Empty;
    public string ShippingAddress { get; set; } = string.Empty;
    public OrderStatus Status { get; set; }
    public decimal Total { get; set; }
    public string CheckoutToken { get; set; } = string.Empty;
    public string PaymentReference { get; set; } = string.Empty;
    public string CardLast4 { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public List<OrderItem> Items { get; set; } = [];

    public int ItemCount => Items.Sum(i => i.Quantity);
}

public class OrderItem
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public int? ProductId { get; set; }
    public Product? Product { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }

    public decimal LineTotal => UnitPrice * Quantity;
}

public enum OrderStatus
{
    Paid = 1
}

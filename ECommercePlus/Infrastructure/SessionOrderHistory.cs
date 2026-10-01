using System.Text.Json;

namespace ECommercePlus.Infrastructure;

public interface IOrderHistory
{
    void Add(string orderNumber);
    bool Contains(string orderNumber);
}

public sealed class SessionOrderHistory(IHttpContextAccessor httpContextAccessor) : IOrderHistory
{
    private const string SessionKey = "orders.v1";
    private const int MaxEntries = 50;

    private ISession Session => httpContextAccessor.HttpContext?.Session
        ?? throw new InvalidOperationException("No active HTTP session.");

    public void Add(string orderNumber)
    {
        var orders = Read();
        orders.Remove(orderNumber);
        orders.Insert(0, orderNumber);
        Session.SetString(SessionKey, JsonSerializer.Serialize(orders.Take(MaxEntries)));
    }

    public bool Contains(string orderNumber) => Read().Contains(orderNumber, StringComparer.Ordinal);

    private List<string> Read()
    {
        var json = Session.GetString(SessionKey);
        if (string.IsNullOrEmpty(json))
            return [];

        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}

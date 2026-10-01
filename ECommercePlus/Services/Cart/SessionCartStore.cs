using System.Text.Json;

namespace ECommercePlus.Services.Cart;

public sealed class SessionCartStore(IHttpContextAccessor httpContextAccessor) : ICartStore
{
    private const string SessionKey = "cart.v1";

    private ISession Session => httpContextAccessor.HttpContext?.Session
        ?? throw new InvalidOperationException("No active HTTP session.");

    public IReadOnlyDictionary<int, int> GetLines()
    {
        var json = Session.GetString(SessionKey);
        if (string.IsNullOrEmpty(json))
            return new Dictionary<int, int>();

        try
        {
            return JsonSerializer.Deserialize<Dictionary<int, int>>(json) ?? new Dictionary<int, int>();
        }
        catch (JsonException)
        {
            return new Dictionary<int, int>();
        }
    }

    public void Save(IReadOnlyDictionary<int, int> lines) =>
        Session.SetString(SessionKey, JsonSerializer.Serialize(lines));

    public void Clear() => Session.Remove(SessionKey);
}

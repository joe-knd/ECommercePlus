namespace ECommercePlus.Services.Cart;

public interface ICartStore
{
    IReadOnlyDictionary<int, int> GetLines();
    void Save(IReadOnlyDictionary<int, int> lines);
    void Clear();
}

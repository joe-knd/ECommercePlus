namespace ECommercePlus.ViewModels;

public sealed record ErrorViewModel(string? RequestId, int? StatusCode = null)
{
    public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);
}

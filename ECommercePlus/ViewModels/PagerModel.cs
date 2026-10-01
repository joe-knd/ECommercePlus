namespace ECommercePlus.ViewModels;

public sealed record PagerModel(int Page, int TotalPages, string Action, Func<int, Dictionary<string, string?>> RouteValuesFor);

namespace ECommercePlus.Domain;

public sealed record ValidationError(string Field, string Message);

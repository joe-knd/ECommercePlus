using System.ComponentModel.DataAnnotations;
using ECommercePlus.Domain;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace ECommercePlus.ViewModels;

[AttributeUsage(AttributeTargets.Property)]
public sealed class SafeTextAttribute : ValidationAttribute, IClientModelValidator
{
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        var problem = ContentSafety.Check(value as string);
        return problem is null
            ? ValidationResult.Success
            : new ValidationResult($"{validationContext.DisplayName} {problem}.", [validationContext.MemberName!]);
    }

    public void AddValidation(ClientModelValidationContext context)
    {
        var name = context.ModelMetadata.GetDisplayName();
        var attributes = context.Attributes;
        attributes.TryAdd("data-val", "true");
        attributes.TryAdd("data-val-safetext", $"{name} is not allowed.");
        attributes.TryAdd("data-val-safetext-markup", ContentSafety.MarkupPattern);
        attributes.TryAdd("data-val-safetext-sql", ContentSafety.SqlPattern);
        attributes.TryAdd("data-val-safetext-markupmsg", $"{name} {ContentSafety.MarkupMessage}.");
        attributes.TryAdd("data-val-safetext-sqlmsg", $"{name} {ContentSafety.SqlMessage}.");
        attributes.TryAdd("data-val-safetext-controlmsg", $"{name} {ContentSafety.ControlMessage}.");
    }
}

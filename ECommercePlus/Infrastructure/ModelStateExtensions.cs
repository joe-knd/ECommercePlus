using ECommercePlus.Domain;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace ECommercePlus.Infrastructure;

public static class ModelStateExtensions
{
    public static void AddErrors(this ModelStateDictionary modelState, OperationResult result, string prefix = "")
    {
        foreach (var error in result.Errors)
        {
            var key = string.IsNullOrEmpty(error.Field) ? string.Empty : prefix + error.Field;
            modelState.AddModelError(key, error.Message);
        }
    }
}

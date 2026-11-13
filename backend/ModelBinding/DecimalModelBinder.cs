using System.Globalization;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace KasseAPI_Final.ModelBinding;

/// <summary>
/// Binds <see cref="decimal"/> / <see cref="decimal"/>? with <see cref="CultureInfo.InvariantCulture"/>
/// so values like <c>0.01</c> succeed when the request UI culture is de-AT (comma decimal separator).
/// </summary>
public sealed class DecimalModelBinder : IModelBinder
{
    public Task BindModelAsync(ModelBindingContext bindingContext)
    {
        ArgumentNullException.ThrowIfNull(bindingContext);

        var valueProviderResult = bindingContext.ValueProvider.GetValue(bindingContext.ModelName);
        if (valueProviderResult == ValueProviderResult.None)
            return Task.CompletedTask;

        bindingContext.ModelState.SetModelValue(bindingContext.ModelName, valueProviderResult);

        var value = valueProviderResult.FirstValue;
        if (string.IsNullOrWhiteSpace(value))
        {
            if (bindingContext.ModelMetadata.IsReferenceOrNullableType)
                bindingContext.Result = ModelBindingResult.Success(null);

            return Task.CompletedTask;
        }

        if (TryParseDecimal(value, out var result))
        {
            bindingContext.Result = ModelBindingResult.Success(result);
            return Task.CompletedTask;
        }

        bindingContext.ModelState.TryAddModelError(
            bindingContext.ModelName,
            bindingContext.ModelMetadata.ModelBindingMessageProvider.ValueIsInvalidAccessor(value));

        return Task.CompletedTask;
    }

    internal static bool TryParseDecimal(string value, out decimal result)
    {
        if (decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out result))
            return true;

        result = default;
        return false;
    }
}

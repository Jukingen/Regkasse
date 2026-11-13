using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace KasseAPI_Final.ModelBinding;

/// <summary>Registers <see cref="DecimalModelBinder"/> for <see cref="decimal"/> and <see cref="decimal"/>?.</summary>
public sealed class DecimalModelBinderProvider : IModelBinderProvider
{
    public IModelBinder? GetBinder(ModelBinderProviderContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var modelType = context.Metadata.ModelType;
        if (modelType == typeof(decimal) || modelType == typeof(decimal?))
            return new DecimalModelBinder();

        return null;
    }
}

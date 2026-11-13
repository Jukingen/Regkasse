namespace KasseAPI_Final.Swagger;

/// <summary>
/// Stable, collision-free OpenAPI schemaIds for nested types and closed generics (e.g. <c>PagedResult&lt;T&gt;</c>).
/// </summary>
public static class OpenApiSchemaIdSelector
{
    public static string Select(Type type)
    {
        if (type.IsGenericType)
        {
            var def = type.GetGenericTypeDefinition();
            var bare = StripArity(def.Name);
            var args = string.Join("_and_", type.GetGenericArguments().Select(Select));
            return $"{bare}Of{args}";
        }

        if (type.IsNested && type.DeclaringType is not null)
            return $"{Select(type.DeclaringType)}_{type.Name}";

        return StripArity(type.Name);
    }

    private static string StripArity(string name)
    {
        var tick = name.IndexOf('`', StringComparison.Ordinal);
        return tick > 0 ? name[..tick] : name;
    }
}

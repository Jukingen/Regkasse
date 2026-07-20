namespace KasseAPI_Final.Services.App;

public sealed class AppCategoryItem
{
    public Guid Id { get; init; }
    public required string Key { get; init; }
    public required string Name { get; init; }
    public string? Color { get; init; }
    public string? Icon { get; init; }
    public int SortOrder { get; init; }
}

namespace KasseAPI_Final.Services.App;

public sealed class AppMenuItem
{
    public Guid Id { get; init; }
    public required string Name { get; init; }
    public Guid CategoryId { get; init; }
    public string? CategoryName { get; init; }
    public decimal Price { get; init; }
    public string? ImageUrl { get; init; }
    public string? Description { get; init; }
}

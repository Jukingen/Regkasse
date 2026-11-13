using KasseAPI_Final.Models;

namespace KasseAPI_Final.Services.DemoRequests;

public sealed class CreateDemoRequestDto
{
    public string? Name { get; set; }
    public string? Email { get; set; }
    public string? Company { get; set; }
    public string? Industry { get; set; }
    public string? Message { get; set; }
}

public sealed class DemoRequestCreatedDto
{
    public Guid Id { get; set; }
    public string Status { get; set; } = DemoRequestStatuses.New;
}

public sealed class DemoRequestErrorDto
{
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

public sealed class DemoRequestListItemDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Company { get; set; }
    public string Industry { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public string? Message { get; set; }
}

public sealed class DemoRequestListDto
{
    public IReadOnlyList<DemoRequestListItemDto> Items { get; set; } = [];
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}

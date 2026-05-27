using System.Text.Json;

namespace KasseAPI_Final.Models.DTOs;

public class DashboardWidgetCatalogItemDto
{
    public string WidgetId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string RequiredPermission { get; set; } = string.Empty;
    public int DefaultOrder { get; set; }
    public bool DefaultVisible { get; set; }
    public bool SupportsAutoRefresh { get; set; }
}

public class DashboardWidgetPreferenceDto
{
    public string WidgetId { get; set; } = string.Empty;
    public int Order { get; set; }
    public bool IsVisible { get; set; }
    public Dictionary<string, JsonElement>? Settings { get; set; }
}

public class DashboardPreferencesResponseDto
{
    public IReadOnlyList<DashboardWidgetPreferenceDto> Widgets { get; set; } = Array.Empty<DashboardWidgetPreferenceDto>();
    public DateTime? UpdatedAtUtc { get; set; }
}

public class SaveDashboardPreferencesRequestDto
{
    public List<DashboardWidgetPreferenceDto> Widgets { get; set; } = new();
}

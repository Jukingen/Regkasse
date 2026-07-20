namespace KasseAPI_Final.DTOs;

/// <summary>Public tenant profile for shared website / PWA (no secrets).</summary>
public sealed class PublicTenantProfileDto
{
    public string Slug { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string? Phone { get; init; }
    public string? Email { get; init; }
    public string? Address { get; init; }
    public string? LogoUrl { get; init; }
    public string PrimaryColor { get; init; } = "#0f172a";
    public string AccentColor { get; init; } = "#38bdf8";

    /// <summary>
    /// Whether Web/App may accept new online orders right now
    /// (working hours + special days + stop-before-close cutoff).
    /// Never apply this gate to POS sales.
    /// </summary>
    public bool AcceptingOnlineOrders { get; init; }

    /// <summary>Restaurant is within today's open window (display; cutoff may still block orders).</summary>
    public bool RestaurantIsOpen { get; init; }

    /// <summary>Customer-facing status message (German default from settings).</summary>
    public string OrderStatusMessage { get; init; } = "Heute geschlossen";
}

public sealed class PublicTenantMenuDto
{
    public string Slug { get; init; } = string.Empty;
    public string Currency { get; init; } = "EUR";
    public IReadOnlyList<PublicTenantCategoryDto> Categories { get; init; } =
        Array.Empty<PublicTenantCategoryDto>();
    public IReadOnlyList<PublicTenantMenuItemDto> Items { get; init; } =
        Array.Empty<PublicTenantMenuItemDto>();
}

public sealed class PublicTenantCategoryDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Color { get; init; }
    public int SortOrder { get; init; }
}

public sealed class PublicTenantMenuItemDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public Guid? CategoryId { get; init; }
    public string? CategoryName { get; init; }
    public decimal Price { get; init; }
    public string? ImageUrl { get; init; }
    public string? Description { get; init; }
}

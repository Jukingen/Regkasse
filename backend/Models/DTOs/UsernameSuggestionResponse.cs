namespace KasseAPI_Final.Models.DTOs;

/// <summary>Preview for Quick Create username allocation.</summary>
public sealed class UsernameSuggestionResponse
{
    public required string SuggestedUsername { get; init; }

    /// <summary>Next free numeric suffixes for the role prefix (e.g. 5, 6, 7).</summary>
    public required IReadOnlyList<int> AvailableNumbers { get; init; }
}

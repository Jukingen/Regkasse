using KasseAPI_Final.DTOs;

namespace KasseAPI_Final.Services;

/// <summary>Predefined operator hints for common Fiskaly SIGN AT error codes.</summary>
public static class FiskalyKnownErrorSolutions
{
    public const string ClientError = "E_CLIENT_ERROR";
    public const string ScuNotFound = "E_SCU_NOT_FOUND";
    public const string ReceiptNotFound = "E_RECEIPT_NOT_FOUND";
    public const string Unauthorized = "E_UNAUTHORIZED";

    private static readonly Dictionary<string, (string Title, string Hint)> Catalog =
        new(StringComparer.OrdinalIgnoreCase)
        {
            [ClientError] = ("Invalid API credentials", "Check the Fiskaly API keys."),
            [ScuNotFound] = ("SCU not found", "Check the SCU ID."),
            [ReceiptNotFound] = ("Receipt not found", "Check the receipt ID."),
            [Unauthorized] = ("Unauthorized", "Refresh the Fiskaly session.")
        };

    public static FiskalyKnownErrorSolutionDto? Resolve(string? errorCode)
    {
        var key = Normalize(errorCode);
        if (key is null || !Catalog.TryGetValue(key, out var entry))
            return null;

        return new FiskalyKnownErrorSolutionDto
        {
            Code = key,
            Title = entry.Title,
            Hint = entry.Hint
        };
    }

    public static string? Normalize(string? errorCode)
    {
        if (string.IsNullOrWhiteSpace(errorCode))
            return null;

        var key = errorCode.Trim().ToUpperInvariant().Replace('-', '_');
        if (key is ClientError or ScuNotFound or ReceiptNotFound or Unauthorized)
            return key;

        var prefixed = key.StartsWith("E_", StringComparison.Ordinal) ? key : "E_" + key;
        return prefixed is ClientError or ScuNotFound or ReceiptNotFound or Unauthorized ? prefixed : null;
    }
}

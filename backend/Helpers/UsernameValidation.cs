using System.ComponentModel.DataAnnotations;
using KasseAPI_Final.Models.DTOs;
using KasseAPI_Final.Validators;

namespace KasseAPI_Final.Helpers;

/// <summary>Shared username format validation (login name, not email).</summary>
public static class UsernameValidation
{
    public static Dictionary<string, string[]>? ValidateNewUsername(string? trimmedUsername)
    {
        var trimmed = trimmedUsername?.Trim() ?? string.Empty;

        if (ReservedUsernames.IsReserved(trimmed))
        {
            return new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                ["newUsername"] = new[] { ReservedUsernameMessages.Detail(trimmed) },
            };
        }

        var probe = new UpdateUsernameRequest { NewUsername = trimmed };
        var results = new List<ValidationResult>();
        if (Validator.TryValidateObject(probe, new ValidationContext(probe), results, validateAllProperties: true))
            return null;

        return new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["newUsername"] = results
                .Select(r => r.ErrorMessage ?? "Invalid value.")
                .Distinct()
                .ToArray(),
        };
    }

    public static string? ValidateAssignableUsername(string? trimmedUsername)
    {
        var formatErrors = ValidateNewUsername(trimmedUsername);
        if (formatErrors == null)
            return null;

        return formatErrors.Values.SelectMany(v => v).FirstOrDefault();
    }
}

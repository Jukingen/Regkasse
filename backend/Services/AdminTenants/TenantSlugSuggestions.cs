using System.Text;
using System.Text.RegularExpressions;
using KasseAPI_Final.Data;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services.AdminTenants;

/// <summary>Generates available subdomain slug candidates from company name and a preferred slug.</summary>
public static class TenantSlugSuggestions
{
    private static readonly Regex SlugRegex = new(@"^[a-z0-9][a-z0-9-]{0,61}[a-z0-9]$|^[a-z0-9]$", RegexOptions.Compiled);

    private static readonly HashSet<string> Reserved = new(StringComparer.OrdinalIgnoreCase)
    {
        "admin", "www", "api", "mail",
    };

    public static string NormalizeSlug(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return string.Empty;

        var value = raw.Trim().ToLowerInvariant();
        value = value.Replace('_', '-');
        value = Regex.Replace(value, @"[^a-z0-9-]+", "-");
        value = Regex.Replace(value, @"-+", "-").Trim('-');
        return value.Length > 63 ? value[..63].TrimEnd('-') : value;
    }

    public static string SuggestFromCompanyName(string companyName)
    {
        if (string.IsNullOrWhiteSpace(companyName))
            return string.Empty;

        var ascii = companyName.Trim().ToLowerInvariant()
            .Replace('ä', 'a')
            .Replace('ö', 'o')
            .Replace('ü', 'u')
            .Replace("ß", "ss", StringComparison.Ordinal);

        var sb = new StringBuilder();
        foreach (var ch in ascii.Normalize(NormalizationForm.FormD))
        {
            var category = char.GetUnicodeCategory(ch);
            if (category == System.Globalization.UnicodeCategory.NonSpacingMark)
                continue;
            sb.Append(ch);
        }

        return NormalizeSlug(sb.ToString());
    }

    public static bool IsValidSlug(string slug) =>
        !string.IsNullOrWhiteSpace(slug)
        && slug.Length <= 63
        && SlugRegex.IsMatch(slug)
        && !Reserved.Contains(slug);

    public static IEnumerable<string> BuildCandidates(string? companyName, string? preferredSlug)
    {
        var baseSlug = NormalizeSlug(preferredSlug ?? string.Empty);
        if (string.IsNullOrEmpty(baseSlug))
            baseSlug = SuggestFromCompanyName(companyName ?? string.Empty);

        if (string.IsNullOrEmpty(baseSlug))
            yield break;

        yield return baseSlug;

        var fromName = SuggestFromCompanyName(companyName ?? string.Empty);
        if (!string.IsNullOrEmpty(fromName) && !string.Equals(fromName, baseSlug, StringComparison.Ordinal))
            yield return fromName;

        var parts = baseSlug.Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length >= 2)
        {
            yield return string.Join('-', parts.Reverse());
            if (parts.Length >= 2)
                yield return $"{parts[0]}-{parts[^1]}";
        }

        yield return $"{baseSlug}-wien";
        yield return $"{baseSlug}-1";
        yield return $"{baseSlug}-2";
        yield return $"{baseSlug}-shop";
    }

    public static async Task<IReadOnlyList<string>> PickAvailableAsync(
        AppDbContext db,
        string? companyName,
        string? preferredSlug,
        int maxCount = 5,
        CancellationToken cancellationToken = default)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var result = new List<string>();

        foreach (var candidate in BuildCandidates(companyName, preferredSlug))
        {
            if (result.Count >= maxCount)
                break;

            if (!IsValidSlug(candidate) || !seen.Add(candidate))
                continue;

            var taken = await db.Tenants
                .AsNoTracking()
                .AnyAsync(t => t.Slug == candidate, cancellationToken)
                .ConfigureAwait(false);

            if (!taken)
                result.Add(candidate);
        }

        return result;
    }
}

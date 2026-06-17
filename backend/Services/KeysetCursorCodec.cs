using System.Globalization;
using System.Text;
using System.Text.Json;

namespace KasseAPI_Final.Services;

/// <summary>Opaque keyset cursor for stable DESC pagination on (sortValue, id).</summary>
public readonly record struct KeysetCursor(DateTime SortValueUtc, Guid Id)
{
    public string Encode()
    {
        var payload = JsonSerializer.Serialize(new CursorPayload
        {
            S = SortValueUtc.ToString("O", CultureInfo.InvariantCulture),
            I = Id.ToString("D"),
        });
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(payload))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    public static bool TryDecode(string? encoded, out KeysetCursor cursor)
    {
        cursor = default;
        if (string.IsNullOrWhiteSpace(encoded))
            return false;

        try
        {
            var normalized = encoded.Trim().Replace('-', '+').Replace('_', '/');
            switch (normalized.Length % 4)
            {
                case 2: normalized += "=="; break;
                case 3: normalized += "="; break;
            }

            var json = Encoding.UTF8.GetString(Convert.FromBase64String(normalized));
            var payload = JsonSerializer.Deserialize<CursorPayload>(json);
            if (payload?.S == null || payload.I == null)
                return false;
            if (!DateTime.TryParse(payload.S, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var sortUtc))
                return false;
            if (!Guid.TryParse(payload.I, out var id))
                return false;

            cursor = new KeysetCursor(sortUtc, id);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private sealed class CursorPayload
    {
        public string? S { get; set; }
        public string? I { get; set; }
    }
}

public static class KeysetPaginationExtensions
{
    /// <summary>Rows strictly after cursor in DESC (sortValue, id) order.</summary>
    public static IQueryable<T> ApplyKeysetAfterDesc<T>(
        this IQueryable<T> query,
        KeysetCursor cursor,
        System.Linq.Expressions.Expression<Func<T, DateTime>> sortSelector,
        System.Linq.Expressions.Expression<Func<T, Guid>> idSelector)
    {
        var sortParam = sortSelector.Parameters[0];
        var idParam = idSelector.Parameters[0];
        if (sortParam != idParam)
            throw new InvalidOperationException("sortSelector and idSelector must share the same parameter.");

        var sortBody = sortSelector.Body;
        var idBody = idSelector.Body;

        var cursorSort = System.Linq.Expressions.Expression.Constant(cursor.SortValueUtc);
        var cursorId = System.Linq.Expressions.Expression.Constant(cursor.Id);

        var sortLess = System.Linq.Expressions.Expression.LessThan(sortBody, cursorSort);
        var sortEqual = System.Linq.Expressions.Expression.Equal(sortBody, cursorSort);
        var idLess = System.Linq.Expressions.Expression.LessThan(idBody, cursorId);
        var tieBreak = System.Linq.Expressions.Expression.AndAlso(sortEqual, idLess);
        var predicate = System.Linq.Expressions.Expression.OrElse(sortLess, tieBreak);

        return query.Where(System.Linq.Expressions.Expression.Lambda<Func<T, bool>>(predicate, sortParam));
    }
}

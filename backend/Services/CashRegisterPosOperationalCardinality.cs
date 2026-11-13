using KasseAPI_Final.Models;

namespace KasseAPI_Final.Services;

/// <summary>
/// POS "single-register" vs "multi-register" behavior must not use raw <see cref="CashRegister"/> table cardinality.
/// Rows in <see cref="RegisterStatus.Maintenance"/>, <see cref="RegisterStatus.Disabled"/>, or <see cref="RegisterStatus.Decommissioned"/>, and inactive rows
/// (<see cref="BaseEntity.IsActive"/> false), do not participate in operational register count for these heuristics.
/// </summary>
public static class CashRegisterPosOperationalCardinality
{
    /// <summary>
    /// True when this row counts as one operational POS register (open/closed lifecycle, not archival or service states).
    /// </summary>
    public static bool CountsTowardPosOperationalCardinality(CashRegister r) =>
        r.IsActive &&
        (r.Status == RegisterStatus.Closed || r.Status == RegisterStatus.Open);

    public static int CountOperationalRegisters(IEnumerable<CashRegister> registers) =>
        registers.Count(CountsTowardPosOperationalCardinality);

    public static bool IsSingleOperationalRegisterMode(IEnumerable<CashRegister> registers) =>
        CountOperationalRegisters(registers) == 1;

    /// <summary>
    /// Returns the single operational register, or null when count is zero or greater than one.
    /// </summary>
    public static CashRegister? GetSingleOperationalRegisterOrNull(IEnumerable<CashRegister> registers)
    {
        CashRegister? found = null;
        foreach (var r in registers)
        {
            if (!CountsTowardPosOperationalCardinality(r))
                continue;
            if (found != null)
                return null;
            found = r;
        }

        return found;
    }

    /// <summary>
    /// EF-translatable filter for operational register count queries.
    /// </summary>
    public static IQueryable<CashRegister> WhereCountsTowardPosOperationalCardinality(
        this IQueryable<CashRegister> query) =>
        query.Where(r =>
            r.IsActive &&
            (r.Status == RegisterStatus.Closed || r.Status == RegisterStatus.Open));
}

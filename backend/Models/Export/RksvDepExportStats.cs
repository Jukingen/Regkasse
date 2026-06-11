namespace KasseAPI_Final.Models.Export;

public static class RksvDepExportStats
{
    public static (int GroupCount, int SignatureCount) Count(RksvDepExportRootDto root)
    {
        var groups = root.BelegeGruppe ?? new List<RksvDepBelegeGruppeDto>();
        return (groups.Count, groups.Sum(g => g.BelegeKompakt?.Count ?? 0));
    }
}

using KasseAPI_Final.Models.Reports;

namespace KasseAPI_Final.Services.Reports;

/// <summary>
/// Back-compat wrapper — delegates to unified <see cref="RksvReportTemplateRenderer"/>.
/// </summary>
[Obsolete("Use RksvReportTemplateRenderer via IRksvReportTextService.")]
public static class TagesabschlussCloudReportTemplate
{
    public sealed record RenderInput(
        TagesabschlussReportModel Model,
        string EnvironmentDisplayName,
        string RksvFooter,
        string QrPayload,
        string? RegisterNumber = null);

    public static string Render(RenderInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(input.Model);

        var template = RksvReportTemplateMapper.FromTagesabschluss(
            input.Model,
            input.EnvironmentDisplayName,
            input.RksvFooter,
            input.QrPayload,
            input.RegisterNumber);

        return RksvReportTemplateRenderer.Render(template);
    }
}

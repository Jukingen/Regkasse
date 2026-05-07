using Microsoft.AspNetCore.Mvc;

namespace KasseAPI_Final.Filters;

/// <summary>Requires <c>X-Disclaimer-Acknowledged: true</c> when <see cref="Configuration.FiscalExportOptions.RequireDisclaimerAcknowledgment"/> is enabled.</summary>
public sealed class RequireDisclaimerAcknowledgmentAttribute : ServiceFilterAttribute
{
    public RequireDisclaimerAcknowledgmentAttribute() : base(typeof(RequireDisclaimerAcknowledgmentFilter))
    {
    }
}

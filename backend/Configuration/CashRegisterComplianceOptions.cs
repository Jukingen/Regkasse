namespace KasseAPI_Final.Configuration;

/// <summary>
/// Cash register compliance and destructive test-only operations.
/// <see cref="AllowHardDelete"/> is effective only when the host environment is Development.
/// </summary>
public sealed class CashRegisterComplianceOptions
{
    public const string SectionName = "CashRegister";

    /// <summary>
    /// When true and <c>ASPNETCORE_ENVIRONMENT=Development</c>, exposes hard-delete of cash register rows (test cleanup only).
    /// </summary>
    public bool AllowHardDelete { get; set; }
}

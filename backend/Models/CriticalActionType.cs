namespace KasseAPI_Final.Models;

/// <summary>
/// High-risk tenant / admin mutations that require two-step confirmation
/// (operator intent + 2FA or Super Admin approval token).
/// </summary>
public enum CriticalActionType
{
    SchlussbelegCreation = 1,
    TenantDeletion = 2,
    TenantArchive = 3,
    LicenseChange = 4,
    CurrencyChange = 5,
    CountryChange = 6,
    DeleteAllProducts = 7,
    DecommissionRegister = 8,
    BackupDisable = 9,
    FiscalExportDelete = 10,
    UserRoleChange = 11,
    MassPermissionUpdate = 12,
}

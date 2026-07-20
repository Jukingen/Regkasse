namespace KasseAPI_Final.Services.DataAccess;

/// <summary>GDPR customer data access request kinds.</summary>
public enum DataRequestType
{
    View = 0,
    Export = 1,
    Delete = 2,
}

public static class DataRequestTypeExtensions
{
    public static string ToRightsType(this DataRequestType type) =>
        type switch
        {
            DataRequestType.View => Models.TenantDataRightsRequestTypes.View,
            DataRequestType.Export => Models.TenantDataRightsRequestTypes.Export,
            DataRequestType.Delete => Models.TenantDataRightsRequestTypes.Delete,
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown data request type."),
        };

    public static bool TryParse(string? value, out DataRequestType type)
    {
        type = default;
        if (string.IsNullOrWhiteSpace(value))
            return false;

        switch (value.Trim().ToLowerInvariant())
        {
            case "view":
                type = DataRequestType.View;
                return true;
            case "export":
                type = DataRequestType.Export;
                return true;
            case "delete":
                type = DataRequestType.Delete;
                return true;
            default:
                return false;
        }
    }
}

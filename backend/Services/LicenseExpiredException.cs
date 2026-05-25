namespace KasseAPI_Final.Services;

/// <summary>
/// Trial bittiğinde ve geçerli ücretli lisans bulunmadığında ödeme oluşturmayı engellemek için fırlatılır.
/// Yalnızca ödeme akışı bloke edilir; GET istekleri ve admin panel erişimi etkilenmez.
/// </summary>
public sealed class LicenseExpiredException : Exception
{
    public const string DefaultMessage =
        "Lisans süresi dolmuştur. Yenilemek için destek ekibiyle iletişime geçin.";

    public string Scope { get; }

    public LicenseExpiredException()
        : base(DefaultMessage)
    {
        Scope = "unknown";
    }

    public LicenseExpiredException(string message)
        : base(message)
    {
        Scope = "unknown";
    }

    public LicenseExpiredException(string scope, string message)
        : base(message)
    {
        Scope = string.IsNullOrWhiteSpace(scope) ? "unknown" : scope.Trim().ToLowerInvariant();
    }

    public LicenseExpiredException(string message, Exception innerException)
        : base(message, innerException)
    {
        Scope = "unknown";
    }

    public LicenseExpiredException(string scope, string message, Exception innerException)
        : base(message, innerException)
    {
        Scope = string.IsNullOrWhiteSpace(scope) ? "unknown" : scope.Trim().ToLowerInvariant();
    }
}

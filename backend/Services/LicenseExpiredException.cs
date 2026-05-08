namespace KasseAPI_Final.Services;

/// <summary>
/// Trial bittiğinde ve geçerli ücretli lisans bulunmadığında ödeme oluşturmayı engellemek için fırlatılır.
/// Yalnızca ödeme akışı bloke edilir; GET istekleri ve admin panel erişimi etkilenmez.
/// </summary>
public sealed class LicenseExpiredException : Exception
{
    public const string DefaultMessage =
        "Lisans süresi dolmuştur. Yenilemek için destek ekibiyle iletişime geçin.";

    public LicenseExpiredException()
        : base(DefaultMessage)
    {
    }

    public LicenseExpiredException(string message)
        : base(message)
    {
    }

    public LicenseExpiredException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

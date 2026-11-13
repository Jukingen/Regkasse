namespace KasseAPI_Final.Services.Rksv;

/// <summary>
/// Thrown when a DEP <c>Belege-Gruppe</c> would be emitted without a leaf <c>Signaturzertifikat</c> (P2-3).
/// </summary>
public sealed class RksvDepExportCertificateMissingException : Exception
{
    public const string ErrorCode = "RKSV_DEP_EXPORT_MISSING_CERTIFICATE";

    public RksvDepExportCertificateMissingException(string thumbprint)
        : base(
            $"DEP export failed: missing Signaturzertifikat (leaf certificate) for certificate group '{thumbprint}'. " +
            "BMF Prüftool requires a non-empty Signaturzertifikat for every Belege-Gruppe.")
    {
        Thumbprint = thumbprint;
    }

    public string Thumbprint { get; }
}

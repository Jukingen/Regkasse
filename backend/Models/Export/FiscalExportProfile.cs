namespace KasseAPI_Final.Models.Export;

/// <summary>
/// Fiscal export kullanım profili: aynı veri üretim hattı, farklı operasyonel amaç ve yetki gereksinimleri.
/// Yasal RKSV beyanı değildir — <see cref="FiscalExportPackageDto.NotLegalProofNotice"/> her profilde geçerlidir.
/// </summary>
public enum FiscalExportProfile
{
    /// <summary>Operator odaklı önizleme paketi; resmi beyan değildir.</summary>
    OperationalPreview = 0,

    /// <summary>Muhasebe odaklı paket; kayıt ve mutabakat için.</summary>
    AccountingReport = 1,

    /// <summary>Dış uyum/hukuki inceleme için yapılandırılmış paket.</summary>
    LegalComplianceExport = 2,

    /// <summary>Destek ve tanılama; teknik çözümleme paketi.</summary>
    DiagnosticPackage = 3,

    [Obsolete("Use OperationalPreview")]
    Diagnostic = OperationalPreview,

    [Obsolete("Use AccountingReport")]
    AuditHandoff = AccountingReport,

    [Obsolete("Use LegalComplianceExport")]
    LegalCompliance = LegalComplianceExport,
}

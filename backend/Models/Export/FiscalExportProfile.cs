namespace KasseAPI_Final.Models.Export;

/// <summary>
/// Fiscal export kullanım profili: aynı veri üretim hattı, farklı operasyonel amaç ve yetki gereksinimleri.
/// Yasal RKSV beyanı değildir — <see cref="FiscalExportPackageDto.NotLegalProofNotice"/> her profilde geçerlidir.
/// </summary>
public enum FiscalExportProfile
{
    /// <summary>Destek ve tanılama; slice-içi bütünlük bayrakları deneysel.</summary>
    Diagnostic = 0,

    /// <summary>Üçüncü taraf denetçi / iç denetim devri için paketleme; audit izi zorunlu.</summary>
    AuditHandoff = 1,

    /// <summary>Dış uyum veya hukuki inceleme için yapılandırılmış kanıt paketi; yine de yasal beyan değildir.</summary>
    LegalCompliance = 2,
}

using System.Text.Json.Serialization;

namespace KasseAPI_Final.Models.RestoreVerification;

/// <summary>
/// L6 harici bağımlılık kanıtı için makine okumalı durum; restore başarısı tek başına üst kanıt seviyesini yükseltmez.
/// JSON: camelCase (ör. <c>notImplemented</c>, <c>manualCheckRequired</c>).
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ExternalDependencyProofState
{
    /// <summary>Otomasyon henüz yok veya bu çalıştırmada yürütülmedi.</summary>
    NotImplemented = 0,

    /// <summary>Yapılandırma veya operasyonel kanıt eksik; iddia edilemez.</summary>
    NotProven = 1,

    /// <summary>Operatör/manuel doğrulama gerekir (canlı TSE, arşiv WORM vb.).</summary>
    ManualCheckRequired = 2,

    /// <summary>Otomatik/manuel kanıt geçti.</summary>
    Passed = 3,

    /// <summary>Kanıt başarısız veya kritik eksiklik.</summary>
    Failed = 4
}

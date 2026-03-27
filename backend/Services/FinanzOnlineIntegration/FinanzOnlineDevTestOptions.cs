namespace KasseAPI_Final.Services.FinanzOnlineIntegration;

/// <summary>
/// Geliştirme ortamı: TEST modunda sentetik outbox kuyruğu için bayraklar (üretimde kapalı tutulmalı).
/// </summary>
public sealed class FinanzOnlineDevTestOptions
{
    public const string SectionName = "FinanzOnline:DevTest";

    /// <summary>Development + açıkça true olmadan <see cref="FinanzOnlineDevTestController"/> 404 döner.</summary>
    public bool AllowEnqueueSmokeTest { get; set; }
}

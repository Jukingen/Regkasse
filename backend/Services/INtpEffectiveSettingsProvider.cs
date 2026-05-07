using KasseAPI_Final.Configuration;

namespace KasseAPI_Final.Services;

/// <summary>
/// Merges <see cref="NtpSettings"/> from configuration with optional DB row <see cref="Models.NtpAdminSettings"/>.
/// </summary>
public interface INtpEffectiveSettingsProvider
{
    Task<NtpSettings> GetEffectiveAsync(CancellationToken cancellationToken = default);
}

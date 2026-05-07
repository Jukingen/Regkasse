using KasseAPI_Final.Configuration;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services;

/// <summary>
/// Test / fallback: exposes static appsettings <see cref="NtpSettings"/> without DB merge.
/// </summary>
internal sealed class OptionsOnlyNtpEffectiveSettingsProvider : INtpEffectiveSettingsProvider
{
    private readonly IOptions<NtpSettings> _options;

    public OptionsOnlyNtpEffectiveSettingsProvider(IOptions<NtpSettings> options)
    {
        _options = options;
    }

    public Task<NtpSettings> GetEffectiveAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(_options.Value);
}

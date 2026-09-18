using KasseAPI_Final.Configuration;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.Countries;

/// <summary>
/// Host-level DE/CH fiscal config lock (Production + Staging). Does not inspect tenants.
/// Austrian RKSV/TSE remains <see cref="Tse.TseFiscalConfigLockEvaluator"/>.
/// </summary>
public static class CountryFiscalLockEvaluator
{
    public const string ReasonProviderFake =
        "KassenSicherheit:Provider must not be fake in Production/Staging.";
    public const string ReasonAllowSimulatedTse =
        "KassenSicherheit:AllowSimulatedTse must be false in Production/Staging.";
    public const string ReasonMwstTestEndpoint =
        "Mwst:UseTestEndpoint must be false in Production/Staging.";
    public const string ReasonQrDryRun =
        "QrRechnung:BuilderMode must not be dryRun in Production/Staging.";

    public const string SentinelNotConfigured = "not-configured";
    public const string ForbiddenProviderFake = "fake";
    public const string ForbiddenBuilderDryRun = "dryRun";

    public sealed record Result(bool LockApplies, bool IsSafe, IReadOnlyList<string> Reasons)
    {
        public bool Ok => !LockApplies || IsSafe;
    }

    public static bool LockAppliesToHost(IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(environment);
        return environment.IsProduction() || environment.IsStaging();
    }

    public static Result Evaluate(IHostEnvironment environment, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(configuration);

        if (!LockAppliesToHost(environment))
        {
            return new Result(
                LockApplies: false,
                IsSafe: true,
                Reasons: Array.Empty<string>());
        }

        var reasons = CollectViolations(configuration);
        return new Result(
            LockApplies: true,
            IsSafe: reasons.Count == 0,
            Reasons: reasons);
    }

    public static List<string> CollectViolations(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var reasons = new List<string>();

        var provider = TrimmedOrNull(configuration["KassenSicherheit:Provider"]);
        var allowSimulated = TrimmedOrNull(configuration["KassenSicherheit:AllowSimulatedTse"]);
        if (provider is not null || allowSimulated is not null)
        {
            if (provider is not null
                && string.Equals(provider, ForbiddenProviderFake, StringComparison.OrdinalIgnoreCase))
            {
                reasons.Add(ReasonProviderFake);
            }

            if (IsExplicitTrue(allowSimulated))
                reasons.Add(ReasonAllowSimulatedTse);
        }

        var useTestEndpoint = TrimmedOrNull(configuration["Mwst:UseTestEndpoint"]);
        if (IsExplicitTrue(useTestEndpoint))
            reasons.Add(ReasonMwstTestEndpoint);

        var builderMode = TrimmedOrNull(configuration["QrRechnung:BuilderMode"]);
        if (builderMode is not null
            && string.Equals(builderMode, ForbiddenBuilderDryRun, StringComparison.OrdinalIgnoreCase))
        {
            reasons.Add(ReasonQrDryRun);
        }

        return reasons;
    }

    private static string? TrimmedOrNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static bool IsExplicitTrue(string? value) =>
        value is not null && bool.TryParse(value, out var flag) && flag;
}

/// <summary>
/// Fails startup (<c>ValidateOnStart</c>) when Production/Staging DE/CH fiscal stubs are unsafe.
/// No escape hatch. Does not change Austrian TSE validation.
/// </summary>
public sealed class CountryFiscalLockOptionsValidator : IValidateOptions<CountryFiscalLockOptions>
{
    public static readonly EventId RejectedEventId = new(71011, "CountryFiscalLockRejected");

    private readonly IHostEnvironment _environment;
    private readonly IConfiguration _configuration;
    private readonly ILogger<CountryFiscalLockOptionsValidator> _logger;

    public CountryFiscalLockOptionsValidator(
        IHostEnvironment environment,
        IConfiguration configuration,
        ILogger<CountryFiscalLockOptionsValidator> logger)
    {
        _environment = environment;
        _configuration = configuration;
        _logger = logger;
    }

    public ValidateOptionsResult Validate(string? name, CountryFiscalLockOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var result = CountryFiscalLockEvaluator.Evaluate(_environment, _configuration);
        if (result.Ok)
            return ValidateOptionsResult.Success;

        _logger.LogCritical(
            RejectedEventId,
            "Country fiscal lock rejected KassenSicherheit:Provider={Provider} AllowSimulatedTse={AllowSimulated} Mwst:UseTestEndpoint={UseTestEndpoint} QrRechnung:BuilderMode={BuilderMode} Reasons={Reasons}",
            _configuration["KassenSicherheit:Provider"] ?? "(unset)",
            _configuration["KassenSicherheit:AllowSimulatedTse"] ?? "(unset)",
            _configuration["Mwst:UseTestEndpoint"] ?? "(unset)",
            _configuration["QrRechnung:BuilderMode"] ?? "(unset)",
            string.Join("; ", result.Reasons));

        return ValidateOptionsResult.Fail(string.Join(" ", result.Reasons));
    }
}

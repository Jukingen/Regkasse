using KasseAPI_Final.Configuration;
using KasseAPI_Final.Services.Deployment;
using KasseAPI_Final.Services.Rksv;
using KasseAPI_Final.Services.Tse;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace KasseAPI_Final.DTOs;

/// <summary>RKSV deployment environment snapshot for POS and Admin UI badges.</summary>
public sealed class RksvEnvironmentStatusDto
{
    /// <summary>Demo | Production — mirrors <c>RKSV:Mode</c> and host policy.</summary>
    public string Environment { get; init; } = "Production";

    public bool IsSimulated { get; init; }

    public bool ShowDemoLabel { get; init; }

    public string TseStatusDisplay { get; init; } = string.Empty;

    public string TseStatusBadge { get; init; } = string.Empty;

    public string EnvironmentDisplayName { get; init; } = string.Empty;

    /// <summary>ASP.NET Core host environment name (Development / Staging / Production).</summary>
    public string HostEnvironment { get; init; } = string.Empty;

    /// <summary>True when <c>ASPNETCORE_ENVIRONMENT=Development</c>.</summary>
    public bool IsHostDevelopment { get; init; }

    /// <summary>True when <c>ASPNETCORE_ENVIRONMENT=Staging</c>.</summary>
    public bool IsHostStaging { get; init; }

    /// <summary>
    /// Release stage: <c>dev</c> | <c>staging</c> | <c>canary</c> | <c>production</c>
    /// (from <c>RELEASE_STAGE</c> / <c>Deployment:ReleaseStage</c>, host env, or canary tenant).
    /// </summary>
    public string ReleaseStage { get; init; } = ReleaseStageResolver.Production;

    /// <summary>True when effective <see cref="ReleaseStage"/> is <c>canary</c>.</summary>
    public bool IsCanary { get; init; }

    /// <summary>True when any FinanzOnline nested <c>UseSimulation</c> (or Mode=Simulation) is active.</summary>
    public bool IsFinanzOnlineSimulated { get; init; }

    /// <summary>
    /// Composite: TSE/RKSV simulation or FinanzOnline simulation — FA/POS "SIMULATION" banner.
    /// </summary>
    public bool IsSimulationMode { get; init; }

    /// <summary>True when Production/Staging lock does not apply, config is safe, or escape hatch is on.</summary>
    public bool FiscalConfigLockOk { get; init; } = true;

    /// <summary>True when unsafe modes are allowed only because <c>Tse:AllowUnsafeFiscalModesInProduction</c> is set.</summary>
    public bool FiscalConfigLockEscapeHatchActive { get; init; }

    /// <summary>Human-readable lock violation reasons (no secrets).</summary>
    public IReadOnlyList<string> FiscalConfigLockReasons { get; init; } = Array.Empty<string>();

    public static RksvEnvironmentStatusDto FromService(
        IRksvEnvironmentService service,
        IHostEnvironment? hostEnvironment = null,
        IConfiguration? configuration = null,
        TseFiscalConfigLockEvaluator.Result? fiscalLock = null,
        Guid? tenantId = null,
        string? tenantSlug = null)
    {
        var isSimulated = service.IsTseSimulated();
        var fonSimulated = configuration != null
            && TseFiscalConfigLockEvaluator.IsFinanzOnlineSimulated(configuration);
        var isHostDevelopment = hostEnvironment?.IsDevelopment() == true;
        var isHostStaging = hostEnvironment?.IsStaging() == true;

        DeploymentOptions? deployment = null;
        if (configuration != null)
        {
            deployment = new DeploymentOptions();
            configuration.GetSection(DeploymentOptions.SectionName).Bind(deployment);
        }

        var releaseStage = hostEnvironment != null
            ? ReleaseStageResolver.Resolve(hostEnvironment, deployment, tenantId, tenantSlug)
            : ReleaseStageResolver.Normalize(deployment?.ReleaseStage) is { Length: > 0 } s
                ? s
                : ReleaseStageResolver.Production;

        return new RksvEnvironmentStatusDto
        {
            Environment = service.IsDemoMode() ? "Demo" : "Production",
            IsSimulated = isSimulated,
            ShowDemoLabel = service.ShowDemoLabel(),
            TseStatusDisplay = service.GetTseStatusDisplay(),
            TseStatusBadge = service.GetTseStatusBadge(),
            EnvironmentDisplayName = service.GetEnvironmentDisplayName(),
            HostEnvironment = hostEnvironment?.EnvironmentName ?? string.Empty,
            IsHostDevelopment = isHostDevelopment,
            IsHostStaging = isHostStaging,
            ReleaseStage = releaseStage,
            IsCanary = releaseStage == ReleaseStageResolver.Canary,
            IsFinanzOnlineSimulated = fonSimulated,
            IsSimulationMode = isSimulated || fonSimulated,
            FiscalConfigLockOk = fiscalLock?.Ok ?? true,
            FiscalConfigLockEscapeHatchActive = fiscalLock?.EscapeHatchActive ?? false,
            FiscalConfigLockReasons = fiscalLock?.Reasons ?? Array.Empty<string>(),
        };
    }
}

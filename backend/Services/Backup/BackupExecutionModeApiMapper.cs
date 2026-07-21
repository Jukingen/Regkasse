using KasseAPI_Final.Configuration;
using KasseAPI_Final.DTOs;

namespace KasseAPI_Final.Services.Backup;

/// <summary>
/// Admin API: kullanıcıya dönük mod adları, seçilebilirlik ve PUT gövdesi çözümlemesi.
/// </summary>
public static class BackupExecutionModeApiMapper
{
    public const string UserFacingUseConfigurationDefault = "UseConfigurationDefault";

    public const string UserFacingFake = "Fake";

    public const string UserFacingRealPgDump = "RealPgDump";

    public const string UserFacingProductionStub = "ProductionStub";

    /// <summary>İç <see cref="AdminBackupRuntimeExecutionMode"/> → API kullanıcı modu.</summary>
    public static string ToUserFacingMode(AdminBackupRuntimeExecutionMode adminMode) =>
        adminMode switch
        {
            AdminBackupRuntimeExecutionMode.InheritFromConfiguration => UserFacingUseConfigurationDefault,
            AdminBackupRuntimeExecutionMode.SimulatedFake => UserFacingFake,
            AdminBackupRuntimeExecutionMode.PostgreSqlPgDump => UserFacingRealPgDump,
            _ => UserFacingUseConfigurationDefault
        };

    /// <summary><see cref="BackupExecutionAdapterKind"/> → kullanıcıya dönük özet (yapılandırma veya etkin adaptör).</summary>
    public static string AdapterKindToUserFacingMode(BackupExecutionAdapterKind kind) =>
        kind switch
        {
            BackupExecutionAdapterKind.Fake => UserFacingFake,
            BackupExecutionAdapterKind.PgDump => UserFacingRealPgDump,
            BackupExecutionAdapterKind.ProductionStub => UserFacingProductionStub,
            _ => UserFacingFake
        };

    public static bool TryParseAdminMode(string? raw, out AdminBackupRuntimeExecutionMode mode, out string? errorMessage)
    {
        mode = AdminBackupRuntimeExecutionMode.InheritFromConfiguration;
        errorMessage = null;
        if (string.IsNullOrWhiteSpace(raw))
        {
            errorMessage = "Mode must be a non-empty string.";
            return false;
        }

        var s = raw.Trim();
        if (Enum.TryParse<AdminBackupRuntimeExecutionMode>(s, ignoreCase: true, out var direct))
        {
            mode = direct;
            return true;
        }

        switch (s.ToLowerInvariant())
        {
            case "useconfigurationdefault":
            case "configurationdefault":
            case "inherit":
            case "default":
                mode = AdminBackupRuntimeExecutionMode.InheritFromConfiguration;
                return true;
            case "fake":
            case "simulated":
            case "simulatedfake":
                mode = AdminBackupRuntimeExecutionMode.SimulatedFake;
                return true;
            case "realpgdump":
            case "pgdump":
            case "real":
            case "postgresql":
            case "postgresqlpgdump":
                mode = AdminBackupRuntimeExecutionMode.PostgreSqlPgDump;
                return true;
        }

        errorMessage =
            "Mode must be one of: UseConfigurationDefault (or InheritFromConfiguration), Fake (or SimulatedFake), RealPgDump (or PostgreSqlPgDump).";
        return false;
    }

    /// <summary>
    /// Gerçek (PgDump) önkoşul değerlendirmesinden — engelleyici / uyarı düzeyi tanılar (bilgi amaçlı başarı satırları hariç).
    /// </summary>
    public static IReadOnlyList<BackupConfigurationDiagnostic> FilterRealModeBlockingDiagnostics(
        BackupConfigurationHealthSnapshot pgDumpHypotheticalHealth)
    {
        return pgDumpHypotheticalHealth.Diagnostics
            .Where(d => d.Severity is BackupConfigurationDiagnosticSeverity.Error or BackupConfigurationDiagnosticSeverity.Warning)
            .ToList();
    }

    public static IReadOnlyList<BackupExecutionSelectableModeDto> BuildSelectableModes(
        BackupOptions options,
        IHostEnvironment environment,
        BackupConfigurationHealthSnapshot pgDumpHypotheticalHealth)
    {
        var list = new List<BackupExecutionSelectableModeDto>(3);
        var prodLike = BackupConfigurationEvaluation.IsProductionLikeEnvironment(environment);

        list.Add(new BackupExecutionSelectableModeDto
        {
            UserFacingMode = UserFacingUseConfigurationDefault,
            InternalMode = AdminBackupRuntimeExecutionMode.InheritFromConfiguration.ToString(),
            Selectable = true,
            BlockReason = null
        });

        var fakeBlocked = prodLike && !options.AcknowledgeFakeBackupAdapterOutsideDevelopment;
        list.Add(new BackupExecutionSelectableModeDto
        {
            UserFacingMode = UserFacingFake,
            InternalMode = AdminBackupRuntimeExecutionMode.SimulatedFake.ToString(),
            Selectable = !fakeBlocked,
            BlockReason = fakeBlocked
                ? "Fake is blocked in production-like environments until Backup:AcknowledgeFakeBackupAdapterOutsideDevelopment=true (deployment intent)."
                : prodLike
                    ? "Allowed with explicit API confirmation (ConfirmSimulatedOnlyOperationalRiskInProduction) when saving."
                    : null
        });

        var realBlocked = pgDumpHypotheticalHealth.Level == BackupConfigurationHealthLevel.Unhealthy;
        var reason = realBlocked && pgDumpHypotheticalHealth.Issues.Count > 0
            ? string.Join(" ", pgDumpHypotheticalHealth.Issues.Take(3))
            : realBlocked
                ? "Configuration health is Unhealthy for PgDump prerequisites."
                : null;

        list.Add(new BackupExecutionSelectableModeDto
        {
            UserFacingMode = UserFacingRealPgDump,
            InternalMode = AdminBackupRuntimeExecutionMode.PostgreSqlPgDump.ToString(),
            Selectable = !realBlocked,
            BlockReason = reason
        });

        return list;
    }

    public static string BuildResolutionSummaryEnglish(
        BackupConfigurationHealthSnapshot currentSnap,
        BackupOptions options,
        AdminBackupRuntimeExecutionMode storedMode,
        bool effectiveRunnable)
    {
        var req = ToUserFacingMode(storedMode);
        var def = AdapterKindToUserFacingMode(options.ExecutionAdapterKind);
        var eff = AdapterKindToUserFacingMode(currentSnap.EffectiveAdapterKind);
        if (storedMode == AdminBackupRuntimeExecutionMode.PostgreSqlPgDump && !effectiveRunnable)
        {
            return
                $"Requested={req}, configuration default adapter={def}, effective={eff} (PgDump selected but configuration is Unhealthy — runs target PgDump and may fail until prerequisites are fixed; consider UseConfigurationDefault or fix staging/connection/archive/tooling).";
        }

        return $"Requested={req}, configuration default adapter={def}, effective={eff}, runnable={effectiveRunnable}.";
    }

    /// <summary>
    /// Kalıcı RealPgDump istenirken sağlık bozuksa önerilen kullanıcı modu (çalışma zamanı adaptörü zorla değiştirilmez).
    /// </summary>
    public static string? RecommendedFallbackUserFacingMode(
        AdminBackupRuntimeExecutionMode storedMode,
        bool effectiveRunnable)
    {
        if (storedMode == AdminBackupRuntimeExecutionMode.PostgreSqlPgDump && !effectiveRunnable)
            return UserFacingUseConfigurationDefault;
        return null;
    }

    /// <summary>Yapılandırma dosyasına dönse etkin olacak adaptör (bilgi; kalıcı modu değiştirmez).</summary>
    public static BackupExecutionAdapterKind AdapterIfUsingConfigurationDefaultOnly(BackupOptions options) =>
        options.ExecutionAdapterKind;
}

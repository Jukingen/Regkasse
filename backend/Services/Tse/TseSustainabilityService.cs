using System.Globalization;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.Tse;

/// <summary>
/// Estimates TSE energy / CO₂e footprint from device inventory and signed receipt volume.
/// Figures are indicative green-IT analytics for Super Admin — not certified LCA or ESG audit.
/// </summary>
public sealed class TseSustainabilityService : ITseSustainabilityService
{
    private const int MaxPeriodDays = 366;

    private readonly AppDbContext _db;
    private readonly IOptionsMonitor<TseOptions> _tseOptions;
    private readonly ILogger<TseSustainabilityService> _logger;

    public TseSustainabilityService(
        AppDbContext db,
        IOptionsMonitor<TseOptions> tseOptions,
        ILogger<TseSustainabilityService> logger)
    {
        _db = db;
        _tseOptions = tseOptions;
        _logger = logger;
    }

    public async Task<TseSustainabilityReportDto> GetSustainabilityReportAsync(
        Guid tenantId,
        DateTime? fromUtc = null,
        DateTime? toUtc = null,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("tenantId is required.", nameof(tenantId));

        var opts = _tseOptions.CurrentValue;
        var to = NormalizeUtc(toUtc ?? DateTime.UtcNow);
        var lookback = Math.Clamp(opts.SustainabilityDefaultLookbackDays, 7, MaxPeriodDays);
        var from = NormalizeUtc(fromUtc ?? to.AddDays(-lookback));
        (from, to) = NormalizePeriod(from, to);

        var tenant = await RequireTenantAsync(tenantId, cancellationToken).ConfigureAwait(false);
        var devices = await LoadActiveDevicesAsync(tenantId, cancellationToken).ConfigureAwait(false);
        var softCount = devices.Count(IsSoftOrDemo);
        var cloudCount = devices.Count - softCount;
        var periodDays = Math.Max(1.0, (to - from).TotalDays);

        var receipts = await LoadReceiptSnippetsAsync(tenantId, from, to, cancellationToken)
            .ConfigureAwait(false);
        var totalTx = receipts.Count;
        var signed = receipts.Count(r => r.Signed);

        var footprint = ComputeFootprint(devices, softCount, cloudCount, signed, periodDays, opts);
        var baselineCloudOnly = ComputeFootprint(
            devices,
            softCount: 0,
            cloudCount: devices.Count,
            signed,
            periodDays,
            opts);

        var energySaved = Math.Max(0, Round(baselineCloudOnly.EnergyKwh - footprint.EnergyKwh, 4));
        var carbonSaved = Math.Max(0, Round(baselineCloudOnly.TotalKgCo2 - footprint.TotalKgCo2, 4));
        var costSaved = RoundMoney((decimal)energySaved * Math.Max(0m, opts.SustainabilityEurPerKwh));

        var industryPerTx = Math.Max(0.0001, opts.SustainabilityIndustryKgCo2PerTransaction);
        var industryTotal = industryPerTx * Math.Max(1, signed);
        var percentile = signed == 0
            ? 50
            : Clamp(
                Round(100.0 * (1.0 - footprint.TotalKgCo2 / industryTotal), 1),
                0,
                99.9);

        var perTx = signed == 0 ? 0 : Round(footprint.TotalKgCo2 / signed, 6);
        var perDevice = devices.Count == 0 ? 0 : Round(footprint.TotalKgCo2 / devices.Count, 4);
        var avgDeviceEnergy = devices.Count == 0
            ? 0
            : Round(footprint.EnergyKwh / devices.Count, 4);

        var trend = BuildTrend(receipts, devices, softCount, cloudCount, from, to, opts);

        _logger.LogInformation(
            "TSE sustainability report TenantId={TenantId} Period={From:o}..{To:o} CO2={Co2} kWh={Kwh}",
            tenantId,
            from,
            to,
            footprint.TotalKgCo2,
            footprint.EnergyKwh);

        return new TseSustainabilityReportDto
        {
            TenantId = tenantId,
            TenantName = tenant.Name,
            PeriodStart = from,
            PeriodEnd = to,
            GeneratedAt = DateTime.UtcNow,
            TotalCarbonEmission = footprint.TotalKgCo2,
            PerTransactionEmission = perTx,
            PerDeviceEmission = perDevice,
            TotalEnergyUsage = footprint.EnergyKwh,
            AverageDeviceEnergyUsage = avgDeviceEnergy,
            CarbonSaved = carbonSaved,
            EnergySaved = energySaved,
            CostSaved = (double)costSaved,
            IndustryAverage = Round(industryPerTx, 6),
            Percentile = percentile,
            ActiveDeviceCount = devices.Count,
            SoftOrDemoDeviceCount = softCount,
            SignedTransactions = signed,
            TotalTransactions = totalTx,
            CarbonTrend = trend,
            DiagnosticOnly = true,
        };
    }

    public async Task<TseCarbonFootprintDto> CalculateCarbonFootprintAsync(
        Guid tenantId,
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("tenantId is required.", nameof(tenantId));

        (fromUtc, toUtc) = NormalizePeriod(NormalizeUtc(fromUtc), NormalizeUtc(toUtc));
        await RequireTenantAsync(tenantId, cancellationToken).ConfigureAwait(false);

        var opts = _tseOptions.CurrentValue;
        var devices = await LoadActiveDevicesAsync(tenantId, cancellationToken).ConfigureAwait(false);
        var softCount = devices.Count(IsSoftOrDemo);
        var cloudCount = devices.Count - softCount;
        var periodDays = Math.Max(1.0, (toUtc - fromUtc).TotalDays);

        var receipts = await LoadReceiptSnippetsAsync(tenantId, fromUtc, toUtc, cancellationToken)
            .ConfigureAwait(false);
        var signed = receipts.Count(r => r.Signed);
        var footprint = ComputeFootprint(devices, softCount, cloudCount, signed, periodDays, opts);

        return new TseCarbonFootprintDto
        {
            TenantId = tenantId,
            PeriodStart = fromUtc,
            PeriodEnd = toUtc,
            TotalKgCo2 = footprint.TotalKgCo2,
            DeviceEnergyKgCo2 = footprint.DeviceEnergyKgCo2,
            TransactionApiKgCo2 = footprint.TransactionApiKgCo2,
            PerTransactionKgCo2 = signed == 0 ? 0 : Round(footprint.TotalKgCo2 / signed, 6),
            IndustryAverageKgCo2PerTransaction = Math.Max(0, opts.SustainabilityIndustryKgCo2PerTransaction),
            SignedTransactions = signed,
            ActiveDeviceCount = devices.Count,
            DiagnosticOnly = true,
        };
    }

    public async Task<TseSustainabilityOptimizationResultDto> GetOptimizationSuggestionsAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("tenantId is required.", nameof(tenantId));

        await RequireTenantAsync(tenantId, cancellationToken).ConfigureAwait(false);
        var opts = _tseOptions.CurrentValue;
        var devices = await LoadActiveDevicesAsync(tenantId, cancellationToken).ConfigureAwait(false);
        var softCount = devices.Count(IsSoftOrDemo);
        var cloudCount = devices.Count - softCount;
        var primaries = devices.Where(d => d.IsPrimary || d.IsFailoverActive).ToList();
        var backups = devices.Where(d => d.IsBackup && !d.IsFailoverActive).ToList();

        var suggestions = new List<TseSustainabilitySuggestionDto>();
        var kgPerKwh = Math.Max(0, opts.SustainabilityKgCo2PerKwh);
        var eurPerKwh = Math.Max(0m, opts.SustainabilityEurPerKwh);
        var cloudDay = Math.Max(0, opts.SustainabilityKwhPerCloudDeviceDay);
        var softDay = Math.Max(0, opts.SustainabilityKwhPerSoftDeviceDay);
        var deltaDay = Math.Max(0, cloudDay - softDay);

        if (opts.IsFakeSigningMode && cloudCount > 0)
        {
            var kwhMonth = cloudCount * deltaDay * 30;
            suggestions.Add(new TseSustainabilitySuggestionDto
            {
                Code = "prefer_soft_in_fake_mode",
                Title = "Prefer Soft/Fake TSE in non-production",
                Description =
                    $"Signing mode is Fake but {cloudCount} cloud/hardware device(s) remain active. "
                    + "Switching to Soft TSE reduces estimated energy and CO₂e.",
                Severity = "Info",
                EstimatedEnergySavedKwhPerMonth = Round(kwhMonth, 2),
                EstimatedCarbonSavedKgPerMonth = Round(kwhMonth * kgPerKwh, 3),
                EstimatedCostSavedEurPerMonth = (double)RoundMoney((decimal)kwhMonth * eurPerKwh),
            });
        }

        if (backups.Count > Math.Max(1, primaries.Count))
        {
            var excess = backups.Count - Math.Max(1, primaries.Count);
            var kwhMonth = excess * cloudDay * 30;
            suggestions.Add(new TseSustainabilitySuggestionDto
            {
                Code = "retire_idle_backups",
                Title = "Retire idle backup TSE devices",
                Description =
                    $"{excess} backup device(s) exceed primary capacity. Deactivating idle spares reduces standby energy.",
                Severity = "Warning",
                EstimatedEnergySavedKwhPerMonth = Round(kwhMonth, 2),
                EstimatedCarbonSavedKgPerMonth = Round(kwhMonth * kgPerKwh, 3),
                EstimatedCostSavedEurPerMonth = (double)RoundMoney((decimal)kwhMonth * eurPerKwh),
            });
        }

        if (softCount == 0 && cloudCount > 1 && !opts.IsFakeSigningMode)
        {
            suggestions.Add(new TseSustainabilitySuggestionDto
            {
                Code = "rightsize_fleet",
                Title = "Right-size active TSE fleet",
                Description =
                    "Multiple cloud/hardware devices are active. Consolidate underutilized primaries where operations allow.",
                Severity = "Info",
                EstimatedEnergySavedKwhPerMonth = Round(cloudDay * 30, 2),
                EstimatedCarbonSavedKgPerMonth = Round(cloudDay * 30 * kgPerKwh, 3),
                EstimatedCostSavedEurPerMonth = (double)RoundMoney((decimal)(cloudDay * 30) * eurPerKwh),
            });
        }

        if (suggestions.Count == 0)
        {
            suggestions.Add(new TseSustainabilitySuggestionDto
            {
                Code = "healthy_green_posture",
                Title = "Sustainability posture looks reasonable",
                Description =
                    "No high-impact green-IT optimizations detected for the current TSE inventory.",
                Severity = "Info",
            });
        }

        return new TseSustainabilityOptimizationResultDto
        {
            TenantId = tenantId,
            GeneratedAt = DateTime.UtcNow,
            PotentialCarbonSavedKg = Round(suggestions.Sum(s => s.EstimatedCarbonSavedKgPerMonth), 3),
            PotentialEnergySavedKwh = Round(suggestions.Sum(s => s.EstimatedEnergySavedKwhPerMonth), 2),
            PotentialCostSavedEur = Round(suggestions.Sum(s => s.EstimatedCostSavedEurPerMonth), 2),
            Suggestions = suggestions,
            DiagnosticOnly = true,
        };
    }

    private async Task<Tenant> RequireTenantAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var tenant = await _db.Tenants.AsNoTracking().IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken)
            .ConfigureAwait(false);
        if (tenant is null)
            throw new KeyNotFoundException($"Tenant {tenantId} was not found.");
        return tenant;
    }

    private async Task<List<TseDevice>> LoadActiveDevicesAsync(
        Guid tenantId,
        CancellationToken cancellationToken) =>
        await _db.TseDevices.AsNoTracking().IgnoreQueryFilters()
            .Where(d => d.TenantId == tenantId && d.IsActive)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    private async Task<List<ReceiptSnippet>> LoadReceiptSnippetsAsync(
        Guid tenantId,
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken)
    {
        var rows = await _db.Receipts.AsNoTracking().IgnoreQueryFilters()
            .Where(r => r.TenantId == tenantId && r.IssuedAt >= fromUtc && r.IssuedAt < toUtc)
            .Select(r => new { r.IssuedAt, r.SignatureValue })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return rows
            .Select(r => new ReceiptSnippet(r.IssuedAt, !string.IsNullOrWhiteSpace(r.SignatureValue)))
            .ToList();
    }

    private static Footprint ComputeFootprint(
        IReadOnlyList<TseDevice> devices,
        int softCount,
        int cloudCount,
        int signed,
        double periodDays,
        TseOptions opts)
    {
        var softKwh = Math.Max(0, opts.SustainabilityKwhPerSoftDeviceDay) * softCount * periodDays;
        var cloudKwh = Math.Max(0, opts.SustainabilityKwhPerCloudDeviceDay) * cloudCount * periodDays;
        var energy = softKwh + cloudKwh;
        var kgPerKwh = Math.Max(0, opts.SustainabilityKgCo2PerKwh);
        var deviceCo2 = energy * kgPerKwh;
        var txCo2 = Math.Max(0, opts.SustainabilityKgCo2PerSignedTransaction) * signed;
        return new Footprint(
            Round(energy, 4),
            Round(deviceCo2, 4),
            Round(txCo2, 4),
            Round(deviceCo2 + txCo2, 4));
    }

    private static IReadOnlyList<TseSustainabilityTrendDto> BuildTrend(
        IReadOnlyList<ReceiptSnippet> receipts,
        IReadOnlyList<TseDevice> devices,
        int softCount,
        int cloudCount,
        DateTime fromUtc,
        DateTime toUtc,
        TseOptions opts)
    {
        var byDay = receipts
            .GroupBy(r => r.IssuedAt.Date)
            .ToDictionary(g => g.Key, g => g.Count(x => x.Signed));

        var softDay = Math.Max(0, opts.SustainabilityKwhPerSoftDeviceDay);
        var cloudDay = Math.Max(0, opts.SustainabilityKwhPerCloudDeviceDay);
        var kgPerKwh = Math.Max(0, opts.SustainabilityKgCo2PerKwh);
        var kgPerTx = Math.Max(0, opts.SustainabilityKgCo2PerSignedTransaction);
        var dailyDeviceEnergy = softCount * softDay + cloudCount * cloudDay;

        var list = new List<TseSustainabilityTrendDto>();
        for (var d = fromUtc.Date; d <= toUtc.Date; d = d.AddDays(1))
        {
            byDay.TryGetValue(d, out var signed);
            var energy = dailyDeviceEnergy;
            var carbon = energy * kgPerKwh + signed * kgPerTx;
            list.Add(new TseSustainabilityTrendDto
            {
                Date = d,
                Label = d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                CarbonKg = Round(carbon, 4),
                EnergyKwh = Round(energy, 4),
                TransactionCount = signed,
            });
        }

        return list;
    }

    private static bool IsSoftOrDemo(TseDevice d) =>
        string.Equals(d.DeviceType, "Soft", StringComparison.OrdinalIgnoreCase)
        || string.Equals(d.Provider, TseOptions.ProviderSoft, StringComparison.OrdinalIgnoreCase)
        || string.Equals(d.Provider, TseOptions.ProviderFake, StringComparison.OrdinalIgnoreCase);

    private static (DateTime From, DateTime To) NormalizePeriod(DateTime fromUtc, DateTime toUtc)
    {
        if (toUtc <= fromUtc)
            throw new ArgumentException("toUtc must be strictly greater than fromUtc.");
        if ((toUtc - fromUtc).TotalDays > MaxPeriodDays)
            throw new ArgumentException($"Period must be at most {MaxPeriodDays} days.");
        return (fromUtc, toUtc);
    }

    private static DateTime NormalizeUtc(DateTime value) =>
        value.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(value, DateTimeKind.Utc)
            : value.ToUniversalTime();

    private static double Round(double value, int digits) =>
        Math.Round(value, digits, MidpointRounding.AwayFromZero);

    private static double Clamp(double value, double min, double max) =>
        value < min ? min : value > max ? max : value;

    private static decimal RoundMoney(decimal value) =>
        Math.Round(value, 2, MidpointRounding.AwayFromZero);

    private sealed record ReceiptSnippet(DateTime IssuedAt, bool Signed);

    private sealed record Footprint(
        double EnergyKwh,
        double DeviceEnergyKgCo2,
        double TransactionApiKgCo2,
        double TotalKgCo2);
}

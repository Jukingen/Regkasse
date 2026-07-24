using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services.Tse;

public sealed class TseTrainingService : ITseTrainingService
{
    private static readonly IReadOnlyList<CatalogModule> Catalog =
    [
        new("tse_basics", "Basics", "TSE basics", "Health status, connection, and when POS can issue receipts.", 10),
        new("certificate_lifecycle", "Certificate", "Certificate lifecycle", "Expiry, renewal, and invalid certificate failure drills.", 15),
        new("network_failures", "Failures", "Network failures", "Timeouts and connection loss — operational response without rewriting fiscal rows.", 12),
        new("signature_errors", "Failures", "Signature errors", "Device-level signature error flags vs real RKSV compact JWS (diagnostic only).", 12),
        new("failover_drill", "Operations", "Failover drill", "Primary/standby awareness and what Super Admin tools mutate.", 15),
        new("offline_queue", "Operations", "Offline queue awareness", "Legacy offline intents vs offline orders — never merge the two systems.", 10),
    ];

    private readonly AppDbContext _db;
    private readonly ITseSimulatorService _simulator;
    private readonly ITseTrainingConsoleStore _console;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<TseTrainingService> _logger;

    public TseTrainingService(
        AppDbContext db,
        ITseSimulatorService simulator,
        ITseTrainingConsoleStore console,
        IHostEnvironment environment,
        ILogger<TseTrainingService> logger)
    {
        _db = db;
        _simulator = simulator;
        _console = console;
        _environment = environment;
        _logger = logger;
    }

    public async Task<TseTrainingEnvironmentDto> GetEnvironmentAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        var modules = await GetModulesAsync(userId, cancellationToken).ConfigureAwait(false);
        return new TseTrainingEnvironmentDto
        {
            Modules = modules,
            CompletedCount = modules.Count(m => m.IsCompleted),
            TotalCount = modules.Count,
            SimulationEnabled = _environment.IsDevelopment(),
            DiagnosticOnly = true,
        };
    }

    public async Task<IReadOnlyList<TseTrainingModuleDto>> GetModulesAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        RequireUser(userId);
        var progress = await _db.TseTrainingProgress.AsNoTracking()
            .Where(p => p.UserId == userId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var byModule = progress.ToDictionary(p => p.ModuleId, StringComparer.OrdinalIgnoreCase);

        return Catalog.Select(c =>
        {
            byModule.TryGetValue(c.Id, out var row);
            return new TseTrainingModuleDto
            {
                Id = c.Id,
                Title = c.Title,
                Description = c.Description,
                EstimatedMinutes = c.EstimatedMinutes,
                Category = c.Category,
                IsStarted = row?.IsStarted == true || row?.IsCompleted == true,
                IsCompleted = row?.IsCompleted == true,
                CompletedAt = row?.CompletedAt,
            };
        }).ToList();
    }

    public async Task<TseTrainingModuleDto> StartModuleAsync(
        string userId,
        string moduleId,
        CancellationToken cancellationToken = default)
    {
        RequireUser(userId);
        if (string.IsNullOrWhiteSpace(moduleId))
            throw new ArgumentException("moduleId is required.", nameof(moduleId));

        var catalog = Catalog.FirstOrDefault(c =>
            string.Equals(c.Id, moduleId.Trim(), StringComparison.OrdinalIgnoreCase));
        if (catalog is null)
            throw new KeyNotFoundException($"Training module '{moduleId}' was not found.");

        var now = DateTime.UtcNow;
        var row = await _db.TseTrainingProgress
            .FirstOrDefaultAsync(
                p => p.UserId == userId && p.ModuleId == catalog.Id,
                cancellationToken)
            .ConfigureAwait(false);

        if (row is null)
        {
            row = new TseTrainingProgress
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                ModuleId = catalog.Id,
                IsStarted = true,
                IsCompleted = true,
                StartedAt = now,
                CompletedAt = now,
                UpdatedAt = now,
            };
            _db.TseTrainingProgress.Add(row);
        }
        else
        {
            row.IsStarted = true;
            row.StartedAt ??= now;
            row.IsCompleted = true;
            row.CompletedAt ??= now;
            row.UpdatedAt = now;
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("TSE training module completed UserId={UserId} ModuleId={ModuleId}", userId, catalog.Id);

        return new TseTrainingModuleDto
        {
            Id = catalog.Id,
            Title = catalog.Title,
            Description = catalog.Description,
            EstimatedMinutes = catalog.EstimatedMinutes,
            Category = catalog.Category,
            IsStarted = true,
            IsCompleted = true,
            CompletedAt = row.CompletedAt,
        };
    }

    public Task<IReadOnlyList<TseTrainingConsoleEntryDto>> GetConsoleAsync(
        string userId,
        int take = 100,
        CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;
        RequireUser(userId);
        return Task.FromResult(_console.GetEntries(userId, take));
    }

    public void ClearConsole(string userId)
    {
        RequireUser(userId);
        _console.Clear(userId);
    }

    public async Task<TseTrainingSimulateResultDto> SimulateFailureAsync(
        string userId,
        Guid deviceId,
        string failureType,
        CancellationToken cancellationToken = default)
    {
        RequireUser(userId);
        if (deviceId == Guid.Empty)
            return Fail("deviceId is required.", failureType);
        if (string.IsNullOrWhiteSpace(failureType))
            return Fail("failureType is required.", failureType);

        if (!_environment.IsDevelopment())
        {
            var denied = AppendConsole(userId, false, "error", failureType, deviceId,
                "Simulation is only available in Development.");
            return new TseTrainingSimulateResultDto
            {
                Success = false,
                Error = "Simulation is only available in Development.",
                Message = denied.Message,
                Scenario = failureType,
                ConsoleEntry = denied,
                DiagnosticOnly = true,
            };
        }

        var typeKey = failureType.Trim();
        TseSimulationResultDto result;
        string scenario;

        if (string.Equals(typeKey, "CertificateExpiry", StringComparison.OrdinalIgnoreCase)
            || string.Equals(typeKey, "certificate.expiry", StringComparison.OrdinalIgnoreCase))
        {
            scenario = "CertificateExpiry";
            result = await _simulator
                .SimulateCertificateExpiryAsync(deviceId, DateTime.UtcNow.AddDays(-1), userId, cancellationToken)
                .ConfigureAwait(false);
        }
        else if (Enum.TryParse<TseSimulatorFailureType>(typeKey, ignoreCase: true, out var parsed))
        {
            scenario = parsed.ToString();
            result = await _simulator
                .SimulateTseFailureAsync(deviceId, parsed, userId, cancellationToken)
                .ConfigureAwait(false);
        }
        else
        {
            return Fail(
                $"Unknown failureType '{failureType}'. Allowed: {string.Join(", ", Enum.GetNames<TseSimulatorFailureType>())}, CertificateExpiry.",
                typeKey);
        }

        var entry = AppendConsole(
            userId,
            result.Success,
            result.Success ? "warn" : "error",
            scenario,
            deviceId,
            result.Success ? result.Message : (result.Error ?? result.Message));

        return new TseTrainingSimulateResultDto
        {
            Success = result.Success,
            Error = result.Error,
            Message = result.Message,
            Scenario = scenario,
            Device = result.Device,
            ConsoleEntry = entry,
            DiagnosticOnly = true,
        };
    }

    public async Task<TseTrainingSimulateResultDto> ResetSimulationAsync(
        string userId,
        Guid deviceId,
        CancellationToken cancellationToken = default)
    {
        RequireUser(userId);
        if (deviceId == Guid.Empty)
            return Fail("deviceId is required.", "Reset");

        if (!_environment.IsDevelopment())
        {
            var denied = AppendConsole(userId, false, "error", "Reset", deviceId,
                "Simulation is only available in Development.");
            return new TseTrainingSimulateResultDto
            {
                Success = false,
                Error = "Simulation is only available in Development.",
                Message = denied.Message,
                Scenario = "Reset",
                ConsoleEntry = denied,
                DiagnosticOnly = true,
            };
        }

        var result = await _simulator
            .ResetSimulationAsync(deviceId, userId, cancellationToken)
            .ConfigureAwait(false);

        var entry = AppendConsole(
            userId,
            result.Success,
            result.Success ? "info" : "error",
            "Reset",
            deviceId,
            result.Success ? result.Message : (result.Error ?? result.Message));

        return new TseTrainingSimulateResultDto
        {
            Success = result.Success,
            Error = result.Error,
            Message = result.Message,
            Scenario = "Reset",
            Device = result.Device,
            ConsoleEntry = entry,
            DiagnosticOnly = true,
        };
    }

    private TseTrainingConsoleEntryDto AppendConsole(
        string userId,
        bool success,
        string level,
        string scenario,
        Guid? deviceId,
        string message)
    {
        return _console.Append(userId, new TseTrainingConsoleEntryDto
        {
            Id = Guid.NewGuid(),
            TimestampUtc = DateTime.UtcNow,
            Level = level,
            Scenario = scenario,
            Message = message,
            DeviceId = deviceId,
            Success = success,
        });
    }

    private static TseTrainingSimulateResultDto Fail(string error, string scenario) => new()
    {
        Success = false,
        Error = error,
        Message = error,
        Scenario = scenario,
        DiagnosticOnly = true,
    };

    private static void RequireUser(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("userId is required.", nameof(userId));
    }

    private sealed record CatalogModule(
        string Id,
        string Category,
        string Title,
        string Description,
        int EstimatedMinutes);
}

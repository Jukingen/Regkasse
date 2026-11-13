using System.Text.Json;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services;

public interface IDepExportComplianceScoreService
{
    Task<DepExportComplianceScoreDto> CalculateScoreAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default);

    Task<DepExportComplianceScoreHistoryDto> GetScoreHistoryAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DepExportImprovementSuggestionDto>> GetImprovementSuggestionsAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Weighted operational DEP export readiness score (legal periods + validation + archive).
/// Not an official BMF/RKSV certification grade.
/// </summary>
public sealed class DepExportComplianceScoreService : IDepExportComplianceScoreService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    private readonly AppDbContext _db;
    private readonly IDepExportRequirementService _requirementService;
    private readonly IDepExportValidationService _validationService;
    private readonly IDepExportArchiveService _archiveService;
    private readonly ILogger<DepExportComplianceScoreService> _logger;

    public DepExportComplianceScoreService(
        AppDbContext db,
        IDepExportRequirementService requirementService,
        IDepExportValidationService validationService,
        IDepExportArchiveService archiveService,
        ILogger<DepExportComplianceScoreService> logger)
    {
        _db = db;
        _requirementService = requirementService;
        _validationService = validationService;
        _archiveService = archiveService;
        _logger = logger;
    }

    public async Task<DepExportComplianceScoreDto> CalculateScoreAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var dto = await BuildScoreAsync(tenantId, cancellationToken).ConfigureAwait(false);

        try
        {
            _db.DepExportComplianceScores.Add(new DepExportComplianceScoreSnapshot
            {
                TenantId = tenantId,
                Score = dto.Score,
                Grade = dto.Grade,
                CalculatedAt = dto.CalculatedAt,
                FactorsJson = JsonSerializer.Serialize(dto.Factors, JsonOptions),
                CriticalIssuesJson = JsonSerializer.Serialize(dto.CriticalIssues, JsonOptions),
                WarningsJson = JsonSerializer.Serialize(dto.Warnings, JsonOptions),
            });
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to persist DEP compliance score snapshot for {TenantId}", tenantId);
        }

        return dto;
    }

    public async Task<DepExportComplianceScoreHistoryDto> GetScoreHistoryAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var items = await _db.DepExportComplianceScores
            .AsNoTracking()
            .Where(s => s.TenantId == tenantId)
            .OrderByDescending(s => s.CalculatedAt)
            .Take(30)
            .Select(s => new DepExportComplianceScoreHistoryItemDto
            {
                Id = s.Id,
                Score = s.Score,
                Grade = s.Grade,
                CalculatedAt = s.CalculatedAt,
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new DepExportComplianceScoreHistoryDto
        {
            TenantId = tenantId,
            Items = items,
        };
    }

    public async Task<IReadOnlyList<DepExportImprovementSuggestionDto>> GetImprovementSuggestionsAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var score = await BuildScoreAsync(tenantId, cancellationToken).ConfigureAwait(false);
        var suggestions = new List<DepExportImprovementSuggestionDto>();

        foreach (var factor in score.Factors.Where(f => f.Status != DepExportScoreFactorStatuses.Passed))
        {
            suggestions.Add(SuggestionForFactor(factor));
        }

        foreach (var issue in score.CriticalIssues)
        {
            if (suggestions.Any(s => s.Description.Contains(issue, StringComparison.Ordinal)))
                continue;

            suggestions.Add(new DepExportImprovementSuggestionDto
            {
                Code = "critical-issue",
                Severity = "Critical",
                Title = "Critical DEP export issue",
                Description = issue,
                DeepLink = "/rksv/dep-export-compliance",
            });
        }

        return suggestions
            .OrderByDescending(s => s.Severity == "Critical")
            .ThenByDescending(s => s.Severity == "Warning")
            .ToList();
    }

    private async Task<DepExportComplianceScoreDto> BuildScoreAsync(
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var status = await _requirementService
            .GetComplianceStatusAsync(tenantId, cancellationToken)
            .ConfigureAwait(false);
        var validation = await _validationService
            .GetValidationReportAsync(tenantId, cancellationToken)
            .ConfigureAwait(false);
        var archive = await _archiveService
            .GetArchiveReportAsync(tenantId, cancellationToken)
            .ConfigureAwait(false);

        var factors = BuildFactors(status, validation, archive);
        var critical = new List<string>();
        var warnings = new List<string>();
        CollectIssues(status, validation, archive, critical, warnings);
        var score = ComputeWeightedScore(factors);

        return new DepExportComplianceScoreDto
        {
            TenantId = tenantId,
            Score = score,
            Grade = GradeFor(score),
            CalculatedAt = DateTime.UtcNow,
            Factors = factors,
            CriticalIssues = critical,
            Warnings = warnings,
        };
    }

    internal static List<DepExportScoreFactorDto> BuildFactors(
        DepExportComplianceStatus status,
        DepExportValidationReport validation,
        DepExportArchiveReport archive)
    {
        var legalScore = status.LegalIncompleteCount <= 0
            ? 100
            : Math.Max(0, 100 - status.LegalIncompleteCount * 50);

        var overdueScore = status.OverdueCount <= 0
            ? 100
            : Math.Max(0, 100 - status.OverdueCount * 40);

        var requirementScore = status.TotalRequirements <= 0
            ? (status.IsCompliant ? 100 : 0)
            : (int)Math.Round(100.0 * status.CompletedCount / status.TotalRequirements);

        var validated = validation.PassedCount + validation.FailedCount;
        int validationScore;
        if (validation.TotalExports <= 0)
            validationScore = 100;
        else if (validated <= 0)
            validationScore = validation.PendingCount > 0 ? 60 : 100;
        else
            validationScore = (int)Math.Round(100.0 * validation.PassedCount / validated);

        var archiveDenom = archive.ArchivedCount + archive.PendingArchiveCount;
        var archiveScore = archive.TotalCompletedExports <= 0
            ? 100
            : archiveDenom <= 0
                ? (archive.ArchivedCount > 0 ? 100 : 0)
                : (int)Math.Round(100.0 * archive.ArchivedCount / archiveDenom);

        return
        [
            Factor(
                "Legal obligations",
                weight: 35,
                legalScore,
                legalScore >= 100 ? DepExportScoreFactorStatuses.Passed
                    : legalScore >= 50 ? DepExportScoreFactorStatuses.Warning
                    : DepExportScoreFactorStatuses.Failed,
                status.LegalIncompleteCount <= 0
                    ? "Yearly legal DEP export obligation is fulfilled."
                    : $"{status.LegalIncompleteCount} legal obligation(s) incomplete."),
            Factor(
                "Overdue clearance",
                weight: 20,
                overdueScore,
                overdueScore >= 100 ? DepExportScoreFactorStatuses.Passed
                    : overdueScore >= 50 ? DepExportScoreFactorStatuses.Warning
                    : DepExportScoreFactorStatuses.Failed,
                status.OverdueCount <= 0
                    ? "No overdue DEP export requirements."
                    : $"{status.OverdueCount} overdue requirement(s)."),
            Factor(
                "Requirements completion",
                weight: 15,
                requirementScore,
                StatusForScore(requirementScore, pass: 90, warn: 60),
                $"{status.CompletedCount}/{status.TotalRequirements} requirements completed."),
            Factor(
                "Validation health",
                weight: 15,
                validationScore,
                validation.FailedCount > 0
                    ? DepExportScoreFactorStatuses.Failed
                    : StatusForScore(validationScore, pass: 90, warn: 70),
                validation.TotalExports <= 0
                    ? "No completed exports to validate yet."
                    : $"{validation.PassedCount} passed, {validation.FailedCount} failed, {validation.PendingCount} pending."),
            Factor(
                "Archive coverage",
                weight: 15,
                archiveScore,
                StatusForScore(archiveScore, pass: 90, warn: 60),
                archive.TotalCompletedExports <= 0
                    ? "No completed exports to archive yet."
                    : $"{archive.ArchivedCount} archived, {archive.PendingArchiveCount} pending ({archive.RetentionYears}y retention)."),
        ];
    }

    internal static int ComputeWeightedScore(IReadOnlyList<DepExportScoreFactorDto> factors)
    {
        if (factors.Count == 0)
            return 0;

        var weightSum = factors.Sum(f => f.Weight);
        if (weightSum <= 0)
            return 0;

        var weighted = factors.Sum(f => f.Score * f.Weight);
        return (int)Math.Round(weighted / (double)weightSum);
    }

    internal static string GradeFor(int score) =>
        score >= 90 ? "A"
        : score >= 80 ? "B"
        : score >= 70 ? "C"
        : score >= 60 ? "D"
        : "F";

    private static void CollectIssues(
        DepExportComplianceStatus status,
        DepExportValidationReport validation,
        DepExportArchiveReport archive,
        List<string> critical,
        List<string> warnings)
    {
        if (status.LegalIncompleteCount > 0)
            critical.Add($"{status.LegalIncompleteCount} legal yearly DEP export obligation(s) incomplete.");

        if (status.OverdueCount > 0)
            critical.Add($"{status.OverdueCount} DEP export requirement(s) are overdue.");

        if (validation.FailedCount > 0)
            critical.Add($"{validation.FailedCount} DEP export validation(s) failed.");

        if (validation.PendingCount > 0)
            warnings.Add($"{validation.PendingCount} completed export(s) not validated yet.");

        if (archive.PendingArchiveCount > 0)
            warnings.Add($"{archive.PendingArchiveCount} completed export(s) not archived yet.");

        var recommendedPending = status.PendingCount - status.LegalIncompleteCount;
        if (recommendedPending > 0)
            warnings.Add($"{recommendedPending} recommended/optional requirement(s) still open.");
    }

    private static DepExportScoreFactorDto Factor(
        string name,
        int weight,
        int score,
        string status,
        string description) =>
        new()
        {
            Name = name,
            Weight = weight,
            Score = Math.Clamp(score, 0, 100),
            Status = status,
            Description = description,
        };

    private static string StatusForScore(int score, int pass, int warn) =>
        score >= pass ? DepExportScoreFactorStatuses.Passed
        : score >= warn ? DepExportScoreFactorStatuses.Warning
        : DepExportScoreFactorStatuses.Failed;

    private static DepExportImprovementSuggestionDto SuggestionForFactor(DepExportScoreFactorDto factor) =>
        factor.Name switch
        {
            "Legal obligations" => new DepExportImprovementSuggestionDto
            {
                Code = "legal-export",
                Severity = factor.Status == DepExportScoreFactorStatuses.Failed ? "Critical" : "Warning",
                Title = "Complete legal yearly DEP export",
                Description = factor.Description,
                DeepLink = "/rksv/dep-export-compliance",
            },
            "Overdue clearance" => new DepExportImprovementSuggestionDto
            {
                Code = "overdue",
                Severity = "Critical",
                Title = "Clear overdue DEP requirements",
                Description = factor.Description,
                DeepLink = "/rksv/dep-export-compliance",
            },
            "Validation health" => new DepExportImprovementSuggestionDto
            {
                Code = "validation",
                Severity = factor.Status == DepExportScoreFactorStatuses.Failed ? "Critical" : "Warning",
                Title = "Re-run DEP export validation",
                Description = factor.Description,
                DeepLink = "/admin/rksv/dep-export",
            },
            "Archive coverage" => new DepExportImprovementSuggestionDto
            {
                Code = "archive",
                Severity = "Warning",
                Title = "Archive pending DEP exports",
                Description = factor.Description,
                DeepLink = "/rksv/dep-export-compliance",
            },
            _ => new DepExportImprovementSuggestionDto
            {
                Code = "requirements",
                Severity = "Warning",
                Title = "Complete open DEP requirements",
                Description = factor.Description,
                DeepLink = "/rksv/dep-export-compliance",
            },
        };
}

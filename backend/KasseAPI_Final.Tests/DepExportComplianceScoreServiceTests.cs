using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class DepExportComplianceScoreServiceTests
{
    [Fact]
    public void GradeFor_MapsBands()
    {
        Assert.Equal("A", DepExportComplianceScoreService.GradeFor(95));
        Assert.Equal("B", DepExportComplianceScoreService.GradeFor(85));
        Assert.Equal("C", DepExportComplianceScoreService.GradeFor(75));
        Assert.Equal("D", DepExportComplianceScoreService.GradeFor(65));
        Assert.Equal("F", DepExportComplianceScoreService.GradeFor(40));
    }

    [Fact]
    public void ComputeWeightedScore_RespectsWeights()
    {
        var factors = new List<DepExportScoreFactorDto>
        {
            new() { Name = "a", Weight = 50, Score = 100 },
            new() { Name = "b", Weight = 50, Score = 0 },
        };

        Assert.Equal(50, DepExportComplianceScoreService.ComputeWeightedScore(factors));
    }

    [Fact]
    public void BuildFactors_PerfectInputs_YieldHighScoreAndPassedStatuses()
    {
        var status = new DepExportComplianceStatus
        {
            IsCompliant = true,
            TotalRequirements = 2,
            CompletedCount = 2,
            PendingCount = 0,
            OverdueCount = 0,
            LegalIncompleteCount = 0,
        };
        var validation = new DepExportValidationReport
        {
            TotalExports = 2,
            PassedCount = 2,
            FailedCount = 0,
            PendingCount = 0,
        };
        var archive = new DepExportArchiveReport
        {
            TotalCompletedExports = 2,
            ArchivedCount = 2,
            PendingArchiveCount = 0,
            RetentionYears = 7,
        };

        var factors = DepExportComplianceScoreService.BuildFactors(status, validation, archive);
        var score = DepExportComplianceScoreService.ComputeWeightedScore(factors);

        Assert.Equal(100, score);
        Assert.Equal("A", DepExportComplianceScoreService.GradeFor(score));
        Assert.All(factors, f => Assert.Equal(DepExportScoreFactorStatuses.Passed, f.Status));
        Assert.Equal(100, factors.Sum(f => f.Weight));
    }

    [Fact]
    public void BuildFactors_LegalOverdueAndFailedValidation_LowerScore()
    {
        var status = new DepExportComplianceStatus
        {
            IsCompliant = false,
            TotalRequirements = 3,
            CompletedCount = 1,
            PendingCount = 2,
            OverdueCount = 2,
            LegalIncompleteCount = 1,
        };
        var validation = new DepExportValidationReport
        {
            TotalExports = 3,
            PassedCount = 0,
            FailedCount = 2,
            PendingCount = 1,
        };
        var archive = new DepExportArchiveReport
        {
            TotalCompletedExports = 3,
            ArchivedCount = 0,
            PendingArchiveCount = 3,
            RetentionYears = 7,
        };

        var factors = DepExportComplianceScoreService.BuildFactors(status, validation, archive);
        var score = DepExportComplianceScoreService.ComputeWeightedScore(factors);

        Assert.True(score < 50);
        Assert.Equal("F", DepExportComplianceScoreService.GradeFor(score));
        Assert.Contains(factors, f => f.Name == "Legal obligations" && f.Status == DepExportScoreFactorStatuses.Warning);
        Assert.Contains(factors, f => f.Name == "Validation health" && f.Status == DepExportScoreFactorStatuses.Failed);
    }
}

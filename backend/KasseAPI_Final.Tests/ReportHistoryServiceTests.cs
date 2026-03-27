using Microsoft.EntityFrameworkCore;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.Reports;
using KasseAPI_Final.Services;
using Xunit;

namespace KasseAPI_Final.Tests;

public class ReportHistoryServiceTests
{
    [Fact]
    public async Task GetHistoryAsync_SingleReport_ReturnsOriginalAndCurrent()
    {
        await using var db = CreateDb();
        var reportId = Guid.NewGuid();
        db.Set<TagesberichtReport>().Add(new TagesberichtReport
        {
            Id = reportId,
            OriginalReportId = reportId,
            ReportVersion = 1,
            ReportStatus = TagesberichtReportStatuses.Finalized,
            CorrectionKind = TagesberichtCorrectionKinds.None,
            CashRegisterId = Guid.NewGuid(),
            SnapshotJson = "{}",
            SnapshotHash = "abc",
            CreatedByUserId = "u",
            CreatedAtUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var service = new ReportHistoryService(db, new FakeSubmissionCompatService());
        var history = await service.GetHistoryAsync("tagesbericht", reportId);

        Assert.NotNull(history);
        Assert.Single(history!.Items);
        var row = history.Items[0];
        Assert.True(row.IsOriginalVersion);
        Assert.True(row.IsCurrentActiveVersion);
        Assert.Contains("original", row.LabelKeys);
        Assert.Contains("current", row.LabelKeys);
    }

    [Fact]
    public async Task GetHistoryAsync_CorrectionChain_ContainsSupersededAndCurrentWithSubmissionStates()
    {
        await using var db = CreateDb();
        var rootId = Guid.NewGuid();
        var correctionId = Guid.NewGuid();
        var outboxId = Guid.NewGuid();

        db.Set<MonatsberichtReport>().AddRange(
            new MonatsberichtReport
            {
                Id = rootId,
                OriginalReportId = rootId,
                ReportVersion = 1,
                ReportStatus = MonatsberichtReportStatuses.Superseded,
                CorrectionKind = TagesberichtCorrectionKinds.None,
                ScopeKind = MonatsberichtScopeKinds.Company,
                SnapshotJson = "{}",
                SnapshotHash = "r1",
                CreatedByUserId = "u",
                CreatedAtUtc = DateTime.UtcNow.AddMinutes(-10),
                SupersededByReportId = correctionId,
                LastFinanzOnlineOutboxMessageId = outboxId
            },
            new MonatsberichtReport
            {
                Id = correctionId,
                OriginalReportId = rootId,
                CorrectionOfReportId = rootId,
                SupersedesReportId = rootId,
                ReportVersion = 2,
                ReportStatus = MonatsberichtReportStatuses.Finalized,
                CorrectionKind = TagesberichtCorrectionKinds.Rebuild,
                ScopeKind = MonatsberichtScopeKinds.Company,
                SnapshotJson = "{}",
                SnapshotHash = "r2",
                CreatedByUserId = "u",
                CreatedAtUtc = DateTime.UtcNow
            });
        await db.SaveChangesAsync();

        var compat = new FakeSubmissionCompatService(new Dictionary<Guid, ReportSubmissionEnvelopeDto>
        {
            [rootId] = new ReportSubmissionEnvelopeDto
            {
                ReportId = rootId,
                SubmissionState = "accepted",
                OutboxMessageId = outboxId,
                State = new ReportSubmissionStateDto { Lifecycle = "accepted", IsAccepted = true }
            },
            [correctionId] = new ReportSubmissionEnvelopeDto
            {
                ReportId = correctionId,
                SubmissionState = "retry_pending",
                OutboxMessageId = Guid.NewGuid(),
                State = new ReportSubmissionStateDto { Lifecycle = "retry_pending", RetryScheduled = true }
            }
        });

        var service = new ReportHistoryService(db, compat);
        var history = await service.GetHistoryAsync("monatsbericht", correctionId);

        Assert.NotNull(history);
        Assert.Equal(2, history!.Items.Count);
        var first = history.Items[0];
        var second = history.Items[1];
        Assert.Contains("superseded", first.LabelKeys);
        Assert.Contains("accepted", first.LabelKeys);
        Assert.Contains("current", second.LabelKeys);
        Assert.Contains("retrying", second.LabelKeys);
    }

    private static AppDbContext CreateDb()
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new AppDbContext(opts);
    }

    private sealed class FakeSubmissionCompatService : IReportSubmissionCompatibilityService
    {
        private readonly IReadOnlyDictionary<Guid, ReportSubmissionEnvelopeDto> _map;

        public FakeSubmissionCompatService(IReadOnlyDictionary<Guid, ReportSubmissionEnvelopeDto>? map = null)
        {
            _map = map ?? new Dictionary<Guid, ReportSubmissionEnvelopeDto>();
        }

        public Task<ReportSubmissionEnvelopeDto> BuildEnvelopeAsync(
            BuildReportSubmissionEnvelopeRequest request,
            CancellationToken cancellationToken = default)
        {
            if (_map.TryGetValue(request.ReportId, out var dto))
            {
                dto.ReportType = request.ReportType;
                return Task.FromResult(dto);
            }

            return Task.FromResult(new ReportSubmissionEnvelopeDto
            {
                ReportType = request.ReportType,
                ReportId = request.ReportId,
                SubmissionState = "not_submitted",
                State = new ReportSubmissionStateDto { Lifecycle = "not_submitted" },
            });
        }

        public TagesberichtSubmissionStateDto ToLegacySubmissionState(ReportSubmissionEnvelopeDto envelope)
        {
            return new TagesberichtSubmissionStateDto
            {
                Lifecycle = envelope.SubmissionState,
                FinanzOnlineOutboxMessageId = envelope.OutboxMessageId,
                OutboxStatus = envelope.OutboxStatus,
                ExternalReferenceId = envelope.State.ExternalReferenceId,
                LastErrorMessage = envelope.State.LastErrorMessage
            };
        }
    }
}

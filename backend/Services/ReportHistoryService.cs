using Microsoft.EntityFrameworkCore;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.Reports;

namespace KasseAPI_Final.Services;

public sealed class ReportHistoryService : IReportHistoryService
{
    private readonly AppDbContext _db;
    private readonly IReportSubmissionCompatibilityService _submissionCompat;

    public ReportHistoryService(
        AppDbContext db,
        IReportSubmissionCompatibilityService submissionCompat)
    {
        _db = db;
        _submissionCompat = submissionCompat;
    }

    public async Task<ReportHistoryTimelineDto?> GetHistoryAsync(
        string reportType,
        Guid reportId,
        CancellationToken cancellationToken = default)
    {
        var normalized = (reportType ?? string.Empty).Trim().ToLowerInvariant();
        return normalized switch
        {
            "tagesbericht" => await BuildTagesberichtHistoryAsync(reportId, cancellationToken),
            "monatsbericht" => await BuildMonatsberichtHistoryAsync(reportId, cancellationToken),
            "jahresbericht" => await BuildJahresberichtHistoryAsync(reportId, cancellationToken),
            _ => null
        };
    }

    private async Task<ReportHistoryTimelineDto?> BuildTagesberichtHistoryAsync(Guid reportId, CancellationToken cancellationToken)
    {
        var target = await _db.Set<TagesberichtReport>()
            .AsNoTracking()
            .Where(x => x.Id == reportId)
            .Select(x => new HistoryRow
            {
                ReportId = x.Id,
                ReportVersion = x.ReportVersion,
                ReportStatus = x.ReportStatus,
                OriginalReportId = x.OriginalReportId,
                CorrectionOfReportId = x.CorrectionOfReportId,
                SupersedesReportId = x.SupersedesReportId,
                SupersededByReportId = x.SupersededByReportId,
                CreatedAtUtc = x.CreatedAtUtc,
                FinalizedAtUtc = x.FinalizedAtUtc,
                LastOutboxMessageId = x.LastFinanzOnlineOutboxMessageId,
            })
            .FirstOrDefaultAsync(cancellationToken);
        if (target == null) return null;

        var rootId = target.OriginalReportId ?? target.ReportId;

        var chain = await _db.Set<TagesberichtReport>()
            .AsNoTracking()
            .Where(x => x.OriginalReportId == rootId || x.Id == rootId)
            .OrderBy(x => x.ReportVersion)
            .ThenBy(x => x.CreatedAtUtc)
            .Select(x => new HistoryRow
            {
                ReportId = x.Id,
                ReportVersion = x.ReportVersion,
                ReportStatus = x.ReportStatus,
                OriginalReportId = x.OriginalReportId,
                CorrectionOfReportId = x.CorrectionOfReportId,
                SupersedesReportId = x.SupersedesReportId,
                SupersededByReportId = x.SupersededByReportId,
                CreatedAtUtc = x.CreatedAtUtc,
                FinalizedAtUtc = x.FinalizedAtUtc,
                LastOutboxMessageId = x.LastFinanzOnlineOutboxMessageId,
            })
            .ToListAsync(cancellationToken);

        return await BuildTimelineDtoAsync("tagesbericht", "Tagesbericht", reportId, rootId, chain, cancellationToken);
    }

    private async Task<ReportHistoryTimelineDto?> BuildMonatsberichtHistoryAsync(Guid reportId, CancellationToken cancellationToken)
    {
        var target = await _db.Set<MonatsberichtReport>()
            .AsNoTracking()
            .Where(x => x.Id == reportId)
            .Select(x => new HistoryRow
            {
                ReportId = x.Id,
                ReportVersion = x.ReportVersion,
                ReportStatus = x.ReportStatus,
                OriginalReportId = x.OriginalReportId,
                CorrectionOfReportId = x.CorrectionOfReportId,
                SupersedesReportId = x.SupersedesReportId,
                SupersededByReportId = x.SupersededByReportId,
                CreatedAtUtc = x.CreatedAtUtc,
                FinalizedAtUtc = x.FinalizedAtUtc,
                LastOutboxMessageId = x.LastFinanzOnlineOutboxMessageId,
            })
            .FirstOrDefaultAsync(cancellationToken);
        if (target == null) return null;

        var rootId = target.OriginalReportId ?? target.ReportId;

        var chain = await _db.Set<MonatsberichtReport>()
            .AsNoTracking()
            .Where(x => x.OriginalReportId == rootId || x.Id == rootId)
            .OrderBy(x => x.ReportVersion)
            .ThenBy(x => x.CreatedAtUtc)
            .Select(x => new HistoryRow
            {
                ReportId = x.Id,
                ReportVersion = x.ReportVersion,
                ReportStatus = x.ReportStatus,
                OriginalReportId = x.OriginalReportId,
                CorrectionOfReportId = x.CorrectionOfReportId,
                SupersedesReportId = x.SupersedesReportId,
                SupersededByReportId = x.SupersededByReportId,
                CreatedAtUtc = x.CreatedAtUtc,
                FinalizedAtUtc = x.FinalizedAtUtc,
                LastOutboxMessageId = x.LastFinanzOnlineOutboxMessageId,
            })
            .ToListAsync(cancellationToken);

        return await BuildTimelineDtoAsync("monatsbericht", "Monatsbericht", reportId, rootId, chain, cancellationToken);
    }

    private async Task<ReportHistoryTimelineDto?> BuildJahresberichtHistoryAsync(Guid reportId, CancellationToken cancellationToken)
    {
        var target = await _db.Set<JahresberichtReport>()
            .AsNoTracking()
            .Where(x => x.Id == reportId)
            .Select(x => new HistoryRow
            {
                ReportId = x.Id,
                ReportVersion = x.ReportVersion,
                ReportStatus = x.ReportStatus,
                OriginalReportId = x.OriginalReportId,
                CorrectionOfReportId = x.CorrectionOfReportId,
                SupersedesReportId = x.SupersedesReportId,
                SupersededByReportId = x.SupersededByReportId,
                CreatedAtUtc = x.CreatedAtUtc,
                FinalizedAtUtc = x.FinalizedAtUtc,
                LastOutboxMessageId = x.LastFinanzOnlineOutboxMessageId,
            })
            .FirstOrDefaultAsync(cancellationToken);
        if (target == null) return null;

        var rootId = target.OriginalReportId ?? target.ReportId;

        var chain = await _db.Set<JahresberichtReport>()
            .AsNoTracking()
            .Where(x => x.OriginalReportId == rootId || x.Id == rootId)
            .OrderBy(x => x.ReportVersion)
            .ThenBy(x => x.CreatedAtUtc)
            .Select(x => new HistoryRow
            {
                ReportId = x.Id,
                ReportVersion = x.ReportVersion,
                ReportStatus = x.ReportStatus,
                OriginalReportId = x.OriginalReportId,
                CorrectionOfReportId = x.CorrectionOfReportId,
                SupersedesReportId = x.SupersedesReportId,
                SupersededByReportId = x.SupersededByReportId,
                CreatedAtUtc = x.CreatedAtUtc,
                FinalizedAtUtc = x.FinalizedAtUtc,
                LastOutboxMessageId = x.LastFinanzOnlineOutboxMessageId,
            })
            .ToListAsync(cancellationToken);

        return await BuildTimelineDtoAsync("jahresbericht", "Jahresbericht", reportId, rootId, chain, cancellationToken);
    }

    private async Task<ReportHistoryTimelineDto> BuildTimelineDtoAsync(
        string reportType,
        string submissionReportType,
        Guid requestedReportId,
        Guid rootId,
        IReadOnlyList<HistoryRow> chain,
        CancellationToken cancellationToken)
    {
        var currentActive = chain
            .Where(x => x.SupersededByReportId == null && !string.Equals(x.ReportStatus, "Superseded", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(x => x.ReportVersion)
            .ThenByDescending(x => x.CreatedAtUtc)
            .FirstOrDefault();

        var currentActiveId = currentActive?.ReportId;
        var items = new List<ReportHistoryItemDto>(chain.Count);

        foreach (var row in chain)
        {
            var envelope = await _submissionCompat.BuildEnvelopeAsync(new BuildReportSubmissionEnvelopeRequest
            {
                ReportType = submissionReportType,
                ReportId = row.ReportId,
                ReportState = row.ReportStatus,
                OutboxMessageId = row.LastOutboxMessageId,
                SupersedesReportId = row.SupersedesReportId,
                SupersededByReportId = row.SupersededByReportId
            }, cancellationToken);

            var submission = BuildSubmissionSummary(envelope);
            var isCurrent = currentActiveId.HasValue && currentActiveId.Value == row.ReportId;

            items.Add(new ReportHistoryItemDto
            {
                ReportId = row.ReportId,
                ReportVersion = row.ReportVersion,
                ReportStatus = row.ReportStatus,
                OriginalReportId = row.OriginalReportId,
                CorrectionOfReportId = row.CorrectionOfReportId,
                SupersedesReportId = row.SupersedesReportId,
                SupersededByReportId = row.SupersededByReportId,
                CreatedAtUtc = row.CreatedAtUtc,
                FinalizedAtUtc = row.FinalizedAtUtc,
                IsCurrentActiveVersion = isCurrent,
                IsOriginalVersion = row.ReportId == rootId,
                IsCorrectionVersion = row.CorrectionOfReportId.HasValue || row.SupersedesReportId.HasValue,
                Submission = submission,
                LabelKeys = BuildLabelKeys(row, submission, isCurrent, rootId)
            });
        }

        return new ReportHistoryTimelineDto
        {
            ReportType = reportType,
            RequestedReportId = requestedReportId,
            ChainRootReportId = rootId,
            CurrentActiveReportId = currentActiveId,
            Items = items
        };
    }

    private static ReportHistorySubmissionSummaryDto BuildSubmissionSummary(ReportSubmissionEnvelopeDto envelope)
    {
        var lifecycle = envelope.SubmissionState ?? envelope.State.Lifecycle ?? "not_submitted";
        var isSubmitted = envelope.OutboxMessageId.HasValue || !string.Equals(lifecycle, "not_submitted", StringComparison.OrdinalIgnoreCase);
        var isAccepted = envelope.State.IsAccepted || string.Equals(lifecycle, "accepted", StringComparison.OrdinalIgnoreCase);
        var isRejected = string.Equals(lifecycle, "rejected", StringComparison.OrdinalIgnoreCase)
                         || string.Equals(lifecycle, "failed_terminal", StringComparison.OrdinalIgnoreCase)
                         || envelope.State.RequiresCorrection;
        var isRetrying = string.Equals(lifecycle, "retry_pending", StringComparison.OrdinalIgnoreCase)
                         || string.Equals(lifecycle, "queued", StringComparison.OrdinalIgnoreCase)
                         || string.Equals(lifecycle, "pending", StringComparison.OrdinalIgnoreCase)
                         || string.Equals(lifecycle, "awaiting_protocol", StringComparison.OrdinalIgnoreCase);

        return new ReportHistorySubmissionSummaryDto
        {
            Lifecycle = lifecycle,
            OutboxMessageId = envelope.OutboxMessageId,
            OutboxStatus = envelope.OutboxStatus,
            LatestStatusCode = envelope.OutboxStatus ?? lifecycle,
            ExternalReferenceId = envelope.State.ExternalReferenceId,
            LastErrorMessage = envelope.State.LastErrorMessage,
            IsSubmitted = isSubmitted,
            IsAccepted = isAccepted,
            IsRejected = isRejected,
            IsRetrying = isRetrying,
            HasMissingOutboxReference = envelope.RejectionReasons.Any(x => string.Equals(x, "outbox_row_missing", StringComparison.OrdinalIgnoreCase))
        };
    }

    private static IReadOnlyList<string> BuildLabelKeys(
        HistoryRow row,
        ReportHistorySubmissionSummaryDto submission,
        bool isCurrent,
        Guid rootId)
    {
        var keys = new List<string>(8);

        if (row.ReportId == rootId) keys.Add("original");
        if (row.CorrectionOfReportId.HasValue || row.SupersedesReportId.HasValue) keys.Add("correction");
        if (row.SupersededByReportId.HasValue || string.Equals(row.ReportStatus, "Superseded", StringComparison.OrdinalIgnoreCase)) keys.Add("superseded");
        if (isCurrent) keys.Add("current");
        if (submission.IsSubmitted) keys.Add("submitted");
        if (submission.IsAccepted) keys.Add("accepted");
        if (submission.IsRejected) keys.Add("rejected");
        if (submission.IsRetrying) keys.Add("retrying");

        return keys;
    }

    private sealed class HistoryRow
    {
        public Guid ReportId { get; set; }
        public int ReportVersion { get; set; }
        public string ReportStatus { get; set; } = string.Empty;
        public Guid? OriginalReportId { get; set; }
        public Guid? CorrectionOfReportId { get; set; }
        public Guid? SupersedesReportId { get; set; }
        public Guid? SupersededByReportId { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public DateTime? FinalizedAtUtc { get; set; }
        public Guid? LastOutboxMessageId { get; set; }
    }
}

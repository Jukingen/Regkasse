using KasseAPI_Final.Models.Reports;
using KasseAPI_Final.Tse;

namespace KasseAPI_Final.Services;

/// <summary>
/// Analyzes signed receipts for RKSV chain continuity, BelegNr sequence gaps, and duplicates.
/// </summary>
internal static class TseChainContinuityAnalyzer
{
    internal sealed class ReceiptLink
    {
        public Guid ReceiptId { get; init; }
        public string ReceiptNumber { get; init; } = string.Empty;
        public DateTime CreatedAtUtc { get; init; }
        public string? SignatureValue { get; init; }
        public string? PrevSignatureValue { get; init; }
        public int? ParsedSequence { get; init; }
        public string? ParsedSequenceDateYmd { get; init; }
    }

    internal static TseContinuityRegisterReportDto AnalyzeRegister(
        Guid cashRegisterId,
        string? registerNumber,
        DateTime periodStartLocal,
        DateTime periodEndLocal,
        IReadOnlyList<ReceiptLink> orderedReceipts,
        int lastCounterFromState)
    {
        var signed = orderedReceipts
            .Where(r => !string.IsNullOrWhiteSpace(r.SignatureValue))
            .OrderBy(r => r.CreatedAtUtc)
            .ToList();

        var firstAt = signed.Count > 0 ? signed[0].CreatedAtUtc : (DateTime?)null;
        var lastAt = signed.Count > 0 ? signed[^1].CreatedAtUtc : (DateTime?)null;

        var chainBreaks = new List<TseChainBreakDto>();
        string? previousSig = null;
        foreach (var r in signed)
        {
            var actualPrev = r.PrevSignatureValue ?? string.Empty;
            if (previousSig != null && !string.Equals(actualPrev, RksvChainingValue.Compute(previousSig, registerNumber ?? string.Empty), StringComparison.Ordinal))
            {
                chainBreaks.Add(new TseChainBreakDto
                {
                    ReceiptId = r.ReceiptId,
                    ReceiptNumber = r.ReceiptNumber,
                    CreatedAtUtc = r.CreatedAtUtc,
                    ExpectedPrevSignature = TruncateSig(previousSig),
                    ActualPrevSignature = TruncateSig(actualPrev),
                });
            }

            previousSig = r.SignatureValue;
        }

        var duplicateCount = signed
            .GroupBy(r => r.ReceiptNumber, StringComparer.OrdinalIgnoreCase)
            .Count(g => g.Count() > 1);

        var sequenceGaps = 0;
        var maxGapDurationSeconds = 0.0;
        ReceiptLink? prevSeqReceipt = null;
        int? prevSeq = null;

        foreach (var r in signed)
        {
            if (!r.ParsedSequence.HasValue)
                continue;

            if (prevSeq.HasValue
                && string.Equals(r.ParsedSequenceDateYmd, prevSeqReceipt?.ParsedSequenceDateYmd, StringComparison.Ordinal)
                && r.ParsedSequence.Value > prevSeq.Value + 1)
            {
                var missing = r.ParsedSequence.Value - prevSeq.Value - 1;
                sequenceGaps += missing;
                var gapSeconds = (r.CreatedAtUtc - prevSeqReceipt!.CreatedAtUtc).TotalSeconds;
                if (gapSeconds > maxGapDurationSeconds)
                    maxGapDurationSeconds = gapSeconds;
            }

            prevSeq = r.ParsedSequence;
            prevSeqReceipt = r;
        }

        var missingSignatureCount = orderedReceipts.Count(r => string.IsNullOrWhiteSpace(r.SignatureValue));
        var gapsCount = sequenceGaps + chainBreaks.Count + missingSignatureCount;
        var hasGaps = gapsCount > 0;
        var hasDuplicates = duplicateCount > 0;

        var exportQuery =
            $"startDate={periodStartLocal:yyyy-MM-dd}&endDate={periodEndLocal:yyyy-MM-dd}&cashRegisterId={cashRegisterId}";

        return new TseContinuityRegisterReportDto
        {
            CashRegisterId = cashRegisterId,
            RegisterNumber = registerNumber,
            PeriodStartLocal = periodStartLocal,
            PeriodEndLocal = periodEndLocal,
            FirstSignatureAtUtc = firstAt,
            LastSignatureAtUtc = lastAt,
            SignatureCount = signed.Count,
            GapsCount = gapsCount,
            DuplicateCount = duplicateCount,
            ChainBreakCount = chainBreaks.Count,
            SequenceGapCount = sequenceGaps,
            MissingSignatureCount = missingSignatureCount,
            HasGaps = hasGaps,
            HasDuplicates = hasDuplicates,
            MaxGapDurationSeconds = maxGapDurationSeconds,
            LastCounterFromState = lastCounterFromState,
            DetailsExportPath = $"/api/Reports/operational/tse-chain-continuity/export?{exportQuery}&format=csv",
            DetailsExportJsonPath = $"/api/Reports/operational/tse-chain-continuity/export?{exportQuery}&format=json",
            Breaks = chainBreaks.Take(20).ToList(),
            ReceiptsInRange = orderedReceipts.Count,
        };
    }

    internal static bool TryParseBelegNrSequence(string receiptNumber, out int sequence, out string? dateYmd)
    {
        sequence = 0;
        dateYmd = null;
        if (string.IsNullOrWhiteSpace(receiptNumber))
            return false;

        var parts = receiptNumber.Split('-', StringSplitOptions.TrimEntries);
        if (parts.Length < 4)
            return false;
        if (!string.Equals(parts[0], "AT", StringComparison.OrdinalIgnoreCase))
            return false;
        dateYmd = parts[^2];
        return int.TryParse(parts[^1], out sequence) && sequence > 0;
    }

    private static string? TruncateSig(string? sig)
    {
        if (string.IsNullOrEmpty(sig)) return sig;
        return sig.Length <= 48 ? sig : sig[..48] + "…";
    }
}

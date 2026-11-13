using KasseAPI_Final.Models.Reports;

namespace KasseAPI_Final.Services.LegalExportCompleteness;

/// <summary>
/// LegalComplianceExport için anlık görünüm verisinden bütünlük değerlendirmesi (TSE/FO akışına dokunmaz).
/// </summary>
public static class LegalExportCompletenessEvaluator
{
    public const string SeverityBlock = "block";
    public const string SeverityWarn = "warn";

    public const string GateAllowed = "allowed";
    public const string GateAllowedWithWarnings = "allowed_with_warnings";
    public const string GateBlocked = "blocked";

    public static LegalExportCompletenessResultDto EvaluateTagesbericht(TagesberichtDto dto)
    {
        var issues = new List<LegalExportCompletenessIssueDto>();
        var s = dto.Summary;
        var rec = s.Reconciliation;

        if (!string.Equals(dto.ReportStatus, "Finalized", StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(Block(
                LegalExportCompletenessCodes.ProvisionalNotFinalized,
                "Bericht ist nicht finalisiert — Legal Compliance Export ist gesperrt.",
                "Report is not finalized — legal compliance export is blocked."));
        }

        if (dto.SupersededByReportId != null)
        {
            issues.Add(Block(
                LegalExportCompletenessCodes.StaleSupersededChain,
                "Dieser Bericht wurde durch eine neuere Version ersetzt — nicht als aktuelle Rechtsgrundlage verwenden.",
                "This report was superseded by a newer version — not valid as current legal basis."));
        }

        if (rec.UnknownPaymentMethodRowCount > 0)
        {
            issues.Add(Block(
                LegalExportCompletenessCodes.IncompletePaymentMapping,
                $"Unbekannte Zahlart in {rec.UnknownPaymentMethodRowCount} Zeile(n) — Zuordnung unvollständig.",
                $"Unknown payment method in {rec.UnknownPaymentMethodRowCount} row(s) — mapping incomplete."));
        }

        if (rec.OfflineLinkedPaymentCount > 0)
        {
            issues.Add(Block(
                LegalExportCompletenessCodes.UnresolvedOfflineReplayGap,
                $"Offline-/Replay-Verknüpfung: {rec.OfflineLinkedPaymentCount} Zahlung(en) — Lücke nicht aufgelöst.",
                $"Offline/replay-linked payments: {rec.OfflineLinkedPaymentCount} — gap unresolved."));
        }

        var hasSalesActivity = s.SalePaymentRowCount > 0 || s.GrossSalesAmount != 0;
        if (hasSalesActivity && (s.TaxBreakdown == null || s.TaxBreakdown.Count == 0))
        {
            issues.Add(Block(
                LegalExportCompletenessCodes.MissingTaxClassification,
                "Steueraufschlüsselung fehlt trotz Umsatz — Klassifizierung unvollständig.",
                "Tax breakdown missing despite revenue — classification incomplete."));
        }

        if (s.TaxBreakdown != null)
        {
            foreach (var t in s.TaxBreakdown)
            {
                if (string.IsNullOrWhiteSpace(t.TaxBucketKey) ||
                    t.TaxBucketKey.Contains("unknown", StringComparison.OrdinalIgnoreCase))
                {
                    issues.Add(Block(
                        LegalExportCompletenessCodes.MissingTaxClassification,
                        "Steuer-Bucket enthält „unknown“ oder ist leer.",
                        "Tax bucket is empty or marked unknown."));
                    break;
                }
            }
        }

        if (hasSalesActivity && !rec.DayClosedInRksv && dto.LinkedDailyClosingId == null && rec.DailyClosingId == null)
        {
            issues.Add(Warn(
                LegalExportCompletenessCodes.MissingClosingReferences,
                "Kein Tagesabschluss-/RKSV-Bezug in den Daten — Abschlussreferenzen prüfen.",
                "No daily closing / RKSV day-closed linkage in snapshot — verify closing references."));
        }
        else if (hasSalesActivity && !rec.DayClosedInRksv)
        {
            issues.Add(Warn(
                LegalExportCompletenessCodes.MissingClosingReferences,
                "Tag laut RKSV nicht als geschlossen markiert — Abschlussstatus prüfen.",
                "Business day not marked closed in RKSV — verify closing status."));
        }

        if (rec.PaymentsWithoutInvoiceCount > 0)
        {
            issues.Add(Warn(
                LegalExportCompletenessCodes.IncompletePaymentMapping,
                $"{rec.PaymentsWithoutInvoiceCount} Zahlung(en) ohne zugehörige Rechnung — Zuordnung prüfen.",
                $"{rec.PaymentsWithoutInvoiceCount} payment(s) without invoice — review mapping."));
        }

        AppendSubmissionNoteIfAccepted(dto.Submission.Lifecycle, issues);

        return Finalize("tagesbericht", dto.Id, issues);
    }

    public static LegalExportCompletenessResultDto EvaluateMonatsbericht(MonatsberichtDto dto)
    {
        var issues = new List<LegalExportCompletenessIssueDto>();
        var summary = dto.Summary;
        var agg = summary.AggregationFromDaily;

        if (!string.Equals(dto.ReportStatus, "Finalized", StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(Block(
                LegalExportCompletenessCodes.ProvisionalNotFinalized,
                "Monatsbericht nicht finalisiert — Legal Compliance Export gesperrt.",
                "Monthly report not finalized — legal export blocked."));
        }

        if (dto.SupersededByReportId != null)
        {
            issues.Add(Block(
                LegalExportCompletenessCodes.StaleSupersededChain,
                "Bericht wurde ersetzt — keine aktuelle Rechtsgrundlage.",
                "Report was superseded — not a current legal basis."));
        }

        if (dto.UpstreamPropagation.RequiresReview)
        {
            issues.Add(Block(
                LegalExportCompletenessCodes.UpstreamReviewRequired,
                string.IsNullOrWhiteSpace(dto.UpstreamPropagation.NoteDe)
                    ? "Upstream-Korrektur erfordert Prüfung — Export blockiert."
                    : dto.UpstreamPropagation.NoteDe!,
                "Upstream correction requires review — export blocked."));
        }

        if (summary.Adjustment.RequiresReview)
        {
            issues.Add(Block(
                LegalExportCompletenessCodes.AdjustmentRequiresReview,
                string.IsNullOrWhiteSpace(summary.Adjustment.NoteDe)
                    ? "Tages- vs. Rohdaten-Abweichung erfordert Prüfung."
                    : summary.Adjustment.NoteDe!,
                "Daily vs raw rollup adjustment requires review."));
        }

        UnknownPaymentAndTax(summary.PaymentMethodBreakdown, summary.TaxBreakdown, agg.SalePaymentRowCount, issues);

        if (agg.ExpectedCalendarDaysInMonth > 0 && agg.DistinctDaysCovered < agg.ExpectedCalendarDaysInMonth)
        {
            issues.Add(Warn(
                LegalExportCompletenessCodes.IncompleteAggregationCoverage,
                $"Kalenderabdeckung: {agg.DistinctDaysCovered}/{agg.ExpectedCalendarDaysInMonth} Tage — Aggregation unvollständig.",
                $"Calendar coverage: {agg.DistinctDaysCovered}/{agg.ExpectedCalendarDaysInMonth} days — aggregation incomplete."));
        }

        AppendSubmissionNoteIfAccepted(dto.Submission.Lifecycle, issues);

        return Finalize("monatsbericht", dto.Id, issues);
    }

    public static LegalExportCompletenessResultDto EvaluateJahresbericht(JahresberichtDto dto)
    {
        var issues = new List<LegalExportCompletenessIssueDto>();
        var summary = dto.Summary;
        var agg = summary.AggregationFromMonthly;

        if (!string.Equals(dto.ReportStatus, "Finalized", StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(Block(
                LegalExportCompletenessCodes.ProvisionalNotFinalized,
                "Jahresbericht nicht finalisiert — Legal Compliance Export gesperrt.",
                "Annual report not finalized — legal export blocked."));
        }

        if (dto.SupersededByReportId != null)
        {
            issues.Add(Block(
                LegalExportCompletenessCodes.StaleSupersededChain,
                "Bericht wurde ersetzt — keine aktuelle Rechtsgrundlage.",
                "Report was superseded — not a current legal basis."));
        }

        if (dto.UpstreamPropagation.RequiresReview)
        {
            issues.Add(Block(
                LegalExportCompletenessCodes.UpstreamReviewRequired,
                string.IsNullOrWhiteSpace(dto.UpstreamPropagation.NoteDe)
                    ? "Upstream erfordert Prüfung — Export blockiert."
                    : dto.UpstreamPropagation.NoteDe!,
                "Upstream requires review — export blocked."));
        }

        if (summary.Adjustment.RequiresReview)
        {
            issues.Add(Block(
                LegalExportCompletenessCodes.AdjustmentRequiresReview,
                string.IsNullOrWhiteSpace(summary.Adjustment.NoteDe)
                    ? "Monats- vs. Rohdaten-Abweichung erfordert Prüfung."
                    : summary.Adjustment.NoteDe!,
                "Monthly vs raw rollup adjustment requires review."));
        }

        foreach (var line in summary.LinkedFinalizedMonatsberichte)
        {
            if (!string.Equals(line.ReportStatus, "Finalized", StringComparison.OrdinalIgnoreCase))
            {
                issues.Add(Block(
                    LegalExportCompletenessCodes.LinkedReportNotFinalized,
                    $"Verknüpfter Monatsbericht ({line.ViennaMonthStart:yyyy-MM}) ist nicht finalisiert ({line.ReportStatus}).",
                    $"Linked monthly report ({line.ViennaMonthStart:yyyy-MM}) is not finalized ({line.ReportStatus})."));
                break;
            }
        }

        UnknownPaymentAndTax(summary.PaymentMethodBreakdown, summary.TaxBreakdown, agg.SalePaymentRowCount, issues);

        if (agg.ExpectedMonthsInYear > 0 && agg.DistinctMonthsCovered < agg.ExpectedMonthsInYear)
        {
            issues.Add(Warn(
                LegalExportCompletenessCodes.IncompleteAggregationCoverage,
                $"Monatsabdeckung: {agg.DistinctMonthsCovered}/{agg.ExpectedMonthsInYear} — Aggregation unvollständig.",
                $"Month coverage: {agg.DistinctMonthsCovered}/{agg.ExpectedMonthsInYear} — aggregation incomplete."));
        }

        AppendSubmissionNoteIfAccepted(dto.Submission.Lifecycle, issues);

        return Finalize("jahresbericht", dto.Id, issues);
    }

    public static LegalExportCompletenessResultDto EvaluatePeriodenbericht(PeriodenberichtRunDto dto)
    {
        var issues = new List<LegalExportCompletenessIssueDto>();

        if (string.IsNullOrWhiteSpace(dto.SnapshotSchemaVersion) ||
            string.IsNullOrWhiteSpace(dto.SnapshotHash) ||
            string.IsNullOrWhiteSpace(dto.QueryParametersHash))
        {
            issues.Add(Block(
                LegalExportCompletenessCodes.MissingPeriodMetadata,
                "Eingefrorener Periodenbericht: Schema-, Snapshot- oder Parameter-Hash fehlt.",
                "Frozen period report: schema, snapshot, or parameter hash missing."));
        }

        if (string.IsNullOrWhiteSpace(dto.ExportProfileKey))
        {
            issues.Add(Warn(
                LegalExportCompletenessCodes.MissingPeriodMetadata,
                "Exportprofil beim Einfrieren nicht gesetzt — Metadaten unvollständig.",
                "Export profile key was not set at freeze — metadata incomplete."));
        }

        var sum = dto.Summary;
        foreach (var bucket in sum.ByPaymentMethod ?? Array.Empty<PaymentMethodBucketDto>())
        {
            if (IsUnknownMethodKey(bucket.MethodKey))
            {
                issues.Add(Block(
                    LegalExportCompletenessCodes.IncompletePaymentMapping,
                    "Zahlart „unknown“ oder leer im eingefrorenen Snapshot.",
                    "Unknown or empty payment method bucket in frozen snapshot."));
                break;
            }
        }

        if (sum.PaymentRowCount > 0 && sum.GrossTotalAmount != 0 && sum.TaxTotalAmount == 0)
        {
            issues.Add(Warn(
                LegalExportCompletenessCodes.MissingTaxClassification,
                "Steuerbetrag ist 0 bei Umsatz — Steuerklassifikation prüfen.",
                "Tax total is zero with revenue — verify tax classification."));
        }

        if (dto.Warnings.Count > 0)
        {
            issues.Add(Warn(
                LegalExportCompletenessCodes.IncompleteAggregationCoverage,
                $"Eingefrorener Lauf meldet {dto.Warnings.Count} Hinweis(e) — Datenlage prüfen.",
                $"Frozen run has {dto.Warnings.Count} warning(s) — review data coverage."));
        }

        return Finalize("periodenbericht", dto.Id, issues);
    }

    private static void UnknownPaymentAndTax(
        IReadOnlyList<TagesberichtPaymentMethodBreakdownDto> paymentBreakdown,
        IReadOnlyList<TagesberichtTaxBreakdownDto> taxBreakdown,
        int salePaymentRowCount,
        List<LegalExportCompletenessIssueDto> issues)
    {
        foreach (var p in paymentBreakdown)
        {
            if (IsUnknownMethodKey(p.MethodKey))
            {
                issues.Add(Block(
                    LegalExportCompletenessCodes.IncompletePaymentMapping,
                    "Zahlart „unknown“ oder leer in der Aufschlüsselung.",
                    "Unknown or empty payment method in breakdown."));
                break;
            }
        }

        var hasSales = salePaymentRowCount > 0;
        if (hasSales && (taxBreakdown == null || taxBreakdown.Count == 0))
        {
            issues.Add(Block(
                LegalExportCompletenessCodes.MissingTaxClassification,
                "Steueraufschlüsselung fehlt trotz Verkaufszeilen.",
                "Tax breakdown missing despite sale rows."));
        }

        if (taxBreakdown != null)
        {
            foreach (var t in taxBreakdown)
            {
                if (string.IsNullOrWhiteSpace(t.TaxBucketKey) ||
                    t.TaxBucketKey.Contains("unknown", StringComparison.OrdinalIgnoreCase))
                {
                    issues.Add(Block(
                        LegalExportCompletenessCodes.MissingTaxClassification,
                        "Steuer-Bucket unbekannt oder leer.",
                        "Tax bucket unknown or empty."));
                    break;
                }
            }
        }
    }

    private static bool IsUnknownMethodKey(string methodKey) =>
        string.IsNullOrWhiteSpace(methodKey) ||
        string.Equals(methodKey, "unknown", StringComparison.OrdinalIgnoreCase) ||
        methodKey.Contains("unknown", StringComparison.OrdinalIgnoreCase);

    private static void AppendSubmissionNoteIfAccepted(string lifecycle, List<LegalExportCompletenessIssueDto> issues)
    {
        if (!lifecycle.Contains("accepted", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(lifecycle, "submitted_ok", StringComparison.OrdinalIgnoreCase))
            return;

        var hasBlock = issues.Any(i => i.Severity == SeverityBlock);
        if (!hasBlock && !issues.Any(i => i.Severity == SeverityWarn))
            return;

        issues.Add(Warn(
            LegalExportCompletenessCodes.SubmissionNotProofOfCompleteness,
            "FinanzOnline-Annahme ersetzt keine vollständige Datenprüfung für Legal Export.",
            "FinanzOnline acceptance does not replace data completeness checks for legal export."));
    }

    private static LegalExportCompletenessResultDto Finalize(string reportType, Guid id, List<LegalExportCompletenessIssueDto> issues)
    {
        var hasBlock = issues.Any(i => i.Severity == SeverityBlock);
        var hasWarn = issues.Any(i => i.Severity == SeverityWarn);
        var gate = hasBlock ? GateBlocked : hasWarn ? GateAllowedWithWarnings : GateAllowed;

        return new LegalExportCompletenessResultDto
        {
            ReportType = reportType,
            ReportId = id,
            Gate = gate,
            Issues = issues
        };
    }

    private static LegalExportCompletenessIssueDto Block(string code, string de, string en) => new()
    {
        Code = code,
        Severity = SeverityBlock,
        MessageDe = de,
        MessageEn = en
    };

    private static LegalExportCompletenessIssueDto Warn(string code, string de, string en) => new()
    {
        Code = code,
        Severity = SeverityWarn,
        MessageDe = de,
        MessageEn = en
    };
}

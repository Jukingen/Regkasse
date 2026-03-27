using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KasseAPI_Final.Models;

/// <summary>
/// Formal Tagesbericht (günlük rapor) kaydı: immutable snapshot (finalize sonrası) ve FinanzOnline gönderim durumu.
/// </summary>
[Table("tagesbericht_reports")]
public sealed class TagesberichtReport
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Vienna takvim günü (00:00 yerel, Unspecified kind).</summary>
    [Required]
    public DateTime ViennaBusinessDate { get; set; }

    [Required]
    public Guid CashRegisterId { get; set; }

    /// <summary>cash_registers.location — mağaza/şube etiketi (store scope).</summary>
    [MaxLength(200)]
    public string? StoreLabel { get; set; }

    /// <summary>Operatör filtresi: yalnızca bu kasiyerin ödemeleri dahil edildiyse dolu.</summary>
    [MaxLength(450)]
    public string? OperatorUserIdScope { get; set; }

    [Required]
    [Column(TypeName = "jsonb")]
    public string SnapshotJson { get; set; } = "{}";

    [Required]
    [MaxLength(64)]
    public string SnapshotHash { get; set; } = string.Empty;

    [Required]
    [MaxLength(20)]
    public string SnapshotSchemaVersion { get; set; } = "1.0";

    /// <summary>Provisional = gün açık veya henüz finalize edilmemiş; Finalized = immutable; Superseded = düzeltme zincirinde eski kayıt.</summary>
    [Required]
    [MaxLength(30)]
    public string ReportStatus { get; set; } = TagesberichtReportStatuses.Provisional;

    [Required]
    [MaxLength(30)]
    public string CorrectionKind { get; set; } = TagesberichtCorrectionKinds.None;

    public Guid? OriginalReportId { get; set; }
    public Guid? CorrectionOfReportId { get; set; }
    public Guid? SupersedesReportId { get; set; }

    public Guid? SupersededByReportId { get; set; }

    public int ReportVersion { get; set; } = 1;

    [MaxLength(200)]
    public string? ReportRevisionReason { get; set; }

    [MaxLength(80)]
    public string? RebuildCause { get; set; }

    [MaxLength(40)]
    public string CorrectionType { get; set; } = ReportCorrectionTypes.None;

    [MaxLength(40)]
    public string SubmissionImpact { get; set; } = ReportSubmissionImpacts.None;

    public Guid? LinkedDailyClosingId { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    [MaxLength(450)]
    public string CreatedByUserId { get; set; } = string.Empty;

    public DateTime? FinalizedAtUtc { get; set; }

    [MaxLength(450)]
    public string? FinalizedByUserId { get; set; }

    public Guid? LastFinanzOnlineOutboxMessageId { get; set; }

    /// <summary>Denormalize: son bilinen gönderim özeti (UI için).</summary>
    [MaxLength(40)]
    public string? LastSubmissionStatusCode { get; set; }

    [MaxLength(500)]
    public string? LastSubmissionError { get; set; }

    /// <summary>Liste sorguları için denormalize brüt satış (snapshot ile uyumlu).</summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal SnapshotGrossSalesAmount { get; set; }

    [ForeignKey(nameof(CashRegisterId))]
    public CashRegister? CashRegister { get; set; }
}

public static class TagesberichtReportStatuses
{
    public const string Provisional = "Provisional";
    public const string Finalized = "Finalized";
    public const string Superseded = "Superseded";
}

public static class TagesberichtCorrectionKinds
{
    public const string None = "None";
    public const string Rebuild = "Rebuild";
}

/// <summary>FinanzOnline outbox ile hizalı mesaj tipi (SOAP/RKDB dışı bilgilendirici gönderim).</summary>
public static class FinanzOnlineTagesberichtMessageTypes
{
    public const string TagesberichtDailySummary = "TagesberichtDailySummary";
}

public static class ReportCorrectionTypes
{
    public const string None = "None";
    public const string Correction = "Correction";
    public const string Amendment = "Amendment";
    public const string Rebuild = "Rebuild";
}

public static class ReportSubmissionImpacts
{
    public const string None = "None";
    public const string RequiresResubmission = "RequiresResubmission";
    public const string SupersededAfterAccepted = "SupersededAfterAccepted";
    public const string RejectedRebuild = "RejectedRebuild";
}

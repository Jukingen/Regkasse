using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KasseAPI_Final.Models;

/// <summary>
/// Formal Jahresbericht: aylık rapor zincirinden (Monatsbericht) üretilen yıllık immutable snapshot.
/// </summary>
[Table("jahresbericht_reports")]
public sealed class JahresberichtReport
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Vienna takvim yılı başlangıcı (01.01.YYYY, Unspecified).</summary>
    [Required]
    public DateTime ViennaYearStart { get; set; }

    /// <summary>Register = tek kasa; Company = tüm kasalar (CashRegisterId null).</summary>
    [Required]
    [MaxLength(20)]
    public string ScopeKind { get; set; } = MonatsberichtScopeKinds.Register;

    public Guid? CashRegisterId { get; set; }

    [MaxLength(200)]
    public string? StoreLabel { get; set; }

    [Required]
    [Column(TypeName = "jsonb")]
    public string SnapshotJson { get; set; } = "{}";

    [Required]
    [MaxLength(64)]
    public string SnapshotHash { get; set; } = string.Empty;

    [Required]
    [MaxLength(20)]
    public string SnapshotSchemaVersion { get; set; } = "1.0";

    [Required]
    [MaxLength(30)]
    public string ReportStatus { get; set; } = MonatsberichtReportStatuses.Provisional;

    [Required]
    [MaxLength(30)]
    public string CorrectionKind { get; set; } = MonatsberichtCorrectionKinds.None;

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

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    [MaxLength(450)]
    public string CreatedByUserId { get; set; } = string.Empty;

    public DateTime? FinalizedAtUtc { get; set; }

    [MaxLength(450)]
    public string? FinalizedByUserId { get; set; }

    public Guid? LastFinanzOnlineOutboxMessageId { get; set; }

    [MaxLength(40)]
    public string? LastSubmissionStatusCode { get; set; }

    [MaxLength(500)]
    public string? LastSubmissionError { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal SnapshotGrossSalesAmount { get; set; }

    [ForeignKey(nameof(CashRegisterId))]
    public CashRegister? CashRegister { get; set; }
}

public static class FinanzOnlineJahresberichtMessageTypes
{
    public const string JahresberichtAnnualSummary = "JahresberichtAnnualSummary";
}

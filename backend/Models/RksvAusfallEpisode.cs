using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KasseAPI_Final.Models;

/// <summary>FON Ausfall / Wiederinbetriebnahme episode (SE or Kasse). Not a fiscal Beleg.</summary>
[Table("rksv_ausfall_episodes")]
public sealed class RksvAusfallEpisode
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [Column("tenant_id")]
    public Guid TenantId { get; set; }

    /// <summary>Optional TSE device (SE episodes).</summary>
    [Column("device_id")]
    public Guid? DeviceId { get; set; }

    /// <summary><see cref="RksvAusfallEpisodeTypes"/> — SCU or Kasse.</summary>
    [Required]
    [MaxLength(16)]
    [Column("episode_type")]
    public string EpisodeType { get; set; } = RksvAusfallEpisodeTypes.Scu;

    /// <summary><see cref="RksvAusfallOperationKinds"/> — Ausfall vs Wiederinbetriebnahme.</summary>
    [Required]
    [MaxLength(32)]
    [Column("operation_kind")]
    public string OperationKind { get; set; } = RksvAusfallOperationKinds.Ausfall;

    /// <summary>BMF Begründung code (see <c>RksvAusfallBegruendungCodes</c>).</summary>
    [Required]
    [MaxLength(80)]
    [Column("begruendung")]
    public string Begruendung { get; set; } = string.Empty;

    [Column("beginn_utc")]
    public DateTimeOffset? BeginnUtc { get; set; }

    [Column("ende_utc")]
    public DateTimeOffset? EndeUtc { get; set; }

    /// <summary><see cref="RksvAusfallEpisodeStatuses"/>.</summary>
    [Required]
    [MaxLength(32)]
    [Column("status")]
    public string Status { get; set; } = RksvAusfallEpisodeStatuses.Suggested;

    [Column("outbox_message_id")]
    public Guid? OutboxMessageId { get; set; }

    [MaxLength(120)]
    [Column("external_reference")]
    public string? ExternalReference { get; set; }

    /// <summary>Certificate serial for SE; empty for Kasse-only when not applicable.</summary>
    [MaxLength(128)]
    [Column("certificate_serial")]
    public string? CertificateSerial { get; set; }

    /// <summary>Kassenidentifikationsnummer for Kasse episodes.</summary>
    [MaxLength(100)]
    [Column("kassen_id")]
    public string? KassenId { get; set; }

    [Column("cash_register_id")]
    public Guid? CashRegisterId { get; set; }

    /// <summary>Links Wiederinbetriebnahme to prior Ausfall episode when known.</summary>
    [Column("related_ausfall_episode_id")]
    public Guid? RelatedAusfallEpisodeId { get; set; }

    [MaxLength(500)]
    [Column("operator_note")]
    public string? OperatorNote { get; set; }

    [MaxLength(128)]
    [Column("created_by")]
    public string? CreatedBy { get; set; }

    [MaxLength(128)]
    [Column("approved_by")]
    public string? ApprovedBy { get; set; }

    [Column("approved_at_utc")]
    public DateTimeOffset? ApprovedAtUtc { get; set; }

    [MaxLength(80)]
    [Column("last_error_code")]
    public string? LastErrorCode { get; set; }

    [MaxLength(500)]
    [Column("last_error_message")]
    public string? LastErrorMessage { get; set; }

    [Column("created_at_utc")]
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    [Column("updated_at_utc")]
    public DateTimeOffset? UpdatedAtUtc { get; set; }
}

public static class RksvAusfallEpisodeTypes
{
    public const string Scu = "SCU";
    public const string Kasse = "Kasse";
}

public static class RksvAusfallOperationKinds
{
    public const string Ausfall = "Ausfall";
    public const string Wiederinbetriebnahme = "Wiederinbetriebnahme";
}

public static class RksvAusfallEpisodeStatuses
{
    public const string Suggested = "Suggested";
    public const string PendingApproval = "PendingApproval";
    public const string Submitted = "Submitted";
    public const string Verified = "Verified";
    public const string Failed = "Failed";
    public const string Closed = "Closed";
}

/// <summary>BMF-facing Begründung values for rkdb ausfall_* elements (catalog; Compliance may extend).</summary>
public static class RksvAusfallBegruendungCodes
{
    public const string HardwareDefect = "HARDWARE_DEFEKT";
    public const string SoftwareDefect = "SOFTWARE_DEFEKT";
    public const string CertificateUnavailable = "ZERTIFIKAT_NICHT_VERFUEGBAR";
    public const string PlannedMaintenance = "GEPLANTE_WARTUNG";
    public const string NetworkOutage = "NETZWERK_AUSFALL";
    public const string Other = "SONSTIGES";

    public static readonly IReadOnlyList<string> All =
    [
        HardwareDefect,
        SoftwareDefect,
        CertificateUnavailable,
        PlannedMaintenance,
        NetworkOutage,
        Other,
    ];

    public static bool IsKnown(string? code) =>
        !string.IsNullOrWhiteSpace(code) &&
        All.Any(c => string.Equals(c, code.Trim(), StringComparison.OrdinalIgnoreCase));
}

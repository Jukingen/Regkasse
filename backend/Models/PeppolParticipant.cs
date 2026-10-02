using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KasseAPI_Final.Models;

/// <summary>
/// Peppol participant id for one mandant. No API key, certificate, or other credential.
/// </summary>
[Table("peppol_participants")]
public class PeppolParticipant : ITenantEntity
{
    [Column("id")]
    public Guid Id { get; set; }

    [Column("tenant_id")]
    public Guid TenantId { get; set; }

    [Column("participant_id")]
    [MaxLength(255)]
    public string ParticipantId { get; set; } = string.Empty;

    /// <summary><c>TEST</c> or <c>LIVE</c>.</summary>
    [Column("ap_environment")]
    [MaxLength(8)]
    public string ApEnvironment { get; set; } = PeppolApEnvironments.Test;

    /// <summary>Storecove legal entity id supplied by the operator. Not derived from a VAT id.</summary>
    [Column("legal_entity_id")]
    [MaxLength(255)]
    public string? LegalEntityId { get; set; }

    /// <summary>Operator-supplied routing scheme, for example <c>iso6523-actorid-upis</c>.</summary>
    [Column("eidentifier_scheme")]
    [MaxLength(64)]
    public string? EIdentifierScheme { get; set; }

    /// <summary>Operator-supplied routing identifier value.</summary>
    [Column("eidentifier_value")]
    [MaxLength(255)]
    public string? EIdentifierValue { get; set; }

    [Column("created_at_utc")]
    public DateTime CreatedAtUtc { get; set; }

    [Column("updated_at_utc")]
    public DateTime UpdatedAtUtc { get; set; }
}

public static class PeppolApEnvironments
{
    public const string Test = "TEST";
    public const string Live = "LIVE";

    public static bool IsAllowed(string? value) =>
        string.Equals(value, Test, StringComparison.Ordinal)
        || string.Equals(value, Live, StringComparison.Ordinal);
}

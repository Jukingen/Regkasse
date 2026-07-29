using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KasseAPI_Final.Models;

/// <summary>
/// Persisted DEP export compliance score snapshot (tenant-scoped).
/// Distinct from period tracking <see cref="DepExportCompliancePeriod"/>.
/// </summary>
[Table("dep_export_compliance_scores")]
public class DepExportComplianceScoreSnapshot : ITenantEntity
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [Column("tenant_id")]
    public Guid TenantId { get; set; }

    [Column("score")]
    public int Score { get; set; }

    [Required]
    [MaxLength(1)]
    [Column("grade")]
    public string Grade { get; set; } = "F";

    [Column("calculated_at")]
    public DateTime CalculatedAt { get; set; } = DateTime.UtcNow;

    [Column("factors_json", TypeName = "jsonb")]
    public string FactorsJson { get; set; } = "[]";

    [Column("critical_issues_json", TypeName = "jsonb")]
    public string CriticalIssuesJson { get; set; } = "[]";

    [Column("warnings_json", TypeName = "jsonb")]
    public string WarningsJson { get; set; } = "[]";
}

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KasseAPI_Final.Models;

/// <summary>
/// SaaS tenant root. Wave 0–1: single seeded legacy row; no membership model yet.
/// </summary>
[Table("tenants")]
public class Tenant : BaseEntity
{
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    /// <summary>Stable external key (e.g. <c>default</c> for legacy single-tenant deployments).</summary>
    [Required]
    [MaxLength(64)]
    public string Slug { get; set; } = string.Empty;
}

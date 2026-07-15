using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KasseAPI_Final.Models;

/// <summary>Tenant-scoped persisted RKSV report PDF metadata (filesystem path + audit fields).</summary>
[Table("report_pdfs")]
public class ReportPdf : ITenantEntity
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [Column("tenant_id")]
    public Guid TenantId { get; set; }

    [Required]
    [MaxLength(32)]
    [Column("report_type")]
    public string ReportType { get; set; } = string.Empty;

    [Required]
    [Column("report_id")]
    public Guid ReportId { get; set; }

    [Required]
    [MaxLength(500)]
    [Column("pdf_path")]
    public string PdfPath { get; set; } = string.Empty;

    [Required]
    [Column("generated_at")]
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;

    [Required]
    [Column("generated_by_user_id")]
    public Guid GeneratedByUserId { get; set; }

    [Required]
    [Column("file_size_bytes")]
    public long FileSizeBytes { get; set; }

    [Required]
    [MaxLength(8)]
    [Column("language")]
    public string Language { get; set; } = "de";

    [Required]
    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }
}

using System.ComponentModel.DataAnnotations;

namespace KasseAPI_Final.DTOs;

/// <summary>RKSV Startbeleg (first zero signed receipt for a cash register).</summary>
public sealed class CreateStartbelegRequest
{
    [Required]
    public Guid CashRegisterId { get; set; }

    /// <summary>Optional client correlation (stored on payment CorrelationId when short enough).</summary>
    [MaxLength(100)]
    public string CorrelationId { get; set; } = string.Empty;

    [MaxLength(450)]
    public string Reason { get; set; } = string.Empty;
}

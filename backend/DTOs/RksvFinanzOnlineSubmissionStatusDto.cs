using System;

namespace KasseAPI_Final.DTOs;

/// <summary>
/// Admin-facing snapshot of <c>rksv_special_receipt_finanz_online_submissions</c> for Startbeleg/Jahresbeleg (no secrets).
/// </summary>
public sealed class RksvFinanzOnlineSubmissionStatusDto
{
    public string Status { get; set; } = string.Empty;

    public DateTime? LastAttemptAtUtc { get; set; }

    public string? LastErrorCode { get; set; }

    public string? LastErrorMessage { get; set; }

    public string? ExternalReference { get; set; }

    public int AttemptCount { get; set; }

    public DateTime? SubmittedAtUtc { get; set; }

    public DateTime? VerifiedAtUtc { get; set; }
}

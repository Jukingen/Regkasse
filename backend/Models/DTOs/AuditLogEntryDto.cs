namespace KasseAPI_Final.Models.DTOs
{
    /// <summary>
    /// Audit log entry for API response. Mirrors AuditLog entity plus resolved actor display info.
    /// </summary>
    public class AuditLogEntryDto
    {
        public Guid Id { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public string? CreatedBy { get; set; }
        public string? UpdatedBy { get; set; }
        public bool IsActive { get; set; }

        public string SessionId { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string UserRole { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public string EntityType { get; set; } = string.Empty;
        public Guid? EntityId { get; set; }
        public string? EntityName { get; set; }
        public string? OldValues { get; set; }
        public string? NewValues { get; set; }
        public string? RequestData { get; set; }
        public string? ResponseData { get; set; }
        public AuditLogStatus Status { get; set; }
        public DateTime Timestamp { get; set; }
        public string? Description { get; set; }
        public string? Notes { get; set; }
        public string? IpAddress { get; set; }
        public string? UserAgent { get; set; }
        public string? Endpoint { get; set; }
        public string? HttpMethod { get; set; }
        public int? HttpStatusCode { get; set; }
        public double? ProcessingTimeMs { get; set; }
        public string? ErrorDetails { get; set; }
        public string? CorrelationId { get; set; }
        public string? TransactionId { get; set; }
        public decimal? Amount { get; set; }
        public string? PaymentMethod { get; set; }
        public string? TseSignature { get; set; }

        /// <summary>Resolved display name (e.g. FirstName LastName) when available; null if not resolved.</summary>
        public string? ActorDisplayName { get; set; }

        // Enterprise audit event fields
        /// <summary>Typed action for user-lifecycle/role events; null for legacy or other events.</summary>
        public AuditEventType? ActionType { get; set; }
        /// <summary>Structured diff JSON: [{ "field", "oldValue", "newValue" }].</summary>
        public string? Changes { get; set; }
        /// <summary>Additional metadata JSON (e.g. reason, targetUserId).</summary>
        public string? Metadata { get; set; }

        /// <summary>Super Admin user id when the row was written under impersonation.</summary>
        public string? ImpersonatedBy { get; set; }

        /// <summary>Target tenant id when the row was written under impersonation.</summary>
        public Guid? ImpersonatedTenantId { get; set; }
    }
}

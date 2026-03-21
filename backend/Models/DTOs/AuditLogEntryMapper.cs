using KasseAPI_Final.Models;

namespace KasseAPI_Final.Models.DTOs
{
    /// <summary>Maps AuditLog entity to AuditLogEntryDto for API response. Resolves actor display name via optional dictionary.</summary>
    public static class AuditLogEntryMapper
    {
        public static AuditLogEntryDto ToDto(AuditLog log, string? actorDisplayName = null)
        {
            if (log == null) throw new ArgumentNullException(nameof(log));
            return new AuditLogEntryDto
            {
                Id = log.Id,
                CreatedAt = log.CreatedAt,
                UpdatedAt = log.UpdatedAt,
                CreatedBy = log.CreatedBy,
                UpdatedBy = log.UpdatedBy,
                IsActive = log.IsActive,
                SessionId = log.SessionId,
                UserId = log.UserId,
                UserRole = log.UserRole,
                Action = log.Action,
                EntityType = log.EntityType,
                EntityId = log.EntityId,
                EntityName = log.EntityName,
                OldValues = log.OldValues,
                NewValues = log.NewValues,
                RequestData = log.RequestData,
                ResponseData = log.ResponseData,
                Status = log.Status,
                Timestamp = log.Timestamp,
                Description = log.Description,
                Notes = log.Notes,
                IpAddress = log.IpAddress,
                UserAgent = log.UserAgent,
                Endpoint = log.Endpoint,
                HttpMethod = log.HttpMethod,
                HttpStatusCode = log.HttpStatusCode,
                ProcessingTimeMs = log.ProcessingTimeMs,
                ErrorDetails = log.ErrorDetails,
                CorrelationId = log.CorrelationId,
                TransactionId = log.TransactionId,
                Amount = log.Amount,
                PaymentMethod = log.PaymentMethod,
                TseSignature = log.TseSignature,
                ActorDisplayName = actorDisplayName ?? log.ActorDisplayName,
                ActionType = log.ActionType,
                Changes = log.Changes,
                Metadata = log.Metadata
            };
        }

        public static List<AuditLogEntryDto> ToDtoList(
            IEnumerable<AuditLog> logs,
            IReadOnlyDictionary<string, string>? actorDisplayNames = null)
        {
            if (logs == null) return new List<AuditLogEntryDto>();
            return logs.Select(log => ToDto(log, actorDisplayNames?.GetValueOrDefault(log.UserId))).ToList();
        }
    }
}

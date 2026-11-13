namespace KasseAPI_Final.Models.DTOs
{
    /// <summary>Maps AuditLog entity to AuditLogEntryDto for API response. Resolves actor display name via optional dictionary or User navigation.</summary>
    public static class AuditLogEntryMapper
    {
        public static AuditLogEntryDto ToDto(AuditLog log, string? actorDisplayName = null)
        {
            if (log == null)
                throw new ArgumentNullException(nameof(log));

            var userInfo = BuildUserInfo(log.User);
            var displayName = ResolveDisplayName(log, userInfo, actorDisplayName);

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
                ActorDisplayName = displayName,
                User = userInfo,
                UserName = userInfo?.UserName,
                UserEmail = userInfo?.Email,
                UserDisplayName = displayName,
                ActionType = log.ActionType,
                Changes = log.Changes,
                Metadata = log.Metadata,
                ImpersonatedBy = log.ImpersonatedBy,
                ImpersonatedTenantId = log.ImpersonatedTenantId
            };
        }

        public static List<AuditLogEntryDto> ToDtoList(
            IEnumerable<AuditLog> logs,
            IReadOnlyDictionary<string, string>? actorDisplayNames = null)
        {
            if (logs == null)
                return new List<AuditLogEntryDto>();
            return logs.Select(log => ToDto(log, actorDisplayNames?.GetValueOrDefault(log.UserId))).ToList();
        }

        internal static UserInfoDto? BuildUserInfo(ApplicationUser? user)
        {
            if (user == null)
                return null;

            var displayName = $"{user.FirstName} {user.LastName}".Trim();
            if (string.IsNullOrEmpty(displayName))
                displayName = user.UserName ?? user.Id;

            return new UserInfoDto
            {
                Id = user.Id,
                UserName = user.UserName,
                Email = user.Email,
                DisplayName = displayName,
                Role = user.Role
            };
        }

        private static string? ResolveDisplayName(
            AuditLog log,
            UserInfoDto? userInfo,
            string? actorDisplayNameOverride)
        {
            if (!string.IsNullOrWhiteSpace(actorDisplayNameOverride))
                return actorDisplayNameOverride.Trim();
            if (!string.IsNullOrWhiteSpace(userInfo?.DisplayName))
                return userInfo.DisplayName;
            if (!string.IsNullOrWhiteSpace(log.ActorDisplayName))
                return log.ActorDisplayName.Trim();
            return null;
        }
    }
}

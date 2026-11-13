namespace KasseAPI_Final.Services.Support;

public interface ISupportTicketService
{
    Task<SupportTicketDetailDto> CreateAsync(
        Guid tenantId,
        string userId,
        string? displayName,
        CreateSupportTicketRequest request,
        CancellationToken cancellationToken = default);

    Task<SupportTicketListResponse> ListForTenantAsync(
        Guid tenantId,
        SupportTicketListQuery? query = null,
        CancellationToken cancellationToken = default);

    Task<SupportTicketListResponse> ListAllAsync(
        SupportTicketListQuery? query = null,
        CancellationToken cancellationToken = default);

    Task<SupportTicketInboxSummaryDto> GetInboxSummaryAsync(
        CancellationToken cancellationToken = default);

    Task<SupportTicketDetailDto> GetForTenantAsync(
        Guid tenantId,
        Guid ticketId,
        CancellationToken cancellationToken = default);

    Task<SupportTicketDetailDto> GetAnyAsync(
        Guid ticketId,
        CancellationToken cancellationToken = default);

    Task<SupportTicketDetailDto> AddMessageForTenantAsync(
        Guid tenantId,
        Guid ticketId,
        string userId,
        string? displayName,
        string body,
        CancellationToken cancellationToken = default);

    Task<SupportTicketDetailDto> AddStaffMessageAsync(
        Guid ticketId,
        string userId,
        string? displayName,
        string body,
        bool isInternal,
        CancellationToken cancellationToken = default);

    Task<SupportTicketDetailDto> UpdateStatusAsync(
        Guid ticketId,
        string status,
        CancellationToken cancellationToken = default);

    Task<SupportTicketDetailDto> UpdateStatusForTenantAsync(
        Guid tenantId,
        Guid ticketId,
        string status,
        CancellationToken cancellationToken = default);

    Task<SupportTicketDetailDto> AssignTicketAsync(
        Guid ticketId,
        string assignedToUserId,
        string? assignedToDisplayName,
        CancellationToken cancellationToken = default);

    Task<int> GetOpenTicketCountAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default);
}

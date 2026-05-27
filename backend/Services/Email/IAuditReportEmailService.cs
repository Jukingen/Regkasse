namespace KasseAPI_Final.Services.Email;

public interface IAuditReportEmailService
{
    bool IsConfigured { get; }

    Task SendReportAsync(
        IReadOnlyList<string> recipients,
        string subject,
        string plainBody,
        string attachmentFileName,
        byte[] attachmentContent,
        string contentType,
        CancellationToken cancellationToken = default);
}

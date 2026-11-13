namespace KasseAPI_Final.Models;

/// <summary>Admin FA test-console actions for <c>POST /api/admin/online-payments/test</c>.</summary>
public static class OnlinePaymentTestActions
{
    public const string Create = "create";
    public const string WebhookSucceeded = "webhookSucceeded";
    public const string WebhookFailed = "webhookFailed";
    public const string Success = "success";
    public const string Failed = "failed";
    public const string Expire = "expire";
}

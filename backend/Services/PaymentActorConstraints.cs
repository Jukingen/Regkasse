namespace KasseAPI_Final.Services;

public static class PaymentActorConstraints
{
    public static void EnsurePrincipalDerivedActor(string? userId, string paramName = "userId")
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("Authenticated actor user id is required.", paramName);
    }
}

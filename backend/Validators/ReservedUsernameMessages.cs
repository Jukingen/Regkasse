namespace KasseAPI_Final.Validators;

/// <summary>Operator-facing errors for reserved login names.</summary>
public static class ReservedUsernameMessages
{
    public const string Title = "Username is reserved";

    public static string Detail(string userName) =>
        $"Username '{userName}' is reserved and cannot be used. Please choose another.";
}

namespace KasseAPI_Final.Validators;

/// <summary>Shared operator-facing username uniqueness errors (case-insensitive).</summary>
public static class UsernameConflictMessages
{
    public const string Title = "Username already exists";

    public static string Detail(string userName) =>
        $"Username '{userName}' is already taken (case-insensitive). Please choose another.";

    public static string FieldError(string userName) => Detail(userName);
}

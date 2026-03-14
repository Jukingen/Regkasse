namespace KasseAPI_Final.Models;

public class AuthOptions
{
    public const string SectionName = "Auth";

    /// <summary>
    /// When true, login requests without a clientApp field are accepted in legacy mode
    /// (no app_context claim in token). When false, clientApp is required and requests
    /// without it receive 400.
    /// </summary>
    public bool AllowLegacyLoginWithoutClientApp { get; set; } = false;
}

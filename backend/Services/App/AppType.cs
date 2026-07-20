namespace KasseAPI_Final.Services.App;

/// <summary>One-click mobile app packaging mode.</summary>
public enum AppType
{
    /// <summary>Installable progressive web app (static files under public site root).</summary>
    Pwa = 0,

    /// <summary>Expo / React Native source package (ZIP download; not compiled on the API host).</summary>
    Native = 1
}

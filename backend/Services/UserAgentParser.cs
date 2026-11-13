using System.Text.RegularExpressions;

namespace KasseAPI_Final.Services;

/// <summary>Lightweight User-Agent → browser / OS labels for session device management.</summary>
public static partial class UserAgentParser
{
    public readonly record struct ParsedUserAgent(string? Browser, string? OS, string? DeviceName);

    public static ParsedUserAgent Parse(string? userAgent)
    {
        if (string.IsNullOrWhiteSpace(userAgent))
            return default;

        var browser = DetectBrowser(userAgent);
        var os = DetectOs(userAgent);
        string? deviceName = null;
        if (!string.IsNullOrEmpty(browser) || !string.IsNullOrEmpty(os))
        {
            deviceName = string.Join(" · ", new[] { browser, os }.Where(static s => !string.IsNullOrEmpty(s)));
        }

        return new ParsedUserAgent(browser, os, deviceName);
    }

    private static string? DetectBrowser(string ua)
    {
        if (EdgeRegex().IsMatch(ua))
            return "Microsoft Edge";
        if (EdgARegex().IsMatch(ua) || EdgiOSRegex().IsMatch(ua))
            return "Microsoft Edge";
        if (OperaRegex().IsMatch(ua))
            return "Opera";
        if (ChromeRegex().IsMatch(ua) && !ChromiumEdgeFamily(ua))
            return "Chrome";
        if (FirefoxRegex().IsMatch(ua))
            return "Firefox";
        if (SafariRegex().IsMatch(ua) && !ChromeRegex().IsMatch(ua) && !ChromiumEdgeFamily(ua))
            return "Safari";
        if (ua.Contains("Regkasse", StringComparison.OrdinalIgnoreCase)
            || ua.Contains("Expo", StringComparison.OrdinalIgnoreCase)
            || ua.Contains("okhttp", StringComparison.OrdinalIgnoreCase))
            return "Mobile App";
        return null;
    }

    private static bool ChromiumEdgeFamily(string ua) =>
        EdgeRegex().IsMatch(ua) || EdgARegex().IsMatch(ua) || EdgiOSRegex().IsMatch(ua);

    private static string? DetectOs(string ua)
    {
        if (WindowsRegex().IsMatch(ua))
            return "Windows";
        if (AndroidRegex().IsMatch(ua))
            return "Android";
        if (iPhoneRegex().IsMatch(ua) || iPadRegex().IsMatch(ua))
            return "iOS";
        if (MacOsRegex().IsMatch(ua))
            return "macOS";
        if (LinuxRegex().IsMatch(ua))
            return "Linux";
        return null;
    }

    [GeneratedRegex(@"Edg/\d", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex EdgeRegex();

    [GeneratedRegex(@"EdgA/\d", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex EdgARegex();

    [GeneratedRegex(@"EdgiOS/\d", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex EdgiOSRegex();

    [GeneratedRegex(@"OPR/\d|Opera", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex OperaRegex();

    [GeneratedRegex(@"Chrome/\d|CriOS/\d", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ChromeRegex();

    [GeneratedRegex(@"Firefox/\d|FxiOS/\d", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex FirefoxRegex();

    [GeneratedRegex(@"Safari/\d", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SafariRegex();

    [GeneratedRegex(@"Windows NT", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex WindowsRegex();

    [GeneratedRegex(@"Android", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex AndroidRegex();

    [GeneratedRegex(@"iPhone", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex iPhoneRegex();

    [GeneratedRegex(@"iPad", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex iPadRegex();

    [GeneratedRegex(@"Mac OS X|Macintosh", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex MacOsRegex();

    [GeneratedRegex(@"Linux", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex LinuxRegex();
}

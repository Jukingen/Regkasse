using System.Text.RegularExpressions;

namespace KasseAPI_Final.Security;

/// <summary>
/// Connection string maskeleme: log ve konsol çıktılarında parola ve URI parolası sızmasını önler.
/// </summary>
public static class ConnectionStringMasking
{
    /// <summary>
    /// Parola içeren alanları (Password=, Pwd=) ve postgres URI kullanıcı:parola kısmını maskeler.
    /// </summary>
    public static string Mask(string? connectionString)
    {
        if (string.IsNullOrEmpty(connectionString))
            return connectionString ?? string.Empty;

        var s = connectionString;
        s = Regex.Replace(s, @"(?i)(Password|Pwd)\s*=\s*[^;]*", "$1=***");
        s = Regex.Replace(s, @"(?i)(postgres(?:ql)?://)([^:/]+):([^@]+)(@)", m => $"{m.Groups[1].Value}{m.Groups[2].Value}:***{m.Groups[4].Value}");
        return s;
    }
}

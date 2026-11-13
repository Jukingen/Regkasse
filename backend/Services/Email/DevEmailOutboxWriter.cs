using System.Globalization;
using System.Text;
using KasseAPI_Final.Configuration;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.Email;

public sealed class DevEmailOutboxWriter
{
    private readonly EmailDevCaptureOptions _options;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<DevEmailOutboxWriter> _logger;

    public DevEmailOutboxWriter(
        IOptions<EmailDevCaptureOptions> options,
        IWebHostEnvironment environment,
        ILogger<DevEmailOutboxWriter> logger)
    {
        _options = options.Value;
        _environment = environment;
        _logger = logger;
    }

    public bool IsEnabled => _environment.IsDevelopment() && _options.Enabled;

    public string ResolveDirectory()
    {
        var relative = string.IsNullOrWhiteSpace(_options.Directory)
            ? "App_Data/dev-mail"
            : _options.Directory.Trim();

        return Path.GetFullPath(Path.Combine(_environment.ContentRootPath, relative));
    }

    public async Task<string?> TryWriteAsync(
        string toEmail,
        string subject,
        string body,
        CancellationToken cancellationToken = default)
    {
        if (!IsEnabled)
            return null;

        var to = toEmail.Trim();
        if (string.IsNullOrEmpty(to))
            return null;

        var directory = ResolveDirectory();
        Directory.CreateDirectory(directory);

        var stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture);
        var safeSubject = SanitizeFileToken(subject);
        var fileName = $"{stamp}_{safeSubject}.txt";
        var path = Path.Combine(directory, fileName);

        var content = new StringBuilder()
            .AppendLine($"To: {to}")
            .AppendLine($"Subject: {subject}")
            .AppendLine($"CapturedUtc: {DateTime.UtcNow:O}")
            .AppendLine()
            .AppendLine(body)
            .ToString();

        await File.WriteAllTextAsync(path, content, Encoding.UTF8, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Dev email captured for {EmailMasked}. File: {FilePath}",
            MaskEmail(to),
            path);

        return path;
    }

    private static string SanitizeFileToken(string value)
    {
        var trimmed = value.Trim();
        if (trimmed.Length == 0)
            return "email";

        var invalid = Path.GetInvalidFileNameChars();
        var builder = new StringBuilder(trimmed.Length);
        foreach (var ch in trimmed)
            builder.Append(invalid.Contains(ch) ? '_' : ch);

        var sanitized = builder.ToString().Trim('_');
        if (sanitized.Length == 0)
            return "email";

        return sanitized.Length <= 60 ? sanitized : sanitized[..60];
    }

    private static string MaskEmail(string email)
    {
        var at = email.IndexOf('@');
        return at <= 1 ? "***" : $"{email[..2]}***{email[at..]}";
    }
}

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Regkasse.LicenseTools;

var builder = WebApplication.CreateBuilder(args);

// Internal tool: localhost only by default (override with ASPNETCORE_URLS / launchSettings).
if (builder.Configuration["ASPNETCORE_URLS"] is null or "")
    builder.WebHost.UseUrls("http://127.0.0.1:5055");

builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.PropertyNameCaseInsensitive = true);

var app = builder.Build();

var privateKeyPath = builder.Configuration["LicenseGenerator:PrivateKeyPath"]
                     ?? Environment.GetEnvironmentVariable("LICENSE_GENERATOR_PRIVATE_KEY_PATH");

app.MapGet("/", () => Results.Content(LicenseGeneratorHtml.Page, "text/html; charset=utf-8"));

app.MapPost("/api/generate", (GenerateRequest body) =>
{
    if (body is null || string.IsNullOrWhiteSpace(body.Customer))
        return Results.BadRequest(new { message = "customer is required." });

    if (string.IsNullOrWhiteSpace(privateKeyPath) || !File.Exists(privateKeyPath))
    {
        return Results.Json(
            new
            {
                message =
                    "Private key path not configured. Set LicenseGenerator:PrivateKeyPath or LICENSE_GENERATOR_PRIVATE_KEY_PATH.",
            },
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }

    try
    {
        var pem = File.ReadAllText(privateKeyPath, Encoding.UTF8);
        using var rsa = RSA.Create();
        rsa.ImportFromPem(pem);

        DateTimeOffset expiry;
        if (string.IsNullOrWhiteSpace(body.Expiry))
        {
            var d = DateTime.UtcNow.Date.AddYears(1);
            expiry = new DateTimeOffset(d.Year, d.Month, d.Day, 23, 59, 59, TimeSpan.Zero);
        }
        else if (DateOnly.TryParse(body.Expiry, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            expiry = new DateTimeOffset(date.Year, date.Month, date.Day, 23, 59, 59, TimeSpan.Zero);
        }
        else
            return Results.BadRequest(new { message = "expiry must be yyyy-MM-dd or empty." });

        var result = LicenseIssuer.Issue(body.Customer, body.MachineHash, expiry, rsa);
        return Results.Ok(new GenerateResponse(
            result.LicenseKey,
            result.SignedPayload,
            result.CanonicalPayload,
            result.PublicKeyPem,
            result.ExpiresAtUtc));
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { message = ex.Message });
    }
});

app.Run();

internal sealed record GenerateRequest(
    string Customer,
    string? MachineHash,
    string? Expiry);

internal sealed record GenerateResponse(
    string LicenseKey,
    string OfflineActivationJwt,
    string CanonicalSignedPayload,
    string PublicKeyPem,
    DateTimeOffset ExpiresAtUtc);

internal static class LicenseGeneratorHtml
{
    public const string Page = """
<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="utf-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1" />
  <title>Regkasse License Generator (internal)</title>
  <style>
    body { font-family: system-ui, sans-serif; max-width: 52rem; margin: 2rem auto; padding: 0 1rem; }
    label { display: block; font-weight: 600; margin-top: 1rem; }
    input, textarea { width: 100%; box-sizing: border-box; padding: 0.5rem; margin-top: 0.25rem; }
    textarea { min-height: 8rem; font-family: ui-monospace, monospace; font-size: 0.85rem; }
    button { margin-top: 1rem; padding: 0.5rem 1rem; cursor: pointer; }
    .warn { background: #fff3cd; border: 1px solid #ffc107; padding: 0.75rem; border-radius: 4px; margin-bottom: 1rem; }
    .ok { background: #d4edda; border: 1px solid #28a745; padding: 0.75rem; border-radius: 4px; white-space: pre-wrap; word-break: break-all; }
  </style>
</head>
<body>
  <h1>Regkasse license generator</h1>
  <p class="warn">Internal use only. Run on a trusted machine. Never expose the private key path to the network.</p>
  <form id="f">
    <label for="customer">Customer</label>
    <input id="customer" name="customer" required placeholder="Firma GmbH" />
    <label for="machineHash">Machine hash (optional, from GET /api/admin/license/status)</label>
    <input id="machineHash" name="machineHash" placeholder="64-char hex; leave empty for floating" />
    <label for="expiry">Expiry (yyyy-MM-dd, optional — default 1 year)</label>
    <input id="expiry" name="expiry" placeholder="2026-12-31" />
    <button type="submit">Generate</button>
  </form>
  <div id="out"></div>
  <script>
    document.getElementById('f').onsubmit = async (e) => {
      e.preventDefault();
      const out = document.getElementById('out');
      out.innerHTML = 'Generating…';
      const body = {
        customer: document.getElementById('customer').value,
        machineHash: document.getElementById('machineHash').value || null,
        expiry: document.getElementById('expiry').value || null
      };
      const r = await fetch('/api/generate', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(body)
      });
      const j = await r.json();
      if (!r.ok) {
        out.innerHTML = '<p class="warn">' + (j.message || JSON.stringify(j)) + '</p>';
        return;
      }
      out.innerHTML = '<div class="ok"><strong>LicenseKey</strong><br>' + j.licenseKey +
        '<br><br><strong>OfflineActivationJwt</strong><br>' + j.offlineActivationJwt +
        '<br><br><strong>PublicKeyPem</strong> (embed in backend License:OfflineVerificationPublicKeyPem)<br>' +
        j.publicKeyPem.replace(/\\n/g, '\n') +
        '<br><br><strong>CanonicalSignedPayload</strong><br>' + j.canonicalSignedPayload +
        '<br><br><strong>ExpiresAtUtc</strong> ' + j.expiresAtUtc + '</div>';
    };
  </script>
</body>
</html>
""";
}

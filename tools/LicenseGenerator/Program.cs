using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Regkasse.LicenseTools;

namespace Regkasse.LicenseTools.Cli;

internal static class Program
{
    private static int Main(string[] args)
    {
        try
        {
            if (args.Length > 0 && string.Equals(args[0], "init-keys", StringComparison.OrdinalIgnoreCase))
                return RunInitKeys(args.AsSpan(1));

            return RunIssue(args);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("Error: " + ex.Message);
            return 1;
        }
    }

    private static int RunInitKeys(ReadOnlySpan<string> args)
    {
        var dir = Environment.CurrentDirectory;
        for (var i = 0; i < args.Length; i++)
        {
            if (string.Equals(args[i], "--output-dir", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                dir = args[++i];
        }

        Directory.CreateDirectory(dir);
        var priv = Path.Combine(dir, "license_private.pem");
        var pub = Path.Combine(dir, "license_public.pem");
        LicenseIssuer.GenerateKeyPair(priv, pub);
        Console.WriteLine($"Wrote private key: {priv}");
        Console.WriteLine($"Wrote public key:  {pub}");
        Console.WriteLine("Keep the private key offline; embed only the public key in backend appsettings (License:OfflineVerificationPublicKeyPem).");
        return 0;
    }

    private static int RunIssue(string[] args)
    {
        var opts = ParseIssueArgs(args);
        if (string.IsNullOrWhiteSpace(opts.Customer))
        {
            PrintUsage();
            Console.Error.WriteLine("Missing required --customer.");
            return 2;
        }

        if (!File.Exists(opts.PrivateKeyPath))
        {
            Console.Error.WriteLine($"Private key not found: {opts.PrivateKeyPath}");
            Console.Error.WriteLine("Generate keys: dotnet run -- init-keys --output-dir .");
            return 3;
        }

        var pem = File.ReadAllText(opts.PrivateKeyPath, Encoding.UTF8);
        using var rsa = RSA.Create();
        rsa.ImportFromPem(pem);

        var expiry = ResolveExpiryUtc(opts.ExpiryRaw);
        var result = LicenseIssuer.Issue(opts.Customer, opts.MachineHash, expiry, rsa);

        var sb = new StringBuilder(512);
        sb.AppendLine("# Regkasse license bundle (internal issuance)");
        sb.AppendLine($"Customer={result.CanonicalPayload.Split('|')[0]}");
        sb.AppendLine($"MachineHash={(string.IsNullOrEmpty(opts.MachineHash) ? "(floating)" : opts.MachineHash)}");
        sb.AppendLine($"ExpiresAtUtc={result.ExpiresAtUtc:O}");
        sb.AppendLine($"CanonicalSignedPayload={result.CanonicalPayload}");
        sb.AppendLine($"LicenseKey={result.LicenseKey}");
        sb.AppendLine($"OfflineActivationJwt={result.SignedPayload}");
        sb.AppendLine();
        sb.AppendLine("# --- Backend: set License:OfflineVerificationPublicKeyPem to the following (PEM) ---");
        sb.AppendLine(result.PublicKeyPem.TrimEnd());

        File.WriteAllText(opts.OutputPath, sb.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        Console.WriteLine($"Wrote {opts.OutputPath}");
        Console.WriteLine($"LicenseKey: {result.LicenseKey}");
        Console.WriteLine();
        Console.WriteLine("--- Public key (embed in backend appsettings.json, License:OfflineVerificationPublicKeyPem) ---");
        Console.WriteLine(result.PublicKeyPem);
        return 0;
    }

    private static DateTimeOffset ResolveExpiryUtc(string? expiryRaw)
    {
        if (string.IsNullOrWhiteSpace(expiryRaw))
        {
            var d = DateTime.UtcNow.Date.AddYears(1);
            return new DateTimeOffset(d.Year, d.Month, d.Day, 23, 59, 59, TimeSpan.Zero);
        }

        if (!DateOnly.TryParse(expiryRaw, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            throw new FormatException($"Invalid --expiry '{expiryRaw}'. Use yyyy-MM-dd.");

        return new DateTimeOffset(date.Year, date.Month, date.Day, 23, 59, 59, TimeSpan.Zero);
    }

    private sealed class IssueOptions
    {
        public string Customer { get; set; } = "";
        public string? MachineHash { get; set; }
        public string? ExpiryRaw { get; set; }
        public string PrivateKeyPath { get; set; } = Path.Combine(Environment.CurrentDirectory, "license_private.pem");
        public string OutputPath { get; set; } = Path.Combine(Environment.CurrentDirectory, "license.txt");
    }

    private static IssueOptions ParseIssueArgs(string[] args)
    {
        var o = new IssueOptions();
        for (var i = 0; i < args.Length; i++)
        {
            var a = args[i];
            if (string.Equals(a, "--customer", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                o.Customer = args[++i];
                continue;
            }

            if (string.Equals(a, "--machine-hash", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                o.MachineHash = args[++i];
                continue;
            }

            if (string.Equals(a, "--expiry", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                o.ExpiryRaw = args[++i];
                continue;
            }

            if (string.Equals(a, "--private-key", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                o.PrivateKeyPath = args[++i];
                continue;
            }

            if (string.Equals(a, "--output", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                o.OutputPath = args[++i];
                continue;
            }

            if (string.Equals(a, "--help", StringComparison.OrdinalIgnoreCase) || string.Equals(a, "-h", StringComparison.OrdinalIgnoreCase))
            {
                PrintUsage();
                Environment.Exit(0);
            }
        }

        return o;
    }

    private static void PrintUsage()
    {
        Console.WriteLine("""
            Regkasse license generator (internal)

            Usage:
              LicenseGenerator init-keys [--output-dir <path>]
              LicenseGenerator --customer "Firma GmbH" [--machine-hash <sha256hex>] [--expiry yyyy-MM-dd] [--private-key path] [--output path]

            Defaults:
              --expiry: one year from today (end of UTC day)
              --private-key: ./license_private.pem
              --output: ./license.txt

            Floating license: omit --machine-hash (JWT machineHash empty; any machine).

            After init-keys, paste license_public.pem into backend appsettings:
              "License": { "OfflineVerificationPublicKeyPem": "...PEM..." }
            """);
    }
}

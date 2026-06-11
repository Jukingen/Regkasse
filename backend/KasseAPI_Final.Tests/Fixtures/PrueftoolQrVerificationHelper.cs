using System.Diagnostics;
using System.Text.Json;

namespace KasseAPI_Final.Tests.Fixtures;

/// <summary>
/// Runs BMF <c>regkassen-verification-receipts-1.1.1.jar</c> when JDK 17+ and JARs are present under <c>backend/Tests/</c>.
/// </summary>
internal static class PrueftoolQrVerificationHelper
{
    public static string TestsDirectory =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Tests"));

    public static string ReceiptsJarPath =>
        Path.Combine(TestsDirectory, "regkassen-verification-receipts-1.1.1.jar");

    public static string LibDirectory => Path.Combine(TestsDirectory, "lib");

    public static bool IsReceiptVerificationAvailable(out string? skipReason)
    {
        skipReason = null;

        if (!File.Exists(ReceiptsJarPath))
        {
            skipReason = $"BMF receipts JAR not found: {ReceiptsJarPath}";
            return false;
        }

        if (!Directory.Exists(LibDirectory) || !Directory.EnumerateFiles(LibDirectory, "*.jar").Any())
        {
            skipReason = $"Prüftool lib directory missing or empty: {LibDirectory}";
            return false;
        }

        if (ResolveJavaExecutable() is null)
        {
            skipReason = "JDK 17+ not found (set PRUEFTOOL_JAVA or install Microsoft.OpenJDK.17).";
            return false;
        }

        return true;
    }

    public static PrueftoolQrVerificationResult RunCheckSingleReceipt(
        string qrCodeRepPath,
        string cryptoMaterialPath,
        string outputDirectory)
    {
        var javaExe = ResolveJavaExecutable()
            ?? throw new InvalidOperationException("Java executable not found.");

        Directory.CreateDirectory(outputDirectory);

        var classpathParts = new List<string> { ReceiptsJarPath };
        classpathParts.AddRange(Directory.EnumerateFiles(LibDirectory, "*.jar").Select(Path.GetFullPath));
        var classpath = string.Join(
            OperatingSystem.IsWindows() ? ';' : ':',
            classpathParts);

        var mainClass = "at.asitplus.regkassen.verification.cmdline.CheckSingleReceipt";

        var args = new List<string>
        {
            "-cp", classpath,
            mainClass,
            "-v", "-f",
            "-i", Path.GetFullPath(qrCodeRepPath),
            "-c", Path.GetFullPath(cryptoMaterialPath),
            "-o", Path.GetFullPath(outputDirectory),
        };

        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = javaExe,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            },
        };

        foreach (var arg in args)
            process.StartInfo.ArgumentList.Add(arg);

        process.Start();
        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();

        var verificationState = TryReadVerificationState(outputDirectory);
        return new PrueftoolQrVerificationResult(
            process.ExitCode,
            verificationState,
            stdout,
            stderr);
    }

    private static string? TryReadVerificationState(string outputDirectory)
    {
        if (!Directory.Exists(outputDirectory))
            return null;

        foreach (var summaryPath in Directory.EnumerateFiles(outputDirectory, "*global*.json", SearchOption.AllDirectories))
        {
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(summaryPath));
                if (doc.RootElement.TryGetProperty("verificationState", out var state))
                    return state.GetString();
            }
            catch
            {
                // try next candidate
            }
        }

        foreach (var jsonPath in Directory.EnumerateFiles(outputDirectory, "*.json", SearchOption.AllDirectories))
        {
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(jsonPath));
                if (doc.RootElement.TryGetProperty("verificationState", out var state))
                    return state.GetString();
            }
            catch
            {
                // try next candidate
            }
        }

        return null;
    }

    private static string? ResolveJavaExecutable()
    {
        var candidates = new List<string?>();
        var envJava = Environment.GetEnvironmentVariable("PRUEFTOOL_JAVA");
        if (!string.IsNullOrWhiteSpace(envJava))
            candidates.Add(envJava);

        var javaHome = Environment.GetEnvironmentVariable("JAVA_HOME");
        if (!string.IsNullOrWhiteSpace(javaHome))
            candidates.Add(Path.Combine(javaHome, "bin", OperatingSystem.IsWindows() ? "java.exe" : "java"));

        foreach (var candidate in candidates)
        {
            if (!string.IsNullOrWhiteSpace(candidate) && File.Exists(candidate) && GetJavaMajorVersion(candidate) >= 17)
                return candidate;
        }

        return null;
    }

    private static int GetJavaMajorVersion(string javaExecutable)
    {
        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = javaExecutable,
            Arguments = "-version",
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        });

        if (process == null)
            return 0;

        var text = process.StandardError.ReadToEnd() + process.StandardOutput.ReadToEnd();
        process.WaitForExit();

        var match = System.Text.RegularExpressions.Regex.Match(text, @"version ""(\d+)");
        return match.Success && int.TryParse(match.Groups[1].Value, out var major) ? major : 0;
    }

    internal sealed record PrueftoolQrVerificationResult(
        int ExitCode,
        string? VerificationState,
        string StdOut,
        string StdErr);
}

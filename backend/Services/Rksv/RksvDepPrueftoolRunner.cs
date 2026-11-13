using System.Diagnostics;
using System.Text.Json;

namespace KasseAPI_Final.Services.Rksv;

/// <summary>
/// Production wrapper for BMF DEP format verification JAR (same toolchain as <c>scripts/verify-rksv-dep-export.ps1</c>).
/// </summary>
public sealed class RksvDepPrueftoolRunner : IRksvDepPrueftoolRunner
{
    private const string MainClass = "at.asitplus.regkassen.verification.cmdline.CheckDEPExportFormat";
    private const string DepJarFileName = "regkassen-verification-depformat-1.1.1.jar";

    private readonly IHostEnvironment _hostEnvironment;
    private readonly ILogger<RksvDepPrueftoolRunner> _logger;

    public RksvDepPrueftoolRunner(IHostEnvironment hostEnvironment, ILogger<RksvDepPrueftoolRunner> logger)
    {
        _hostEnvironment = hostEnvironment;
        _logger = logger;
    }

    public bool IsAvailable(out string? unavailableReason)
    {
        unavailableReason = null;

        if (!File.Exists(DepJarPath))
        {
            unavailableReason = $"BMF DEP JAR not found: {DepJarPath}";
            return false;
        }

        if (!Directory.Exists(LibDirectory) || !Directory.EnumerateFiles(LibDirectory, "*.jar").Any())
        {
            unavailableReason = $"Prüftool lib directory missing or empty: {LibDirectory}";
            return false;
        }

        if (ResolveJavaExecutable() is null)
        {
            unavailableReason = "JDK 17+ not found (set PRUEFTOOL_JAVA or install Microsoft.OpenJDK.17).";
            return false;
        }

        return true;
    }

    public RksvDepPrueftoolRunResult RunCheckDepExport(
        string depExportPath,
        string cryptoMaterialPath,
        string outputDirectory)
    {
        var javaExe = ResolveJavaExecutable()
            ?? throw new InvalidOperationException("Java executable not found.");

        Directory.CreateDirectory(outputDirectory);

        var classpathParts = new List<string> { DepJarPath };
        classpathParts.AddRange(Directory.EnumerateFiles(LibDirectory, "*.jar").Select(Path.GetFullPath));
        var classpath = string.Join(
            OperatingSystem.IsWindows() ? ';' : ':',
            classpathParts);

        var args = new List<string>
        {
            "-cp", classpath,
            MainClass,
            "-v", "-f",
            "-i", Path.GetFullPath(depExportPath),
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

        _logger.LogInformation(
            "Running BMF DEP Prüftool: dep={DepPath} crypto={CryptoPath}",
            depExportPath,
            cryptoMaterialPath);

        process.Start();
        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();

        var verificationState = TryReadVerificationState(outputDirectory);
        return new RksvDepPrueftoolRunResult(process.ExitCode, verificationState, stdout, stderr);
    }

    internal string TestsDirectory
    {
        get
        {
            var fromContentRoot = Path.Combine(_hostEnvironment.ContentRootPath, "Tests");
            if (Directory.Exists(fromContentRoot))
                return Path.GetFullPath(fromContentRoot);

            var fromBase = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Tests"));
            return fromBase;
        }
    }

    internal string DepJarPath => Path.Combine(TestsDirectory, DepJarFileName);

    internal string LibDirectory => Path.Combine(TestsDirectory, "lib");

    private static string? TryReadVerificationState(string outputDirectory)
    {
        var summaryPath = Path.Combine(outputDirectory, "DEP-global.json");
        if (!File.Exists(summaryPath))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(summaryPath));
            if (doc.RootElement.TryGetProperty("verificationState", out var state))
                return state.GetString();
        }
        catch
        {
            // ignore parse errors
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
}

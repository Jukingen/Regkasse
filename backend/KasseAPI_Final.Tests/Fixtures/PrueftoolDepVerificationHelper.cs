using KasseAPI_Final.Models.Export;
using KasseAPI_Final.Services.Rksv;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace KasseAPI_Final.Tests.Fixtures;

/// <summary>
/// Runs BMF <c>regkassen-verification-depformat-1.1.1.jar</c> when JDK 17+ and JARs are present under <c>backend/Tests/</c>.
/// </summary>
internal static class PrueftoolDepVerificationHelper
{
    private static readonly Lazy<IRksvDepPrueftoolRunner> Runner = new(() =>
        new RksvDepPrueftoolRunner(
            new TestHostEnvironment(),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<RksvDepPrueftoolRunner>.Instance));

    public static string TestsDirectory => ((RksvDepPrueftoolRunner)Runner.Value).TestsDirectory;

    public static string DepJarPath => ((RksvDepPrueftoolRunner)Runner.Value).DepJarPath;

    public static string LibDirectory => ((RksvDepPrueftoolRunner)Runner.Value).LibDirectory;

    public static bool IsDepVerificationAvailable(out string? skipReason) =>
        Runner.Value.IsAvailable(out skipReason);

    public static PrueftoolDepVerificationResult RunCheckDepExport(
        string depExportPath,
        string cryptoMaterialPath,
        string outputDirectory)
    {
        var result = Runner.Value.RunCheckDepExport(depExportPath, cryptoMaterialPath, outputDirectory);
        return new PrueftoolDepVerificationResult(
            result.ExitCode,
            result.VerificationState,
            result.StdOut,
            result.StdErr);
    }

    public static string WritePrueftoolCryptoMaterial(CryptoMaterialDto material, string outputPath) =>
        RksvDepPrueftoolCryptoMaterialWriter.Write(material, outputPath);

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "KasseAPI_Final.Tests";
        public string ContentRootPath { get; set; } =
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    internal sealed record PrueftoolDepVerificationResult(
        int ExitCode,
        string? VerificationState,
        string StdOut,
        string StdErr);
}

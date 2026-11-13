using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Tse;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class TseTrainingServiceTests
{
    [Fact]
    public async Task GetModulesAsync_ReturnsCatalogWithProgress()
    {
        await using var db = CreateDb();
        var console = new TseTrainingConsoleStore();
        var sim = new Mock<ITseSimulatorService>(MockBehavior.Strict);
        var svc = new TseTrainingService(
            db,
            sim.Object,
            console,
            new FakeEnv(Environments.Development),
            NullLogger<TseTrainingService>.Instance);

        var modules = await svc.GetModulesAsync("user-1");
        Assert.True(modules.Count >= 3);
        Assert.All(modules, m => Assert.False(m.IsCompleted));

        var started = await svc.StartModuleAsync("user-1", modules[0].Id);
        Assert.True(started.IsCompleted);

        modules = await svc.GetModulesAsync("user-1");
        Assert.True(modules.First(m => m.Id == started.Id).IsCompleted);
        Assert.Equal(1, modules.Count(m => m.IsCompleted));
    }

    [Fact]
    public async Task SimulateFailureAsync_WritesConsole_InDevelopment()
    {
        await using var db = CreateDb();
        var console = new TseTrainingConsoleStore();
        var deviceId = Guid.NewGuid();
        var sim = new Mock<ITseSimulatorService>(MockBehavior.Strict);
        sim.Setup(s => s.SimulateTseFailureAsync(
                deviceId,
                TseSimulatorFailureType.NetworkTimeout,
                "user-1",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TseSimulationResultDto
            {
                Success = true,
                DeviceId = deviceId,
                Message = "Simulated NetworkTimeout",
                ScenarioId = "failure.NetworkTimeout",
            });

        var svc = new TseTrainingService(
            db,
            sim.Object,
            console,
            new FakeEnv(Environments.Development),
            NullLogger<TseTrainingService>.Instance);

        var result = await svc.SimulateFailureAsync("user-1", deviceId, "NetworkTimeout");
        Assert.True(result.Success);
        Assert.NotNull(result.ConsoleEntry);

        var entries = await svc.GetConsoleAsync("user-1");
        Assert.Single(entries);
        Assert.Equal("NetworkTimeout", entries[0].Scenario);
    }

    [Fact]
    public async Task SimulateFailureAsync_CertificateExpiry_UsesExpiryApi()
    {
        await using var db = CreateDb();
        var console = new TseTrainingConsoleStore();
        var deviceId = Guid.NewGuid();
        var sim = new Mock<ITseSimulatorService>(MockBehavior.Strict);
        sim.Setup(s => s.SimulateCertificateExpiryAsync(
                deviceId,
                It.IsAny<DateTime>(),
                "user-1",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TseSimulationResultDto
            {
                Success = true,
                DeviceId = deviceId,
                Message = "Expiry applied",
                ScenarioId = "certificate.expiry",
            });

        var svc = new TseTrainingService(
            db,
            sim.Object,
            console,
            new FakeEnv(Environments.Development),
            NullLogger<TseTrainingService>.Instance);

        var result = await svc.SimulateFailureAsync("user-1", deviceId, "CertificateExpiry");
        Assert.True(result.Success);
        Assert.Equal("CertificateExpiry", result.Scenario);
        sim.VerifyAll();
    }

    [Fact]
    public async Task SimulateFailureAsync_DeniedOutsideDevelopment()
    {
        await using var db = CreateDb();
        var console = new TseTrainingConsoleStore();
        var sim = new Mock<ITseSimulatorService>(MockBehavior.Strict);
        var svc = new TseTrainingService(
            db,
            sim.Object,
            console,
            new FakeEnv(Environments.Production),
            NullLogger<TseTrainingService>.Instance);

        var result = await svc.SimulateFailureAsync("user-1", Guid.NewGuid(), "SignatureError");
        Assert.False(result.Success);
        Assert.Contains("Development", result.Error ?? "", StringComparison.OrdinalIgnoreCase);
    }

    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"tse_training_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options, NullCurrentTenantAccessor.Instance);
    }

    private sealed class FakeEnv : IHostEnvironment
    {
        public FakeEnv(string env) => EnvironmentName = env;
        public string EnvironmentName { get; set; }
        public string ApplicationName { get; set; } = "tests";
        public string ContentRootPath { get; set; } = ".";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}

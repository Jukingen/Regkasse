using KasseAPI_Final.Services.FinanzOnlineIntegration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public class FinanzOnlineDeveloperSimulationEngineTests
{
    private static FinanzOnlineRegisterSubmissionRequest CreateSubmitRequest(string correlationId = "corr-1") =>
        new()
        {
            Mode = FinanzOnlineIntegrationMode.TEST,
            Scope = new FinanzOnlineScope { RegisterId = "REG1" },
            Correlation = new FinanzOnlineCorrelationContext
            {
                CorrelationId = correlationId,
                BusinessKey = "BK1",
                PayloadHash = "h"
            },
            SubmissionKind = FinanzOnlineSubmissionKind.Register,
            PayloadJson = "{}"
        };

    private static FinanzOnlineRegisterSubmissionResponse DefaultSuccess() =>
        new()
        {
            Success = true,
            TransmissionId = "SIM-TX-1",
            ReferenceId = "REF1",
            Status = "Submitted",
            ProtocolCode = "SIM_ACCEPTED"
        };

    private static FinanzOnlineDeveloperSimulationEngine CreateEngine(
        string environmentName,
        FinanzOnlineSimulationDeveloperOptions devOpts,
        FinanzOnlineSimulationOptions? simOpts = null)
    {
        simOpts ??= new FinanzOnlineSimulationOptions();
        var env = new Mock<IHostEnvironment>();
        env.Setup(e => e.EnvironmentName).Returns(environmentName);
        var devMon = Mock.Of<IOptionsMonitor<FinanzOnlineSimulationDeveloperOptions>>(m => m.CurrentValue == devOpts);
        var simMon = Mock.Of<IOptionsMonitor<FinanzOnlineSimulationOptions>>(m => m.CurrentValue == simOpts);
        var logger = new Mock<ILogger<FinanzOnlineDeveloperSimulationEngine>>().Object;
        return new FinanzOnlineDeveloperSimulationEngine(env.Object, devMon, simMon, logger);
    }

    [Fact]
    public async Task Production_environment_ignores_permanent_failure_profile()
    {
        var engine = CreateEngine(
            Environments.Production,
            new FinanzOnlineSimulationDeveloperOptions
            {
                BehaviorProfile = "PermanentSubmitFailure",
                EnableBehaviorProfilesOutsideDevelopment = true
            });
        var r = await engine.TryRegistrierkassenSubmitAsync(
            CreateSubmitRequest(),
            () => Task.FromResult(DefaultSuccess()),
            CancellationToken.None);
        Assert.Null(r);
    }

    [Fact]
    public async Task Staging_with_outside_flag_applies_permanent_failure_profile()
    {
        var engine = CreateEngine(Environments.Staging, new FinanzOnlineSimulationDeveloperOptions
        {
            BehaviorProfile = "PermanentSubmitFailure",
            EnableBehaviorProfilesOutsideDevelopment = true
        });
        var r = await engine.TryRegistrierkassenSubmitAsync(
            CreateSubmitRequest(),
            () => Task.FromResult(DefaultSuccess()),
            CancellationToken.None);
        Assert.NotNull(r);
        Assert.False(r!.Success);
        Assert.Equal("RKDB_COMMAND_INVALID", r.ErrorCode);
    }

    [Fact]
    public async Task Staging_without_outside_flag_ignores_profile()
    {
        var engine = CreateEngine(Environments.Staging, new FinanzOnlineSimulationDeveloperOptions
        {
            BehaviorProfile = "PermanentSubmitFailure",
            EnableBehaviorProfilesOutsideDevelopment = false
        });
        var r = await engine.TryRegistrierkassenSubmitAsync(
            CreateSubmitRequest(),
            () => Task.FromResult(DefaultSuccess()),
            CancellationToken.None);
        Assert.Null(r);
    }

    [Fact]
    public async Task ImmediateProtocolSuccess_returns_completed_without_transmission()
    {
        var engine = CreateEngine(Environments.Development, new FinanzOnlineSimulationDeveloperOptions
        {
            BehaviorProfile = "ImmediateProtocolSuccess"
        });
        var r = await engine.TryRegistrierkassenSubmitAsync(
            CreateSubmitRequest(),
            () => Task.FromResult(DefaultSuccess()),
            CancellationToken.None);
        Assert.NotNull(r);
        Assert.True(r!.Success);
        Assert.Null(r.TransmissionId);
        Assert.Equal("completed", r.Status);
        Assert.Equal("SIM_IMMEDIATE_OK", r.ProtocolCode);
        Assert.False(string.IsNullOrWhiteSpace(r.ProtocolSummary));
        Assert.False(string.IsNullOrWhiteSpace(r.ReferenceId));
    }

    [Fact]
    public async Task RetryableUntilDeadLetter_always_returns_transient()
    {
        var engine = CreateEngine(Environments.Development, new FinanzOnlineSimulationDeveloperOptions
        {
            BehaviorProfile = "RetryableUntilDeadLetter"
        });
        for (var i = 0; i < 5; i++)
        {
            var r = await engine.TryRegistrierkassenSubmitAsync(
                CreateSubmitRequest($"c-{i}"),
                () => Task.FromResult(DefaultSuccess()),
                CancellationToken.None);
            Assert.NotNull(r);
            Assert.False(r!.Success);
            Assert.Equal("HTTP_503", r.ErrorCode);
        }
    }

    [Fact]
    public async Task RetryableSubmitThenSuccess_development_transient_then_default_success()
    {
        var opts = new FinanzOnlineSimulationDeveloperOptions
        {
            BehaviorProfile = "RetryableSubmitThenSuccess",
            RetryableSubmitFailuresBeforeSuccess = 1,
            ArtificialLatencyMs = 0
        };
        var engine = CreateEngine(Environments.Development, opts);
        var req = CreateSubmitRequest("pay-a");

        var r1 = await engine.TryRegistrierkassenSubmitAsync(
            req,
            () => Task.FromResult(DefaultSuccess()),
            CancellationToken.None);
        Assert.NotNull(r1);
        Assert.False(r1!.Success);
        Assert.Equal("HTTP_503", r1.ErrorCode);

        var r2 = await engine.TryRegistrierkassenSubmitAsync(
            req,
            () => Task.FromResult(DefaultSuccess()),
            CancellationToken.None);
        Assert.NotNull(r2);
        Assert.True(r2!.Success);
        Assert.Equal("SIM-TX-1", r2.TransmissionId);
    }

    [Fact]
    public async Task ProtocolPendingThenSuccess_development_pending_then_submitted()
    {
        var opts = new FinanzOnlineSimulationDeveloperOptions
        {
            BehaviorProfile = "ProtocolPendingThenSuccess",
            ProtocolPendingQueriesBeforeSuccess = 2,
            ArtificialLatencyMs = 0
        };
        var engine = CreateEngine(Environments.Development, opts);
        var qreq = new FinanzOnlineTransmissionStatusQueryRequest
        {
            Mode = FinanzOnlineIntegrationMode.TEST,
            Scope = new FinanzOnlineScope(),
            Correlation = new FinanzOnlineCorrelationContext(),
            TransmissionId = "tx-proto-1"
        };

        Task<FinanzOnlineTransmissionStatusQueryResponse> BuildDefaultAsync() =>
            Task.FromResult(new FinanzOnlineTransmissionStatusQueryResponse
            {
                Success = true,
                TransmissionId = qreq.TransmissionId,
                Status = "Submitted",
                Protocol = new[] { new FinanzOnlineTransmissionProtocolEntry() }
            });

        var p1 = await engine.TryTransmissionQueryAsync(qreq, BuildDefaultAsync, CancellationToken.None);
        Assert.NotNull(p1);
        Assert.Equal("pending", p1!.Status);

        var p2 = await engine.TryTransmissionQueryAsync(qreq, BuildDefaultAsync, CancellationToken.None);
        Assert.NotNull(p2);
        Assert.Equal("pending", p2!.Status);

        var p3 = await engine.TryTransmissionQueryAsync(qreq, BuildDefaultAsync, CancellationToken.None);
        Assert.NotNull(p3);
        Assert.Equal("Submitted", p3!.Status);
    }

    [Fact]
    public void MapLifecyclePhase_maps_pending_to_pending_queued()
    {
        Assert.Equal("PendingQueued", FinanzOnlineReconciliationOutboxMapper.MapLifecyclePhase(FinanzOnlineOutboxStatuses.Pending));
        Assert.Equal("Sent", FinanzOnlineReconciliationOutboxMapper.MapLifecyclePhase(FinanzOnlineOutboxStatuses.Processing));
    }

    [Fact]
    public void ActiveProfileForAdminList_staging_requires_outside_flag()
    {
        var staging = new Mock<IHostEnvironment>();
        staging.Setup(e => e.EnvironmentName).Returns(Environments.Staging);
        Assert.Null(FinanzOnlineSimulationDeveloperUi.ActiveProfileForAdminList(
            staging.Object,
            new FinanzOnlineSimulationOptions(),
            new FinanzOnlineSimulationDeveloperOptions { BehaviorProfile = "AlwaysSuccess" }));
        Assert.Equal(
            "DeveloperProfile:AlwaysSuccess",
            FinanzOnlineSimulationDeveloperUi.ActiveProfileForAdminList(
                staging.Object,
                new FinanzOnlineSimulationOptions(),
                new FinanzOnlineSimulationDeveloperOptions
                {
                    BehaviorProfile = "AlwaysSuccess",
                    EnableBehaviorProfilesOutsideDevelopment = true
                }));
    }

    [Fact]
    public void ActiveProfileForAdminList_prefers_config_scenario_over_developer_profile()
    {
        var dev = new Mock<IHostEnvironment>();
        dev.Setup(e => e.EnvironmentName).Returns(Environments.Development);
        var label = FinanzOnlineSimulationDeveloperUi.ActiveProfileForAdminList(
            dev.Object,
            new FinanzOnlineSimulationOptions { Scenario = FinanzOnlineSimulationScenarios.DeadLetter },
            new FinanzOnlineSimulationDeveloperOptions { BehaviorProfile = "AlwaysSuccess" });
        Assert.Equal("Scenario:DeadLetter", label);
    }

    [Fact]
    public async Task Config_scenario_RetryThenSuccess_uses_RetryCountBeforeSuccess()
    {
        var sim = new FinanzOnlineSimulationOptions
        {
            Scenario = FinanzOnlineSimulationScenarios.RetryThenSuccess,
            RetryCountBeforeSuccess = 1,
            FixedDelayMs = 0
        };
        var engine = CreateEngine(
            Environments.Development,
            new FinanzOnlineSimulationDeveloperOptions { BehaviorProfile = "None" },
            sim);
        var req = CreateSubmitRequest("cfg-retry");

        var r1 = await engine.TryRegistrierkassenSubmitAsync(
            req,
            () => Task.FromResult(DefaultSuccess()),
            CancellationToken.None);
        Assert.NotNull(r1);
        Assert.False(r1!.Success);

        var r2 = await engine.TryRegistrierkassenSubmitAsync(
            req,
            () => Task.FromResult(DefaultSuccess()),
            CancellationToken.None);
        Assert.NotNull(r2);
        Assert.True(r2!.Success);
    }

    [Fact]
    public async Task Config_scenario_ImmediateSuccess_returns_immediate_protocol_style_success()
    {
        var sim = new FinanzOnlineSimulationOptions
        {
            Scenario = FinanzOnlineSimulationScenarios.ImmediateSuccess,
            FixedDelayMs = 0
        };
        var engine = CreateEngine(
            Environments.Development,
            new FinanzOnlineSimulationDeveloperOptions { BehaviorProfile = "None", ArtificialLatencyMs = 0 },
            sim);
        var r = await engine.TryRegistrierkassenSubmitAsync(
            CreateSubmitRequest("cfg-immediate"),
            () => Task.FromResult(DefaultSuccess()),
            CancellationToken.None);
        Assert.NotNull(r);
        Assert.True(r!.Success);
        Assert.Null(r.TransmissionId);
        Assert.Equal("completed", r.Status);
        Assert.Equal("SIM_IMMEDIATE_OK", r.ProtocolCode);
    }

    [Fact]
    public async Task Config_scenario_PermanentFailure_returns_permanent_error()
    {
        var sim = new FinanzOnlineSimulationOptions
        {
            Scenario = FinanzOnlineSimulationScenarios.PermanentFailure,
            FixedDelayMs = 0
        };
        var engine = CreateEngine(
            Environments.Development,
            new FinanzOnlineSimulationDeveloperOptions { BehaviorProfile = "None", ArtificialLatencyMs = 0 },
            sim);
        var r = await engine.TryRegistrierkassenSubmitAsync(
            CreateSubmitRequest("cfg-perm"),
            () => Task.FromResult(DefaultSuccess()),
            CancellationToken.None);
        Assert.NotNull(r);
        Assert.False(r!.Success);
        Assert.Equal("RKDB_COMMAND_INVALID", r.ErrorCode);
    }

    [Fact]
    public async Task Config_scenario_DeadLetter_returns_transient_for_worker_classification()
    {
        var sim = new FinanzOnlineSimulationOptions
        {
            Scenario = FinanzOnlineSimulationScenarios.DeadLetter,
            FixedDelayMs = 0
        };
        var engine = CreateEngine(
            Environments.Development,
            new FinanzOnlineSimulationDeveloperOptions { BehaviorProfile = "None", ArtificialLatencyMs = 0 },
            sim);
        var r = await engine.TryRegistrierkassenSubmitAsync(
            CreateSubmitRequest("cfg-dl"),
            () => Task.FromResult(DefaultSuccess()),
            CancellationToken.None);
        Assert.NotNull(r);
        Assert.False(r!.Success);
        Assert.Equal("HTTP_503", r.ErrorCode);
    }

    [Fact]
    public async Task Config_scenario_AwaitingProtocolThenSuccess_pends_then_resolves_on_query()
    {
        var sim = new FinanzOnlineSimulationOptions
        {
            Scenario = FinanzOnlineSimulationScenarios.AwaitingProtocolThenSuccess,
            ProtocolPendingQueriesBeforeSuccess = 2,
            FixedDelayMs = 0
        };
        var engine = CreateEngine(
            Environments.Development,
            new FinanzOnlineSimulationDeveloperOptions { BehaviorProfile = "None", ArtificialLatencyMs = 0 },
            sim);
        var qreq = new FinanzOnlineTransmissionStatusQueryRequest
        {
            Mode = FinanzOnlineIntegrationMode.TEST,
            Scope = new FinanzOnlineScope(),
            Correlation = new FinanzOnlineCorrelationContext(),
            TransmissionId = "tx-await-cfg-1"
        };

        Task<FinanzOnlineTransmissionStatusQueryResponse> BuildDefaultAsync() =>
            Task.FromResult(new FinanzOnlineTransmissionStatusQueryResponse
            {
                Success = true,
                TransmissionId = qreq.TransmissionId,
                Status = "Submitted",
                Protocol = new[] { new FinanzOnlineTransmissionProtocolEntry() }
            });

        var p1 = await engine.TryTransmissionQueryAsync(qreq, BuildDefaultAsync, CancellationToken.None);
        Assert.NotNull(p1);
        Assert.Equal("pending", p1!.Status);

        var p2 = await engine.TryTransmissionQueryAsync(qreq, BuildDefaultAsync, CancellationToken.None);
        Assert.NotNull(p2);
        Assert.Equal("pending", p2!.Status);

        var p3 = await engine.TryTransmissionQueryAsync(qreq, BuildDefaultAsync, CancellationToken.None);
        Assert.NotNull(p3);
        Assert.Equal("Submitted", p3!.Status);
    }
}

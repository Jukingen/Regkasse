using System.Text.Json;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.FinanzOnlineIntegration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

/// <summary>
/// FinanzOnline outbox worker: one-shot process/reconcile cycles against PostgreSQL (ExecuteUpdate claim path is not supported on EF InMemory).
/// Uses <see cref="FinanzOnlineOutboxHostedService"/> internal test hooks (InternalsVisibleTo).
/// Skips when <see cref="PostgreSqlReplayFixture"/> has no database (no Docker / no REGKASSE_TEST_POSTGRES).
/// </summary>
[Collection("PostgreSqlReplay")]
[Trait("Category", "PostgreSql")]
public sealed class FinanzOnlineOutboxWorkerIntegrationTests
{
    private readonly PostgreSqlReplayFixture _fixture;

    public FinanzOnlineOutboxWorkerIntegrationTests(PostgreSqlReplayFixture fixture) => _fixture = fixture;

    private static IOptionsMonitor<FinanzOnlineOutboxOptions> OptionsMonitor(FinanzOnlineOutboxOptions value)
    {
        var m = new Mock<IOptionsMonitor<FinanzOnlineOutboxOptions>>();
        m.Setup(x => x.CurrentValue).Returns(value);
        return m.Object;
    }

    private static Mock<IAuditLogService> CreateAuditMock()
    {
        var audit = new Mock<IAuditLogService>();
        audit.Setup(a => a.LogSystemOperationAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<AuditLogStatus>(),
                It.IsAny<string>(),
                It.IsAny<object>(),
                It.IsAny<object>(),
                It.IsAny<string>()))
            .ReturnsAsync(new AuditLog());
        return audit;
    }

    private static string SerializePayload(FinanzOnlineOutboxPayload payload) => JsonSerializer.Serialize(payload);

    private static FinanzOnlineOutboxPayload MinimalRegisterPayload() =>
        new()
        {
            Mode = FinanzOnlineIntegrationMode.TEST,
            Scope = new FinanzOnlineScope
            {
                TenantId = "tenant-a",
                BranchId = "branch-a",
                RegisterId = "REG-TEST",
            },
            Correlation = new FinanzOnlineCorrelationContext
            {
                CorrelationId = "corr-worker-it",
                BusinessKey = "bk-worker-it",
                PayloadHash = "payload-hash",
            },
            SubmissionKind = FinanzOnlineSubmissionKind.Register,
            PayloadJson = "{}",
        };

    private static FinanzOnlineOutboxMessage CreatePendingOutboxRow(
        Guid id,
        string messageType,
        string payloadJson,
        string payloadHashHex)
    {
        var aggregateId = Guid.NewGuid();
        return new FinanzOnlineOutboxMessage
        {
            Id = id,
            TenantId = "tenant-a",
            BranchId = "branch-a",
            AggregateType = "Invoice",
            AggregateId = aggregateId,
            MessageType = messageType,
            BusinessKey = "bk-" + id.ToString("N")[..12],
            IdempotencyKey = "idem-" + id.ToString("N"),
            PayloadJson = payloadJson,
            PayloadHash = payloadHashHex,
            Mode = "TEST",
            Status = FinanzOnlineOutboxStatuses.Pending,
            AttemptCount = 0,
            NextAttemptAt = DateTime.UtcNow.AddMinutes(-10),
            CorrelationId = "corr-" + id.ToString("N")[..8],
            CreatedAt = DateTime.UtcNow,
        };
    }

    private static async Task<(ServiceProvider provider, FinanzOnlineOutboxHostedService worker)> CreateWorkerAsync(
        string connectionString,
        FinanzOnlineOutboxOptions outboxOpts,
        Mock<IFinanzOnlineSubmissionService> submission,
        IFinanzOnlineTransmissionQueryClient queryClient,
        Mock<IAuditLogService> audit)
    {
        var services = new ServiceCollection();
        services.AddScoped(_ =>
            new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseAppNpgsql(connectionString).Options));
        services.AddSingleton(OptionsMonitor(outboxOpts));
        services.AddScoped(_ => submission.Object);
        services.AddScoped(_ => queryClient);
        services.AddScoped(_ => audit.Object);
        var provider = services.BuildServiceProvider();
        var worker = new FinanzOnlineOutboxHostedService(
            provider,
            OptionsMonitor(outboxOpts),
            NullLogger<FinanzOnlineOutboxHostedService>.Instance);
        return (provider, worker);
    }

    [SkippableFact]
    public async Task ProcessOne_TagesberichtInformational_reaches_protocol_success_without_submit_service()
    {
        Skip.IfNot(_fixture.HasDatabase, _fixture.SkipReason);

        var outboxOpts = new FinanzOnlineOutboxOptions
        {
            Enabled = true,
            MaxAttempts = 8,
            JitterMaxSeconds = 0,
            BaseDelaySeconds = 1,
            BackoffCapSeconds = 60,
            ProcessingTimeoutSeconds = 30,
        };
        var submission = new Mock<IFinanzOnlineSubmissionService>(MockBehavior.Strict);
        var query = Mock.Of<IFinanzOnlineTransmissionQueryClient>();
        var audit = CreateAuditMock();
        var (provider, worker) = await CreateWorkerAsync(_fixture.ConnectionString, outboxOpts, submission, query, audit);
        await using (provider)
        {
            var id = Guid.NewGuid();
            var row = CreatePendingOutboxRow(id, FinanzOnlineTagesberichtMessageTypes.TagesberichtDailySummary, "{}", new string('a', 64));
            await using (var scope = provider.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                db.FinanzOnlineOutboxMessages.Add(row);
                await db.SaveChangesAsync();
            }

            await worker.ProcessOneForIntegrationTestsAsync(CancellationToken.None);

            await using (var scope = provider.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var after = await db.FinanzOnlineOutboxMessages.AsNoTracking().SingleAsync(x => x.Id == id);
                Assert.Equal(FinanzOnlineOutboxStatuses.ProtocolSuccess, after.Status);
                Assert.Null(after.ProcessingToken);
                Assert.NotNull(after.ProcessedAt);
                Assert.StartsWith("TBR-", after.ExternalReferenceId ?? "", StringComparison.Ordinal);
            }

            submission.Verify(
                x => x.SubmitAsync(It.IsAny<FinanzOnlineRegisterSubmissionRequest>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }
    }

    [SkippableFact]
    public async Task ProcessOne_immediate_protocol_success_sets_protocol_success_when_transmission_id_null()
    {
        Skip.IfNot(_fixture.HasDatabase, _fixture.SkipReason);

        var outboxOpts = new FinanzOnlineOutboxOptions
        {
            Enabled = true,
            MaxAttempts = 8,
            JitterMaxSeconds = 0,
            BaseDelaySeconds = 1,
            BackoffCapSeconds = 60,
            ProcessingTimeoutSeconds = 30,
        };
        var submission = new Mock<IFinanzOnlineSubmissionService>();
        submission
            .Setup(x => x.SubmitAsync(It.IsAny<FinanzOnlineRegisterSubmissionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FinanzOnlineRegisterSubmissionResponse
            {
                Success = true,
                TransmissionId = null,
                ReferenceId = "REF-IMM",
                Status = "completed",
                ProtocolCode = "SIM_IMMEDIATE_OK",
                ProtocolSummary = "ok",
            });
        var query = Mock.Of<IFinanzOnlineTransmissionQueryClient>();
        var audit = CreateAuditMock();
        var (provider, worker) = await CreateWorkerAsync(_fixture.ConnectionString, outboxOpts, submission, query, audit);
        await using (provider)
        {
            var id = Guid.NewGuid();
            var payloadJson = SerializePayload(MinimalRegisterPayload());
            var row = CreatePendingOutboxRow(id, "InvoiceRegister", payloadJson, new string('b', 64));
            await using (var scope = provider.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                db.FinanzOnlineOutboxMessages.Add(row);
                await db.SaveChangesAsync();
            }

            await worker.ProcessOneForIntegrationTestsAsync(CancellationToken.None);

            await using (var scope = provider.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var after = await db.FinanzOnlineOutboxMessages.AsNoTracking().SingleAsync(x => x.Id == id);
                Assert.Equal(FinanzOnlineOutboxStatuses.ProtocolSuccess, after.Status);
                Assert.Equal("REF-IMM", after.ExternalReferenceId);
                Assert.Equal("SIM_IMMEDIATE_OK", after.ProtocolCode);
            }
        }
    }

    [SkippableFact]
    public async Task ProcessOne_then_reconcile_moves_awaiting_protocol_to_protocol_success()
    {
        Skip.IfNot(_fixture.HasDatabase, _fixture.SkipReason);

        var outboxOpts = new FinanzOnlineOutboxOptions
        {
            Enabled = true,
            MaxAttempts = 8,
            JitterMaxSeconds = 0,
            BaseDelaySeconds = 1,
            BackoffCapSeconds = 60,
            ProcessingTimeoutSeconds = 30,
        };
        var submission = new Mock<IFinanzOnlineSubmissionService>();
        submission
            .Setup(x => x.SubmitAsync(It.IsAny<FinanzOnlineRegisterSubmissionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FinanzOnlineRegisterSubmissionResponse
            {
                Success = true,
                TransmissionId = "tx-recon-1",
                ReferenceId = "REF-TX",
                Status = "Submitted",
                ProtocolCode = "ACCEPTED",
            });
        var query = new Mock<IFinanzOnlineTransmissionQueryClient>();
        query.Setup(x => x.QueryStatusAsync(It.IsAny<FinanzOnlineTransmissionStatusQueryRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FinanzOnlineTransmissionStatusQueryResponse
            {
                Success = true,
                TransmissionId = "tx-recon-1",
                Status = "Submitted",
                Protocol = Array.Empty<FinanzOnlineTransmissionProtocolEntry>(),
            });
        var audit = CreateAuditMock();
        var (provider, worker) = await CreateWorkerAsync(_fixture.ConnectionString, outboxOpts, submission, query.Object, audit);
        await using (provider)
        {
            var id = Guid.NewGuid();
            var payloadJson = SerializePayload(MinimalRegisterPayload());
            var row = CreatePendingOutboxRow(id, "InvoiceRegister", payloadJson, new string('c', 64));
            await using (var scope = provider.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                db.FinanzOnlineOutboxMessages.Add(row);
                await db.SaveChangesAsync();
            }

            await worker.ProcessOneForIntegrationTestsAsync(CancellationToken.None);

            await using (var scope = provider.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var mid = await db.FinanzOnlineOutboxMessages.AsNoTracking().SingleAsync(x => x.Id == id);
                Assert.Equal(FinanzOnlineOutboxStatuses.AwaitingProtocol, mid.Status);
                Assert.Equal("tx-recon-1", mid.TransmissionId);
            }

            await using (var scope = provider.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var entity = await db.FinanzOnlineOutboxMessages.SingleAsync(x => x.Id == id);
                entity.NextAttemptAt = DateTime.UtcNow.AddMinutes(-1);
                await db.SaveChangesAsync();
            }

            await worker.ReconcileOneForIntegrationTestsAsync(CancellationToken.None);

            await using (var scope = provider.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var after = await db.FinanzOnlineOutboxMessages.AsNoTracking().SingleAsync(x => x.Id == id);
                Assert.Equal(FinanzOnlineOutboxStatuses.ProtocolSuccess, after.Status);
                Assert.NotNull(after.ProcessedAt);
            }
        }
    }

    [SkippableFact]
    public async Task ProcessOne_transient_failure_with_max_attempts_one_goes_dead_letter()
    {
        Skip.IfNot(_fixture.HasDatabase, _fixture.SkipReason);

        var outboxOpts = new FinanzOnlineOutboxOptions
        {
            Enabled = true,
            MaxAttempts = 1,
            JitterMaxSeconds = 0,
            BaseDelaySeconds = 1,
            BackoffCapSeconds = 60,
            ProcessingTimeoutSeconds = 30,
        };
        var submission = new Mock<IFinanzOnlineSubmissionService>();
        submission
            .Setup(x => x.SubmitAsync(It.IsAny<FinanzOnlineRegisterSubmissionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FinanzOnlineRegisterSubmissionResponse
            {
                Success = false,
                ErrorCode = "HTTP_503",
                ErrorMessage = "transient",
            });
        var query = Mock.Of<IFinanzOnlineTransmissionQueryClient>();
        var audit = CreateAuditMock();
        var (provider, worker) = await CreateWorkerAsync(_fixture.ConnectionString, outboxOpts, submission, query, audit);
        await using (provider)
        {
            var id = Guid.NewGuid();
            var payloadJson = SerializePayload(MinimalRegisterPayload());
            var row = CreatePendingOutboxRow(id, "InvoiceRegister", payloadJson, new string('d', 64));
            await using (var scope = provider.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                db.FinanzOnlineOutboxMessages.Add(row);
                await db.SaveChangesAsync();
            }

            await worker.ProcessOneForIntegrationTestsAsync(CancellationToken.None);

            await using (var scope = provider.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var after = await db.FinanzOnlineOutboxMessages.AsNoTracking().SingleAsync(x => x.Id == id);
                Assert.Equal(FinanzOnlineOutboxStatuses.DeadLetter, after.Status);
                Assert.Equal("MaxAttemptsExceeded", after.FailureCategory);
            }
        }
    }

    [SkippableFact]
    public async Task ProcessOne_permanent_rkdb_failure_maps_to_permanent_failure_when_max_attempts_above_one()
    {
        Skip.IfNot(_fixture.HasDatabase, _fixture.SkipReason);

        var outboxOpts = new FinanzOnlineOutboxOptions
        {
            Enabled = true,
            MaxAttempts = 8,
            JitterMaxSeconds = 0,
            BaseDelaySeconds = 1,
            BackoffCapSeconds = 60,
            ProcessingTimeoutSeconds = 30,
        };
        var submission = new Mock<IFinanzOnlineSubmissionService>();
        submission
            .Setup(x => x.SubmitAsync(It.IsAny<FinanzOnlineRegisterSubmissionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FinanzOnlineRegisterSubmissionResponse
            {
                Success = false,
                ErrorCode = "RKDB_COMMAND_INVALID",
                ErrorMessage = "invalid",
            });
        var query = Mock.Of<IFinanzOnlineTransmissionQueryClient>();
        var audit = CreateAuditMock();
        var (provider, worker) = await CreateWorkerAsync(_fixture.ConnectionString, outboxOpts, submission, query, audit);
        await using (provider)
        {
            var id = Guid.NewGuid();
            var payloadJson = SerializePayload(MinimalRegisterPayload());
            var row = CreatePendingOutboxRow(id, "InvoiceRegister", payloadJson, new string('e', 64));
            await using (var scope = provider.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                db.FinanzOnlineOutboxMessages.Add(row);
                await db.SaveChangesAsync();
            }

            await worker.ProcessOneForIntegrationTestsAsync(CancellationToken.None);

            await using (var scope = provider.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var after = await db.FinanzOnlineOutboxMessages.AsNoTracking().SingleAsync(x => x.Id == id);
                Assert.Equal(FinanzOnlineOutboxStatuses.PermanentFailure, after.Status);
                Assert.Equal(FinanzOnlineFailureCategories.PermanentBusiness, after.FailureCategory);
            }
        }
    }
}

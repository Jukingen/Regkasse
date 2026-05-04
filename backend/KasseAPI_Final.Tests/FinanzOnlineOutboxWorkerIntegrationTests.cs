using System.Text;
using System.Text.Json;
using KasseAPI_Final.Constants;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.FinanzOnlineIntegration;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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

    private static Mock<IRksvFinanzOnlineSubmissionClient> CreateDefaultRksvFinanzOnlineSubmissionClientMock()
    {
        var m = new Mock<IRksvFinanzOnlineSubmissionClient>();
        var ok = new RksvFinanzOnlineSubmissionResult
        {
            Success = true,
            ExternalReference = "MOCK-RKS-REF",
            VerificationStatus = "Verified",
            RawResponseSnapshot = """{"mock":true}""",
        };
        m.Setup(x => x.SubmitStartbelegAsync(It.IsAny<RksvFinanzOnlineSubmissionPayload>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ok);
        m.Setup(x => x.SubmitJahresbelegAsync(It.IsAny<RksvFinanzOnlineSubmissionPayload>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ok);
        return m;
    }

    private static async Task SeedRksvFoOutboxGraphAsync(
        AppDbContext db,
        RksvSpecialReceiptFinanzOnlineOutboxPayloadBody inner,
        string registerNumber)
    {
        TenantTestDoubles.EnsureDefaultTenant(db);
        if (!await db.Customers.AnyAsync(c => c.Id == WalkInCustomerConstants.GuestCustomerId))
        {
            db.Customers.Add(new Customer
            {
                Id = WalkInCustomerConstants.GuestCustomerId,
                Name = "Gast",
                Email = "gast@test",
                Phone = "0",
                Address = "",
                TaxNumber = "",
                CustomerNumber = "",
                IsActive = true,
            });
        }

        if (!await db.CashRegisters.AnyAsync(r => r.Id == inner.CashRegisterId))
        {
            db.CashRegisters.Add(new CashRegister
            {
                Id = inner.CashRegisterId,
                TenantId = LegacyDefaultTenantIds.Primary,
                RegisterNumber = registerNumber,
                Location = "T",
                StartingBalance = 0,
                CurrentBalance = 0,
                LastBalanceUpdate = DateTime.UtcNow,
                Status = RegisterStatus.Open,
                CreatedAt = DateTime.UtcNow,
                IsActive = true,
            });
        }

        if (!await db.PaymentDetails.AnyAsync(p => p.Id == inner.PaymentId))
        {
            db.PaymentDetails.Add(new PaymentDetails
            {
                Id = inner.PaymentId,
                CustomerId = WalkInCustomerConstants.GuestCustomerId,
                CustomerName = "Gast",
                TableNumber = 0,
                CashierId = "system",
                TotalAmount = 0,
                TaxAmount = 0,
                Steuernummer = "ATU12345678",
                CashRegisterId = inner.CashRegisterId,
                TseSignature = "sig",
                TseTimestamp = DateTime.UtcNow,
                ReceiptNumber = inner.ReceiptNumber,
                IsActive = true,
                RksvSpecialReceiptKind = RksvSpecialReceiptKinds.Startbeleg,
                CreatedAt = DateTime.UtcNow,
            });
        }

        if (!await db.Receipts.AnyAsync(r => r.ReceiptId == inner.ReceiptId))
        {
            db.Receipts.Add(new Receipt
            {
                ReceiptId = inner.ReceiptId,
                PaymentId = inner.PaymentId,
                ReceiptNumber = inner.ReceiptNumber,
                IssuedAt = DateTime.UtcNow,
                CashRegisterId = inner.CashRegisterId,
                SubTotal = 0,
                TaxTotal = 0,
                GrandTotal = 0,
                QrCodePayload = inner.QrPayload,
                CreatedAt = DateTime.UtcNow,
            });
        }

        if (!await db.RksvSpecialReceiptFinanzOnlineSubmissions.AnyAsync(s => s.PaymentId == inner.PaymentId))
        {
            db.RksvSpecialReceiptFinanzOnlineSubmissions.Add(new RksvSpecialReceiptFinanzOnlineSubmission
            {
                PaymentId = inner.PaymentId,
                ReceiptId = inner.ReceiptId,
                CashRegisterId = inner.CashRegisterId,
                Kind = RksvSpecialReceiptKinds.Startbeleg,
                Status = RksvSpecialReceiptFinanzOnlineSubmissionStatuses.Pending,
                AttemptCount = 0,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow,
            });
        }

        await db.SaveChangesAsync();
    }

    private static async Task<(ServiceProvider provider, FinanzOnlineOutboxHostedService worker)> CreateWorkerAsync(
        string connectionString,
        FinanzOnlineOutboxOptions outboxOpts,
        Mock<IFinanzOnlineSubmissionService> submission,
        IFinanzOnlineTransmissionQueryClient queryClient,
        Mock<IAuditLogService> audit,
        Mock<IRksvFinanzOnlineSubmissionClient>? rksvFinanzOnlineSubmissionClient = null)
    {
        var services = new ServiceCollection();
        services.AddScoped(_ =>
            new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseAppNpgsql(connectionString).Options));
        services.AddSingleton(OptionsMonitor(outboxOpts));
        services.AddScoped(_ => submission.Object);
        services.AddScoped(_ => queryClient);
        services.AddScoped(_ => audit.Object);
        var rksv = rksvFinanzOnlineSubmissionClient ?? CreateDefaultRksvFinanzOnlineSubmissionClientMock();
        services.AddSingleton<ILogger<RksvSpecialReceiptFinanzOnlineOutboxHandler>>(
            NullLogger<RksvSpecialReceiptFinanzOnlineOutboxHandler>.Instance);
        services.AddScoped<IRksvFinanzOnlineSubmissionClient>(_ => rksv.Object);
        services.AddScoped<RksvSpecialReceiptFinanzOnlineOutboxHandler>();
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
                // Informational Tagesbericht path assigns a synthetic TransmissionId for audit/traceability (not a live FinanzOnline tx).
                Assert.StartsWith("TBR-TX-", after.TransmissionId ?? "", StringComparison.Ordinal);
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

    [SkippableFact]
    public async Task ProcessOne_RksvStartbelegSubmission_uses_fake_submission_client_and_updates_tracker()
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
            var receiptId = Guid.NewGuid();
            var paymentId = Guid.NewGuid();
            var cashRegisterId = Guid.NewGuid();
            const string registerNumber = "REG-01";
            var inner = new RksvSpecialReceiptFinanzOnlineOutboxPayloadBody
            {
                Kind = "Startbeleg",
                PaymentId = paymentId,
                ReceiptId = receiptId,
                CashRegisterId = cashRegisterId,
                ReceiptNumber = "AT-REG-20260101-1",
                QrPayload = "_R1-AT1_test_qr",
            };
            var innerJson = JsonSerializer.Serialize(inner, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            var innerHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(innerJson))).ToLowerInvariant();
            var outer = new FinanzOnlineOutboxPayload
            {
                Mode = FinanzOnlineIntegrationMode.TEST,
                Scope = new FinanzOnlineScope { TenantId = "tenant-a", RegisterId = registerNumber },
                Correlation = new FinanzOnlineCorrelationContext
                {
                    BusinessKey = $"rksv|{receiptId:N}|Startbeleg",
                    PayloadHash = innerHash,
                    CorrelationId = paymentId.ToString("N"),
                },
                SubmissionKind = FinanzOnlineSubmissionKind.Register,
                PayloadJson = innerJson,
            };
            var outerJson = SerializePayload(outer);
            var rowHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(outerJson))).ToLowerInvariant();
            var row = CreatePendingOutboxRow(id, FinanzOnlineRksvSpecialReceiptOutboxMessageTypes.RksvStartbelegSubmission, outerJson, rowHash);
            await using (var scope = provider.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                await SeedRksvFoOutboxGraphAsync(db, inner, registerNumber);
                db.FinanzOnlineOutboxMessages.Add(row);
                await db.SaveChangesAsync();
            }

            await worker.ProcessOneForIntegrationTestsAsync(CancellationToken.None);

            await using (var scope = provider.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var after = await db.FinanzOnlineOutboxMessages.AsNoTracking().SingleAsync(x => x.Id == id);
                Assert.Equal(FinanzOnlineOutboxStatuses.ProtocolSuccess, after.Status);
                Assert.Equal("MOCK-RKS-REF", after.ExternalReferenceId);
                var sub = await db.RksvSpecialReceiptFinanzOnlineSubmissions.AsNoTracking().SingleAsync(s => s.PaymentId == paymentId);
                Assert.Equal(RksvSpecialReceiptFinanzOnlineSubmissionStatuses.Verified, sub.Status);
                Assert.NotNull(sub.VerifiedAtUtc);
                Assert.Equal("MOCK-RKS-REF", sub.ExternalReference);
            }

            submission.Verify(
                x => x.SubmitAsync(It.IsAny<FinanzOnlineRegisterSubmissionRequest>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }
    }
}

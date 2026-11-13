using KasseAPI_Final.Models;
using KasseAPI_Final.Services.FinanzOnlineIntegration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class RksvFinanzOnlineSubmissionClientTests
{
    private static FakeRksvFinanzOnlineSubmissionClient CreateFake(RksvFinanzOnlineSubmissionClientOptions options)
    {
        var monitor = new Mock<IOptionsMonitor<RksvFinanzOnlineSubmissionClientOptions>>();
        monitor.Setup(m => m.CurrentValue).Returns(options);
        return new FakeRksvFinanzOnlineSubmissionClient(
            monitor.Object,
            Mock.Of<ILogger<FakeRksvFinanzOnlineSubmissionClient>>());
    }

    private static RksvFinanzOnlineSubmissionClient CreateReal(
        RksvFinanzOnlineSubmissionClientOptions options,
        IFinanzOnlineSubmissionService? submissionService = null,
        string mode = "Test",
        FinanzOnlineCutoverGuardOptions? cutover = null)
    {
        var monitor = new Mock<IOptionsMonitor<RksvFinanzOnlineSubmissionClientOptions>>();
        monitor.Setup(m => m.CurrentValue).Returns(options);

        var modeMon = new Mock<IOptionsMonitor<FinanzOnlineModeOptions>>();
        modeMon.Setup(m => m.CurrentValue).Returns(new FinanzOnlineModeOptions { Mode = mode });

        var cutoverMon = new Mock<IOptionsMonitor<FinanzOnlineCutoverGuardOptions>>();
        cutoverMon.Setup(m => m.CurrentValue).Returns(cutover ?? new FinanzOnlineCutoverGuardOptions());

        return new RksvFinanzOnlineSubmissionClient(
            monitor.Object,
            modeMon.Object,
            cutoverMon.Object,
            submissionService ?? Mock.Of<IFinanzOnlineSubmissionService>(),
            Mock.Of<ILogger<RksvFinanzOnlineSubmissionClient>>());
    }

    private static RksvFinanzOnlineSubmissionClientOptions CompleteRealOptions(bool allowOutbound = true) => new()
    {
        Enabled = true,
        EndpointUrl = "https://example.invalid/rksv-soap",
        TimeoutSeconds = 60,
        Environment = RksvFinanzOnlineSubmissionDeploymentEnvironment.Test,
        ParticipantCredentialsConfigurationKey = "FinanzOnline:ParticipantRef",
        ClientCertificateSecretName = "kv/rksv-client-cert",
        AllowOutboundNetworkCalls = allowOutbound,
        ClientKind = RksvFinanzOnlineSubmissionClientKind.Real,
    };

    private static string ValidDepBeleg() => FinanzOnlineDevTestSmoke.BuildSyntheticDepBeleg();

    [Fact]
    public async Task Fake_SubmitStartbelegAsync_WhenConfiguredSuccess_ReturnsReferenceAndSnapshotWithoutQr()
    {
        var fake = CreateFake(new RksvFinanzOnlineSubmissionClientOptions
        {
            FakeSuccess = true,
            FakeVerificationStatus = "Verified",
        });
        var payload = new RksvFinanzOnlineSubmissionPayload
        {
            CashRegisterId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"),
            RegisterNumber = "REG-1",
            ReceiptNumber = "AT-TSE-20260101-1",
            QrPayload = "MACHINE-READABLE-DO-NOT-LOG-IN-TESTS-BUT-SNAPSHOT-SHOULD-NOT-CONTAIN-IT",
            TimestampUtc = DateTimeOffset.Parse("2026-01-01T12:00:00Z"),
        };

        var result = await fake.SubmitStartbelegAsync(payload);

        Assert.True(result.Success);
        Assert.NotNull(result.ExternalReference);
        Assert.Contains("Startbeleg", result.ExternalReference, StringComparison.Ordinal);
        Assert.Equal("Verified", result.VerificationStatus);
        Assert.Null(result.ErrorCode);
        Assert.NotNull(result.RawResponseSnapshot);
        Assert.DoesNotContain("MACHINE-READABLE", result.RawResponseSnapshot, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Fake_SubmitJahresbelegAsync_WhenConfiguredFailure_ReturnsErrorFields()
    {
        var fake = CreateFake(new RksvFinanzOnlineSubmissionClientOptions
        {
            FakeSuccess = false,
            FakeErrorCode = "TEST_ERR",
            FakeErrorMessage = "Simulated rejection.",
            FakeVerificationStatus = "Rejected",
        });
        var payload = new RksvFinanzOnlineSubmissionPayload
        {
            CashRegisterId = Guid.NewGuid(),
            RegisterNumber = "REG-2",
            ReceiptNumber = "AT-TSE-20260101-99",
            QrPayload = "x",
            TimestampUtc = DateTimeOffset.UtcNow,
        };

        var result = await fake.SubmitJahresbelegAsync(payload);

        Assert.False(result.Success);
        Assert.Null(result.ExternalReference);
        Assert.Equal("Rejected", result.VerificationStatus);
        Assert.Equal("TEST_ERR", result.ErrorCode);
        Assert.Equal("Simulated rejection.", result.ErrorMessage);
        Assert.NotNull(result.RawResponseSnapshot);
    }

    [Fact]
    public async Task NotImplemented_SubmitStartbelegAsync_ThrowsNotImplementedException()
    {
        var client = new NotImplementedRksvFinanzOnlineSubmissionClient();
        await Assert.ThrowsAsync<NotImplementedException>(() =>
            client.SubmitStartbelegAsync(new RksvFinanzOnlineSubmissionPayload(), CancellationToken.None));
    }

    [Fact]
    public async Task NotImplemented_SubmitJahresbelegAsync_ThrowsNotImplementedException()
    {
        var client = new NotImplementedRksvFinanzOnlineSubmissionClient();
        await Assert.ThrowsAsync<NotImplementedException>(() =>
            client.SubmitJahresbelegAsync(new RksvFinanzOnlineSubmissionPayload(), CancellationToken.None));
    }

    [Fact]
    public async Task Real_WhenDisabled_ReturnsSubmissionDisabledWithoutSuccess()
    {
        var client = CreateReal(new RksvFinanzOnlineSubmissionClientOptions
        {
            Enabled = false,
            ClientKind = RksvFinanzOnlineSubmissionClientKind.Real,
        });
        var result = await client.SubmitStartbelegAsync(new RksvFinanzOnlineSubmissionPayload
        {
            CashRegisterId = Guid.NewGuid(),
            RegisterNumber = "R1",
            ReceiptNumber = "B1",
            QrPayload = "x",
            TimestampUtc = DateTimeOffset.UtcNow,
        });
        Assert.False(result.Success);
        Assert.Equal(RksvFinanzOnlineSubmissionKnownErrorCodes.SubmissionDisabled, result.ErrorCode);
        Assert.Equal(RksvSpecialReceiptFinanzOnlineSubmissionStatuses.ManualVerificationRequired, result.VerificationStatus);
    }

    [Fact]
    public async Task Real_WhenOutboundDisabled_ReturnsOutboundDisabledWithoutCallingSubmission()
    {
        var submission = new Mock<IFinanzOnlineSubmissionService>(MockBehavior.Strict);
        var client = CreateReal(CompleteRealOptions(allowOutbound: false), submission.Object);
        var result = await client.SubmitStartbelegAsync(new RksvFinanzOnlineSubmissionPayload
        {
            CashRegisterId = Guid.NewGuid(),
            RegisterNumber = "R1",
            ReceiptNumber = "B1",
            QrPayload = ValidDepBeleg(),
            TimestampUtc = DateTimeOffset.UtcNow,
        });
        Assert.False(result.Success);
        Assert.Equal(RksvFinanzOnlineSubmissionKnownErrorCodes.OutboundDisabled, result.ErrorCode);
        submission.Verify(
            s => s.SubmitAsync(It.IsAny<FinanzOnlineRegisterSubmissionRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Real_WhenEnabledButIncomplete_ReturnsConfigIncomplete()
    {
        var client = CreateReal(new RksvFinanzOnlineSubmissionClientOptions
        {
            Enabled = true,
            AllowOutboundNetworkCalls = true,
            EndpointUrl = null,
            TimeoutSeconds = 120,
            ParticipantCredentialsConfigurationKey = "FinanzOnline:ParticipantRef",
            ClientCertificateSecretName = "kv/rksv-client-cert",
        });
        var result = await client.SubmitStartbelegAsync(new RksvFinanzOnlineSubmissionPayload
        {
            CashRegisterId = Guid.NewGuid(),
            RegisterNumber = "R1",
            ReceiptNumber = "B1",
            QrPayload = "x",
            TimestampUtc = DateTimeOffset.UtcNow,
        });
        Assert.False(result.Success);
        Assert.Equal(RksvFinanzOnlineSubmissionKnownErrorCodes.ConfigIncomplete, result.ErrorCode);
    }

    [Fact]
    public async Task Real_WhenInvalidBeleg_ReturnsBelegInvalidWithoutCallingSubmission()
    {
        var submission = new Mock<IFinanzOnlineSubmissionService>(MockBehavior.Strict);
        var client = CreateReal(CompleteRealOptions(), submission.Object);
        var result = await client.SubmitJahresbelegAsync(new RksvFinanzOnlineSubmissionPayload
        {
            CashRegisterId = Guid.NewGuid(),
            RegisterNumber = "R1",
            ReceiptNumber = "B1",
            QrPayload = "not-a-valid-beleg",
            TimestampUtc = DateTimeOffset.UtcNow,
        });
        Assert.False(result.Success);
        Assert.Equal(RksvFinanzOnlineSubmissionKnownErrorCodes.BelegInvalid, result.ErrorCode);
    }

    [Fact]
    public async Task Real_WhenEnabledAndComplete_SubmitsBelegpruefungViaSubmissionService()
    {
        var beleg = ValidDepBeleg();
        FinanzOnlineRegisterSubmissionRequest? captured = null;
        var submission = new Mock<IFinanzOnlineSubmissionService>();
        submission
            .Setup(s => s.SubmitAsync(It.IsAny<FinanzOnlineRegisterSubmissionRequest>(), It.IsAny<CancellationToken>()))
            .Callback<FinanzOnlineRegisterSubmissionRequest, CancellationToken>((req, _) => captured = req)
            .ReturnsAsync(new FinanzOnlineRegisterSubmissionResponse
            {
                Success = true,
                TransmissionId = "TX-1",
                Status = "Accepted",
            });

        var client = CreateReal(CompleteRealOptions(), submission.Object);
        var result = await client.SubmitStartbelegAsync(new RksvFinanzOnlineSubmissionPayload
        {
            CashRegisterId = Guid.NewGuid(),
            RegisterNumber = "R1",
            ReceiptNumber = "B1",
            QrPayload = beleg,
            TimestampUtc = DateTimeOffset.Parse("2026-01-01T12:00:00Z"),
            TenantId = "tenant1",
            CompanyTaxNumber = "ATU12345678",
        });

        Assert.True(result.Success);
        Assert.Equal("TX-1", result.ExternalReference);
        Assert.Equal(RksvSpecialReceiptFinanzOnlineSubmissionStatuses.Verified, result.VerificationStatus);
        Assert.NotNull(captured);
        Assert.Equal(FinanzOnlineIntegrationMode.TEST, captured!.Mode);
        Assert.NotNull(captured.RkdbBelegpruefung);
        Assert.Equal(beleg, captured.RkdbBelegpruefung!.Beleg);
        Assert.Equal("ATU12345678", captured.RkdbBelegpruefung.Kundeninfo);
        Assert.DoesNotContain(beleg, result.RawResponseSnapshot ?? "", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Real_SubmitMonatsbelegAsync_ReturnsNotRequired()
    {
        var client = CreateReal(CompleteRealOptions());
        var result = await client.SubmitMonatsbelegAsync(new RksvFinanzOnlineSubmissionPayload
        {
            CashRegisterId = Guid.NewGuid(),
            ReceiptNumber = "M1",
            QrPayload = ValidDepBeleg(),
        });
        Assert.False(result.Success);
        Assert.Equal(RksvFinanzOnlineSubmissionKnownErrorCodes.MonatsbelegNotRequired, result.ErrorCode);
        Assert.Equal(RksvSpecialReceiptFinanzOnlineSubmissionStatuses.NotRequired, result.VerificationStatus);
    }

    [Fact]
    public async Task Fake_SubmitMonatsbelegAsync_ReturnsNotRequired()
    {
        var client = CreateFake(new RksvFinanzOnlineSubmissionClientOptions { FakeSuccess = true });
        var result = await client.SubmitMonatsbelegAsync(new RksvFinanzOnlineSubmissionPayload
        {
            CashRegisterId = Guid.NewGuid(),
            ReceiptNumber = "M1",
            QrPayload = ValidDepBeleg(),
        });
        Assert.False(result.Success);
        Assert.Equal(RksvFinanzOnlineSubmissionKnownErrorCodes.MonatsbelegNotRequired, result.ErrorCode);
        Assert.Equal(RksvSpecialReceiptFinanzOnlineSubmissionStatuses.NotRequired, result.VerificationStatus);
    }
}

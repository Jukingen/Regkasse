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

    private static RksvFinanzOnlineSubmissionClient CreateReal(RksvFinanzOnlineSubmissionClientOptions options)
    {
        var monitor = new Mock<IOptionsMonitor<RksvFinanzOnlineSubmissionClientOptions>>();
        monitor.Setup(m => m.CurrentValue).Returns(options);
        return new RksvFinanzOnlineSubmissionClient(
            monitor.Object,
            Mock.Of<ILogger<RksvFinanzOnlineSubmissionClient>>());
    }

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
    public async Task Real_WhenEnabledButIncomplete_ReturnsConfigIncomplete()
    {
        var client = CreateReal(new RksvFinanzOnlineSubmissionClientOptions
        {
            Enabled = true,
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
    public async Task Real_WhenEnabledAndComplete_ReturnsSoapNotImplementedWithoutOutboundCall()
    {
        var client = CreateReal(new RksvFinanzOnlineSubmissionClientOptions
        {
            Enabled = true,
            EndpointUrl = "https://example.invalid/rksv-soap",
            TimeoutSeconds = 60,
            Environment = RksvFinanzOnlineSubmissionDeploymentEnvironment.Test,
            ParticipantCredentialsConfigurationKey = "FinanzOnline:ParticipantRef",
            ClientCertificateSecretName = "kv/rksv-client-cert",
            AllowOutboundNetworkCalls = true,
        });
        var result = await client.SubmitJahresbelegAsync(new RksvFinanzOnlineSubmissionPayload
        {
            CashRegisterId = Guid.NewGuid(),
            RegisterNumber = "R1",
            ReceiptNumber = "B1",
            QrPayload = "x",
            TimestampUtc = DateTimeOffset.UtcNow,
        });
        Assert.False(result.Success);
        Assert.Equal(RksvFinanzOnlineSubmissionKnownErrorCodes.SoapTransportNotImplemented, result.ErrorCode);
        Assert.Contains("example.invalid", result.RawResponseSnapshot ?? "", StringComparison.OrdinalIgnoreCase);
    }
}

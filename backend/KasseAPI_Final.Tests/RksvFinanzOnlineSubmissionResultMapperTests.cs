using KasseAPI_Final.Models;
using KasseAPI_Final.Services.FinanzOnlineIntegration;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class RksvFinanzOnlineSubmissionResultMapperTests
{
    [Fact]
    public void FromRegistrierkassenResponse_Success_MapsVerified()
    {
        var mapped = RksvFinanzOnlineSubmissionResultMapper.FromRegistrierkassenResponse(
            new FinanzOnlineRegisterSubmissionResponse
            {
                Success = true,
                TransmissionId = "TX-99",
                Status = "Accepted",
            },
            "Startbeleg");

        Assert.True(mapped.Success);
        Assert.Equal("TX-99", mapped.ExternalReference);
        Assert.Equal(RksvSpecialReceiptFinanzOnlineSubmissionStatuses.Verified, mapped.VerificationStatus);
        Assert.Null(mapped.ErrorCode);
        Assert.DoesNotContain("pin", mapped.RawResponseSnapshot ?? "", StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void FromRegistrierkassenResponse_Failure_PreservesErrorCode()
    {
        var mapped = RksvFinanzOnlineSubmissionResultMapper.FromRegistrierkassenResponse(
            new FinanzOnlineRegisterSubmissionResponse
            {
                Success = false,
                Status = "Rejected",
                ErrorCode = "RKDB_RC_123",
                ErrorMessage = "Beleg rejected",
            },
            "Jahresbeleg");

        Assert.False(mapped.Success);
        Assert.Equal("Rejected", mapped.VerificationStatus);
        Assert.Equal("RKDB_RC_123", mapped.ErrorCode);
        Assert.Contains("Beleg rejected", mapped.ErrorMessage, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("HTTP_503", true)]
    [InlineData("TRANSIENT_NETWORK_FAILURE", true)]
    [InlineData("FAKE_RKSV_SUBMISSION_FAILED", true)]
    [InlineData(null, true)]
    [InlineData(RksvFinanzOnlineSubmissionKnownErrorCodes.OutboundDisabled, false)]
    [InlineData(RksvFinanzOnlineSubmissionKnownErrorCodes.BelegInvalid, false)]
    [InlineData(RksvFinanzOnlineSubmissionKnownErrorCodes.ConfigIncomplete, false)]
    [InlineData(RksvFinanzOnlineSubmissionKnownErrorCodes.MonatsbelegNotImplemented, false)]
    [InlineData(RksvFinanzOnlineSubmissionKnownErrorCodes.MonatsbelegNotRequired, false)]
    [InlineData("RKDB_RC_1", false)]
    [InlineData("SESSION_EXPIRED", false)]
    public void IsTransientVsPermanent_Classification(string? code, bool expectTransient)
    {
        Assert.Equal(expectTransient, RksvFinanzOnlineSubmissionResultMapper.IsTransientErrorCode(code));
        if (expectTransient)
            Assert.False(RksvFinanzOnlineSubmissionResultMapper.IsPermanentErrorCode(code));
        else
            Assert.True(RksvFinanzOnlineSubmissionResultMapper.IsPermanentErrorCode(code));
    }
}

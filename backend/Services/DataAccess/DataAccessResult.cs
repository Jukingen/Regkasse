using KasseAPI_Final.Services.DataRights;

namespace KasseAPI_Final.Services.DataAccess;

public sealed class DataAccessResult
{
    public bool Succeeded { get; init; }
    public bool IsPending { get; init; }
    public string? Error { get; init; }
    public string? ErrorCode { get; init; }
    public DataAccessRequest? Request { get; init; }
    public TenantDataRightsRequestDto? Rights { get; init; }

    public static DataAccessResult Success(
        DataAccessRequest request,
        TenantDataRightsRequestDto? rights = null) =>
        new()
        {
            Succeeded = true,
            IsPending = false,
            Request = request,
            Rights = rights,
        };

    public static DataAccessResult Pending(
        DataAccessRequest request,
        TenantDataRightsRequestDto? rights = null) =>
        new()
        {
            Succeeded = true,
            IsPending = true,
            Request = request,
            Rights = rights,
        };

    public static DataAccessResult Fail(string error, string? errorCode = null) =>
        new()
        {
            Succeeded = false,
            IsPending = false,
            Error = error,
            ErrorCode = errorCode,
        };
}

public static class DataAccessErrorCodes
{
    public const string UnknownType = "UNKNOWN_TYPE";
    public const string NotFound = "NOT_FOUND";
    public const string ProcessingFailed = "PROCESSING_FAILED";
}

using KasseAPI_Final.DTOs;

namespace KasseAPI_Final.Services;

public sealed class FiskalyBatchException : Exception
{
    public string Code { get; }

    public int StatusCode { get; }

    public int? MaxItems { get; }

    public FiskalyBatchException(string code, string message, int statusCode = 400, int? maxItems = null)
        : base(message)
    {
        Code = code;
        StatusCode = statusCode;
        MaxItems = maxItems;
    }
}

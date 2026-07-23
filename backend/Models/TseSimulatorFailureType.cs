namespace KasseAPI_Final.Models;

/// <summary>Development-only TSE failure scenarios (device row / probe simulation).</summary>
public enum TseSimulatorFailureType
{
    NetworkTimeout = 0,
    ConnectionLost = 1,
    CertificateInvalid = 2,
    SignatureError = 3,
    RateLimitExceeded = 4,
    InternalServerError = 5,
}

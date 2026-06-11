namespace KasseAPI_Final.Tse;

/// <summary>
/// RKSV algorithm suite constants (Detailspezifikation Abs. 2 — R1-AT1 open system).
/// </summary>
public static class RksvSuite
{
    public const string SuiteId = "R1-AT1";
    public const string HashAlgorithm = "SHA-256";
    public const int ChainingBytesExtracted = 8;
    public const int TurnoverCounterLengthBytes = 8;
}

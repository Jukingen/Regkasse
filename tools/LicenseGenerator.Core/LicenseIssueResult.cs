namespace Regkasse.LicenseTools;

/// <summary>Output of issuing one license (short REGK key + RS256 JWT for API activation).</summary>
public sealed record LicenseIssueResult(
    string LicenseKey,
    string SignedPayload,
    string CanonicalPayload,
    string PublicKeyPem,
    DateTimeOffset ExpiresAtUtc);

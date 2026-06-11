using System.Security.Cryptography;

namespace KasseAPI_Final.Tse.Fiskaly;

/// <summary>
/// ECDsa wrapper: verify locally with the SCU public key; sign by delegating to fiskaly (no private key export).
/// </summary>
internal sealed class FiskalyDelegatedSigningEcdsa : ECDsa
{
    private readonly IFiskalyClient _client;
    private readonly string _signatureCreationUnitId;
    private readonly ECDsa _verifyKey;

    public FiskalyDelegatedSigningEcdsa(
        IFiskalyClient client,
        string signatureCreationUnitId,
        ECDsa verifyKey)
    {
        _client = client;
        _signatureCreationUnitId = signatureCreationUnitId;
        _verifyKey = verifyKey;
    }

    public override byte[] SignHash(byte[] hash) =>
        _client.SignSha256HashAsync(hash, _signatureCreationUnitId, CancellationToken.None)
            .GetAwaiter()
            .GetResult();

    public override bool VerifyHash(byte[] hash, byte[] signature) =>
        _verifyKey.VerifyHash(hash, signature);

    public override ECParameters ExportParameters(bool includePrivateParameters)
    {
        if (includePrivateParameters)
            throw new CryptographicException("fiskaly SCU private key is not exportable.");

        return _verifyKey.ExportParameters(includePrivateParameters: false);
    }

    public override void ImportParameters(ECParameters parameters) =>
        throw new CryptographicException("fiskaly SCU key material cannot be imported.");

    public override void GenerateKey(ECCurve curve) =>
        throw new CryptographicException("fiskaly SCU key material is provisioned remotely.");

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _verifyKey.Dispose();

        base.Dispose(disposing);
    }
}

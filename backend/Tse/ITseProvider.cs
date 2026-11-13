namespace KasseAPI_Final.Tse
{
    /// <summary>
    /// Pluggable TSE signing backend for closing flows (daily/monthly/yearly). Real hardware/software pipeline vs dev-only fake JWS.
    /// </summary>
    public interface ITseProvider
    {
        /// <summary>Produces a compact JWS-like string and certificate label for persistence.</summary>
        Task<TseSignResult> SignAsync(BelegdatenPayload payload, string correlationId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Fake mode: always true. Real mode: TSE device row exists, connected, and can sign.
        /// </summary>
        Task<bool> IsReadyAsync(CancellationToken cancellationToken = default);
    }

    /// <param name="CompactJws">Full compact serialization stored in DB (may be long).</param>
    /// <param name="CertificateSerialNumber">Human-readable certificate / serial label for audit rows.</param>
    public sealed record TseSignResult(string CompactJws, string CertificateSerialNumber);
}

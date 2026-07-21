using KasseAPI_Final.Data;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Tse
{
    /// <summary>
    /// Production path: RKSV <see cref="SignaturePipeline"/> + key material; readiness follows configured TSE device row.
    /// </summary>
    public sealed class RealTseProvider : ITseProvider
    {
        private readonly SignaturePipeline _pipeline;
        private readonly ITseKeyProvider _keyProvider;
        private readonly AppDbContext _context;
        private readonly ILogger<RealTseProvider> _logger;

        public RealTseProvider(
            SignaturePipeline pipeline,
            ITseKeyProvider keyProvider,
            AppDbContext context,
            ILogger<RealTseProvider> logger)
        {
            _pipeline = pipeline;
            _keyProvider = keyProvider;
            _context = context;
            _logger = logger;
        }

        public Task<TseSignResult> SignAsync(BelegdatenPayload payload, string correlationId, CancellationToken cancellationToken = default)
        {
            var compact = _pipeline.Sign(payload, correlationId);
            var serial = _keyProvider.GetCertificateSerialNumber() ?? "UNKNOWN";
            return Task.FromResult(new TseSignResult(compact, serial));
        }

        public async Task<bool> IsReadyAsync(CancellationToken cancellationToken = default)
        {
            var device = await _context.TseDevices
                .AsNoTracking()
                .OrderBy(t => t.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);

            if (device == null)
            {
                _logger.LogWarning("RealTseProvider.IsReadyAsync: no TSE device row");
                return false;
            }

            var ready = device.IsConnected && device.CanCreateInvoices && device.IsActive;
            if (!ready)
                _logger.LogWarning(
                    "RealTseProvider.IsReadyAsync: device not ready (connected={Connected}, canSign={CanSign}, active={Active})",
                    device.IsConnected, device.CanCreateInvoices, device.IsActive);

            return ready;
        }
    }
}

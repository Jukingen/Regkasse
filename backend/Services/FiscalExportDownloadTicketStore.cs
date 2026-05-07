using Microsoft.Extensions.Caching.Memory;

namespace KasseAPI_Final.Services;

/// <summary>Memory-backed tickets (single tab / server process; not for multi-node without shared cache).</summary>
public sealed class FiscalExportDownloadTicketStore : IFiscalExportDownloadTicketStore
{
    private const string CacheKeyPrefix = "fiscal-export-download:";
    private static readonly TimeSpan TicketTtl = TimeSpan.FromMinutes(30);

    private readonly IMemoryCache _cache;

    public FiscalExportDownloadTicketStore(IMemoryCache cache)
    {
        _cache = cache;
    }

    public Guid CreateTicket(FiscalExportDownloadTicket ticket)
    {
        var id = Guid.NewGuid();
        var key = CacheKeyPrefix + id.ToString("D");
        _cache.Set(
            key,
            ticket,
            new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = TicketTtl });
        return id;
    }

    public bool TryConsume(Guid exportId, out FiscalExportDownloadTicket? ticket)
    {
        var key = CacheKeyPrefix + exportId.ToString("D");
        if (_cache.TryGetValue(key, out FiscalExportDownloadTicket? cached) && cached != null)
        {
            _cache.Remove(key);
            ticket = cached;
            return true;
        }

        ticket = null;
        return false;
    }
}

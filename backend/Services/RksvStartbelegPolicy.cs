using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services;

/// <inheritdoc />
public sealed class RksvStartbelegPolicy : IRksvStartbelegPolicy
{
    private readonly AppDbContext _db;
    private readonly TseOptions _tseOptions;

    public RksvStartbelegPolicy(AppDbContext db, IOptions<TseOptions> tseOptions)
    {
        _db = db;
        _tseOptions = tseOptions.Value;
    }

    /// <inheritdoc />
    public bool SessionGateApplies => !_tseOptions.IsOff && !_tseOptions.UseSoftTseWhenNoDevice;

    /// <inheritdoc />
    public Task<bool> HasStartbelegForRegisterAsync(Guid cashRegisterId, CancellationToken cancellationToken = default) =>
        _db.CashRegisters.AsNoTracking()
            .AnyAsync(
                r => r.Id == cashRegisterId && r.StartbelegCreatedAt != null,
                cancellationToken);
}

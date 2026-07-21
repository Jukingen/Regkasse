using Microsoft.EntityFrameworkCore.Storage;

namespace KasseAPI_Final.Services
{
    /// <summary>
    /// Allocates sequential BelegNr per (CashRegisterId, date). Format: AT-{RegisterNumber}-{yyyyMMdd}-{seq}.
    /// </summary>
    public interface IReceiptSequenceService
    {
        /// <summary>Allocates next BelegNr for the given register UUID and fiscal display number.</summary>
        Task<string> AllocateNextBelegNrAsync(Guid cashRegisterId, string registerNumber, DateTime sequenceDate);

        /// <summary>Same as AllocateNextBelegNrAsync but participates in the active transaction.</summary>
        Task<string> AllocateNextBelegNrInTransactionAsync(IDbContextTransaction transaction, Guid cashRegisterId, string registerNumber, DateTime sequenceDate);
    }
}

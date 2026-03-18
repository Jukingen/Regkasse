using System;
using System.Threading.Tasks;

namespace KasseAPI_Final.Services
{
    /// <summary>
    /// Single authority for allocating the next sequential BelegNr (AT-{KassenId}-{YYYYMMDD}-{SEQ}) per register per day.
    /// Allocated numbers are never reused; daily reset; gaps allowed.
    /// </summary>
    public interface IReceiptSequenceService
    {
        /// <summary>
        /// Allocates the next sequence number for the given register and date, and returns the full BelegNr string.
        /// Format: AT-{kassenId}-{yyyyMMdd}-{seq} with numeric seq. Thread-safe and transaction-safe.
        /// </summary>
        /// <param name="kassenId">Cash register identifier (required).</param>
        /// <param name="sequenceDate">Calendar date for the sequence (typically UTC today).</param>
        /// <returns>Full receipt number, e.g. AT-KASSE01-20260318-1.</returns>
        Task<string> AllocateNextBelegNrAsync(string kassenId, DateTime sequenceDate);
    }
}

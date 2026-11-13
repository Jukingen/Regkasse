using System.Data;
using Npgsql;

namespace KasseAPI_Final.Services
{
    /// <summary>
    /// Session-level PostgreSQL advisory locks per cash register so replay for the same register serializes across API instances.
    /// Uses try-lock + retry with configurable max wait to avoid deadlock-like indefinite blocking.
    /// </summary>
    public static class OfflineReplayRegisterLock
    {
        private const string TryLockSql = "SELECT pg_try_advisory_lock(@k1::integer, @k2::integer)";
        private const string UnlockSql = "SELECT pg_advisory_unlock(@k1::integer, @k2::integer)";

        /// <summary>Default max wait ms when not specified (10s).</summary>
        public const int DefaultMaxLockWaitMs = 10_000;

        /// <summary>Default retry interval ms when not specified (100ms).</summary>
        public const int DefaultLockRetryIntervalMs = 100;

        public static (int K1, int K2) ToAdvisoryKey(Guid cashRegisterId)
        {
            var b = cashRegisterId.ToByteArray();
            var k1 = BitConverter.ToInt32(b, 0) ^ BitConverter.ToInt32(b, 4);
            var k2 = BitConverter.ToInt32(b, 8) ^ BitConverter.ToInt32(b, 12);
            return (k1, k2);
        }

        /// <summary>
        /// Acquires locks for all distinct registers in sorted order (deadlock-safe) using try-lock + retry.
        /// Throws <see cref="OfflineReplayLockTimeoutException"/> if not acquired within maxWaitMs.
        /// Caller must dispose the returned scope. WaitDurationMs on scope is set for logging.
        /// </summary>
        /// <param name="cashRegisterIds">Register IDs to lock (will be deduplicated and sorted).</param>
        /// <param name="connectionString">PostgreSQL connection string.</param>
        /// <param name="cancellationToken">Cancellation.</param>
        /// <param name="maxWaitMs">Max total wait in ms (default 10000).</param>
        /// <param name="retryIntervalMs">Delay between try attempts in ms (default 100).</param>
        public static async Task<OfflineReplayRegisterLockScope> AcquireAsync(
            IEnumerable<Guid> cashRegisterIds,
            string connectionString,
            CancellationToken cancellationToken = default,
            int maxWaitMs = DefaultMaxLockWaitMs,
            int retryIntervalMs = DefaultLockRetryIntervalMs)
        {
            var registerIds = cashRegisterIds.Distinct().OrderBy(x => x).ToList();
            var orderedKeys = registerIds.Select(ToAdvisoryKey).ToList();
            if (orderedKeys.Count == 0)
                throw new ArgumentException("At least one cash register id is required.", nameof(cashRegisterIds));

            var conn = new NpgsqlConnection(connectionString);
            await conn.OpenAsync(cancellationToken).ConfigureAwait(false);

            var start = DateTime.UtcNow;
            var acquired = new List<(int K1, int K2)>();

            try
            {
                while (true)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var elapsedMs = (int)(DateTime.UtcNow - start).TotalMilliseconds;
                    if (elapsedMs >= maxWaitMs)
                    {
                        await ReleaseAllAsync(conn, acquired).ConfigureAwait(false);
                        throw new OfflineReplayLockTimeoutException(elapsedMs, registerIds);
                    }

                    acquired.Clear();
                    var allAcquired = true;
                    foreach (var (k1, k2) in orderedKeys)
                    {
                        var got = await TryLockOnceAsync(conn, k1, k2, cancellationToken).ConfigureAwait(false);
                        if (!got)
                        {
                            allAcquired = false;
                            break;
                        }
                        acquired.Add((k1, k2));
                    }

                    if (allAcquired)
                    {
                        var waitDurationMs = (int)(DateTime.UtcNow - start).TotalMilliseconds;
                        return new OfflineReplayRegisterLockScope(conn, orderedKeys, waitDurationMs);
                    }

                    await ReleaseAllAsync(conn, acquired).ConfigureAwait(false);
                    await Task.Delay(Math.Min(retryIntervalMs, Math.Max(0, maxWaitMs - elapsedMs)), cancellationToken).ConfigureAwait(false);
                }
            }
            catch
            {
                await conn.DisposeAsync().ConfigureAwait(false);
                throw;
            }
        }

        private static async Task<bool> TryLockOnceAsync(NpgsqlConnection conn, int k1, int k2, CancellationToken cancellationToken)
        {
            await using var cmd = new NpgsqlCommand(TryLockSql, conn);
            cmd.Parameters.AddWithValue("k1", k1);
            cmd.Parameters.AddWithValue("k2", k2);
            var result = await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            return result is bool b && b;
        }

        private static async Task ReleaseAllAsync(NpgsqlConnection conn, List<(int K1, int K2)> keys)
        {
            for (var i = keys.Count - 1; i >= 0; i--)
            {
                var (k1, k2) = keys[i];
                try
                {
                    await using var cmd = new NpgsqlCommand(UnlockSql, conn);
                    cmd.Parameters.AddWithValue("k1", k1);
                    cmd.Parameters.AddWithValue("k2", k2);
                    await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
                }
                catch
                {
                    /* best-effort unlock */
                }
            }
        }

        public sealed class OfflineReplayRegisterLockScope : IAsyncDisposable
        {
            private NpgsqlConnection? _conn;
            private readonly List<(int K1, int K2)> _keys;

            /// <summary>Time spent waiting to acquire the lock (ms). Zero if acquired immediately.</summary>
            public int WaitDurationMs { get; }

            internal OfflineReplayRegisterLockScope(NpgsqlConnection conn, List<(int K1, int K2)> keys, int waitDurationMs = 0)
            {
                _conn = conn;
                _keys = keys;
                WaitDurationMs = waitDurationMs;
            }

            public async ValueTask DisposeAsync()
            {
                var conn = Interlocked.Exchange(ref _conn, null);
                if (conn == null)
                    return;

                if (conn.State == ConnectionState.Open)
                {
                    for (var i = _keys.Count - 1; i >= 0; i--)
                    {
                        var (k1, k2) = _keys[i];
                        try
                        {
                            await using var cmd = new NpgsqlCommand(UnlockSql, conn);
                            cmd.Parameters.AddWithValue("k1", k1);
                            cmd.Parameters.AddWithValue("k2", k2);
                            await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
                        }
                        catch
                        {
                            /* best-effort unlock */
                        }
                    }
                }

                await conn.DisposeAsync().ConfigureAwait(false);
            }
        }
    }
}

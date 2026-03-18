using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Npgsql;

namespace KasseAPI_Final.Services
{
    /// <summary>
    /// Session-level PostgreSQL advisory locks per cash register so replay for the same register serializes across API instances.
    /// </summary>
    public static class OfflineReplayRegisterLock
    {
        private const string LockSql = "SELECT pg_advisory_lock(@k1::integer, @k2::integer)";
        private const string UnlockSql = "SELECT pg_advisory_unlock(@k1::integer, @k2::integer)";

        public static (int K1, int K2) ToAdvisoryKey(Guid cashRegisterId)
        {
            var b = cashRegisterId.ToByteArray();
            var k1 = BitConverter.ToInt32(b, 0) ^ BitConverter.ToInt32(b, 4);
            var k2 = BitConverter.ToInt32(b, 8) ^ BitConverter.ToInt32(b, 12);
            return (k1, k2);
        }

        /// <summary>
        /// Acquires locks for all distinct registers in sorted order (deadlock-safe). Caller must dispose the returned scope.
        /// </summary>
        public static async Task<OfflineReplayRegisterLockScope> AcquireAsync(
            IEnumerable<Guid> cashRegisterIds,
            string connectionString,
            CancellationToken cancellationToken = default)
        {
            var orderedKeys = cashRegisterIds
                .Distinct()
                .OrderBy(x => x)
                .Select(ToAdvisoryKey)
                .ToList();

            var conn = new NpgsqlConnection(connectionString);
            await conn.OpenAsync(cancellationToken).ConfigureAwait(false);

            try
            {
                foreach (var (k1, k2) in orderedKeys)
                {
                    await using var cmd = new NpgsqlCommand(LockSql, conn);
                    cmd.Parameters.AddWithValue("k1", k1);
                    cmd.Parameters.AddWithValue("k2", k2);
                    await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                }

                return new OfflineReplayRegisterLockScope(conn, orderedKeys);
            }
            catch
            {
                await conn.DisposeAsync();
                throw;
            }
        }

        public sealed class OfflineReplayRegisterLockScope : IAsyncDisposable
        {
            private NpgsqlConnection? _conn;
            private readonly List<(int K1, int K2)> _keys;

            internal OfflineReplayRegisterLockScope(NpgsqlConnection conn, List<(int K1, int K2)> keys)
            {
                _conn = conn;
                _keys = keys;
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

using Xunit;

namespace KasseAPI_Final.Tests;

/// <summary>
/// Shared PostgreSQL container; tests in this collection run sequentially to avoid cross-test interference.
/// </summary>
[CollectionDefinition("PostgreSqlReplay")]
public sealed class PostgreSqlReplayCollection : ICollectionFixture<PostgreSqlReplayFixture>
{
}

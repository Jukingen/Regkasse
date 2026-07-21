using Xunit;

namespace KasseAPI_Final.Tests;

/// <summary>
/// Serializes WebApplicationFactory hosts that toggle process-wide
/// <c>REGKASSE_OPENAPI_EXPORT</c> / <c>REGKASSE_TEST_INMEMORY_DB</c>.
/// Parallel hosts overwrite those env vars and break license middleware + login.
/// </summary>
[CollectionDefinition("OpenApiExportWebHost", DisableParallelization = true)]
public sealed class OpenApiExportWebHostCollection;

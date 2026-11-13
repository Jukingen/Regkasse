using Xunit;

namespace KasseAPI_Final.Tests;

/// <summary>
/// Serializes WebApplicationFactory hosts that toggle process-wide
/// <c>REGKASSE_OPENAPI_EXPORT</c> / <c>REGKASSE_TEST_INMEMORY_DB</c>, plus unit tests that require
/// those flags off (license enforcement). Parallel hosts leak export mode into payment/license tests.
/// </summary>
[CollectionDefinition("OpenApiExportWebHost", DisableParallelization = true)]
public sealed class OpenApiExportWebHostCollection;

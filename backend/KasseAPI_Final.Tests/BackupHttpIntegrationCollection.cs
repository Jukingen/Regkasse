using Xunit;

namespace KasseAPI_Final.Tests;

/// <summary>
/// Serializes backup HTTP integration tests — shared <see cref="BackupImportWebApplicationFactory"/> and one in-memory DB.
/// </summary>
[CollectionDefinition("BackupHttpIntegration", DisableParallelization = true)]
public sealed class BackupHttpIntegrationCollection : ICollectionFixture<BackupImportWebApplicationFactory>;

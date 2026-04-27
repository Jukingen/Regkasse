using KasseAPI_Final.Data;
using KasseAPI_Final.Services;
using KasseAPI_Final.Tse;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

/// <summary>
/// <see cref="KasseAPI_Final.Services.TseService.CreateInvoiceSignatureAsync"/> uses PostgreSQL-only
/// <c>signature_chain_state</c> SQL; this is the minimal live-DB check that signing + validation work.
/// When Docker / REGKASSE_TEST_POSTGRES is unavailable, the test is skipped.
/// </summary>
[Collection("PostgreSqlReplay")]
[Trait("Category", "PostgreSql")]
public sealed class TseServiceSignatureChainPostgreSqlTests
{
    private readonly PostgreSqlReplayFixture _fixture;

    public TseServiceSignatureChainPostgreSqlTests(PostgreSqlReplayFixture fixture) => _fixture = fixture;

    private AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseAppNpgsql(_fixture.ConnectionString).Options);

    private static TseService CreateTseService(AppDbContext context, SignaturePipeline pipeline, SoftwareTseKeyProvider keyProvider)
    {
        var closing = new RealTseProvider(pipeline, keyProvider, context, NullLogger<RealTseProvider>.Instance);
        return new TseService(context, pipeline, keyProvider, closing, Mock.Of<ILogger<TseService>>());
    }

    [SkippableFact]
    public async Task CreateInvoiceSignature_OnPostgres_PersistsChain_AndValidates()
    {
        Skip.IfNot(_fixture.HasDatabase, _fixture.SkipReason);

        await using var context = CreateContext();

        var keyProvider = new SoftwareTseKeyProvider();
        var pipeline = new SignaturePipeline(keyProvider, Mock.Of<ILogger<SignaturePipeline>>());
        var tseService = CreateTseService(context, pipeline, keyProvider);

        var cashRegisterId = Guid.NewGuid();
        var receiptNumber = $"AT-TSE-PG-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..8]}";
        const string kassenId = "KASSE-PG-01";
        const decimal totalAmount = 10.00m;

        var sigResult = await tseService.CreateInvoiceSignatureAsync(
            cashRegisterId,
            receiptNumber,
            totalAmount,
            kassenId,
            taxDetailsJson: "{}");

        Assert.NotNull(sigResult.CompactJws);
        Assert.Equal(3, sigResult.CompactJws.Split('.').Length);

        var valid = await tseService.ValidateTseSignatureAsync(sigResult.CompactJws);
        Assert.True(valid);
    }
}

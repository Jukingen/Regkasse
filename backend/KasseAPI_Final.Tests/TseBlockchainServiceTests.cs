using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Tse;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class TseBlockchainServiceTests
{
    [Fact]
    public async Task StoreSignatureAsync_CreatesSimulatedAnchor()
    {
        await using var db = CreateDb();
        var tenantId = await SeedTenantAsync(db);
        var svc = new TseBlockchainService(db, NullLogger<TseBlockchainService>.Instance);

        var record = await svc.StoreSignatureAsync(new TseBlockchainSignatureDataDto
        {
            TenantId = tenantId,
            SignatureData = "header.payload.signature",
            SourceType = "Receipt",
        });

        Assert.True(record.IsSimulated);
        Assert.True(record.IsVerified);
        Assert.Equal(64, record.TransactionHash.Length);
        Assert.Equal(1, record.BlockNumber);
        Assert.Equal(1, await db.TseBlockchainRecords.CountAsync());
    }

    [Fact]
    public async Task StoreSignatureAsync_IsIdempotentOnSameHash()
    {
        await using var db = CreateDb();
        var tenantId = await SeedTenantAsync(db);
        var svc = new TseBlockchainService(db, NullLogger<TseBlockchainService>.Instance);
        var data = new TseBlockchainSignatureDataDto
        {
            TenantId = tenantId,
            SignatureData = "same-jws",
        };

        var a = await svc.StoreSignatureAsync(data);
        var b = await svc.StoreSignatureAsync(data);

        Assert.Equal(a.Id, b.Id);
        Assert.Equal(1, await db.TseBlockchainRecords.CountAsync());
    }

    [Fact]
    public async Task VerifyAndList_Work()
    {
        await using var db = CreateDb();
        var tenantId = await SeedTenantAsync(db);
        var svc = new TseBlockchainService(db, NullLogger<TseBlockchainService>.Instance);
        var record = await svc.StoreSignatureAsync(new TseBlockchainSignatureDataDto
        {
            TenantId = tenantId,
            SignatureData = "abc.def.ghi",
        });

        var verify = await svc.VerifySignatureAsync(record.Id);
        Assert.True(verify.IsVerified);
        Assert.True(verify.DiagnosticOnly);

        var txs = await svc.GetTransactionsAsync(tenantId);
        Assert.Single(txs);
        Assert.Equal(record.TransactionHash, txs[0].TransactionHash);
    }

    [Fact]
    public async Task SyncBlockchainAsync_MarksConnected()
    {
        await using var db = CreateDb();
        var svc = new TseBlockchainService(db, NullLogger<TseBlockchainService>.Instance);
        var status = await svc.SyncBlockchainAsync();
        Assert.Equal("connected", status.BlockchainStatus);
        Assert.Equal("regkasse-sim", status.NetworkName);
        Assert.True(status.IsSimulated);
    }

    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"tse_bc_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options, NullCurrentTenantAccessor.Instance);
    }

    private static async Task<Guid> SeedTenantAsync(AppDbContext db)
    {
        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Chain Cafe",
            Slug = "chain-cafe",
            Status = TenantStatuses.Active,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
        return tenantId;
    }
}

using System.Reflection;
using KasseAPI_Final.Data;
using KasseAPI_Final.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class TseHealthSampleResponseTimeMsMigrationTests
{
    [Fact]
    public void AddTseHealthSampleResponseTimeMs_IsDiscoveredByEf()
    {
        var attr = typeof(AddTseHealthSampleResponseTimeMs).GetCustomAttribute<MigrationAttribute>();
        Assert.NotNull(attr);
        Assert.Equal("20260723280000_AddTseHealthSampleResponseTimeMs", attr!.Id);

        var ctxAttr = typeof(AddTseHealthSampleResponseTimeMs).GetCustomAttribute<DbContextAttribute>();
        Assert.NotNull(ctxAttr);
        Assert.Equal(typeof(AppDbContext), ctxAttr!.ContextType);
    }
}

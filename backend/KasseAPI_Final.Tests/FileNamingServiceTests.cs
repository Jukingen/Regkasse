using KasseAPI_Final.Services;
using KasseAPI_Final.Tenancy;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class FileNamingServiceTests
{
    [Fact]
    public void GenerateFileName_uses_ambient_slug_and_stamp()
    {
        var accessor = new CurrentTenantAccessor { TenantId = Guid.NewGuid(), TenantSlug = "cafe" };
        var svc = new FileNamingService(accessor);
        var at = new DateTime(2026, 7, 22, 14, 30, 22);

        var name = svc.GenerateFileName("product", "csv", at: at);

        Assert.Equal("product_cafe_20260722_143022.csv", name);
    }

    [Fact]
    public void GenerateFileName_includes_register_and_additional()
    {
        var accessor = new CurrentTenantAccessor { TenantSlug = "cafe" };
        var svc = new FileNamingService(accessor);
        var at = new DateTime(2026, 7, 22, 14, 30, 22);

        var name = svc.GenerateFileName("dep", "json", registerId: "K1", additional: "202607", at: at);

        Assert.Equal("dep_cafe_K1_202607_20260722_143022.json", name);
    }

    [Fact]
    public void GenerateFileName_falls_back_to_unknown_without_slug()
    {
        var svc = new FileNamingService(new CurrentTenantAccessor());
        var at = new DateTime(2026, 7, 22, 14, 30, 22);

        var name = svc.GenerateFileName("log", ".txt", at: at);

        Assert.Equal("log_unknown_20260722_143022.txt", name);
    }

    [Fact]
    public void GenerateFileName_sanitizes_unsafe_segments()
    {
        var accessor = new CurrentTenantAccessor { TenantSlug = "cafe/bad" };
        var svc = new FileNamingService(accessor);
        var at = new DateTime(2026, 7, 22, 14, 30, 22);

        var name = svc.GenerateFileName("my file", "CSV", registerId: "r 1", at: at);

        Assert.Equal("my_file_cafe_bad_r_1_20260722_143022.csv", name);
    }
}

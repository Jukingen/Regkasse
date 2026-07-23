using KasseAPI_Final.Configuration;
using KasseAPI_Final.Controllers;
using KasseAPI_Final.Data;
using KasseAPI_Final.Data.Repositories;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.DTOs;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.AdminProducts;
using KasseAPI_Final.Services.Dev;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class AdminProductsDevPurgeCatalogTests
{
    private sealed class FakeWebHostEnvironment : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;
        public string ApplicationName { get; set; } = "KasseAPI_Final.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public string WebRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"AdminProductsDevPurge_{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options, TenantTestDoubles.TenantAccessorReturning(LegacyDefaultTenantIds.Primary));
    }

    private static AdminProductsController CreateController(AppDbContext ctx, bool isDevelopment)
    {
        var env = new FakeWebHostEnvironment
        {
            EnvironmentName = isDevelopment ? Environments.Development : Environments.Production,
        };

        return new AdminProductsController(
            ctx,
            Mock.Of<IGenericRepository<Product>>(),
            NullLogger<AdminProductsController>.Instance,
            TenantTestDoubles.SettingsResolverReturning(LegacyDefaultTenantIds.Primary),
            env,
            Options.Create(new ProductMediaOptions()),
            new ProductImageThumbnailService(
                Options.Create(new ProductMediaOptions()),
                NullLogger<ProductImageThumbnailService>.Instance),
            Mock.Of<IDemoProductImportService>(),
            NullCurrentTenantAccessor.Instance,
            new AdminProductListService(ctx, TenantTestDoubles.SettingsResolverReturning(LegacyDefaultTenantIds.Primary)),
            Mock.Of<IProductService>(),
            Mock.Of<IProductExportService>(),
            Mock.Of<KasseAPI_Final.Services.Operations.IOperationLogService>());
    }

    [Fact]
    public async Task DevPurgeTenantCatalog_OutsideDevelopment_Returns403()
    {
        await using var ctx = CreateContext();
        var controller = CreateController(ctx, isDevelopment: false);

        var result = await controller.DevPurgeTenantCatalog(new DevTenantCatalogCleanupRequest
        {
            ConfirmPhrase = DevTenantCatalogCleanup.ConfirmPhrase,
        });

        var status = Assert.IsType<ObjectResult>(result);
        Assert.Equal(403, status.StatusCode);
    }
}

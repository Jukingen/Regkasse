using KasseAPI_Final.Configuration;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.DataDeletion;
using KasseAPI_Final.Services.DataRights;
using KasseAPI_Final.Tenancy;
using Microsoft.Extensions.Options;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class CustomerDataRightsServiceTests
{
    [Fact]
    public void Catalog_ContainsViewExportDelete_WithExpectedApprovalSla()
    {
        var catalog = new CustomerDataRightsService(
            dbFactory: null!,
            export: null!,
            deletion: null!,
            artifacts: null!,
            exportOptions: Options.Create(new DataExportOptions()),
            audit: null!,
            fileNaming: new FileNamingService(NullCurrentTenantAccessor.Instance),
            logger: null!).GetRequestTypeCatalog();

        Assert.Equal(3, catalog.Count);

        var view = Assert.Single(catalog, c => c.Type == TenantDataRightsRequestTypes.View);
        Assert.Equal(TenantDataRightsApprovalModes.Auto, view.ApprovalMode);
        Assert.Equal(0, view.MaxProcessingHours);
        Assert.Equal("Instant", view.ProcessingTime);

        var export = Assert.Single(catalog, c => c.Type == TenantDataRightsRequestTypes.Export);
        Assert.Equal(TenantDataRightsApprovalModes.Auto, export.ApprovalMode);
        Assert.Equal(CustomerDataRightsService.ExportMaxProcessingHours, export.MaxProcessingHours);
        Assert.Equal(24, export.MaxProcessingHours);

        var delete = Assert.Single(catalog, c => c.Type == TenantDataRightsRequestTypes.Delete);
        Assert.Equal(TenantDataRightsApprovalModes.Manual, delete.ApprovalMode);
        Assert.Equal(DataDeletionService.ConfirmationWaitDays, delete.ConfirmationWaitDays);
        Assert.Equal(7, delete.ConfirmationWaitDays);
    }

    [Theory]
    [InlineData("view", true)]
    [InlineData("export", true)]
    [InlineData("delete", true)]
    [InlineData("VIEW", true)]
    [InlineData("unknown", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void RequestTypes_IsKnown(string? value, bool expected)
    {
        Assert.Equal(expected, TenantDataRightsRequestTypes.IsKnown(value));
    }

    [Fact]
    public void Normalize_LowercasesTrimmed()
    {
        Assert.Equal("export", TenantDataRightsRequestTypes.Normalize(" Export "));
    }
}

using KasseAPI_Final.Models;
using KasseAPI_Final.Services.DataExport;
using KasseAPI_Final.Services.License;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class LicenseLifecycleResolverTests
{
    private readonly LicenseLifecycleResolver _sut = new();
    private static readonly DateTime Now = new(2026, 7, 19, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Resolve_ActiveLicense_ReturnsActive()
    {
        var state = _sut.Resolve(Now.AddDays(10), customerDataPurgedAtUtc: null, hasPendingDeletionRequest: false, Now);
        Assert.Equal(LicenseLifecycleState.Active, state);
    }

    [Fact]
    public void Resolve_WithinGrace_ReturnsGrace()
    {
        var state = _sut.Resolve(Now.AddDays(-3), null, false, Now);
        Assert.Equal(LicenseLifecycleState.Grace, state);
    }

    [Fact]
    public void Resolve_LockedWindow_ReturnsLocked()
    {
        var state = _sut.Resolve(Now.AddDays(-15), null, false, Now);
        Assert.Equal(LicenseLifecycleState.Locked, state);
    }

    [Fact]
    public void Resolve_ArchivedWindow_ReturnsArchived()
    {
        var state = _sut.Resolve(Now.AddDays(-40), null, false, Now);
        Assert.Equal(LicenseLifecycleState.Archived, state);
    }

    [Fact]
    public void Resolve_PendingDeletion_ReturnsExportRequest()
    {
        var state = _sut.Resolve(Now.AddDays(-15), null, hasPendingDeletionRequest: true, Now);
        Assert.Equal(LicenseLifecycleState.ExportRequest, state);
    }

    [Fact]
    public void Resolve_PurgedCustomerData_ReturnsDeleted()
    {
        var state = _sut.Resolve(Now.AddDays(-15), customerDataPurgedAtUtc: Now.AddDays(-1), false, Now);
        Assert.Equal(LicenseLifecycleState.Deleted, state);
    }

    [Fact]
    public void Resolve_TenantOverlay_PurgedWinsOverPendingRequest()
    {
        var tenant = new Tenant
        {
            LicenseValidUntilUtc = Now.AddDays(-20),
            CustomerDataPurgedAtUtc = Now.AddDays(-2),
        };
        var state = _sut.Resolve(tenant, hasPendingDeletionRequest: true, Now);
        Assert.Equal(LicenseLifecycleState.Deleted, state);
    }
}

using KasseAPI_Final.DTOs;
using KasseAPI_Final.Services;
using KasseAPI_Final.Tenancy;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class PaymentPostCommitConnectionReleaseTests
{
    [Fact]
    public async Task WithoutScopeFactory_PostCommitStaysOnTheRequestService()
    {
        await using var sale = PaymentServiceCoverageHarness.CreateContext();
        var requestPostCommit = new Mock<IPaymentPostCommitService>();
        requestPostCommit
            .Setup(service => service.DispatchPostCommitAsync(
                It.IsAny<CreatePaymentRequest>(),
                It.IsAny<string>(),
                It.IsAny<KasseAPI_Final.Models.Customer>(),
                It.IsAny<Guid>(),
                It.IsAny<KasseAPI_Final.Models.PaymentDetails>(),
                It.IsAny<KasseAPI_Final.Models.Invoice>(),
                It.IsAny<IReadOnlyList<KasseAPI_Final.Services.Tickets.IssuedTicketDto>>(),
                It.IsAny<int>(),
                It.IsAny<bool>(),
                It.IsAny<Guid?>()))
            .ReturnsAsync(new PaymentResult { Success = true, Message = "request" });

        var result = await PaymentPostCommitDispatcher.DispatchAfterFiscalCommitAsync(
            sale,
            requestPostCommit.Object,
            postCommitScopes: null,
            requestTenant: null,
            postCommit => postCommit.DispatchPostCommitAsync(
                new CreatePaymentRequest(),
                "cashier",
                new KasseAPI_Final.Models.Customer(),
                Guid.NewGuid(),
                new KasseAPI_Final.Models.PaymentDetails(),
                new KasseAPI_Final.Models.Invoice(),
                [],
                0,
                true,
                null));

        Assert.True(result.Success);
        Assert.Equal("request", result.Message);
        requestPostCommit.Verify(
            service => service.DispatchPostCommitAsync(
                It.IsAny<CreatePaymentRequest>(),
                It.IsAny<string>(),
                It.IsAny<KasseAPI_Final.Models.Customer>(),
                It.IsAny<Guid>(),
                It.IsAny<KasseAPI_Final.Models.PaymentDetails>(),
                It.IsAny<KasseAPI_Final.Models.Invoice>(),
                It.IsAny<IReadOnlyList<KasseAPI_Final.Services.Tickets.IssuedTicketDto>>(),
                It.IsAny<int>(),
                It.IsAny<bool>(),
                It.IsAny<Guid?>()),
            Times.Once);
    }

    [Fact]
    public async Task WithScopeFactory_PostCommitRunsOnTheNewScopeAfterTenantIsCopied()
    {
        await using var sale = PaymentServiceCoverageHarness.CreateContext();
        var tenantId = Guid.NewGuid();
        var requestTenant = new CurrentTenantAccessor { TenantId = tenantId, TenantSlug = "dev" };
        var scopedTenant = new CurrentTenantAccessor();
        var requestPostCommit = new Mock<IPaymentPostCommitService>(MockBehavior.Strict);
        var scopedPostCommit = new Mock<IPaymentPostCommitService>();
        Guid? tenantSeenAtDispatch = null;
        string? slugSeenAtDispatch = null;
        scopedPostCommit
            .Setup(service => service.DispatchPostCommitAsync(
                It.IsAny<CreatePaymentRequest>(),
                It.IsAny<string>(),
                It.IsAny<KasseAPI_Final.Models.Customer>(),
                It.IsAny<Guid>(),
                It.IsAny<KasseAPI_Final.Models.PaymentDetails>(),
                It.IsAny<KasseAPI_Final.Models.Invoice>(),
                It.IsAny<IReadOnlyList<KasseAPI_Final.Services.Tickets.IssuedTicketDto>>(),
                It.IsAny<int>(),
                It.IsAny<bool>(),
                It.IsAny<Guid?>()))
            .Callback(() =>
            {
                tenantSeenAtDispatch = scopedTenant.TenantId;
                slugSeenAtDispatch = scopedTenant.TenantSlug;
            })
            .ReturnsAsync(new PaymentResult { Success = true, Message = "scoped" });

        var result = await PaymentPostCommitDispatcher.DispatchAfterFiscalCommitAsync(
            sale,
            requestPostCommit.Object,
            new FixedScopeFactory(scopedPostCommit.Object, scopedTenant),
            requestTenant,
            postCommit => postCommit.DispatchPostCommitAsync(
                new CreatePaymentRequest(),
                "cashier",
                new KasseAPI_Final.Models.Customer(),
                tenantId,
                new KasseAPI_Final.Models.PaymentDetails(),
                new KasseAPI_Final.Models.Invoice(),
                [],
                0,
                true,
                null));

        Assert.Equal("scoped", result.Message);
        Assert.Equal(tenantId, tenantSeenAtDispatch);
        Assert.Equal("dev", slugSeenAtDispatch);
        requestPostCommit.VerifyNoOtherCalls();
    }

    private sealed class FixedScopeFactory : IServiceScopeFactory
    {
        private readonly IPaymentPostCommitService _postCommit;
        private readonly ICurrentTenantAccessor _tenant;

        public FixedScopeFactory(IPaymentPostCommitService postCommit, ICurrentTenantAccessor tenant)
        {
            _postCommit = postCommit;
            _tenant = tenant;
        }

        public IServiceScope CreateScope() => new Scope(_postCommit, _tenant);

        private sealed class Scope : IServiceScope
        {
            public Scope(IPaymentPostCommitService postCommit, ICurrentTenantAccessor tenant)
            {
                ServiceProvider = new Provider(postCommit, tenant);
            }

            public IServiceProvider ServiceProvider { get; }

            public void Dispose()
            {
            }

            private sealed class Provider : IServiceProvider
            {
                private readonly IPaymentPostCommitService _postCommit;
                private readonly ICurrentTenantAccessor _tenant;

                public Provider(IPaymentPostCommitService postCommit, ICurrentTenantAccessor tenant)
                {
                    _postCommit = postCommit;
                    _tenant = tenant;
                }

                public object? GetService(Type serviceType)
                {
                    if (serviceType == typeof(ICurrentTenantAccessor))
                        return _tenant;
                    if (serviceType == typeof(IPaymentPostCommitService))
                        return _postCommit;
                    return null;
                }
            }
        }
    }
}

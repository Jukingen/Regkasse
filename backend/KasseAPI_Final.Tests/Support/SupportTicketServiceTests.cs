using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Support;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests.Support;

public sealed class SupportTicketServiceTests
{
    [Fact]
    public async Task ListForTenantAsync_ReturnsOnlyOwnTenantTickets()
    {
        var (sut, db, tenantA, tenantB) = CreateSut();
        await using var _ = db;

        db.SupportTickets.Add(CreateTicket(tenantA, "Own ticket"));
        db.SupportTickets.Add(CreateTicket(tenantB, "Other ticket"));
        await db.SaveChangesAsync();

        var result = await sut.ListForTenantAsync(tenantA);

        var item = Assert.Single(result.Items);
        Assert.Equal("Own ticket", item.Title);
        Assert.Equal(tenantA, item.TenantId);
        Assert.Equal(1, result.OpenCount);
    }

    [Fact]
    public async Task GetForTenantAsync_OtherTenant_ThrowsNotFound()
    {
        var (sut, db, tenantA, tenantB) = CreateSut();
        await using var _ = db;

        var other = CreateTicket(tenantB, "Secret");
        db.SupportTickets.Add(other);
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            sut.GetForTenantAsync(tenantA, other.Id));
    }

    [Fact]
    public async Task CreateAsync_PersistsInitialMessageAndTicketNumber()
    {
        var (sut, db, tenantA, _) = CreateSut();
        await using var _ = db;

        var created = await sut.CreateAsync(
            tenantA,
            "user-1",
            "Manager",
            new CreateSupportTicketRequest
            {
                Category = SupportTicketCategories.Billing,
                Priority = SupportTicketPriorities.High,
                Title = "Invoice PDF missing",
                Message = "I cannot download last month's invoice PDF.",
            });

        Assert.Equal(SupportTicketStatuses.Open, created.Status);
        Assert.Equal(SupportTicketCategories.Billing, created.Category);
        Assert.Equal($"SUP-{DateTime.UtcNow.Year}-0001", created.TicketNumber);
        Assert.Equal("Invoice PDF missing", created.Title);
        Assert.Equal("Invoice PDF missing", created.Subject);
        Assert.Contains("invoice PDF", created.Message, StringComparison.OrdinalIgnoreCase);
        var message = Assert.Single(created.Messages);
        Assert.False(message.IsStaffReply);
        Assert.False(message.IsInternal);
        Assert.Contains("invoice PDF", message.Body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AddMessageForTenantAsync_SetsWaitingOnStaff()
    {
        var (sut, db, tenantA, _) = CreateSut();
        await using var _ = db;

        var created = await sut.CreateAsync(
            tenantA,
            "user-1",
            "Manager",
            new CreateSupportTicketRequest
            {
                Category = SupportTicketCategories.Technical,
                Priority = SupportTicketPriorities.Low,
                Title = "POS login issue",
                Message = "Cashier cannot log in after password reset.",
            });

        var updated = await sut.AddMessageForTenantAsync(
            tenantA,
            created.Id,
            "user-1",
            "Manager",
            "Still blocked this morning.");

        Assert.Equal(SupportTicketStatuses.WaitingOnStaff, updated.Status);
        Assert.Equal(2, updated.Messages.Count);
    }

    [Fact]
    public async Task AddStaffMessageAsync_SetsWaitingOnTenant()
    {
        var (sut, db, tenantA, _) = CreateSut();
        await using var _ = db;

        var created = await sut.CreateAsync(
            tenantA,
            "user-1",
            "Manager",
            new CreateSupportTicketRequest
            {
                Category = SupportTicketCategories.License,
                Priority = SupportTicketPriorities.Medium,
                Title = "License key question",
                Message = "Where do I enter the new license key after purchase?",
            });

        var updated = await sut.AddStaffMessageAsync(
            created.Id,
            "staff-1",
            "Support",
            "Please open Lizenzverwaltung and paste the key there.",
            isInternal: false);

        Assert.Equal(SupportTicketStatuses.WaitingOnTenant, updated.Status);
        Assert.Equal(2, updated.Messages.Count);
        Assert.True(updated.Messages[1].IsStaffReply);
        Assert.False(updated.Messages[1].IsInternal);
    }

    [Fact]
    public async Task AddStaffMessageAsync_InternalNote_IsHiddenFromTenant()
    {
        var (sut, db, tenantA, _) = CreateSut();
        await using var _ = db;

        var created = await sut.CreateAsync(
            tenantA,
            "user-1",
            "Manager",
            new CreateSupportTicketRequest
            {
                Category = SupportTicketCategories.General,
                Priority = SupportTicketPriorities.Medium,
                Title = "Need help with export",
                Message = "How do I export last week's receipts?",
            });

        await sut.AddStaffMessageAsync(
            created.Id,
            "staff-1",
            "Support",
            "Internal: check DEP export logs first.",
            isInternal: true);

        var tenantView = await sut.GetForTenantAsync(tenantA, created.Id);
        var staffView = await sut.GetAnyAsync(created.Id);

        Assert.Single(tenantView.Messages);
        Assert.Equal(2, staffView.Messages.Count);
        Assert.True(staffView.Messages[1].IsInternal);
        Assert.Equal(SupportTicketStatuses.Open, tenantView.Status);
    }

    [Fact]
    public async Task UpdateStatusAsync_Resolved_SetsResolvedAtAndNotifies()
    {
        var (sut, db, tenantA, _, notify) = CreateSutWithNotify();
        await using var _ = db;

        var created = await sut.CreateAsync(
            tenantA,
            "user-1",
            "Manager",
            new CreateSupportTicketRequest
            {
                Category = SupportTicketCategories.Billing,
                Priority = SupportTicketPriorities.Medium,
                Title = "Wrong VAT on invoice",
                Message = "The VAT amount on invoice 12 looks incorrect.",
            });

        var updated = await sut.UpdateStatusAsync(created.Id, SupportTicketStatuses.Resolved);

        Assert.Equal(SupportTicketStatuses.Resolved, updated.Status);
        Assert.NotNull(updated.ResolvedAtUtc);
        notify.Verify(n => n.NotifyResolvedAsync(It.IsAny<SupportTicket>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateStatusAsync_Closed_Notifies()
    {
        var (sut, db, tenantA, _, notify) = CreateSutWithNotify();
        await using var _ = db;

        var created = await sut.CreateAsync(
            tenantA,
            "user-1",
            "Manager",
            new CreateSupportTicketRequest
            {
                Category = SupportTicketCategories.General,
                Priority = SupportTicketPriorities.Urgent,
                Title = "Need to close",
                Message = "Please close this ticket after review.",
            });

        var updated = await sut.UpdateStatusAsync(created.Id, SupportTicketStatuses.Closed);

        Assert.Equal(SupportTicketStatuses.Closed, updated.Status);
        notify.Verify(n => n.NotifyClosedAsync(It.IsAny<SupportTicket>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_AcceptsSubjectAlias()
    {
        var (sut, db, tenantA, _) = CreateSut();
        await using var _ = db;

        var created = await sut.CreateAsync(
            tenantA,
            "user-1",
            "Manager",
            new CreateSupportTicketRequest
            {
                Category = SupportTicketCategories.Technical,
                Priority = SupportTicketPriorities.Low,
                Subject = "Bondrucker defekt",
                Message = "Der Bondrucker reagiert nicht mehr nach dem Update.",
            });

        Assert.Equal("Bondrucker defekt", created.Title);
        Assert.Equal("Bondrucker defekt", created.Subject);
    }

    [Fact]
    public async Task ListAllAsync_SearchFiltersByTicketNumber()
    {
        var (sut, db, tenantA, _) = CreateSut();
        await using var _ = db;

        await sut.CreateAsync(
            tenantA,
            "user-1",
            "Manager",
            new CreateSupportTicketRequest
            {
                Category = SupportTicketCategories.Billing,
                Priority = SupportTicketPriorities.Medium,
                Title = "Invoice PDF missing",
                Message = "I cannot download last month's invoice PDF.",
            });
        await sut.CreateAsync(
            tenantA,
            "user-1",
            "Manager",
            new CreateSupportTicketRequest
            {
                Category = SupportTicketCategories.Technical,
                Priority = SupportTicketPriorities.Low,
                Title = "Printer jam",
                Message = "The receipt printer jams on every second receipt.",
            });

        var result = await sut.ListAllAsync(new SupportTicketListQuery { Search = "Printer" });

        var item = Assert.Single(result.Items);
        Assert.Equal("Printer jam", item.Title);
        Assert.Equal(1, result.TotalCount);
    }

    [Fact]
    public async Task ListAllAsync_IncludesTicketsFromEveryTenant()
    {
        var (sut, db, tenantA, tenantB) = CreateSut();
        await using var _ = db;

        db.SupportTickets.Add(CreateTicket(tenantA, "Tenant A issue"));
        db.SupportTickets.Add(CreateTicket(tenantB, "Tenant B issue"));
        await db.SaveChangesAsync();

        var result = await sut.ListAllAsync(new SupportTicketListQuery());

        Assert.Equal(2, result.TotalCount);
        Assert.Contains(result.Items, i => i.TenantId == tenantA);
        Assert.Contains(result.Items, i => i.TenantId == tenantB);
    }

    [Fact]
    public async Task CreateAsync_InvalidCategory_Throws()
    {
        var (sut, db, tenantA, _) = CreateSut();
        await using var _ = db;

        await Assert.ThrowsAsync<ArgumentException>(() =>
            sut.CreateAsync(
                tenantA,
                "user-1",
                "Manager",
                new CreateSupportTicketRequest
                {
                    Category = "not-a-category",
                    Title = "Need help please",
                    Message = "This message is long enough to pass validation.",
                }));
    }

    [Fact]
    public async Task UpdateStatusForTenantAsync_RejectsResolved()
    {
        var (sut, db, tenantA, _) = CreateSut();
        await using var _ = db;

        var created = await sut.CreateAsync(
            tenantA,
            "user-1",
            "Manager",
            new CreateSupportTicketRequest
            {
                Category = SupportTicketCategories.Technical,
                Priority = SupportTicketPriorities.Low,
                Title = "Printer offline",
                Message = "The kitchen printer stopped printing tickets.",
            });

        await Assert.ThrowsAsync<ArgumentException>(() =>
            sut.UpdateStatusForTenantAsync(tenantA, created.Id, SupportTicketStatuses.Resolved));
    }

    [Fact]
    public async Task AssignTicketAsync_SetsAssigneeAndMovesOpenToInProgress()
    {
        var (sut, db, tenantA, _) = CreateSut();
        await using var _ = db;

        var created = await sut.CreateAsync(
            tenantA,
            "user-1",
            "Manager",
            new CreateSupportTicketRequest
            {
                Category = SupportTicketCategories.License,
                Priority = SupportTicketPriorities.High,
                Title = "Cannot activate key",
                Message = "The license key from the invoice is rejected.",
            });

        var updated = await sut.AssignTicketAsync(created.Id, "staff-9", "Ada Support");

        Assert.Equal("staff-9", updated.AssignedToUserId);
        Assert.Equal("Ada Support", updated.AssignedToDisplayName);
        Assert.Equal(SupportTicketStatuses.InProgress, updated.Status);
    }

    [Fact]
    public async Task GetOpenTicketCountAsync_IgnoresResolvedAndClosed()
    {
        var (sut, db, tenantA, _) = CreateSut();
        await using var _ = db;

        var open = CreateTicket(tenantA, "Open one");
        var resolved = CreateTicket(tenantA, "Resolved one");
        resolved.Status = SupportTicketStatuses.Resolved;
        var closed = CreateTicket(tenantA, "Closed one");
        closed.Status = SupportTicketStatuses.Closed;
        db.SupportTickets.AddRange(open, resolved, closed);
        await db.SaveChangesAsync();

        Assert.Equal(1, await sut.GetOpenTicketCountAsync(tenantA));
    }

    private static (SupportTicketService Sut, AppDbContext Db, Guid TenantA, Guid TenantB) CreateSut()
    {
        var (sut, db, tenantA, tenantB, _) = CreateSutWithNotify();
        return (sut, db, tenantA, tenantB);
    }

    private static (
        SupportTicketService Sut,
        AppDbContext Db,
        Guid TenantA,
        Guid TenantB,
        Mock<ISupportTicketNotificationService> Notify) CreateSutWithNotify()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"SupportTickets_{Guid.NewGuid():N}")
            .Options;
        var db = new AppDbContext(options, new FixedTenantAccessor(tenantA));
        db.Tenants.AddRange(
            new Tenant { Id = tenantA, Name = "A", Slug = "a", Status = TenantStatuses.Active },
            new Tenant { Id = tenantB, Name = "B", Slug = "b", Status = TenantStatuses.Active });
        db.SaveChanges();

        var notify = new Mock<ISupportTicketNotificationService>();
        notify.Setup(n => n.NotifyNewTicketAsync(It.IsAny<SupportTicket>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        notify.Setup(n => n.NotifyStaffReplyAsync(It.IsAny<SupportTicket>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        notify.Setup(n => n.NotifyTenantReplyAsync(It.IsAny<SupportTicket>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        notify.Setup(n => n.NotifyResolvedAsync(It.IsAny<SupportTicket>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        notify.Setup(n => n.NotifyClosedAsync(It.IsAny<SupportTicket>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        return (new SupportTicketService(db, notify.Object, Mock.Of<ILogger<SupportTicketService>>()), db, tenantA, tenantB, notify);
    }

    private static SupportTicket CreateTicket(Guid tenantId, string title) => new()
    {
        TenantId = tenantId,
        TicketNumber = $"SUP-TEST-{Guid.NewGuid().ToString("N")[..6]}".ToUpperInvariant(),
        Category = SupportTicketCategories.Technical,
        Priority = SupportTicketPriorities.Medium,
        Status = SupportTicketStatuses.Open,
        Title = title,
        Message = title,
        CreatedByUserId = "user-1",
        CreatedAtUtc = DateTime.UtcNow,
        UpdatedAtUtc = DateTime.UtcNow,
    };

    private sealed class FixedTenantAccessor(Guid? tenantId) : ICurrentTenantAccessor
    {
        public Guid? TenantId { get; set; } = tenantId;
        public string? TenantSlug { get; set; }
    }
}

using System.Text.Json;
using KasseAPI_Final.Data;
using KasseAPI_Final.Migrations;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.DataDeletion;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;
using Xunit;

namespace KasseAPI_Final.Tests;

/// <summary>
/// One purge proof per store that can hold personal data. Fiscal amounts and TSE signatures stay.
/// </summary>
public sealed class DataDeletionTableCoverageTests
{
    private const string PiiEmail = "max.purge@test.at";
    private const string PiiName = "Max Mustermann";
    private const string PiiPhone = "+436641112233";
    private const string PiiAddress = "Hauptstrasse 1, 1010 Wien";
    private const string PiiTax = "ATU12345678";
    private const string KeptSignature = "jws-fiscal-keep";
    private const decimal KeptTotal = 42.50m;
    private const decimal KeptTax = 7.08m;

    [Fact]
    public async Task PaymentDetails_AnonymizesCustomerIdentity_AndKeepsAmountsAndSignature()
    {
        await using var fx = await PurgeFixture.CreateAsync();
        var paymentId = Guid.NewGuid();
        var otherPaymentId = Guid.NewGuid();

        fx.Db.PaymentDetails.Add(Payment(fx.CustomerId, fx.RegisterId, paymentId, PiiName));
        fx.Db.PaymentDetails.Add(Payment(fx.OtherCustomerId, fx.OtherRegisterId, otherPaymentId, "Erika Other"));
        await fx.Db.SaveChangesAsync();

        await fx.PurgeAsync();

        var payment = await fx.Db.PaymentDetails.IgnoreQueryFilters().SingleAsync(p => p.Id == paymentId);
        Assert.Equal(KeptTotal, payment.TotalAmount);
        Assert.Equal(KeptTax, payment.TaxAmount);
        Assert.Equal("R-100", payment.ReceiptNumber);
        Assert.Equal(KeptSignature, payment.TseSignature);
        Assert.Equal(TenantPiiPurge.AnonymousName, payment.CustomerName);
        Assert.Equal("Cafe GmbH", payment.CompanyName);
        Assert.Equal(PiiAddress, payment.CompanyAddress);
        Assert.Equal(PiiTax, payment.Steuernummer);
        Assert.Null(payment.Notes);
        Assert.Null(payment.RouteFrom);
        Assert.Null(payment.RouteTo);
        Assert.Null(payment.PrescriptionReference);
        Assert.Null(payment.RefundReason);
        Assert.Null(payment.CancellationReason);
        Assert.NotEqual(fx.CustomerId, payment.CustomerId);
        AssertNoPii(payment.CustomerName);
        AssertNoPii(payment.Notes);

        var other = await fx.Db.PaymentDetails.IgnoreQueryFilters().SingleAsync(p => p.Id == otherPaymentId);
        Assert.Equal("Erika Other", other.CustomerName);
        Assert.Equal("Nebenstrasse 2", other.CompanyAddress);
        Assert.Equal("keep-other-note", other.Notes);
    }

    [Fact]
    public async Task Invoices_AnonymizesCustomerFields_DeletesNonFiscal_KeepsSignedAmounts()
    {
        await using var fx = await PurgeFixture.CreateAsync();
        var fiscalId = Guid.NewGuid();
        var draftId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();

        fx.Db.Invoices.Add(Invoice(fx.TenantId, fx.RegisterId, fiscalId, paymentId));
        fx.Db.Invoices.Add(Invoice(fx.TenantId, fx.RegisterId, draftId, sourcePaymentId: null));
        await fx.Db.SaveChangesAsync();

        await fx.PurgeAsync();

        var fiscal = await fx.Db.Invoices.IgnoreQueryFilters().SingleAsync(i => i.Id == fiscalId);
        Assert.Equal(KeptTotal, fiscal.TotalAmount);
        Assert.Equal(KeptTax, fiscal.TaxAmount);
        Assert.Equal(35.42m, fiscal.Subtotal);
        Assert.Equal(KeptSignature, fiscal.TseSignature);
        Assert.Equal("RE-1", fiscal.InvoiceNumber);
        Assert.Equal("Cafe GmbH", fiscal.CompanyName);
        Assert.Equal("ATU87654321", fiscal.CompanyTaxNumber);
        Assert.Equal("Sitz 9", fiscal.CompanyAddress);
        Assert.Null(fiscal.CompanyEmail);
        Assert.Null(fiscal.CompanyPhone);
        Assert.Equal(TenantPiiPurge.AnonymousName, fiscal.CustomerName);
        Assert.Null(fiscal.CustomerEmail);
        Assert.Null(fiscal.CustomerPhone);
        Assert.Null(fiscal.CustomerAddress);
        Assert.Null(fiscal.CustomerTaxNumber);
        AssertNoPii(fiscal.CustomerName);

        Assert.Null(await fx.Db.Invoices.IgnoreQueryFilters().SingleOrDefaultAsync(i => i.Id == draftId));
    }

    [Fact]
    public async Task OnlineOrders_AnonymizesContactFields_AndKeepsTotals()
    {
        await using var fx = await PurgeFixture.CreateAsync();
        var orderId = Guid.NewGuid();
        var otherId = Guid.NewGuid();

        fx.Db.OnlineOrders.Add(OnlineOrderRow(fx.TenantId, orderId, PiiName, PiiEmail));
        fx.Db.OnlineOrders.Add(OnlineOrderRow(fx.OtherTenantId, otherId, "Erika Other", "erika@other.at"));
        await fx.Db.SaveChangesAsync();

        await fx.PurgeAsync();

        var order = await fx.Db.OnlineOrders.IgnoreQueryFilters().SingleAsync(o => o.Id == orderId);
        Assert.Equal(18m, order.Total);
        Assert.Equal(18m, order.Subtotal);
        Assert.Equal("ORD-9", order.OrderNumber);
        Assert.Equal(TenantPiiPurge.AnonymousName, order.CustomerName);
        Assert.Equal(string.Empty, order.CustomerPhone);
        Assert.Null(order.CustomerEmail);
        Assert.Null(order.DeliveryAddress);
        Assert.Null(order.CustomerDeviceToken);
        Assert.Null(order.Notes);
        Assert.Null(order.CustomerId);
        AssertNoPii(order.CustomerName);

        var other = await fx.Db.OnlineOrders.IgnoreQueryFilters().SingleAsync(o => o.Id == otherId);
        Assert.Equal("erika@other.at", other.CustomerEmail);
        Assert.Equal("Erika Other", other.CustomerName);
        Assert.Equal("device-other", other.CustomerDeviceToken);
    }

    [Fact]
    public async Task AuditLogs_RedactsIdentityInPayloads_AndKeepsAmounts()
    {
        await using var fx = await PurgeFixture.CreateAsync();
        var auditId = Guid.NewGuid();
        var otherId = Guid.NewGuid();
        fx.Db.AuditLogs.Add(new AuditLog
        {
            Id = auditId,
            TenantId = fx.TenantId,
            SessionId = "s",
            UserId = "user-purge-1",
            UserRole = "Manager",
            Action = "UPDATE",
            EntityType = "Customer",
            EntityName = PiiEmail,
            OldValues = """{"Email":"max.purge@test.at","name":"Max Mustermann","amountText":"42.50"}""",
            NewValues = """{"phone":"+436641112233","address":"Hauptstrasse 1, 1010 Wien","receiptNumber":"R-100"}""",
            RequestData = """{"taxNumber":"ATU12345678"}""",
            Description = "Changed max.purge@test.at",
            Notes = "max.purge@test.at",
            IpAddress = "203.0.113.10",
            UserAgent = "POS/1 " + PiiEmail,
            Status = AuditLogStatus.Success,
            Timestamp = DateTime.UtcNow,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        fx.Db.AuditLogs.Add(new AuditLog
        {
            Id = otherId,
            TenantId = fx.OtherTenantId,
            SessionId = "s2",
            UserId = "user-other",
            UserRole = "Manager",
            Action = "UPDATE",
            EntityType = "Customer",
            EntityName = "erika@other.at",
            OldValues = """{"Email":"erika@other.at","name":"Erika Other"}""",
            Status = AuditLogStatus.Success,
            Timestamp = DateTime.UtcNow,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        await fx.Db.SaveChangesAsync();

        await fx.PurgeAsync();

        var audit = await fx.Db.AuditLogs.IgnoreQueryFilters().SingleAsync(a => a.Id == auditId);
        AssertNoPii(audit.OldValues);
        AssertNoPii(audit.NewValues);
        AssertNoPii(audit.RequestData);
        AssertNoPii(audit.Description);
        AssertNoPii(audit.Notes);
        AssertNoPii(audit.EntityName);
        Assert.Contains("42.50", audit.OldValues, StringComparison.Ordinal);
        Assert.Contains("R-100", audit.NewValues, StringComparison.Ordinal);
        Assert.Contains(GdprPayloadRedactor.Redacted, audit.OldValues, StringComparison.Ordinal);
        Assert.Null(audit.IpAddress);
        Assert.Null(audit.UserAgent);
        Assert.Equal("UPDATE", audit.Action);

        var other = await fx.Db.AuditLogs.IgnoreQueryFilters().SingleAsync(a => a.Id == otherId);
        Assert.Contains("erika@other.at", other.OldValues!, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("erika@other.at", other.EntityName);
    }

    [Fact]
    public async Task ActivityEvents_AnonymizesActorDescriptionAndMetadata()
    {
        await using var fx = await PurgeFixture.CreateAsync();
        var eventId = Guid.NewGuid();
        var otherId = Guid.NewGuid();
        fx.Db.ActivityEvents.Add(Activity(fx.TenantId, eventId, PiiName, "Changed max.purge@test.at"));
        fx.Db.ActivityEvents.Add(Activity(fx.OtherTenantId, otherId, "Erika Other", "Kept erika@other.at"));
        await fx.Db.SaveChangesAsync();

        await fx.PurgeAsync();

        var evt = await fx.Db.ActivityEvents.IgnoreQueryFilters().SingleAsync(a => a.Id == eventId);
        Assert.Equal(TenantPiiPurge.AnonymousName, evt.ActorName);
        Assert.Null(evt.Description);
        Assert.Null(evt.MetadataJson);
        Assert.Equal("User updated", evt.Title);
        Assert.Equal(ActivityEventType.UserUpdated, evt.Type);

        var other = await fx.Db.ActivityEvents.IgnoreQueryFilters().SingleAsync(a => a.Id == otherId);
        Assert.Equal("Erika Other", other.ActorName);
        Assert.Contains("erika@other.at", other.Description!, StringComparison.OrdinalIgnoreCase);
        Assert.NotNull(other.MetadataJson);
    }

    [Fact]
    public async Task AspNetUsers_AnonymizesSoleTenantUser_AndKeepsUsersOnAnotherTenant()
    {
        await using var fx = await PurgeFixture.CreateAsync();
        const string soleId = "user-last-of-tenant";
        const string sharedId = "user-still-on-other";
        const string otherId = "user-other-tenant";

        fx.Db.Users.AddRange(
            User(soleId, "max", PiiEmail, "Max", "Mustermann", PiiPhone),
            User(sharedId, "shared", "shared@test.at", "Sam", "Shared", "+436640000001"),
            User(otherId, "erika", "erika@other.at", "Erika", "Other", "+436649998877"));
        fx.Db.UserTenantMemberships.AddRange(
            Membership(soleId, fx.TenantId),
            Membership(sharedId, fx.TenantId),
            Membership(sharedId, fx.OtherTenantId),
            Membership(otherId, fx.OtherTenantId));
        await fx.Db.SaveChangesAsync();

        await fx.PurgeAsync();

        var sole = await fx.Db.Users.SingleAsync(u => u.Id == soleId);
        Assert.Equal(soleId, sole.Id);
        Assert.Equal(TenantPiiPurge.AnonymousName, sole.FirstName);
        Assert.Equal("User", sole.LastName);
        Assert.Null(sole.PhoneNumber);
        Assert.Null(sole.TaxNumber);
        Assert.EndsWith("@anonymized.invalid", sole.Email, StringComparison.OrdinalIgnoreCase);
        AssertNoPii(sole.Email);
        AssertNoPii(sole.UserName);
        AssertNoPii(sole.NormalizedEmail);
        Assert.False(sole.IsActive);

        var shared = await fx.Db.Users.SingleAsync(u => u.Id == sharedId);
        Assert.Equal("shared@test.at", shared.Email);
        Assert.Equal("Sam", shared.FirstName);
        Assert.Equal("+436640000001", shared.PhoneNumber);

        var other = await fx.Db.Users.SingleAsync(u => u.Id == otherId);
        Assert.Equal("erika@other.at", other.Email);
        Assert.Equal("Erika", other.FirstName);
    }

    [Fact]
    public async Task SupportTickets_RedactsTitleMessageAndDisplayNames()
    {
        await using var fx = await PurgeFixture.CreateAsync();
        var ticketId = Guid.NewGuid();
        var messageId = Guid.NewGuid();
        var otherTicketId = Guid.NewGuid();

        fx.Db.SupportTickets.Add(new SupportTicket
        {
            Id = ticketId,
            TenantId = fx.TenantId,
            TicketNumber = "T-1",
            Category = SupportTicketCategories.General,
            Priority = SupportTicketPriorities.Medium,
            Status = SupportTicketStatuses.Open,
            Title = "Call " + PiiName,
            Message = "Email " + PiiEmail + " at " + PiiPhone,
            CreatedByUserId = "user-purge-1",
            CreatedByDisplayName = PiiName,
            AssignedToDisplayName = PiiName,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
        });
        fx.Db.SupportTicketMessages.Add(new SupportTicketMessage
        {
            Id = messageId,
            TicketId = ticketId,
            TenantId = fx.TenantId,
            AuthorUserId = "user-purge-1",
            AuthorDisplayName = PiiName,
            Body = "Please call " + PiiPhone + " " + PiiEmail,
            CreatedAtUtc = DateTime.UtcNow,
        });
        fx.Db.SupportTickets.Add(new SupportTicket
        {
            Id = otherTicketId,
            TenantId = fx.OtherTenantId,
            TicketNumber = "T-2",
            Category = SupportTicketCategories.General,
            Priority = SupportTicketPriorities.Medium,
            Status = SupportTicketStatuses.Open,
            Title = "Erika question",
            Message = "erika@other.at",
            CreatedByUserId = "user-other",
            CreatedByDisplayName = "Erika Other",
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
        });
        await fx.Db.SaveChangesAsync();

        await fx.PurgeAsync();

        var ticket = await fx.Db.SupportTickets.IgnoreQueryFilters().SingleAsync(t => t.Id == ticketId);
        Assert.Equal(GdprPayloadRedactor.Redacted, ticket.Title);
        Assert.Equal(GdprPayloadRedactor.Redacted, ticket.Message);
        Assert.Equal(TenantPiiPurge.AnonymousName, ticket.CreatedByDisplayName);
        Assert.Null(ticket.AssignedToDisplayName);
        Assert.Equal("T-1", ticket.TicketNumber);

        var message = await fx.Db.SupportTicketMessages.IgnoreQueryFilters().SingleAsync(m => m.Id == messageId);
        Assert.Equal(GdprPayloadRedactor.Redacted, message.Body);
        Assert.Equal(TenantPiiPurge.AnonymousName, message.AuthorDisplayName);

        var other = await fx.Db.SupportTickets.IgnoreQueryFilters().SingleAsync(t => t.Id == otherTicketId);
        Assert.Equal("erika@other.at", other.Message);
        Assert.Equal("Erika Other", other.CreatedByDisplayName);
    }

    [Fact]
    public async Task CommunicationLogs_ReplacesEmailSubjectAndError()
    {
        await using var fx = await PurgeFixture.CreateAsync();
        var id = Guid.NewGuid();
        var otherId = Guid.NewGuid();
        fx.Db.CommunicationLogs.Add(new CommunicationLog
        {
            Id = id,
            TenantId = fx.TenantId,
            Email = PiiEmail,
            Subject = "Hello " + PiiName,
            Status = CommunicationLogStatuses.Failed,
            ErrorMessage = "bounce " + PiiEmail,
            SentAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
        });
        fx.Db.CommunicationLogs.Add(new CommunicationLog
        {
            Id = otherId,
            TenantId = fx.OtherTenantId,
            Email = "erika@other.at",
            Subject = "Hello Erika",
            Status = CommunicationLogStatuses.Sent,
            SentAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
        });
        await fx.Db.SaveChangesAsync();

        await fx.PurgeAsync();

        var row = await fx.Db.CommunicationLogs.IgnoreQueryFilters().SingleAsync(c => c.Id == id);
        Assert.EndsWith("@anonymized.invalid", row.Email, StringComparison.OrdinalIgnoreCase);
        AssertNoPii(row.Email);
        Assert.Equal(GdprPayloadRedactor.Redacted, row.Subject);
        Assert.Null(row.ErrorMessage);

        var other = await fx.Db.CommunicationLogs.IgnoreQueryFilters().SingleAsync(c => c.Id == otherId);
        Assert.Equal("erika@other.at", other.Email);
        Assert.Equal("Hello Erika", other.Subject);
    }

    [Fact]
    public async Task OperationLogs_RedactsStateAndDropsNetworkIdentifiers()
    {
        await using var fx = await PurgeFixture.CreateAsync();
        var id = Guid.NewGuid();
        var otherId = Guid.NewGuid();
        fx.Db.OperationLogs.Add(new OperationLog
        {
            Id = id,
            TenantId = fx.TenantId,
            UserId = "user-purge-1",
            OperationType = "UpdateCustomer",
            EntityType = "Customer",
            EntityId = fx.CustomerId.ToString(),
            BeforeState = """{"name":"Max Mustermann","email":"max.purge@test.at","total":18}""",
            AfterState = """{"phone":"+436641112233","address":"Hauptstrasse 1, 1010 Wien"}""",
            Reason = "Asked by " + PiiName,
            IpAddress = "203.0.113.10",
            UserAgent = "Browser " + PiiEmail,
            CreatedAt = DateTime.UtcNow,
        });
        fx.Db.OperationLogs.Add(new OperationLog
        {
            Id = otherId,
            TenantId = fx.OtherTenantId,
            UserId = "user-other",
            OperationType = "UpdateCustomer",
            EntityType = "Customer",
            EntityId = fx.OtherCustomerId.ToString(),
            BeforeState = """{"name":"Erika Other","email":"erika@other.at"}""",
            IpAddress = "198.51.100.8",
            CreatedAt = DateTime.UtcNow,
        });
        await fx.Db.SaveChangesAsync();

        await fx.PurgeAsync();

        var row = await fx.Db.OperationLogs.IgnoreQueryFilters().SingleAsync(o => o.Id == id);
        AssertNoPii(row.BeforeState);
        AssertNoPii(row.AfterState);
        Assert.Contains("18", row.BeforeState, StringComparison.Ordinal);
        Assert.Null(row.Reason);
        Assert.Null(row.IpAddress);
        Assert.Null(row.UserAgent);

        var other = await fx.Db.OperationLogs.IgnoreQueryFilters().SingleAsync(o => o.Id == otherId);
        Assert.Contains("erika@other.at", other.BeforeState!, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("198.51.100.8", other.IpAddress);
    }

    [Fact]
    public async Task Appointments_ClearsNotes()
    {
        await using var fx = await PurgeFixture.CreateAsync();
        var id = Guid.NewGuid();
        var otherId = Guid.NewGuid();
        fx.Db.Appointments.Add(new Appointment
        {
            Id = id,
            TenantId = fx.TenantId,
            CustomerId = fx.CustomerId,
            StartUtc = DateTime.UtcNow,
            EndUtc = DateTime.UtcNow.AddHours(1),
            Notes = PiiName + " birthday, " + PiiPhone,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
        });
        fx.Db.Appointments.Add(new Appointment
        {
            Id = otherId,
            TenantId = fx.OtherTenantId,
            CustomerId = fx.OtherCustomerId,
            StartUtc = DateTime.UtcNow,
            EndUtc = DateTime.UtcNow.AddHours(1),
            Notes = "Erika standing appointment",
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
        });
        await fx.Db.SaveChangesAsync();

        await fx.PurgeAsync();

        var row = await fx.Db.Appointments.IgnoreQueryFilters().SingleAsync(a => a.Id == id);
        Assert.Null(row.Notes);
        Assert.Equal(fx.TenantId, row.TenantId);

        var other = await fx.Db.Appointments.IgnoreQueryFilters().SingleAsync(a => a.Id == otherId);
        Assert.Equal("Erika standing appointment", other.Notes);
    }

    [Fact]
    public async Task GuestFolios_AreDeleted()
    {
        await using var fx = await PurgeFixture.CreateAsync();
        var id = Guid.NewGuid();
        var otherId = Guid.NewGuid();
        fx.Db.GuestFolios.Add(new GuestFolio
        {
            Id = id,
            TenantId = fx.TenantId,
            CustomerId = fx.CustomerId,
            RoomId = Guid.NewGuid(),
            CheckIn = DateTime.UtcNow,
            Notes = "late arrival " + PiiName + " " + PiiEmail,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
        });
        fx.Db.GuestFolios.Add(new GuestFolio
        {
            Id = otherId,
            TenantId = fx.OtherTenantId,
            CustomerId = fx.OtherCustomerId,
            RoomId = Guid.NewGuid(),
            CheckIn = DateTime.UtcNow,
            Notes = "Erika late checkout",
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
        });
        await fx.Db.SaveChangesAsync();

        await fx.PurgeAsync();

        Assert.Null(await fx.Db.GuestFolios.IgnoreQueryFilters().SingleOrDefaultAsync(f => f.Id == id));
        var other = await fx.Db.GuestFolios.IgnoreQueryFilters().SingleAsync(f => f.Id == otherId);
        Assert.Equal("Erika late checkout", other.Notes);
    }

    [Fact]
    public async Task DemoRequests_MatchingCollectedEmails_AreAnonymized()
    {
        await using var fx = await PurgeFixture.CreateAsync();
        var id = Guid.NewGuid();
        var otherId = Guid.NewGuid();
        fx.Db.DemoRequests.Add(new DemoRequest
        {
            Id = id,
            Name = PiiName,
            Email = PiiEmail,
            Company = "Mustermann GmbH",
            Industry = "cafe",
            Message = "please call " + PiiPhone,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
        });
        fx.Db.DemoRequests.Add(new DemoRequest
        {
            Id = otherId,
            Name = "Erika Other",
            Email = "erika@other.at",
            Industry = "retail",
            Message = "keep me",
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
        });
        await fx.Db.SaveChangesAsync();

        await fx.PurgeAsync();

        var row = await fx.Db.DemoRequests.SingleAsync(d => d.Id == id);
        Assert.Equal(TenantPiiPurge.AnonymousName, row.Name);
        Assert.EndsWith("@anonymized.invalid", row.Email, StringComparison.OrdinalIgnoreCase);
        AssertNoPii(row.Email);
        Assert.Null(row.Company);
        Assert.Null(row.Message);
        Assert.Equal("cafe", row.Industry);

        var other = await fx.Db.DemoRequests.SingleAsync(d => d.Id == otherId);
        Assert.Equal("Erika Other", other.Name);
        Assert.Equal("erika@other.at", other.Email);
        Assert.Equal("keep me", other.Message);
    }

    [Fact]
    public async Task Customers_AreDeleted_ExceptAnonymousPlaceholderForPaymentFk()
    {
        await using var fx = await PurgeFixture.CreateAsync();
        var paymentId = Guid.NewGuid();
        fx.Db.PaymentDetails.Add(Payment(fx.CustomerId, fx.RegisterId, paymentId, PiiName));
        var customer = await fx.Db.Customers.IgnoreQueryFilters().SingleAsync(c => c.Id == fx.CustomerId);
        customer.BirthDate = new DateTime(1985, 4, 2, 0, 0, 0, DateTimeKind.Utc);
        customer.Notes = "allergic, " + PiiEmail;
        await fx.Db.SaveChangesAsync();

        await fx.PurgeAsync();

        Assert.Null(await fx.Db.Customers.IgnoreQueryFilters().SingleOrDefaultAsync(c => c.Id == fx.CustomerId));

        var placeholder = await fx.Db.Customers.IgnoreQueryFilters().SingleAsync(c => c.TenantId == fx.TenantId);
        Assert.Equal(TenantPiiPurge.AnonymousName, placeholder.Name);
        Assert.EndsWith("@anonymized.invalid", placeholder.Email, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(string.Empty, placeholder.Phone);
        Assert.Equal(string.Empty, placeholder.Address);
        Assert.Equal(string.Empty, placeholder.TaxNumber);
        Assert.Equal(string.Empty, placeholder.Notes);
        Assert.Null(placeholder.BirthDate);
        AssertNoPii(placeholder.Email);
        Assert.False(placeholder.IsActive);

        var payment = await fx.Db.PaymentDetails.IgnoreQueryFilters().SingleAsync(p => p.Id == paymentId);
        Assert.Equal(placeholder.Id, payment.CustomerId);
        Assert.Equal(KeptTotal, payment.TotalAmount);
        Assert.Equal(KeptSignature, payment.TseSignature);

        var other = await fx.Db.Customers.IgnoreQueryFilters().SingleAsync(c => c.Id == fx.OtherCustomerId);
        Assert.Equal("Erika Other", other.Name);
        Assert.Equal("erika@other.at", other.Email);
    }

    [Fact]
    public async Task Vouchers_DropFreeTextAndCustomerLink_AndKeepLedgerAmounts()
    {
        await using var fx = await PurgeFixture.CreateAsync();
        var voucherId = Guid.NewGuid();
        var ledgerId = Guid.NewGuid();
        var otherVoucherId = Guid.NewGuid();
        const string codeHash = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

        fx.Db.Vouchers.Add(new Voucher
        {
            Id = voucherId,
            TenantId = fx.TenantId,
            CustomerId = fx.CustomerId,
            CodeHash = codeHash,
            MaskedCode = "****1234",
            InitialAmount = 50m,
            RemainingAmount = 25m,
            Currency = "EUR",
            Status = VoucherStatus.PartiallyRedeemed,
            ValidFromUtc = DateTime.UtcNow,
            ExpiresAtUtc = DateTime.UtcNow.AddYears(1),
            CreatedByUserId = "user-purge-1",
            CreatedAtUtc = DateTime.UtcNow,
            CancellationReason = "Cancelled for " + PiiName,
            InternalNote = "Buyer " + PiiEmail + " " + PiiPhone,
        });
        fx.Db.VoucherLedgerEntries.Add(new VoucherLedgerEntry
        {
            Id = ledgerId,
            TenantId = fx.TenantId,
            VoucherId = voucherId,
            Type = VoucherTransactionType.Redeem,
            Amount = -25m,
            BalanceAfter = 25m,
            CreatedByUserId = "user-purge-1",
            CreatedAtUtc = DateTime.UtcNow,
            CorrelationId = "ledger-keep",
        });
        fx.Db.Vouchers.Add(new Voucher
        {
            Id = otherVoucherId,
            TenantId = fx.OtherTenantId,
            CustomerId = fx.OtherCustomerId,
            CodeHash = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb",
            MaskedCode = "****9999",
            InitialAmount = 10m,
            RemainingAmount = 10m,
            Currency = "EUR",
            Status = VoucherStatus.Active,
            ValidFromUtc = DateTime.UtcNow,
            ExpiresAtUtc = DateTime.UtcNow.AddYears(1),
            CreatedByUserId = "user-other",
            CreatedAtUtc = DateTime.UtcNow,
            InternalNote = "Erika birthday voucher",
        });
        await fx.Db.SaveChangesAsync();

        await fx.PurgeAsync();

        var voucher = await fx.Db.Vouchers.IgnoreQueryFilters().SingleAsync(v => v.Id == voucherId);
        Assert.Equal(50m, voucher.InitialAmount);
        Assert.Equal(25m, voucher.RemainingAmount);
        Assert.Equal(codeHash, voucher.CodeHash);
        Assert.Equal("****1234", voucher.MaskedCode);
        Assert.Equal(VoucherStatus.PartiallyRedeemed, voucher.Status);
        Assert.Null(voucher.CustomerId);
        Assert.Null(voucher.InternalNote);
        Assert.Null(voucher.CancellationReason);

        var ledger = await fx.Db.VoucherLedgerEntries.IgnoreQueryFilters().SingleAsync(e => e.Id == ledgerId);
        Assert.Equal(-25m, ledger.Amount);
        Assert.Equal(25m, ledger.BalanceAfter);
        Assert.Equal("ledger-keep", ledger.CorrelationId);

        var other = await fx.Db.Vouchers.IgnoreQueryFilters().SingleAsync(v => v.Id == otherVoucherId);
        Assert.Equal("Erika birthday voucher", other.InternalNote);
        Assert.Equal(fx.OtherCustomerId, other.CustomerId);
        Assert.Equal(10m, other.RemainingAmount);
    }

    [Fact]
    public async Task TseSignatures_ArePreserved()
    {
        await using var fx = await PurgeFixture.CreateAsync();
        var id = Guid.NewGuid();
        var otherId = Guid.NewGuid();
        fx.Db.TseSignatures.Add(Signature(fx.TenantId, fx.RegisterId, id, KeptSignature, KeptTotal));
        fx.Db.TseSignatures.Add(Signature(fx.OtherTenantId, fx.OtherRegisterId, otherId, "jws-other", 9m));
        await fx.Db.SaveChangesAsync();

        await fx.PurgeAsync();

        var row = await fx.Db.TseSignatures.IgnoreQueryFilters().SingleAsync(s => s.Id == id);
        Assert.Equal(KeptSignature, row.Signature);
        Assert.Equal("header.keep", row.JwsHeader);
        Assert.Equal("payload.keep", row.JwsPayload);
        Assert.Equal("sig.keep", row.JwsSignature);
        Assert.Equal(KeptTotal, row.Amount);
        Assert.Equal("RE-TSE-1", row.InvoiceNumber);
        Assert.Equal("Invoice", row.SignatureType);
        Assert.True(row.IsValid);

        var other = await fx.Db.TseSignatures.IgnoreQueryFilters().SingleAsync(s => s.Id == otherId);
        Assert.Equal("jws-other", other.Signature);
        Assert.Equal(9m, other.Amount);
    }

    private static void AssertNoPii(string? value)
    {
        var text = value ?? string.Empty;
        Assert.DoesNotContain(PiiEmail, text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Mustermann", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(PiiPhone, text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Hauptstrasse", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(PiiTax, text, StringComparison.OrdinalIgnoreCase);
    }

    private static PaymentDetails Payment(Guid customerId, Guid registerId, Guid id, string customerName) =>
        new()
        {
            Id = id,
            CustomerId = customerId,
            CustomerName = customerName,
            CompanyName = "Cafe GmbH",
            CompanyAddress = customerName == "Erika Other" ? "Nebenstrasse 2" : PiiAddress,
            Steuernummer = customerName == "Erika Other" ? "ATU11111111" : PiiTax,
            TableNumber = 1,
            CashierId = "cashier",
            TotalAmount = customerName == "Erika Other" ? 5m : KeptTotal,
            TaxAmount = customerName == "Erika Other" ? 0.83m : KeptTax,
            PaymentMethodRaw = "0",
            CashRegisterId = registerId,
            ReceiptNumber = customerName == "Erika Other" ? "R-OTHER" : "R-100",
            TseSignature = customerName == "Erika Other" ? "jws-other" : KeptSignature,
            Notes = customerName == "Erika Other" ? "keep-other-note" : "Note for " + PiiName + " " + PiiEmail,
            PrescriptionReference = customerName == "Erika Other" ? null : "RX-" + PiiName,
            RouteFrom = customerName == "Erika Other" ? null : PiiAddress,
            RouteTo = customerName == "Erika Other" ? null : "Hotel " + PiiName,
            RefundReason = customerName == "Erika Other" ? null : "Refund " + PiiName,
            CancellationReason = customerName == "Erika Other" ? null : "Cancel " + PiiPhone,
            TaxDetails = JsonDocument.Parse("{}"),
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        };

    private static Invoice Invoice(Guid tenantId, Guid registerId, Guid id, Guid? sourcePaymentId) =>
        new()
        {
            Id = id,
            TenantId = tenantId,
            InvoiceNumber = sourcePaymentId.HasValue ? "RE-1" : "RE-DRAFT",
            InvoiceDate = DateTime.UtcNow,
            DueDate = DateTime.UtcNow,
            Status = sourcePaymentId.HasValue ? InvoiceStatus.Paid : InvoiceStatus.Draft,
            Subtotal = 35.42m,
            TaxAmount = KeptTax,
            TotalAmount = KeptTotal,
            PaidAmount = sourcePaymentId.HasValue ? KeptTotal : 0,
            RemainingAmount = sourcePaymentId.HasValue ? 0 : KeptTotal,
            CustomerName = PiiName,
            CustomerEmail = PiiEmail,
            CustomerPhone = PiiPhone,
            CustomerAddress = PiiAddress,
            CustomerTaxNumber = PiiTax,
            CompanyName = "Cafe GmbH",
            CompanyTaxNumber = "ATU87654321",
            CompanyAddress = "Sitz 9",
            CompanyEmail = "firma@cafe.at",
            CompanyPhone = "+431234567",
            KassenId = "K1",
            CashRegisterId = registerId,
            TseSignature = KeptSignature,
            SourcePaymentId = sourcePaymentId,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        };

    private static OnlineOrder OnlineOrderRow(Guid tenantId, Guid id, string name, string email) =>
        new()
        {
            Id = id,
            TenantId = tenantId,
            OrderNumber = name == PiiName ? "ORD-9" : "ORD-OTHER",
            CustomerName = name,
            CustomerPhone = name == PiiName ? PiiPhone : "+436649998877",
            CustomerEmail = email,
            DeliveryAddress = name == PiiName ? PiiAddress : "Andere Gasse 3",
            CustomerDeviceToken = name == PiiName ? "device-token-secret" : "device-other",
            Notes = name == PiiName ? "ring " + PiiName : "other note",
            OrderType = OnlineOrderTypes.Delivery,
            Total = name == PiiName ? 18m : 4m,
            Subtotal = name == PiiName ? 18m : 4m,
            PaymentMethod = OnlineOrderPaymentMethods.Cash,
            PaymentStatus = OnlineOrderPaymentStatuses.Pending,
            OrderStatus = OnlineOrderStatuses.Pending,
            Source = OnlineOrderSources.Web,
            CreatedAt = DateTime.UtcNow,
        };

    private static ActivityEvent Activity(Guid tenantId, Guid id, string actor, string description) =>
        new()
        {
            Id = id,
            TenantId = tenantId,
            Type = ActivityEventType.UserUpdated,
            Severity = ActivitySeverityNames.Info,
            Title = "User updated",
            Description = description,
            ActorName = actor,
            MetadataJson = actor == PiiName
                ? """{"email":"max.purge@test.at","name":"Max Mustermann"}"""
                : """{"email":"erika@other.at"}""",
            CreatedAtUtc = DateTime.UtcNow,
        };

    private static ApplicationUser User(
        string id,
        string userName,
        string email,
        string first,
        string last,
        string phone) =>
        new()
        {
            Id = id,
            UserName = userName,
            NormalizedUserName = userName.ToUpperInvariant(),
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            FirstName = first,
            LastName = last,
            PhoneNumber = phone,
            TaxNumber = id == "user-last-of-tenant" ? PiiTax : null,
            Notes = id == "user-last-of-tenant" ? PiiName : string.Empty,
            IsActive = true,
        };

    private static UserTenantMembership Membership(string userId, Guid tenantId) =>
        new()
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TenantId = tenantId,
            IsActive = true,
        };

    private static TseSignature Signature(Guid tenantId, Guid registerId, Guid id, string signature, decimal amount) =>
        new()
        {
            Id = id,
            TenantId = tenantId,
            CashRegisterId = registerId,
            Signature = signature,
            InvoiceNumber = amount == KeptTotal ? "RE-TSE-1" : "RE-OTHER",
            Amount = amount,
            SignatureType = "Invoice",
            JwsHeader = amount == KeptTotal ? "header.keep" : "header.other",
            JwsPayload = amount == KeptTotal ? "payload.keep" : "payload.other",
            JwsSignature = amount == KeptTotal ? "sig.keep" : "sig.other",
            CreatedAt = DateTime.UtcNow,
            IsValid = true,
        };

    private sealed class PurgeFixture : IAsyncDisposable
    {
        public required AppDbContext Db { get; init; }
        public Guid TenantId { get; init; }
        public Guid OtherTenantId { get; init; }
        public Guid CustomerId { get; init; }
        public Guid OtherCustomerId { get; init; }
        public Guid RegisterId { get; init; }
        public Guid OtherRegisterId { get; init; }

        public static async Task<PurgeFixture> CreateAsync()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;
            var db = new AppDbContext(options, TenantTestDoubles.TenantAccessorReturning(SystemTenantIds.Platform));
            var tenantId = Guid.NewGuid();
            var otherTenantId = Guid.NewGuid();
            var customerId = Guid.NewGuid();
            var otherCustomerId = Guid.NewGuid();
            var registerId = Guid.NewGuid();
            var otherRegisterId = Guid.NewGuid();

            db.Tenants.AddRange(
                new Tenant { Id = tenantId, Name = "Cafe", Slug = "cafe-" + tenantId.ToString("N")[..8], IsActive = true, CreatedAt = DateTime.UtcNow },
                new Tenant { Id = otherTenantId, Name = "Other", Slug = "other-" + otherTenantId.ToString("N")[..8], IsActive = true, CreatedAt = DateTime.UtcNow });
            db.Customers.AddRange(
                new Customer
                {
                    Id = customerId,
                    TenantId = tenantId,
                    Name = PiiName,
                    Email = PiiEmail,
                    Phone = PiiPhone,
                    Address = PiiAddress,
                    TaxNumber = PiiTax,
                    Notes = "allergic to nuts",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                },
                new Customer
                {
                    Id = otherCustomerId,
                    TenantId = otherTenantId,
                    Name = "Erika Other",
                    Email = "erika@other.at",
                    Phone = "+436649998877",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                });
            db.CashRegisters.AddRange(
                Register(tenantId, registerId, "K1"),
                Register(otherTenantId, otherRegisterId, "K2"));
            await db.SaveChangesAsync();

            return new PurgeFixture
            {
                Db = db,
                TenantId = tenantId,
                OtherTenantId = otherTenantId,
                CustomerId = customerId,
                OtherCustomerId = otherCustomerId,
                RegisterId = registerId,
                OtherRegisterId = otherRegisterId,
            };
        }

        public Task PurgeAsync() =>
            DataDeletionService.DeleteNonRksvDataAsync(Db, TenantId, CancellationToken.None);

        public async ValueTask DisposeAsync() => await Db.DisposeAsync();

        private static CashRegister Register(Guid tenantId, Guid id, string number) =>
            new()
            {
                Id = id,
                TenantId = tenantId,
                RegisterNumber = number,
                Location = "Bar",
                StartingBalance = 0,
                CurrentBalance = 0,
                LastBalanceUpdate = DateTime.UtcNow,
                Status = RegisterStatus.Open,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
            };
    }
}

/// <summary>
/// <c>elmah_error</c> is raw SQL and only redacted on PostgreSQL.
/// </summary>
[Collection("PostgreSqlReplay")]
[Trait("Category", "PostgreSql")]
public sealed class DataDeletionElmahRedactionTests
{
    private readonly PostgreSqlReplayFixture _fixture;

    public DataDeletionElmahRedactionTests(PostgreSqlReplayFixture fixture) => _fixture = fixture;

    [SkippableFact]
    public async Task ElmahError_MatchingTenantEmail_IsRedacted_OtherRowsStay()
    {
        Skip.IfNot(_fixture.HasDatabase, _fixture.SkipReason);

        var email = $"elmah-{Guid.NewGuid():N}@purge.test";
        var errorId = Guid.NewGuid();
        var controlId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var customerId = Guid.NewGuid();

        await using var db = new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>()
                .UseAppNpgsql(_fixture.ConnectionString)
                .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning))
                .Options,
            TenantTestDoubles.TenantAccessorReturning(SystemTenantIds.Platform));

        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Elmah Purge",
            Slug = "elmah-" + tenantId.ToString("N")[..12],
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        db.Customers.Add(new Customer
        {
            Id = customerId,
            TenantId = tenantId,
            Name = "Max Mustermann",
            Email = email,
            Phone = "+436641112233",
            Address = "Hauptstrasse 1",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        await EnsureElmahTableAsync();
        await InsertElmahAsync(errorId, email, $"failed for {email}", $"<error>{email}</error><name>Max Mustermann</name>");
        await InsertElmahAsync(controlId, "nobody-else@example.com", "unrelated failure", "<error>keep</error>");

        await DataDeletionService.DeleteNonRksvDataAsync(db, tenantId, CancellationToken.None);

        var purged = await ReadElmahAsync(errorId);
        Assert.Equal("anonymous", purged.User);
        Assert.Equal(GdprPayloadRedactor.Redacted, purged.Message);
        Assert.Equal(GdprPayloadRedactor.Redacted, purged.AllXml);
        Assert.DoesNotContain(email, purged.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Mustermann", purged.AllXml, StringComparison.OrdinalIgnoreCase);

        var control = await ReadElmahAsync(controlId);
        Assert.Equal("nobody-else@example.com", control.User);
        Assert.Equal("unrelated failure", control.Message);
        Assert.Equal("<error>keep</error>", control.AllXml);
    }

    private async Task EnsureElmahTableAsync()
    {
        await using var connection = new NpgsqlConnection(_fixture.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(AddElmahUserColumn.EnsureSql, connection);
        await command.ExecuteNonQueryAsync();
    }

    private async Task InsertElmahAsync(Guid errorId, string user, string message, string allXml)
    {
        await using var connection = new NpgsqlConnection(_fixture.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            """
            INSERT INTO elmah_error
                (errorid, application, host, type, source, message, "User", statuscode, timeutc, allxml)
            VALUES
                (@id, 'Regkasse', 'localhost', 'System.Exception', 'gdpr-test', @message, @user, 500, @time, @xml)
            """,
            connection);
        command.Parameters.AddWithValue("id", errorId);
        command.Parameters.AddWithValue("message", message);
        command.Parameters.AddWithValue("user", user);
        command.Parameters.AddWithValue("time", DateTime.UtcNow);
        command.Parameters.AddWithValue("xml", allXml);
        await command.ExecuteNonQueryAsync();
    }

    private async Task<(string? User, string Message, string AllXml)> ReadElmahAsync(Guid errorId)
    {
        await using var connection = new NpgsqlConnection(_fixture.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            """
            SELECT "User", message, allxml
            FROM elmah_error
            WHERE errorid = @id
            """,
            connection);
        command.Parameters.AddWithValue("id", errorId);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        return (
            reader.IsDBNull(0) ? null : reader.GetString(0),
            reader.GetString(1),
            reader.GetString(2));
    }
}

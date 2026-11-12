using System.Text.Json;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.DataDeletion;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class DataDeletionPiiPurgeTests
{
    [Fact]
    public async Task Purge_RemovesPii_AndKeepsFiscalAmountsAndSignatures()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        await using var db = new AppDbContext(options, TenantTestDoubles.TenantAccessorReturning(SystemTenantIds.Platform));

        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var registerId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();
        var userId = "user-purge-1";
        var keptUserId = "user-other-tenant";

        db.Tenants.AddRange(
            new Tenant { Id = tenantId, Name = "Cafe", Slug = "cafe-purge", IsActive = true, CreatedAt = DateTime.UtcNow },
            new Tenant { Id = otherTenantId, Name = "Other", Slug = "other-purge", IsActive = true, CreatedAt = DateTime.UtcNow });

        db.Customers.Add(new Customer
        {
            Id = customerId,
            TenantId = tenantId,
            Name = "Max Mustermann",
            Email = "max@test.at",
            Phone = "+436641112233",
            Address = "Hauptstrasse 1",
            TaxNumber = "ATU12345678",
            Notes = "allergic to nuts",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        db.Customers.Add(new Customer
        {
            Id = Guid.NewGuid(),
            TenantId = otherTenantId,
            Name = "Erika Other",
            Email = "erika@other.at",
            Phone = "+436649998877",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });

        db.CashRegisters.Add(new CashRegister
        {
            Id = registerId,
            TenantId = tenantId,
            RegisterNumber = "K1",
            Location = "Bar",
            StartingBalance = 0,
            CurrentBalance = 0,
            LastBalanceUpdate = DateTime.UtcNow,
            Status = RegisterStatus.Open,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });

        db.PaymentDetails.Add(new PaymentDetails
        {
            Id = paymentId,
            CustomerId = customerId,
            CustomerName = "Max Mustermann",
            CompanyName = "Cafe GmbH",
            CompanyAddress = "Hauptstrasse 1",
            Steuernummer = "ATU12345678",
            TableNumber = 1,
            CashierId = "cashier",
            TotalAmount = 42.50m,
            TaxAmount = 7.08m,
            PaymentMethodRaw = "0",
            CashRegisterId = registerId,
            ReceiptNumber = "R-100",
            TseSignature = "jws-keep",
            TaxDetails = JsonDocument.Parse("{}"),
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });

        db.Invoices.Add(new Invoice
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            InvoiceNumber = "RE-1",
            InvoiceDate = DateTime.UtcNow,
            DueDate = DateTime.UtcNow,
            Status = InvoiceStatus.Paid,
            Subtotal = 35.42m,
            TaxAmount = 7.08m,
            TotalAmount = 42.50m,
            PaidAmount = 42.50m,
            RemainingAmount = 0,
            CustomerName = "Max Mustermann",
            CustomerEmail = "max@test.at",
            CustomerPhone = "+436641112233",
            CustomerAddress = "Hauptstrasse 1",
            CustomerTaxNumber = "ATU12345678",
            CompanyName = "Cafe GmbH",
            CompanyTaxNumber = "ATU87654321",
            CompanyAddress = "Hauptstrasse 1",
            KassenId = "K1",
            CashRegisterId = registerId,
            TseSignature = "invoice-jws-keep",
            SourcePaymentId = paymentId,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });

        db.OnlineOrders.Add(new OnlineOrder
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            OrderNumber = "ORD-9",
            CustomerName = "Max Mustermann",
            CustomerPhone = "+436641112233",
            CustomerEmail = "max@test.at",
            DeliveryAddress = "Hauptstrasse 1",
            CustomerDeviceToken = "device-token",
            Notes = "ring the bell",
            OrderType = OnlineOrderTypes.Delivery,
            Total = 18m,
            Subtotal = 18m,
            PaymentMethod = OnlineOrderPaymentMethods.Cash,
            PaymentStatus = OnlineOrderPaymentStatuses.Pending,
            OrderStatus = OnlineOrderStatuses.Pending,
            Source = OnlineOrderSources.Web,
            CreatedAt = DateTime.UtcNow,
        });

        db.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            SessionId = "s",
            UserId = userId,
            UserRole = "Manager",
            Action = "UPDATE",
            EntityType = "User",
            EntityName = "max@test.at",
            OldValues = """{"Email":"max@test.at","FirstName":"Max"}""",
            NewValues = """{"Email":"max@test.at","LastName":"Mustermann"}""",
            Status = AuditLogStatus.Success,
            Timestamp = DateTime.UtcNow,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });

        db.ActivityEvents.Add(new ActivityEvent
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Type = ActivityEventType.UserUpdated,
            Severity = ActivitySeverityNames.Info,
            Title = "User updated",
            Description = "Changed max@test.at",
            ActorName = "Max Mustermann",
            MetadataJson = """{"email":"max@test.at"}""",
            CreatedAtUtc = DateTime.UtcNow,
        });

        db.SupportTickets.Add(new SupportTicket
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            TicketNumber = "T-1",
            Category = SupportTicketCategories.General,
            Priority = SupportTicketPriorities.Medium,
            Status = SupportTicketStatuses.Open,
            Title = "Call Max",
            Message = "Email max@test.at",
            CreatedByUserId = userId,
            CreatedByDisplayName = "Max Mustermann",
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
        });

        db.CommunicationLogs.Add(new CommunicationLog
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Email = "max@test.at",
            Subject = "Hello Max",
            Status = CommunicationLogStatuses.Sent,
            SentAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
        });

        db.OperationLogs.Add(new OperationLog
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            UserId = userId,
            OperationType = "UpdateCustomer",
            EntityType = "Customer",
            EntityId = customerId.ToString(),
            BeforeState = """{"name":"Max Mustermann","email":"max@test.at"}""",
            IpAddress = "127.0.0.1",
            CreatedAt = DateTime.UtcNow,
        });

        db.Appointments.Add(new Appointment
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CustomerId = customerId,
            StartUtc = DateTime.UtcNow,
            EndUtc = DateTime.UtcNow.AddHours(1),
            Notes = "Max birthday",
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
        });

        db.GuestFolios.Add(new GuestFolio
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CustomerId = customerId,
            RoomId = Guid.NewGuid(),
            CheckIn = DateTime.UtcNow,
            Notes = "late arrival Max",
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
        });

        db.DemoRequests.Add(new DemoRequest
        {
            Id = Guid.NewGuid(),
            Name = "Max Mustermann",
            Email = "max@test.at",
            Industry = "cafe",
            Message = "please call",
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
        });

        db.Users.AddRange(
            new ApplicationUser
            {
                Id = userId,
                UserName = "max",
                NormalizedUserName = "MAX",
                Email = "max@test.at",
                NormalizedEmail = "MAX@TEST.AT",
                FirstName = "Max",
                LastName = "Mustermann",
                PhoneNumber = "+436641112233",
                IsActive = true,
            },
            new ApplicationUser
            {
                Id = keptUserId,
                UserName = "erika",
                NormalizedUserName = "ERIKA",
                Email = "erika@other.at",
                NormalizedEmail = "ERIKA@OTHER.AT",
                FirstName = "Erika",
                LastName = "Other",
                PhoneNumber = "+436649998877",
                IsActive = true,
            });

        db.UserTenantMemberships.AddRange(
            new UserTenantMembership { Id = Guid.NewGuid(), UserId = userId, TenantId = tenantId, IsActive = true },
            new UserTenantMembership { Id = Guid.NewGuid(), UserId = keptUserId, TenantId = otherTenantId, IsActive = true });

        await db.SaveChangesAsync();

        await DataDeletionService.DeleteNonRksvDataAsync(db, tenantId, CancellationToken.None);

        var payment = await db.PaymentDetails.IgnoreQueryFilters().SingleAsync(p => p.Id == paymentId);
        Assert.Equal(42.50m, payment.TotalAmount);
        Assert.Equal(7.08m, payment.TaxAmount);
        Assert.Equal("R-100", payment.ReceiptNumber);
        Assert.Equal("jws-keep", payment.TseSignature);
        Assert.Equal(TenantPiiPurge.AnonymousName, payment.CustomerName);
        Assert.Null(payment.CompanyName);
        Assert.Null(payment.CompanyAddress);
        Assert.Equal(TenantPiiPurge.AnonymousName, payment.Steuernummer);
        Assert.NotEqual(customerId, payment.CustomerId);

        var invoice = await db.Invoices.IgnoreQueryFilters().SingleAsync(i => i.TenantId == tenantId);
        Assert.Equal(42.50m, invoice.TotalAmount);
        Assert.Equal("invoice-jws-keep", invoice.TseSignature);
        Assert.Equal("Cafe GmbH", invoice.CompanyName);
        Assert.Null(invoice.CustomerEmail);
        Assert.Null(invoice.CustomerPhone);
        Assert.Null(invoice.CustomerAddress);

        var online = await db.OnlineOrders.IgnoreQueryFilters().SingleAsync(o => o.TenantId == tenantId);
        Assert.Equal(18m, online.Total);
        Assert.Equal(string.Empty, online.CustomerPhone);
        Assert.Null(online.CustomerEmail);
        Assert.Null(online.DeliveryAddress);
        Assert.Null(online.CustomerDeviceToken);
        Assert.Null(online.Notes);

        var audit = await db.AuditLogs.IgnoreQueryFilters().SingleAsync(a => a.TenantId == tenantId);
        Assert.DoesNotContain("max@test.at", audit.OldValues ?? "", StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Mustermann", audit.NewValues ?? "", StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("max@test.at", audit.EntityName ?? "", StringComparison.OrdinalIgnoreCase);

        var activity = await db.ActivityEvents.IgnoreQueryFilters().SingleAsync(a => a.TenantId == tenantId);
        Assert.Equal(TenantPiiPurge.AnonymousName, activity.ActorName);
        Assert.Null(activity.Description);
        Assert.Null(activity.MetadataJson);

        var ticket = await db.SupportTickets.IgnoreQueryFilters().SingleAsync(t => t.TenantId == tenantId);
        Assert.Equal("[redacted]", ticket.Message);
        Assert.Equal(TenantPiiPurge.AnonymousName, ticket.CreatedByDisplayName);

        var mail = await db.CommunicationLogs.IgnoreQueryFilters().SingleAsync(c => c.TenantId == tenantId);
        Assert.DoesNotContain("max@test.at", mail.Email, StringComparison.OrdinalIgnoreCase);

        var operation = await db.OperationLogs.IgnoreQueryFilters().SingleAsync(o => o.TenantId == tenantId);
        Assert.DoesNotContain("max@test.at", operation.BeforeState ?? "", StringComparison.OrdinalIgnoreCase);
        Assert.Null(operation.IpAddress);

        var appointment = await db.Appointments.IgnoreQueryFilters().SingleAsync(a => a.TenantId == tenantId);
        Assert.Null(appointment.Notes);

        Assert.Equal(0, await db.GuestFolios.IgnoreQueryFilters().CountAsync(f => f.TenantId == tenantId));
        Assert.Equal(0, await db.Customers.IgnoreQueryFilters().CountAsync(c => c.Id == customerId));

        var demo = await db.DemoRequests.SingleAsync();
        Assert.Equal(TenantPiiPurge.AnonymousName, demo.Name);
        Assert.DoesNotContain("max@test.at", demo.Email, StringComparison.OrdinalIgnoreCase);

        var purgedUser = await db.Users.SingleAsync(u => u.Id == userId);
        Assert.Equal(userId, purgedUser.Id);
        Assert.DoesNotContain("max@test.at", purgedUser.Email ?? "", StringComparison.OrdinalIgnoreCase);
        Assert.Equal(TenantPiiPurge.AnonymousName, purgedUser.FirstName);
        Assert.Null(purgedUser.PhoneNumber);

        var kept = await db.Users.SingleAsync(u => u.Id == keptUserId);
        Assert.Equal("erika@other.at", kept.Email);
        Assert.Equal("Erika", kept.FirstName);

        var otherCustomer = await db.Customers.IgnoreQueryFilters().SingleAsync(c => c.TenantId == otherTenantId);
        Assert.Equal("erika@other.at", otherCustomer.Email);
        Assert.Equal("Erika Other", otherCustomer.Name);
    }
}

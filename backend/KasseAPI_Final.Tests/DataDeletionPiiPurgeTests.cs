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
        var otherRegisterId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();
        var userId = "user-purge-1";
        var keptUserId = "user-other-tenant";

        db.Tenants.AddRange(
            new Tenant
            {
                Id = tenantId,
                Name = "Cafe",
                Slug = "cafe-purge",
                Email = "cafe@test.at",
                Phone = "+431111111",
                Address = "Hauptstrasse 1",
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
            },
            new Tenant
            {
                Id = otherTenantId,
                Name = "Other",
                Slug = "other-purge",
                Email = "other@test.at",
                Phone = "+432222222",
                Address = "Nebenstrasse 2",
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
            });

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

        db.CashRegisters.AddRange(
            new CashRegister
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
            },
            new CashRegister
            {
                Id = otherRegisterId,
                TenantId = otherTenantId,
                RegisterNumber = "K2",
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
            Notes = "Max Mustermann, max@test.at, Hauptstrasse 1",
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
            CompanyEmail = "firma@cafe.at",
            CompanyPhone = "+431234567",
            KassenId = "K1",
            CashRegisterId = registerId,
            TseSignature = "invoice-jws-keep",
            SourcePaymentId = paymentId,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });

        var receiptId = Guid.NewGuid();
        db.Receipts.Add(new Receipt
        {
            ReceiptId = receiptId,
            TenantId = tenantId,
            PaymentId = paymentId,
            ReceiptNumber = "R-100",
            IssuedAt = DateTime.UtcNow,
            CashierId = "cashier",
            CashRegisterId = registerId,
            SubTotal = 35.42m,
            TaxTotal = 7.08m,
            GrandTotal = 42.50m,
            QrCodePayload = "fiscal-qr-keep",
            SignatureValue = "receipt-sig-keep",
            PrevSignatureValue = "prev-sig-keep",
            JwsHeader = "header-keep",
            JwsPayload = "payload-keep",
            JwsSignature = "jws-sig-keep",
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

        db.Carts.AddRange(
            new Cart
            {
                CartId = "cart-purge",
                UserId = userId,
                CustomerId = customerId,
                WaiterName = "Max Mustermann",
                Notes = "ring Max at max@test.at",
                TableNumber = 4,
                Status = CartStatus.Active,
                ExpiresAt = DateTime.UtcNow.AddHours(1),
                CreatedAt = DateTime.UtcNow,
            },
            new Cart
            {
                CartId = "cart-other",
                UserId = keptUserId,
                WaiterName = "Erika Other",
                Notes = "keep erika",
                TableNumber = 8,
                Status = CartStatus.Active,
                ExpiresAt = DateTime.UtcNow.AddHours(1),
                CreatedAt = DateTime.UtcNow,
            });
        db.CartItems.Add(new CartItem
        {
            CartId = "cart-purge",
            ProductId = Guid.NewGuid(),
            Quantity = 1,
            UnitPrice = 3m,
            Notes = "no onion for Max",
            CreatedAt = DateTime.UtcNow,
        });

        db.TableOrders.AddRange(
            new TableOrder
            {
                TableOrderId = "to-purge",
                TableNumber = 4,
                UserId = userId,
                CustomerId = customerId,
                CustomerName = "Max Mustermann",
                CustomerPhone = "+436641112233",
                WaiterName = "Max Mustermann",
                Notes = "birthday Max",
                Status = TableOrderStatus.Active,
                Subtotal = 10m,
                TaxAmount = 2m,
                TotalAmount = 12m,
                OrderStartTime = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
            },
            new TableOrder
            {
                TableOrderId = "to-other",
                TableNumber = 8,
                UserId = keptUserId,
                CustomerName = "Erika Other",
                CustomerPhone = "+436649998877",
                WaiterName = "Erika Other",
                Notes = "keep erika",
                Status = TableOrderStatus.Active,
                Subtotal = 5m,
                TaxAmount = 1m,
                TotalAmount = 6m,
                OrderStartTime = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
            });
        db.TableOrderItems.Add(new TableOrderItem
        {
            TableOrderId = "to-purge",
            ProductId = Guid.NewGuid(),
            ProductName = "Kaffee",
            Quantity = 1,
            UnitPrice = 3m,
            TotalPrice = 3m,
            TaxType = 1,
            Notes = "for Max",
            Status = ItemStatus.Pending,
            CreatedAt = DateTime.UtcNow,
        });

        var kitchenId = Guid.NewGuid();
        var otherKitchenId = Guid.NewGuid();
        db.KitchenOrders.AddRange(
            new KitchenOrder
            {
                Id = kitchenId,
                TenantId = tenantId,
                CashRegisterId = registerId,
                CreatedByUserId = userId,
                Notes = "Max allergy",
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow,
            },
            new KitchenOrder
            {
                Id = otherKitchenId,
                TenantId = otherTenantId,
                CashRegisterId = otherRegisterId,
                CreatedByUserId = keptUserId,
                Notes = "Erika allergy",
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow,
            });
        db.KitchenOrderItems.Add(new KitchenOrderItem
        {
            KitchenOrderId = kitchenId,
            ProductName = "Kaffee",
            Quantity = 1,
            Notes = "for Max",
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
        Assert.Equal("Cafe GmbH", payment.CompanyName);
        Assert.Equal("Hauptstrasse 1", payment.CompanyAddress);
        Assert.Equal("ATU12345678", payment.Steuernummer);
        Assert.NotEqual(TenantPiiPurge.AnonymousName, payment.CompanyName);
        Assert.NotEqual(TenantPiiPurge.AnonymousName, payment.Steuernummer);
        Assert.Null(payment.Notes);
        Assert.DoesNotContain("max@test.at", payment.CustomerName, StringComparison.OrdinalIgnoreCase);
        Assert.NotEqual(customerId, payment.CustomerId);

        var invoice = await db.Invoices.IgnoreQueryFilters().SingleAsync(i => i.TenantId == tenantId);
        Assert.Equal(42.50m, invoice.TotalAmount);
        Assert.Equal("invoice-jws-keep", invoice.TseSignature);
        Assert.Equal("Cafe GmbH", invoice.CompanyName);
        Assert.Equal("Hauptstrasse 1", invoice.CompanyAddress);
        Assert.Equal("ATU87654321", invoice.CompanyTaxNumber);
        Assert.Null(invoice.CompanyEmail);
        Assert.Null(invoice.CompanyPhone);
        Assert.Equal(TenantPiiPurge.AnonymousName, invoice.CustomerName);
        Assert.Null(invoice.CustomerEmail);
        Assert.Null(invoice.CustomerPhone);
        Assert.Null(invoice.CustomerAddress);
        Assert.Null(invoice.CustomerTaxNumber);

        var receipt = await db.Receipts.IgnoreQueryFilters().SingleAsync(r => r.ReceiptId == receiptId);
        Assert.Equal(42.50m, receipt.GrandTotal);
        Assert.Equal(7.08m, receipt.TaxTotal);
        Assert.Equal("R-100", receipt.ReceiptNumber);
        Assert.Equal("receipt-sig-keep", receipt.SignatureValue);
        Assert.Equal("prev-sig-keep", receipt.PrevSignatureValue);
        Assert.Equal("fiscal-qr-keep", receipt.QrCodePayload);
        Assert.Equal("jws-sig-keep", receipt.JwsSignature);

        var online = await db.OnlineOrders.IgnoreQueryFilters().SingleAsync(o => o.TenantId == tenantId);
        Assert.Equal(18m, online.Total);
        Assert.Equal(TenantPiiPurge.AnonymousName, online.CustomerName);
        Assert.Equal(string.Empty, online.CustomerPhone);
        Assert.Null(online.CustomerEmail);
        Assert.Null(online.DeliveryAddress);
        Assert.Null(online.CustomerDeviceToken);
        Assert.Null(online.Notes);
        Assert.DoesNotContain("Mustermann", online.CustomerName, StringComparison.OrdinalIgnoreCase);

        var audit = await db.AuditLogs.IgnoreQueryFilters().SingleAsync(a => a.TenantId == tenantId);
        Assert.DoesNotContain("max@test.at", audit.OldValues ?? "", StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Mustermann", audit.NewValues ?? "", StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Max", audit.OldValues ?? "", StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("max@test.at", audit.EntityName ?? "", StringComparison.OrdinalIgnoreCase);

        var activity = await db.ActivityEvents.IgnoreQueryFilters().SingleAsync(a => a.TenantId == tenantId);
        Assert.Equal(TenantPiiPurge.AnonymousName, activity.ActorName);
        Assert.Null(activity.Description);
        Assert.Null(activity.MetadataJson);

        var ticket = await db.SupportTickets.IgnoreQueryFilters().SingleAsync(t => t.TenantId == tenantId);
        Assert.Equal("[redacted]", ticket.Message);
        Assert.Equal("[redacted]", ticket.Title);
        Assert.Equal(TenantPiiPurge.AnonymousName, ticket.CreatedByDisplayName);
        Assert.DoesNotContain("Max", ticket.Title, StringComparison.OrdinalIgnoreCase);

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
        Assert.EndsWith("@anonymized.invalid", purgedUser.Email, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(TenantPiiPurge.AnonymousName, purgedUser.FirstName);
        Assert.Equal("User", purgedUser.LastName);
        Assert.Null(purgedUser.PhoneNumber);
        Assert.DoesNotContain("Mustermann", purgedUser.UserName ?? "", StringComparison.OrdinalIgnoreCase);

        var kept = await db.Users.SingleAsync(u => u.Id == keptUserId);
        Assert.Equal("erika@other.at", kept.Email);
        Assert.Equal("Erika", kept.FirstName);

        var cart = await db.Carts.SingleAsync(c => c.CartId == "cart-purge");
        Assert.Equal(TenantPiiPurge.AnonymousName, cart.WaiterName);
        Assert.Null(cart.Notes);
        Assert.Null(cart.CustomerId);
        Assert.Null((await db.CartItems.SingleAsync(i => i.CartId == "cart-purge")).Notes);
        var otherCart = await db.Carts.SingleAsync(c => c.CartId == "cart-other");
        Assert.Equal("Erika Other", otherCart.WaiterName);
        Assert.Equal("keep erika", otherCart.Notes);

        var tableOrder = await db.TableOrders.SingleAsync(o => o.TableOrderId == "to-purge");
        Assert.Equal(12m, tableOrder.TotalAmount);
        Assert.Equal(TenantPiiPurge.AnonymousName, tableOrder.CustomerName);
        Assert.Null(tableOrder.CustomerPhone);
        Assert.Equal(TenantPiiPurge.AnonymousName, tableOrder.WaiterName);
        Assert.Null(tableOrder.Notes);
        Assert.Null(tableOrder.CustomerId);
        Assert.Equal("Kaffee", (await db.TableOrderItems.SingleAsync(i => i.TableOrderId == "to-purge")).ProductName);
        Assert.Null((await db.TableOrderItems.SingleAsync(i => i.TableOrderId == "to-purge")).Notes);
        var otherTable = await db.TableOrders.SingleAsync(o => o.TableOrderId == "to-other");
        Assert.Equal("Erika Other", otherTable.CustomerName);
        Assert.Equal("keep erika", otherTable.Notes);
        Assert.Equal(6m, otherTable.TotalAmount);

        var kitchen = await db.KitchenOrders.IgnoreQueryFilters().SingleAsync(o => o.Id == kitchenId);
        Assert.Null(kitchen.Notes);
        Assert.Null((await db.KitchenOrderItems.SingleAsync(i => i.KitchenOrderId == kitchenId)).Notes);
        var otherKitchen = await db.KitchenOrders.IgnoreQueryFilters().SingleAsync(o => o.Id == otherKitchenId);
        Assert.Equal("Erika allergy", otherKitchen.Notes);

        var tenant = await db.Tenants.IgnoreQueryFilters().SingleAsync(t => t.Id == tenantId);
        Assert.Equal("Cafe", tenant.Name);
        Assert.Equal("cafe-purge", tenant.Slug);
        Assert.Equal("Hauptstrasse 1", tenant.Address);
        Assert.True(string.IsNullOrEmpty(tenant.Email));
        Assert.True(string.IsNullOrEmpty(tenant.Phone));
        var otherTenant = await db.Tenants.IgnoreQueryFilters().SingleAsync(t => t.Id == otherTenantId);
        Assert.Equal("other@test.at", otherTenant.Email);
        Assert.Equal("+432222222", otherTenant.Phone);

        var otherCustomer = await db.Customers.IgnoreQueryFilters().SingleAsync(c => c.TenantId == otherTenantId);
        Assert.Equal("erika@other.at", otherCustomer.Email);
        Assert.Equal("Erika Other", otherCustomer.Name);
    }
}

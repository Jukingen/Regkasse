using KasseAPI_Final.Authorization;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace KasseAPI_Final.Controllers;

[Authorize(Roles = Roles.SuperAdmin)]
[ApiController]
[Route("api/admin")]
[Produces("application/json")]
public sealed class DemoResetController : ControllerBase
{
    /// <summary>cash_registers.tenant_id — must match an existing tenants row when FK enforced (no EF Tenant seed here).</summary>
    private static readonly Guid DemoTenantId = Guid.Parse("9c8f4e2b-1a3d-4f6e-8b7c-0d1e2f3a4b5c");

    /// <summary>
    /// RKSV Startbeleg path uses fixed guest id (<see cref="RksvSpecialReceiptService"/>); demo reset must recreate this row.
    /// </summary>
    private static readonly Guid DemoGuestCustomerId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    private const string DemoGuestCustomerName = "Gastkunde";

    private readonly AppDbContext _db;
    private readonly IRksvSpecialReceiptService _rksvSpecialReceiptService;
    private readonly IHostEnvironment _environment;
    private readonly IConfiguration _configuration;
    private readonly ILogger<DemoResetController> _logger;

    public DemoResetController(
        AppDbContext db,
        IRksvSpecialReceiptService rksvSpecialReceiptService,
        IHostEnvironment environment,
        IConfiguration configuration,
        ILogger<DemoResetController> logger)
    {
        _db = db;
        _rksvSpecialReceiptService = rksvSpecialReceiptService;
        _environment = environment;
        _configuration = configuration;
        _logger = logger;
    }

    [HttpPost("demo/reset")]
    [Authorize(Roles = "SuperAdmin")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> ResetDemoDatabase(CancellationToken cancellationToken)
    {
        if (!_environment.IsDevelopment())
        {
            _logger.LogWarning("Demo reset rejected: endpoint is only allowed in Development.");
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                success = false,
                message = "Demo reset is only available in Development environment"
            });
        }

        var demoResetEnabled = _configuration.GetValue<bool>("DemoReset:Enabled");
        if (!demoResetEnabled)
        {
            _logger.LogWarning("Demo reset rejected: DemoReset:Enabled is false.");
            return BadRequest(new
            {
                success = false,
                message = "Demo reset feature is disabled"
            });
        }

        var resetAt = DateTime.UtcNow;
        _logger.LogInformation("Demo reset started at {ResetAtUtc}.", resetAt);

        await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var deleteStatements = new[]
            {
                "DELETE FROM offline_transactions;",
                "DELETE FROM tse_signatures;",
                "DELETE FROM rksv_special_receipt_finanz_online_submissions;",
                "DELETE FROM finanz_online_outbox_messages;",
                "DELETE FROM finanz_online_submissions;",
                "DELETE FROM daily_closings;",
                "DELETE FROM voucher_ledger_entries;",
                "DELETE FROM vouchers;",
                "DELETE FROM receipt_items;",
                "DELETE FROM receipts;",
                "DELETE FROM invoices;",
                "DELETE FROM payment_details;"
            };

            foreach (var sql in deleteStatements)
            {
                _logger.LogInformation("Executing demo reset SQL: {Sql}", sql);
                await _db.Database.ExecuteSqlRawAsync(sql, cancellationToken);
            }

            _logger.LogInformation("Resetting receipt_sequences and signature_chain_state.");
            await _db.Database.ExecuteSqlRawAsync("TRUNCATE TABLE receipt_sequences RESTART IDENTITY CASCADE;", cancellationToken);
            await _db.Database.ExecuteSqlRawAsync("TRUNCATE TABLE signature_chain_state RESTART IDENTITY CASCADE;", cancellationToken);

            _logger.LogInformation("Deleting existing cash registers and TSE devices.");
            await _db.Database.ExecuteSqlRawAsync("DELETE FROM tse_devices;", cancellationToken);
            await _db.Database.ExecuteSqlRawAsync("DELETE FROM cash_registers;", cancellationToken);

            var cashRegister = new CashRegister
            {
                Id = Guid.NewGuid(),
                TenantId = DemoTenantId,
                RegisterNumber = "DEMO-001",
                Location = "Demo Location",
                StartingBalance = 0m,
                CurrentBalance = 0m,
                LastBalanceUpdate = DateTime.UtcNow,
                Status = RegisterStatus.Open,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                CreatedBy = "demo-reset",
                UpdatedBy = "demo-reset",
                IsActive = true
            };
            _db.CashRegisters.Add(cashRegister);
            _logger.LogInformation(
                "Created default cash register {RegisterNumber} for tenant {TenantId} (no EF tenant row creation).",
                cashRegister.RegisterNumber,
                DemoTenantId);

            await EnsureDemoGuestCustomerAsync(cancellationToken);

            var tse = new TseDevice
            {
                Id = Guid.NewGuid(),
                SerialNumber = $"DEMO-TSE-{DateTime.UtcNow:yyyyMMddHHmmss}",
                DeviceType = "Fake",
                VendorId = "VID_04B8",
                ProductId = "PID_0E15",
                IsConnected = true,
                LastConnectionTime = DateTime.UtcNow,
                LastSignatureTime = DateTime.UtcNow,
                CertificateStatus = "VALID",
                MemoryStatus = "OK",
                CanCreateInvoices = true,
                TimeoutSeconds = 30,
                KassenId = cashRegister.Id,
                FinanzOnlineUsername = "demo",
                FinanzOnlineEnabled = true,
                LastFinanzOnlineSync = DateTime.UtcNow,
                PendingInvoices = 0,
                PendingReports = 0,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                CreatedBy = "demo-reset",
                UpdatedBy = "demo-reset",
                IsActive = true
            };
            _db.TseDevices.Add(tse);
            _logger.LogInformation("Created default fake TSE device.");

            await _db.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Creating fresh startbeleg for cash register {CashRegisterId}.", cashRegister.Id);
            await _rksvSpecialReceiptService.CreateStartbelegAsync(
                new CreateStartbelegRequest
                {
                    CashRegisterId = cashRegister.Id,
                    Reason = "Demo reset bootstrap startbeleg",
                    CorrelationId = $"demo-reset-{DateTime.UtcNow:yyyyMMddHHmmss}"
                },
                actorUserId: User?.Identity?.Name ?? "demo-reset-system",
                cancellationToken: cancellationToken);

            await tx.CommitAsync(cancellationToken);
            _logger.LogInformation("Demo reset completed successfully at {ResetAtUtc}.", resetAt);

            return Ok(new
            {
                success = true,
                message = "Demo reset completed",
                resetAt
            });
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync(cancellationToken);
            _logger.LogError(ex, "Demo reset failed and transaction rolled back.");
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                success = false,
                message = "Demo reset failed"
            });
        }
    }

    private async Task EnsureDemoGuestCustomerAsync(CancellationToken cancellationToken)
    {
        var byCanonicalId = await _db.Customers.FirstOrDefaultAsync(c => c.Id == DemoGuestCustomerId, cancellationToken);
        if (byCanonicalId is not null)
        {
            if (!byCanonicalId.IsSystem)
            {
                byCanonicalId.IsSystem = true;
                byCanonicalId.UpdatedAt = DateTime.UtcNow;
            }
            _logger.LogInformation("Guest customer row already exists for RKSV Startbeleg id.");
            return;
        }

        var byName = await _db.Customers.FirstOrDefaultAsync(c => c.Name == DemoGuestCustomerName, cancellationToken);
        if (byName is not null)
        {
            _logger.LogWarning(
                "Customer named {Name} exists with id {ExistingId}; Startbeleg still requires guest id {RequiredId}. Inserting canonical guest row.",
                DemoGuestCustomerName,
                byName.Id,
                DemoGuestCustomerId);
        }

        var now = DateTime.UtcNow;
        _db.Customers.Add(new Customer
        {
            Id = DemoGuestCustomerId,
            Name = DemoGuestCustomerName,
            CustomerNumber = "GUEST-0001",
            Email = "guest@demo.local",
            Phone = string.Empty,
            Address = string.Empty,
            Category = CustomerCategory.Regular,
            LoyaltyPoints = 0,
            TotalSpent = 0m,
            VisitCount = 0,
            Notes = "Demo reset walk-in guest (RKSV Startbeleg)",
            IsVip = false,
            DiscountPercentage = 0m,
            PreferredPaymentMethod = CustomerPaymentMethod.Cash,
            IsSystem = true,
            CreatedAt = now,
            UpdatedAt = now,
            CreatedBy = "demo-reset",
            UpdatedBy = "demo-reset",
            IsActive = true
        });

        _logger.LogInformation("Inserted guest customer {Name} with id required for RKSV Startbeleg.", DemoGuestCustomerName);
    }
}

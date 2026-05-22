using System.Data;
using KasseAPI_Final.Constants;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services;

public sealed class DemoResetService : IDemoResetService
{
    private static readonly HashSet<string> ExcludedTables = new(StringComparer.OrdinalIgnoreCase)
    {
        "__EFMigrationsHistory",
    };

    private readonly AppDbContext _db;
    private readonly ILogger<DemoResetService> _logger;
    private readonly IRksvSpecialReceiptService _rksvService;

    public DemoResetService(
        AppDbContext db,
        ILogger<DemoResetService> logger,
        IRksvSpecialReceiptService rksvService)
    {
        _db = db;
        _logger = logger;
        _rksvService = rksvService;
    }

    public async Task<ResetResult> ResetDatabaseAsync(CancellationToken ct)
    {
        var errors = new List<string>();
        var startbelegCreated = false;

        _logger.LogInformation("Demo reset started.");

        await using var transaction = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            var tableNames = await GetTruncatablePublicTableNamesAsync(ct);
            var dependencies = await GetForeignKeyDependenciesAsync(ct);
            var deletionOrder = BuildDeletionOrder(tableNames, dependencies);
            var estimatedRowCount = await EstimateRowsToDeleteAsync(tableNames, ct);

            _logger.LogInformation("Demo reset discovered {TableCount} table(s) for cleanup.", tableNames.Count);
            foreach (var table in deletionOrder)
            {
                _logger.LogInformation("Cleanup order candidate table: {TableName}", table);
            }

            if (tableNames.Count > 0)
            {
                var truncateSql = BuildTruncateSql(tableNames);
                _logger.LogInformation("Executing truncate cleanup with RESTART IDENTITY CASCADE.");
                await _db.Database.ExecuteSqlRawAsync(truncateSql, ct);
                _logger.LogInformation("Truncate cleanup completed.");
            }
            else
            {
                _logger.LogInformation("No truncatable public tables found; skipping truncate.");
            }

            var tenant = new Tenant
            {
                Id = LegacyDefaultTenantIds.Primary,
                Name = "Default",
                Slug = LegacyDefaultTenantIds.PrimarySlug,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                CreatedBy = "demo-reset",
                UpdatedBy = "demo-reset",
                IsActive = true,
            };
            _db.Tenants.Add(tenant);
            _logger.LogInformation("Default tenant recreated with id {TenantId}.", tenant.Id);

            var register = new CashRegister
            {
                Id = Guid.NewGuid(),
                TenantId = LegacyDefaultTenantIds.Primary,
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
                IsActive = true,
            };
            _db.CashRegisters.Add(register);
            _logger.LogInformation("Default cash register recreated: {RegisterNumber}.", register.RegisterNumber);

            var guestCustomer = new Customer
            {
                Id = WalkInCustomerConstants.GuestCustomerId,
                Name = "Gastkunde",
                CustomerNumber = "GUEST-0001",
                Email = "guest@demo.local",
                Phone = string.Empty,
                Address = string.Empty,
                Category = CustomerCategory.Regular,
                LoyaltyPoints = 0,
                TotalSpent = 0m,
                VisitCount = 0,
                Notes = "Demo walk-in customer.",
                IsVip = false,
                DiscountPercentage = 0m,
                PreferredPaymentMethod = CustomerPaymentMethod.Cash,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                CreatedBy = "demo-reset",
                UpdatedBy = "demo-reset",
                IsActive = true,
            };
            _db.Customers.Add(guestCustomer);
            _logger.LogInformation("Guest customer recreated for RKSV special receipts.");

            var tseDevice = new TseDevice
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
                KassenId = register.Id,
                FinanzOnlineUsername = "demo",
                FinanzOnlineEnabled = true,
                LastFinanzOnlineSync = DateTime.UtcNow,
                PendingInvoices = 0,
                PendingReports = 0,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                CreatedBy = "demo-reset",
                UpdatedBy = "demo-reset",
                IsActive = true,
            };
            _db.TseDevices.Add(tseDevice);
            _logger.LogInformation("Fake TSE device recreated for register {RegisterId}.", register.Id);

            await _db.SaveChangesAsync(ct);

            _logger.LogInformation("Creating RKSV Startbeleg for register {RegisterId}.", register.Id);
            await _rksvService.CreateStartbelegAsync(
                new CreateStartbelegRequest
                {
                    CashRegisterId = register.Id,
                    Reason = "Demo reset bootstrap startbeleg",
                    CorrelationId = $"demo-reset-{DateTime.UtcNow:yyyyMMddHHmmss}",
                },
                actorUserId: "demo-reset-system",
                cancellationToken: ct);

            startbelegCreated = true;
            _logger.LogInformation("RKSV Startbeleg created successfully.");

            await transaction.CommitAsync(ct);
            _logger.LogInformation("Demo reset committed successfully.");

            return new ResetResult
            {
                DeletedRecordsCount = estimatedRowCount,
                Errors = errors,
                StartbelegCreated = startbelegCreated,
            };
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(ct);
            var message = $"Demo reset failed: {ex.Message}";
            errors.Add(message);
            _logger.LogError(ex, "Demo reset failed and transaction rolled back.");

            return new ResetResult
            {
                DeletedRecordsCount = 0,
                Errors = errors,
                StartbelegCreated = false,
            };
        }
    }

    private async Task<List<string>> GetTruncatablePublicTableNamesAsync(CancellationToken ct)
    {
        var tables = new List<string>();
        var connection = _db.Database.GetDbConnection();
        var shouldClose = false;

        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(ct);
            shouldClose = true;
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT tablename
                FROM pg_tables
                WHERE schemaname = 'public'
                ORDER BY tablename;
                """;

            await using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                var table = reader.GetString(0);
                if (ExcludedTables.Contains(table))
                {
                    continue;
                }

                if (table.StartsWith("AspNet", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                tables.Add(table);
            }
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }

        return tables;
    }

    private async Task<List<(string ChildTable, string ParentTable)>> GetForeignKeyDependenciesAsync(CancellationToken ct)
    {
        var dependencies = new List<(string ChildTable, string ParentTable)>();
        var connection = _db.Database.GetDbConnection();
        var shouldClose = false;

        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(ct);
            shouldClose = true;
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT tc.table_name AS child_table, ccu.table_name AS parent_table
                FROM information_schema.table_constraints tc
                JOIN information_schema.constraint_column_usage ccu
                  ON tc.constraint_name = ccu.constraint_name
                 AND tc.constraint_schema = ccu.constraint_schema
                WHERE tc.constraint_type = 'FOREIGN KEY'
                  AND tc.table_schema = 'public'
                  AND ccu.table_schema = 'public';
                """;

            await using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                var child = reader.GetString(0);
                var parent = reader.GetString(1);
                dependencies.Add((child, parent));
                _logger.LogInformation("FK dependency detected: {Child} -> {Parent}", child, parent);
            }
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }

        return dependencies;
    }

    private static List<string> BuildDeletionOrder(
        IReadOnlyCollection<string> tables,
        IReadOnlyCollection<(string ChildTable, string ParentTable)> dependencies)
    {
        var set = new HashSet<string>(tables, StringComparer.OrdinalIgnoreCase);
        var outgoing = set.ToDictionary(t => t, _ => new HashSet<string>(StringComparer.OrdinalIgnoreCase), StringComparer.OrdinalIgnoreCase);
        var indegree = set.ToDictionary(t => t, _ => 0, StringComparer.OrdinalIgnoreCase);

        foreach (var (child, parent) in dependencies)
        {
            if (!set.Contains(child) || !set.Contains(parent))
            {
                continue;
            }

            if (outgoing[parent].Add(child))
            {
                indegree[child]++;
            }
        }

        var queue = new Queue<string>(indegree.Where(x => x.Value == 0).Select(x => x.Key).OrderBy(x => x, StringComparer.OrdinalIgnoreCase));
        var ordered = new List<string>();

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            ordered.Add(current);

            foreach (var next in outgoing[current].OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
            {
                indegree[next]--;
                if (indegree[next] == 0)
                {
                    queue.Enqueue(next);
                }
            }
        }

        if (ordered.Count < set.Count)
        {
            foreach (var remaining in set.Except(ordered, StringComparer.OrdinalIgnoreCase).OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
            {
                ordered.Add(remaining);
            }
        }

        ordered.Reverse();
        return ordered;
    }

    private async Task<int> EstimateRowsToDeleteAsync(IReadOnlyCollection<string> tables, CancellationToken ct)
    {
        if (tables.Count == 0)
        {
            return 0;
        }

        var safeNames = tables
            .Select(t => t.Replace("'", "''", StringComparison.Ordinal))
            .ToArray();
        var inList = string.Join(", ", safeNames.Select(t => $"'{t}'"));

        var connection = _db.Database.GetDbConnection();
        var shouldClose = false;

        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(ct);
            shouldClose = true;
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = $"""
                SELECT COALESCE(SUM(CASE WHEN c.reltuples < 0 THEN 0 ELSE c.reltuples END), 0)::bigint
                FROM pg_class c
                JOIN pg_namespace n ON n.oid = c.relnamespace
                WHERE n.nspname = 'public'
                  AND c.relname IN ({inList});
                """;

            var scalar = await command.ExecuteScalarAsync(ct);
            var estimate = scalar is null || scalar == DBNull.Value ? 0L : Convert.ToInt64(scalar);
            if (estimate <= 0)
            {
                return 0;
            }

            return estimate > int.MaxValue ? int.MaxValue : (int)estimate;
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static string BuildTruncateSql(IReadOnlyCollection<string> tables)
    {
        var quotedTables = tables
            .OrderBy(t => t, StringComparer.OrdinalIgnoreCase)
            .Select(t => $"\"{t.Replace("\"", "\"\"", StringComparison.Ordinal)}\"")
            .ToArray();

        return $"TRUNCATE TABLE {string.Join(", ", quotedTables)} RESTART IDENTITY CASCADE;";
    }
}

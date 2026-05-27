using System.ComponentModel.DataAnnotations;
using System.Net.Mail;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Data;
using KasseAPI_Final.Helpers;
using KasseAPI_Final.Models.DTOs;
using KasseAPI_Final.Services.AdminTenants;
using KasseAPI_Final.Tenancy;
using KasseAPI_Final.Validators;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services;

public interface IBulkUserImportService
{
    BulkImportPreviewResponseDto Preview(Stream fileStream, string fileName, int maxPreviewRows = 10);
}

public sealed class BulkUserImportService : IBulkUserImportService
{
    public const int BatchSize = 100;
    private const int MaxErrorsInPollPayload = 100;

    private static readonly HashSet<string> AllowedRoles = new(StringComparer.OrdinalIgnoreCase)
    {
        Roles.Manager,
        Roles.Cashier,
        Roles.Accountant,
    };

    private readonly AppDbContext _db;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly ITenantUserService _tenantUserService;
    private readonly IUserUniquenessValidationService _uniquenessValidation;
    private readonly IBulkUserImportResultStore _resultStore;
    private readonly ILogger<BulkUserImportService> _logger;

    public BulkUserImportService(
        AppDbContext db,
        RoleManager<IdentityRole> roleManager,
        ITenantUserService tenantUserService,
        IUserUniquenessValidationService uniquenessValidation,
        IBulkUserImportResultStore resultStore,
        ILogger<BulkUserImportService> logger)
    {
        _db = db;
        _roleManager = roleManager;
        _tenantUserService = tenantUserService;
        _uniquenessValidation = uniquenessValidation;
        _resultStore = resultStore;
        _logger = logger;
    }

    public BulkImportPreviewResponseDto Preview(Stream fileStream, string fileName, int maxPreviewRows = 10)
    {
        var (rows, parseError) = BulkUserImportFileParser.Parse(fileStream, fileName);
        return BulkUserImportFileParser.BuildPreview(rows, parseError, maxPreviewRows);
    }

    public async Task RunJobAsync(BulkImportJobEntry job, CancellationToken requestAborted = default)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(job.Cancellation.Token, requestAborted);
        var cancellationToken = linked.Token;

        job.Status = BulkImportJobStatus.Running;
        var actor = job.Actor;

        try
        {
            var tenantsBySlug = await _db.Tenants
                .AsNoTracking()
                .Where(t => t.IsActive && t.Slug != "admin" && t.Slug != LegacyDefaultTenantIds.PrimarySlug)
                .ToDictionaryAsync(t => t.Slug, StringComparer.OrdinalIgnoreCase, cancellationToken)
                .ConfigureAwait(false);

            string? actorTenantSlug = null;
            if (actor.ActorTenantId is Guid tid)
            {
                actorTenantSlug = await _db.Tenants.AsNoTracking()
                    .Where(t => t.Id == tid)
                    .Select(t => t.Slug)
                    .FirstOrDefaultAsync(cancellationToken)
                    .ConfigureAwait(false);
            }

            var seenEmails = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var resultLines = new List<string>
            {
                "row,email,status,error,userName,generatedPassword,tenantSlug",
            };

            var rowIndex = 0;
            while (rowIndex < job.Rows.Count)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var batch = job.Rows.Skip(rowIndex).Take(BatchSize).ToList();
                rowIndex += batch.Count;

                foreach (var row in batch)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var rowErrors = await ValidateRowAsync(
                        row,
                        seenEmails,
                        tenantsBySlug,
                        actor.ActorIsSuperAdmin,
                        actorTenantSlug,
                        cancellationToken).ConfigureAwait(false);

                    if (rowErrors.Count > 0)
                    {
                        foreach (var err in rowErrors)
                        {
                            job.AddError(err);
                            job.FailedCount++;
                        }

                        resultLines.Add($"{row.RowNumber},{CsvEscape(row.Email)},failed,{CsvEscape(rowErrors[0].Error)},,,{CsvEscape(row.TenantSlug)}");
                        job.ProcessedRows++;
                        continue;
                    }

                    var tenantRow = tenantsBySlug[row.TenantSlug.Trim()];
                    (CreateTenantUserResultDto? createResult, string? createError) = await _tenantUserService
                        .CreateAsync(
                            tenantRow.Id,
                            new CreateTenantUserRequest
                            {
                                Email = row.Email,
                                UserName = row.Username,
                                FirstName = row.FirstName,
                                LastName = row.LastName,
                                Role = NormalizeRole(row.Role),
                            },
                            actor.ActorUserId,
                            actor.ActorRole,
                            cancellationToken)
                        .ConfigureAwait(false);

                    job.ProcessedRows++;

                    if (createError != null)
                    {
                        job.AddError(new BulkImportErrorDto
                        {
                            Row = row.RowNumber,
                            Email = row.Email,
                            Error = createError,
                        });
                        job.FailedCount++;
                        resultLines.Add($"{row.RowNumber},{CsvEscape(row.Email)},failed,{CsvEscape(createError)},,,{CsvEscape(row.TenantSlug)}");
                        continue;
                    }

                    job.SuccessCount++;
                    resultLines.Add(
                        $"{row.RowNumber},{CsvEscape(row.Email)},success,,{CsvEscape(createResult!.UserName)},{CsvEscape(createResult.GeneratedPassword)},{CsvEscape(row.TenantSlug)}");
                }
            }

            if (resultLines.Count > 1)
            {
                var resultId = await _resultStore.SaveResultCsvAsync(resultLines, cancellationToken).ConfigureAwait(false);
                job.DownloadUrl = $"/api/admin/users/bulk-import/results/{resultId}.csv";
            }

            job.Status = job.Cancellation.IsCancellationRequested
                ? BulkImportJobStatus.Cancelled
                : BulkImportJobStatus.Completed;
        }
        catch (OperationCanceledException)
        {
            job.Status = BulkImportJobStatus.Cancelled;
            job.Message ??= "Import cancelled.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Bulk import job {JobId} failed", job.JobId);
            job.Status = BulkImportJobStatus.Failed;
            job.Message = "Import failed unexpectedly.";
        }
    }

    private async Task<List<BulkImportErrorDto>> ValidateRowAsync(
        BulkImportRow row,
        HashSet<string> seenEmails,
        IReadOnlyDictionary<string, Models.Tenant> tenantsBySlug,
        bool actorIsSuperAdmin,
        string? actorTenantSlug,
        CancellationToken cancellationToken)
    {
        var list = new List<BulkImportErrorDto>();

        void Fail(string message) =>
            list.Add(new BulkImportErrorDto { Row = row.RowNumber, Email = row.Email, Error = message });

        if (string.IsNullOrWhiteSpace(row.Email))
        {
            Fail("Email is required.");
            return list;
        }

        if (!IsValidEmail(row.Email))
        {
            Fail("Invalid email format.");
            return list;
        }

        var emailKey = row.Email.Trim();
        if (!seenEmails.Add(emailKey))
        {
            Fail("Duplicate email in import file.");
            return list;
        }

        if (string.IsNullOrWhiteSpace(row.Role))
        {
            Fail("Role is required.");
            return list;
        }

        var role = row.Role.Trim();
        if (string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase))
        {
            Fail("Role 'Admin' is not supported. Use Manager, Cashier, or Accountant.");
            return list;
        }

        if (!AllowedRoles.Contains(role))
        {
            Fail($"Role '{role}' is not allowed for bulk import. Allowed: Manager, Cashier, Accountant.");
            return list;
        }

        if (await _roleManager.FindByNameAsync(role).ConfigureAwait(false) == null)
        {
            Fail($"Role '{role}' does not exist.");
            return list;
        }

        if (string.IsNullOrWhiteSpace(row.TenantSlug))
        {
            Fail("tenantSlug is required.");
            return list;
        }

        if (!tenantsBySlug.TryGetValue(row.TenantSlug.Trim(), out _))
        {
            Fail($"Tenant '{row.TenantSlug}' was not found or is not active.");
            return list;
        }

        if (!actorIsSuperAdmin)
        {
            if (string.IsNullOrEmpty(actorTenantSlug))
            {
                Fail("Cannot resolve your tenant context for import.");
                return list;
            }

            if (!string.Equals(actorTenantSlug, row.TenantSlug.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                Fail($"You may only import users for tenant '{actorTenantSlug}'.");
                return list;
            }
        }

        if (!string.IsNullOrWhiteSpace(row.Username))
        {
            var reserved = UsernameValidation.ValidateAssignableUsername(row.Username.Trim());
            if (reserved != null)
            {
                Fail(reserved);
                return list;
            }

            if (await _uniquenessValidation
                    .IsUserNameTakenByOtherUserAsync(row.Username.Trim(), excludeUserId: null)
                    .ConfigureAwait(false))
            {
                Fail(UsernameConflictMessages.Detail(row.Username.Trim()));
                return list;
            }
        }

        if (await _uniquenessValidation
                .IsEmailTakenByOtherUserAsync(emailKey, excludeUserId: null)
                .ConfigureAwait(false))
        {
            Fail($"Email '{emailKey}' is already in use.");
        }

        return list;
    }

    private static string NormalizeRole(string role) =>
        role.Trim() switch
        {
            var r when string.Equals(r, Roles.Manager, StringComparison.OrdinalIgnoreCase) => Roles.Manager,
            var r when string.Equals(r, Roles.Cashier, StringComparison.OrdinalIgnoreCase) => Roles.Cashier,
            var r when string.Equals(r, Roles.Accountant, StringComparison.OrdinalIgnoreCase) => Roles.Accountant,
            _ => role.Trim(),
        };

    private static bool IsValidEmail(string email)
    {
        if (!new EmailAddressAttribute().IsValid(email))
            return false;

        try
        {
            _ = new MailAddress(email);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string CsvEscape(string? value)
    {
        var v = value ?? string.Empty;
        if (v.Contains('"', StringComparison.Ordinal) || v.Contains(',', StringComparison.Ordinal) || v.Contains('\n', StringComparison.Ordinal))
            return $"\"{v.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
        return v;
    }
}

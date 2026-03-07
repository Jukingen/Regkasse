using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KasseAPI_Final.Controllers.Base;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Services;

namespace KasseAPI_Final.Controllers
{
    /// <summary>
    /// Phase 2: Admin-only migration endpoints. Legacy modifier → add-on product migration.
    /// </summary>
    [Route("api/admin")]
    [ApiController]
    [Authorize]
    public class AdminMigrationController : BaseController
    {
        private readonly IModifierMigrationService _modifierMigrationService;

        public AdminMigrationController(
            IModifierMigrationService modifierMigrationService,
            ILogger<AdminMigrationController> logger)
            : base(logger)
        {
            _modifierMigrationService = modifierMigrationService;
        }

        /// <summary>
        /// Legacy modifier'ları sellable add-on product'lara migrate eder. Idempotent; zaten migrate edilmiş olanlar atlanır.
        /// Sadece Administrator rolü. DryRun=true ile DB'ye yazmadan rapor alınabilir.
        /// </summary>
        [HttpPost("migrate-legacy-modifiers")]
        [Authorize(Roles = "Administrator")]
        public async Task<IActionResult> MigrateLegacyModifiers([FromBody] ModifierMigrationRequestDto request, CancellationToken cancellationToken)
        {
            if (request == null)
                return ErrorResponse("Request body is required.", 400);
            if (request.DefaultCategoryId == Guid.Empty)
                return ErrorResponse("DefaultCategoryId is required and must be a valid category ID.", 400);

            try
            {
                var result = await _modifierMigrationService.MigrateAsync(
                    request.DefaultCategoryId,
                    request.DryRun,
                    cancellationToken);

                _logger.LogInformation(
                    "Legacy modifier migration completed. DryRun={DryRun}, Total={Total}, Migrated={Migrated}, Skipped/Duplicate={Skipped}, Errors={Errors}",
                    request.DryRun, result.TotalProcessed, result.MigratedCount, result.SkippedCount, result.ErrorCount);

                return SuccessResponse(result, request.DryRun
                    ? "Dry run completed. No changes written."
                    : "Migration completed.");
            }
            catch (Exception ex)
            {
                return HandleException(ex, "AdminMigration.MigrateLegacyModifiers");
            }
        }
    }
}

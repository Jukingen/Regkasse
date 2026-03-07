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
        /// Legacy modifier migration progress (Phase B). Returns active legacy modifier count and groups-with-modifiers-only count.
        /// </summary>
        [HttpGet("migration-progress")]
        [Authorize(Roles = "Administrator")]
        public async Task<IActionResult> GetMigrationProgress(CancellationToken cancellationToken = default)
        {
            try
            {
                var progress = await _modifierMigrationService.GetMigrationProgressAsync(cancellationToken);
                return SuccessResponse(progress, "Migration progress retrieved.");
            }
            catch (Exception ex)
            {
                return HandleException(ex, "AdminMigration.GetMigrationProgress");
            }
        }

        /// <summary>
        /// Batch migration: best-effort. Migrates all active legacy modifiers to add-on products.
        /// Idempotent (already-migrated skipped). Partial success: failures are reported in result.Errors; successful items remain committed.
        /// Batch does not deactivate legacy modifiers. Administrator only. DryRun=true for report without writes.
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

        /// <summary>
        /// Einzelnen Legacy-Modifier als Add-on-Produkt migrieren. Transaktional; verhindert Duplikate.
        /// Legacy-Modifier wird nicht gelöscht, sondern nach Migration deaktiviert (IsActive=false).
        /// </summary>
        /// <param name="modifierId">Zu migrierender Modifier.</param>
        /// <param name="request">CategoryId (erforderlich), MarkModifierInactive (default: true).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        [HttpPost("modifiers/{modifierId:guid}/migrate-to-product")]
        [Authorize(Roles = "Administrator")]
        public async Task<IActionResult> MigrateModifierToProduct(Guid modifierId, [FromBody] MigrateSingleModifierRequestDto request, CancellationToken cancellationToken = default)
        {
            if (request == null)
                return ErrorResponse("Request body is required.", 400);
            if (request.CategoryId == Guid.Empty)
                return ErrorResponse("CategoryId is required and must be a valid category ID.", 400);

            try
            {
                var result = await _modifierMigrationService.MigrateSingleByModifierIdAsync(
                    modifierId,
                    request.CategoryId,
                    request.MarkModifierInactive,
                    cancellationToken);

                _logger.LogInformation(
                    "Modifier migrated to product: ModifierId={ModifierId}, ProductId={ProductId}, AlreadyMigrated={AlreadyMigrated}",
                    modifierId, result.ProductId, result.AlreadyMigrated);

                return SuccessResponse(result, result.AlreadyMigrated
                    ? "Modifier was already migrated. Existing add-on product returned."
                    : "Modifier migrated to add-on product successfully.");
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning("Modifier migration failed: {Message}", ex.Message);
                return ErrorResponse(ex.Message, 400);
            }
            catch (Exception ex)
            {
                return HandleException(ex, "AdminMigration.MigrateModifierToProduct");
            }
        }
    }
}

using System;
using System.Threading.Tasks;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Controllers.Base;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace KasseAPI_Final.Controllers
{
    [ApiController]
    [Route("api/offline-transactions")]
    public class OfflineTransactionsController : BaseController
    {
        private readonly IOfflineTransactionService _offlineService;

        public OfflineTransactionsController(
            ILogger<OfflineTransactionsController> logger,
            IOfflineTransactionService offlineService) : base(logger)
        {
            _offlineService = offlineService;
        }

        /// <summary>
        /// Replays offline intents in the provided order.
        /// Creates canonical fiscal Payment + Receipt for Synced outcomes only.
        /// </summary>
        [HttpPost("replay")]
        [HasPermission(AppPermissions.PaymentTake)]
        public async Task<IActionResult> Replay([FromBody] ReplayOfflineTransactionsRequest request)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (string.IsNullOrEmpty(userId))
                    return ErrorResponse("User not authenticated", 401);

                var userRole = GetCurrentUserRole() ?? "Unknown";

                var response = await _offlineService.ReplayOfflineTransactionsAsync(request, userId, userRole);

                return Ok(new
                {
                    success = true,
                    replayBatchCorrelationId = response.ReplayBatchCorrelationId,
                    data = response.Items,
                    timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Offline replay failed.");
                return ErrorResponse("Offline replay failed", 400);
            }
        }
    }
}


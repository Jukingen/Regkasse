using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Registrierkasse.Services;
using Registrierkasse.Models;

namespace Registrierkasse.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class AuditController : ControllerBase
    {
        private readonly IAuditService _auditService;
        private readonly ILogger<AuditController> _logger;

        public AuditController(IAuditService auditService, ILogger<AuditController> logger)
        {
            _auditService = auditService;
            _logger = logger;
        }

        [HttpGet("logs")]
        public async Task<IActionResult> GetAuditLogs(
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null,
            [FromQuery] string? userId = null,
            [FromQuery] string? action = null,
            [FromQuery] string? entityType = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50)
        {
            try
            {
                var logs = await _auditService.GetAuditLogsAsync(
                    startDate, endDate, userId, action, entityType, page, pageSize);

                return Ok(new
                {
                    data = logs,
                    pagination = new
                    {
                        page,
                        pageSize,
                        total = logs.Count
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve audit logs");
                return StatusCode(500, new { error = "Failed to retrieve audit logs" });
            }
        }

        [HttpGet("logs/{id}")]
        public async Task<IActionResult> GetAuditLogById(string id)
        {
            try
            {
                // This would require adding a method to get by ID in the service
                // For now, return not implemented
                return StatusCode(501, new { error = "Get by ID not implemented yet" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve audit log by ID: {Id}", id);
                return StatusCode(500, new { error = "Failed to retrieve audit log" });
            }
        }

        [HttpGet("summary")]
        public async Task<IActionResult> GetAuditSummary(
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null)
        {
            try
            {
                var logs = await _auditService.GetAuditLogsAsync(startDate, endDate);

                var summary = new
                {
                    totalLogs = logs.Count,
                    actions = logs.GroupBy(l => l.Action)
                        .Select(g => new { action = g.Key, count = g.Count() })
                        .OrderByDescending(x => x.count)
                        .Take(10),
                    entityTypes = logs.GroupBy(l => l.EntityType)
                        .Select(g => new { entityType = g.Key, count = g.Count() })
                        .OrderByDescending(x => x.count)
                        .Take(10),
                    users = logs.GroupBy(l => l.UserName)
                        .Select(g => new { userName = g.Key, count = g.Count() })
                        .OrderByDescending(x => x.count)
                        .Take(10),
                    statuses = logs.GroupBy(l => l.Status)
                        .Select(g => new { status = g.Key, count = g.Count() })
                };

                return Ok(summary);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate audit summary");
                return StatusCode(500, new { error = "Failed to generate audit summary" });
            }
        }

        [HttpGet("export")]
        public async Task<IActionResult> ExportAuditLogs(
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null,
            [FromQuery] string? userId = null,
            [FromQuery] string? action = null,
            [FromQuery] string? entityType = null,
            [FromQuery] string format = "json")
        {
            try
            {
                var logs = await _auditService.GetAuditLogsAsync(
                    startDate, endDate, userId, action, entityType, 1, int.MaxValue);

                switch (format.ToLower())
                {
                    case "json":
                        return Ok(logs);
                    
                    case "csv":
                        var csv = ConvertToCsv(logs);
                        return File(System.Text.Encoding.UTF8.GetBytes(csv), "text/csv", "audit_logs.csv");
                    
                    default:
                        return BadRequest(new { error = "Unsupported format. Use 'json' or 'csv'" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to export audit logs");
                return StatusCode(500, new { error = "Failed to export audit logs" });
            }
        }

        private string ConvertToCsv(List<AuditLog> logs)
        {
            var csv = new System.Text.StringBuilder();
            
            // Header
            csv.AppendLine("ID,Action,EntityType,EntityId,UserId,UserName,UserRole,IpAddress,Description,Status,ErrorMessage,CreatedAt");
            
            // Data
            foreach (var log in logs)
            {
                csv.AppendLine($"\"{log.Id}\",\"{log.Action}\",\"{log.EntityType}\",\"{log.EntityId}\",\"{log.UserId}\",\"{log.UserName}\",\"{log.UserRole}\",\"{log.IpAddress}\",\"{log.Description}\",\"{log.Status}\",\"{log.ErrorMessage}\",\"{log.CreatedAt:yyyy-MM-dd HH:mm:ss}\"");
            }
            
            return csv.ToString();
        }
    }
} 
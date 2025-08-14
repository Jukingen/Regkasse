using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

namespace KasseAPI_Final.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class AuditController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ILogger<AuditController> _logger;

        public AuditController(AppDbContext context, ILogger<AuditController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // GET: api/audit
        [HttpGet]
        [Authorize(Roles = "Administrator,Manager")]
        public async Task<ActionResult<IEnumerable<AuditLog>>> GetAuditLogs(
            [FromQuery] DateTime? startDate,
            [FromQuery] DateTime? endDate,
            [FromQuery] string? action,
            [FromQuery] string? entityType,
            [FromQuery] string? userId,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50)
        {
            try
            {
                var query = _context.AuditLogs.AsQueryable();

                // Filtreleme
                if (startDate.HasValue)
                    query = query.Where(a => a.Timestamp >= startDate.Value);

                if (endDate.HasValue)
                    query = query.Where(a => a.Timestamp <= endDate.Value);

                if (!string.IsNullOrEmpty(action))
                    query = query.Where(a => a.Action.Contains(action));

                if (!string.IsNullOrEmpty(entityType))
                    query = query.Where(a => a.EntityType.Contains(entityType));

                if (!string.IsNullOrEmpty(userId))
                    query = query.Where(a => a.UserId == userId);

                // Sayfalama
                var totalCount = await query.CountAsync();
                var auditLogs = await query
                    .OrderByDescending(a => a.Timestamp)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                var result = new
                {
                    Data = auditLogs,
                    TotalCount = totalCount,
                    Page = page,
                    PageSize = pageSize,
                    TotalPages = (int)Math.Ceiling((double)totalCount / pageSize)
                };

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting audit logs");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        // GET: api/audit/{id}
        [HttpGet("{id}")]
        [Authorize(Roles = "Administrator,Manager")]
        public async Task<ActionResult<AuditLog>> GetAuditLog(Guid id)
        {
            try
            {
                var auditLog = await _context.AuditLogs.FindAsync(id);
                if (auditLog == null)
                {
                    return NotFound(new { message = "Audit log not found" });
                }

                return Ok(auditLog);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting audit log {AuditLogId}", id);
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        // GET: api/audit/entity/{entityType}/{entityId}
        [HttpGet("entity/{entityType}/{entityId}")]
        [Authorize(Roles = "Administrator,Manager")]
        public async Task<ActionResult<IEnumerable<AuditLog>>> GetEntityAuditLogs(
            string entityType, 
            string entityId,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50)
        {
            try
            {
                var query = _context.AuditLogs
                    .Where(a => a.EntityType == entityType && a.EntityId == entityId);

                var totalCount = await query.CountAsync();
                var auditLogs = await query
                    .OrderByDescending(a => a.Timestamp)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                var result = new
                {
                    Data = auditLogs,
                    TotalCount = totalCount,
                    Page = page,
                    PageSize = pageSize,
                    TotalPages = (int)Math.Ceiling((double)totalCount / pageSize)
                };

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting entity audit logs for {EntityType} {EntityId}", entityType, entityId);
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        // GET: api/audit/user/{userId}
        [HttpGet("user/{userId}")]
        [Authorize(Roles = "Administrator,Manager")]
        public async Task<ActionResult<IEnumerable<AuditLog>>> GetUserAuditLogs(
            string userId,
            [FromQuery] DateTime? startDate,
            [FromQuery] DateTime? endDate,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50)
        {
            try
            {
                var query = _context.AuditLogs.Where(a => a.UserId == userId);

                if (startDate.HasValue)
                    query = query.Where(a => a.Timestamp >= startDate.Value);

                if (endDate.HasValue)
                    query = query.Where(a => a.Timestamp <= endDate.Value);

                var totalCount = await query.CountAsync();
                var auditLogs = await query
                    .OrderByDescending(a => a.Timestamp)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                var result = new
                {
                    Data = auditLogs,
                    TotalCount = totalCount,
                    Page = page,
                    PageSize = pageSize,
                    TotalPages = (int)Math.Ceiling((double)totalCount / pageSize)
                };

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user audit logs for {UserId}", userId);
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        // GET: api/audit/summary
        [HttpGet("summary")]
        [Authorize(Roles = "Administrator,Manager")]
        public async Task<ActionResult<AuditSummary>> GetAuditSummary([FromQuery] DateTime? startDate, [FromQuery] DateTime? endDate)
        {
            try
            {
                var start = startDate ?? DateTime.UtcNow.AddDays(-30);
                var end = endDate ?? DateTime.UtcNow;

                var auditLogs = await _context.AuditLogs
                    .Where(a => a.Timestamp >= start && a.Timestamp <= end)
                    .ToListAsync();

                var summary = new AuditSummary
                {
                    StartDate = start,
                    EndDate = end,
                    TotalLogs = auditLogs.Count,
                    ActionsByType = auditLogs.GroupBy(a => a.Action)
                        .Select(g => new ActionSummary
                        {
                            Action = g.Key,
                            Count = g.Count()
                        })
                        .OrderByDescending(a => a.Count)
                        .ToList(),
                    EntitiesByType = auditLogs.GroupBy(a => a.EntityType)
                        .Select(g => new EntityTypeSummary
                        {
                            EntityType = g.Key,
                            Count = g.Count()
                        })
                        .OrderByDescending(e => e.Count)
                        .ToList(),
                    UsersByActivity = auditLogs.GroupBy(a => a.UserId)
                        .Select(g => new UserActivitySummary
                        {
                            UserId = g.Key,
                            ActionCount = g.Count(),
                            LastActivity = g.Max(a => a.Timestamp)
                        })
                        .OrderByDescending(u => u.ActionCount)
                        .Take(10)
                        .ToList()
                };

                return Ok(summary);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting audit summary");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        // GET: api/audit/export
        [HttpGet("export")]
        [Authorize(Roles = "Administrator")]
        public async Task<IActionResult> ExportAuditLogs(
            [FromQuery] DateTime? startDate,
            [FromQuery] DateTime? endDate,
            [FromQuery] string? action,
            [FromQuery] string? entityType,
            [FromQuery] string? userId,
            [FromQuery] string format = "csv")
        {
            try
            {
                var query = _context.AuditLogs.AsQueryable();

                // Filtreleme
                if (startDate.HasValue)
                    query = query.Where(a => a.Timestamp >= startDate.Value);

                if (endDate.HasValue)
                    query = query.Where(a => a.Timestamp <= endDate.Value);

                if (!string.IsNullOrEmpty(action))
                    query = query.Where(a => a.Action.Contains(action));

                if (!string.IsNullOrEmpty(entityType))
                    query = query.Where(a => a.EntityType.Contains(entityType));

                if (!string.IsNullOrEmpty(userId))
                    query = query.Where(a => a.UserId == userId);

                var auditLogs = await query
                    .OrderByDescending(a => a.Timestamp)
                    .ToListAsync();

                if (format.ToLower() == "csv")
                {
                    var csv = GenerateAuditCsv(auditLogs);
                    var bytes = System.Text.Encoding.UTF8.GetBytes(csv);
                    return File(bytes, "text/csv", $"audit_logs_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv");
                }

                return Ok(auditLogs);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting audit logs");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        // POST: api/audit/log
        [HttpPost("log")]
        [AllowAnonymous] // Bu endpoint authentication gerektirmez
        public async Task<IActionResult> CreateAuditLog([FromBody] CreateAuditLogRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var auditLog = new AuditLog
                {
                    Action = request.Action,
                    EntityType = request.EntityType,
                    EntityId = request.EntityId,
                    UserId = request.UserId,
                    UserName = request.UserName,
                    OldValues = request.OldValues,
                    NewValues = request.NewValues,
                    IpAddress = request.IpAddress,
                    UserAgent = request.UserAgent,
                    Timestamp = DateTime.UtcNow,
                    IsActive = true
                };

                _context.AuditLogs.Add(auditLog);
                await _context.SaveChangesAsync();

                return Ok(new { message = "Audit log created successfully", id = auditLog.Id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating audit log");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        // DELETE: api/audit/cleanup
        [HttpDelete("cleanup")]
        [Authorize(Roles = "Administrator")]
        public async Task<IActionResult> CleanupOldAuditLogs([FromQuery] int daysToKeep = 365)
        {
            try
            {
                var cutoffDate = DateTime.UtcNow.AddDays(-daysToKeep);
                var oldLogs = await _context.AuditLogs
                    .Where(a => a.Timestamp < cutoffDate)
                    .ToListAsync();

                var count = oldLogs.Count;
                _context.AuditLogs.RemoveRange(oldLogs);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Cleaned up {Count} old audit logs older than {DaysToKeep} days", count, daysToKeep);

                return Ok(new { message = $"Cleaned up {count} old audit logs", count });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cleaning up old audit logs");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        private string GenerateAuditCsv(List<AuditLog> auditLogs)
        {
            var csv = new System.Text.StringBuilder();
            csv.AppendLine("Timestamp,Action,EntityType,EntityId,UserId,UserName,OldValues,NewValues,IPAddress,UserAgent");

            foreach (var log in auditLogs)
            {
                csv.AppendLine($"{log.Timestamp:yyyy-MM-dd HH:mm:ss}," +
                             $"{log.Action}," +
                             $"{log.EntityType}," +
                             $"{log.EntityId}," +
                             $"{log.UserId}," +
                             $"{log.UserName}," +
                             $"\"{log.OldValues}\"," +
                             $"\"{log.NewValues}\"," +
                             $"{log.IpAddress}," +
                             $"\"{log.UserAgent}\"");
            }

            return csv.ToString();
        }
    }

    // DTOs
    public class CreateAuditLogRequest
    {
        [Required]
        [MaxLength(50)]
        public string Action { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string EntityType { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string EntityId { get; set; } = string.Empty;

        [Required]
        [MaxLength(450)]
        public string UserId { get; set; } = string.Empty;

        [MaxLength(100)]
        public string? UserName { get; set; }

        [MaxLength(4000)]
        public string? OldValues { get; set; }

        [MaxLength(4000)]
        public string? NewValues { get; set; }

        [MaxLength(45)]
        public string? IpAddress { get; set; }

        [MaxLength(500)]
        public string? UserAgent { get; set; }
    }

    public class AuditSummary
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public int TotalLogs { get; set; }
        public List<ActionSummary> ActionsByType { get; set; } = new();
        public List<EntityTypeSummary> EntitiesByType { get; set; } = new();
        public List<UserActivitySummary> UsersByActivity { get; set; } = new();
    }

    public class ActionSummary
    {
        public string Action { get; set; } = string.Empty;
        public int Count { get; set; }
    }

    public class EntityTypeSummary
    {
        public string EntityType { get; set; } = string.Empty;
        public int Count { get; set; }
    }

    public class UserActivitySummary
    {
        public string UserId { get; set; } = string.Empty;
        public int ActionCount { get; set; }
        public DateTime LastActivity { get; set; }
    }
}

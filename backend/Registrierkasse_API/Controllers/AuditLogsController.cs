using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Registrierkasse.Data;
using Microsoft.EntityFrameworkCore;

namespace Registrierkasse.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class AuditLogsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public AuditLogsController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetAuditLogs()
        {
            try
            {
                var auditLogs = await _context.AuditLogs
                    .Select(al => new
                    {
                        id = al.Id,
                        userId = al.UserId,
                        userName = al.UserName,
                        action = al.Action,
                        entityType = al.EntityType,
                        entityId = al.EntityId,
                        oldValues = al.OldValues,
                        newValues = al.NewValues,
                        status = al.Status,
                        ipAddress = al.IpAddress,
                        userAgent = al.UserAgent,
                        createdAt = al.CreatedAt
                    })
                    .OrderByDescending(al => al.createdAt)
                    .ToListAsync();

                return Ok(auditLogs);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Failed to retrieve audit logs", details = ex.Message });
            }
        }
    }
} 
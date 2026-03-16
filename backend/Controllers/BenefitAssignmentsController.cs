using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;

namespace KasseAPI_Final.Controllers
{
    /// <summary>
    /// Admin CRUD for assigning benefit definitions to customers. PaymentService uses active assignments to resolve percentage discount.
    /// </summary>
    [ApiController]
    [Route("api/admin/benefit-assignments")]
    [HasPermission(AppPermissions.BenefitView)]
    public class BenefitAssignmentsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ILogger<BenefitAssignmentsController> _logger;

        public BenefitAssignmentsController(AppDbContext context, ILogger<BenefitAssignmentsController> logger)
        {
            _context = context;
            _logger = logger;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<BenefitAssignment>>> GetAll()
        {
            try
            {
                var list = await _context.BenefitAssignments
                    .Include(ba => ba.BenefitDefinition)
                    .Include(ba => ba.Customer)
                    .OrderBy(ba => ba.CustomerId).ThenBy(ba => ba.ValidFrom)
                    .ToListAsync();
                return Ok(list);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error listing benefit assignments");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<BenefitAssignment>> GetById(Guid id)
        {
            try
            {
                var item = await _context.BenefitAssignments
                    .Include(ba => ba.BenefitDefinition)
                    .Include(ba => ba.Customer)
                    .FirstOrDefaultAsync(ba => ba.Id == id);
                if (item == null)
                    return NotFound(new { message = "Benefit assignment not found" });
                return Ok(item);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting benefit assignment {Id}", id);
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        [HttpPost]
        [HasPermission(AppPermissions.BenefitManage)]
        public async Task<ActionResult<BenefitAssignment>> Create([FromBody] CreateBenefitAssignmentRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                    return BadRequest(ModelState);

                if (request.ValidTo.HasValue && request.ValidTo.Value < request.ValidFrom)
                    return BadRequest(new { message = "ValidTo must not be earlier than ValidFrom." });

                var definitionExists = await _context.BenefitDefinitions.AnyAsync(b => b.Id == request.BenefitDefinitionId && b.IsActive);
                if (!definitionExists)
                    return BadRequest(new { message = "Benefit definition not found or inactive" });
                var customerExists = await _context.Customers.AnyAsync(c => c.Id == request.CustomerId && c.IsActive);
                if (!customerExists)
                    return BadRequest(new { message = "Customer not found or inactive" });

                var entity = new BenefitAssignment
                {
                    BenefitDefinitionId = request.BenefitDefinitionId,
                    CustomerId = request.CustomerId,
                    ValidFrom = request.ValidFrom,
                    ValidTo = request.ValidTo,
                    Priority = request.Priority,
                    IsActive = request.IsActive,
                };
                _context.BenefitAssignments.Add(entity);
                await _context.SaveChangesAsync();
                var created = await _context.BenefitAssignments
                    .Include(ba => ba.BenefitDefinition)
                    .Include(ba => ba.Customer)
                    .FirstAsync(ba => ba.Id == entity.Id);
                return CreatedAtAction(nameof(GetById), new { id = entity.Id }, created);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating benefit assignment");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        [HttpPut("{id}")]
        [HasPermission(AppPermissions.BenefitManage)]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateBenefitAssignmentRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                    return BadRequest(ModelState);

                if (request.ValidTo.HasValue && request.ValidTo.Value < request.ValidFrom)
                    return BadRequest(new { message = "ValidTo must not be earlier than ValidFrom." });

                var entity = await _context.BenefitAssignments.FindAsync(id);
                if (entity == null)
                    return NotFound(new { message = "Benefit assignment not found" });

                var definitionExists = await _context.BenefitDefinitions.AnyAsync(b => b.Id == request.BenefitDefinitionId && b.IsActive);
                if (!definitionExists)
                    return BadRequest(new { message = "Benefit definition not found or inactive" });
                var customerExists = await _context.Customers.AnyAsync(c => c.Id == request.CustomerId && c.IsActive);
                if (!customerExists)
                    return BadRequest(new { message = "Customer not found or inactive" });

                entity.BenefitDefinitionId = request.BenefitDefinitionId;
                entity.CustomerId = request.CustomerId;
                entity.ValidFrom = request.ValidFrom;
                entity.ValidTo = request.ValidTo;
                entity.Priority = request.Priority;
                entity.IsActive = request.IsActive;
                entity.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                var updated = await _context.BenefitAssignments
                    .Include(ba => ba.BenefitDefinition)
                    .Include(ba => ba.Customer)
                    .FirstAsync(ba => ba.Id == id);
                return Ok(updated);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating benefit assignment {Id}", id);
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        [HttpDelete("{id}")]
        [HasPermission(AppPermissions.BenefitManage)]
        public async Task<IActionResult> Delete(Guid id)
        {
            try
            {
                var entity = await _context.BenefitAssignments.FindAsync(id);
                if (entity == null)
                    return NotFound(new { message = "Benefit assignment not found" });

                entity.IsActive = false;
                entity.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                return Ok(new { message = "Benefit assignment deactivated successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting benefit assignment {Id}", id);
                return StatusCode(500, new { message = "Internal server error" });
            }
        }
    }

    public class CreateBenefitAssignmentRequest
    {
        public Guid BenefitDefinitionId { get; set; }
        public Guid CustomerId { get; set; }
        public DateTime ValidFrom { get; set; }
        public DateTime? ValidTo { get; set; }
        public int Priority { get; set; }
        public bool IsActive { get; set; } = true;
    }

    public class UpdateBenefitAssignmentRequest
    {
        public Guid BenefitDefinitionId { get; set; }
        public Guid CustomerId { get; set; }
        public DateTime ValidFrom { get; set; }
        public DateTime? ValidTo { get; set; }
        public int Priority { get; set; }
        public bool IsActive { get; set; } = true;
    }
}

using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;

namespace KasseAPI_Final.Controllers
{
    /// <summary>
    /// Admin CRUD for benefit definitions (percentage discount, free allowance, buy X get Y). Used by PaymentService when resolving assigned benefits.
    /// </summary>
    [ApiController]
    [Route("api/admin/benefit-definitions")]
    [HasPermission(AppPermissions.BenefitView)]
    public class BenefitDefinitionsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ILogger<BenefitDefinitionsController> _logger;

        public BenefitDefinitionsController(AppDbContext context, ILogger<BenefitDefinitionsController> logger)
        {
            _context = context;
            _logger = logger;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<BenefitDefinition>>> GetAll()
        {
            try
            {
                var list = await _context.BenefitDefinitions
                    .OrderBy(b => b.Code)
                    .ToListAsync();
                return Ok(list);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error listing benefit definitions");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<BenefitDefinition>> GetById(Guid id)
        {
            try
            {
                var item = await _context.BenefitDefinitions.FindAsync(id);
                if (item == null)
                    return NotFound(new { message = "Benefit definition not found" });
                return Ok(item);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting benefit definition {Id}", id);
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        [HttpPost]
        [HasPermission(AppPermissions.BenefitManage)]
        public async Task<ActionResult<BenefitDefinition>> Create([FromBody] CreateBenefitDefinitionRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                    return BadRequest(ModelState);

                var existing = await _context.BenefitDefinitions.FirstOrDefaultAsync(b => b.Code == request.Code && b.IsActive);
                if (existing != null)
                    return BadRequest(new { message = "Benefit definition with this code already exists" });

                var entity = new BenefitDefinition
                {
                    Code = request.Code,
                    Name = request.Name,
                    BenefitKind = request.BenefitKind,
                    PercentageValue = request.PercentageValue,
                    AllowanceQuantity = request.AllowanceQuantity,
                    AllowanceScope = request.AllowanceScope,
                    AllowanceCategoryId = request.AllowanceCategoryId,
                    BuyXQuantity = request.BuyXQuantity,
                    GetYQuantity = request.GetYQuantity,
                    IsActive = request.IsActive,
                };
                _context.BenefitDefinitions.Add(entity);
                await _context.SaveChangesAsync();
                return CreatedAtAction(nameof(GetById), new { id = entity.Id }, entity);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating benefit definition");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        [HttpPut("{id}")]
        [HasPermission(AppPermissions.BenefitManage)]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateBenefitDefinitionRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                    return BadRequest(ModelState);

                var entity = await _context.BenefitDefinitions.FindAsync(id);
                if (entity == null)
                    return NotFound(new { message = "Benefit definition not found" });

                var existingCode = await _context.BenefitDefinitions.FirstOrDefaultAsync(b => b.Code == request.Code && b.Id != id && b.IsActive);
                if (existingCode != null)
                    return BadRequest(new { message = "Benefit definition with this code already exists" });

                entity.Code = request.Code;
                entity.Name = request.Name;
                entity.BenefitKind = request.BenefitKind;
                entity.PercentageValue = request.PercentageValue;
                entity.AllowanceQuantity = request.AllowanceQuantity;
                entity.AllowanceScope = request.AllowanceScope;
                entity.AllowanceCategoryId = request.AllowanceCategoryId;
                entity.BuyXQuantity = request.BuyXQuantity;
                entity.GetYQuantity = request.GetYQuantity;
                entity.IsActive = request.IsActive;
                entity.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                return Ok(entity);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating benefit definition {Id}", id);
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        [HttpDelete("{id}")]
        [HasPermission(AppPermissions.BenefitManage)]
        public async Task<IActionResult> Delete(Guid id)
        {
            try
            {
                var entity = await _context.BenefitDefinitions.FindAsync(id);
                if (entity == null)
                    return NotFound(new { message = "Benefit definition not found" });

                var hasAssignments = await _context.BenefitAssignments.AnyAsync(ba => ba.BenefitDefinitionId == id && ba.IsActive);
                if (hasAssignments)
                    return BadRequest(new { message = "Cannot delete benefit definition with active assignments" });

                entity.IsActive = false;
                entity.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                return Ok(new { message = "Benefit definition deactivated successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting benefit definition {Id}", id);
                return StatusCode(500, new { message = "Internal server error" });
            }
        }
    }

    public class CreateBenefitDefinitionRequest
    {
        [Required, MaxLength(50)]
        public string Code { get; set; } = string.Empty;
        [Required, MaxLength(100)]
        public string Name { get; set; } = string.Empty;
        public AppliedBenefitKind BenefitKind { get; set; }
        public decimal? PercentageValue { get; set; }
        public int? AllowanceQuantity { get; set; }
        [MaxLength(50)]
        public string? AllowanceScope { get; set; }
        public Guid? AllowanceCategoryId { get; set; }
        public int? BuyXQuantity { get; set; }
        public int? GetYQuantity { get; set; }
        public bool IsActive { get; set; } = true;
    }

    public class UpdateBenefitDefinitionRequest
    {
        [Required, MaxLength(50)]
        public string Code { get; set; } = string.Empty;
        [Required, MaxLength(100)]
        public string Name { get; set; } = string.Empty;
        public AppliedBenefitKind BenefitKind { get; set; }
        public decimal? PercentageValue { get; set; }
        public int? AllowanceQuantity { get; set; }
        [MaxLength(50)]
        public string? AllowanceScope { get; set; }
        public Guid? AllowanceCategoryId { get; set; }
        public int? BuyXQuantity { get; set; }
        public int? GetYQuantity { get; set; }
        public bool IsActive { get; set; } = true;
    }
}

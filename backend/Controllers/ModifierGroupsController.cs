using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using KasseAPI_Final.Controllers.Base;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;

namespace KasseAPI_Final.Controllers
{
    /// <summary>
    /// Extra Zutaten: Modifier Groups ve Modifiers CRUD. Admin panel için.
    /// </summary>
    [Route("api/modifier-groups")]
    [ApiController]
    [Authorize]
    public class ModifierGroupsController : BaseController
    {
        private readonly AppDbContext _context;

        public ModifierGroupsController(AppDbContext context, ILogger<ModifierGroupsController> logger)
            : base(logger)
        {
            _context = context;
        }

        /// <summary>
        /// Tüm modifier gruplarını listele (modifier'lar ile birlikte).
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            try
            {
                var groups = await _context.ProductModifierGroups
                    .Where(g => g.IsActive)
                    .OrderBy(g => g.SortOrder)
                    .ThenBy(g => g.Name)
                    .Include(g => g.Modifiers.Where(m => m.IsActive))
                    .ToListAsync();

                var dtos = groups.Select(g => new ModifierGroupDto
                {
                    Id = g.Id,
                    Name = g.Name,
                    MinSelections = g.MinSelections,
                    MaxSelections = g.MaxSelections,
                    IsRequired = g.IsRequired,
                    SortOrder = g.SortOrder,
                    IsActive = g.IsActive,
                    Modifiers = g.Modifiers
                        .OrderBy(m => m.SortOrder)
                        .ThenBy(m => m.Name)
                        .Select(m => new ModifierDto
                        {
                            Id = m.Id,
                            Name = m.Name,
                            Price = m.Price,
                            TaxType = m.TaxType,
                            SortOrder = m.SortOrder
                        }).ToList()
                }).ToList();

                return SuccessResponse(dtos, "Modifier groups retrieved.");
            }
            catch (Exception ex)
            {
                return HandleException(ex, "ModifierGroups.GetAll");
            }
        }

        /// <summary>
        /// Tekil modifier group getir.
        /// </summary>
        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            try
            {
                var group = await _context.ProductModifierGroups
                    .Include(g => g.Modifiers.Where(m => m.IsActive))
                    .FirstOrDefaultAsync(g => g.Id == id);

                if (group == null)
                    return ErrorResponse("Modifier group not found.", 404);

                var dto = new ModifierGroupDto
                {
                    Id = group.Id,
                    Name = group.Name,
                    MinSelections = group.MinSelections,
                    MaxSelections = group.MaxSelections,
                    IsRequired = group.IsRequired,
                    SortOrder = group.SortOrder,
                    IsActive = group.IsActive,
                    Modifiers = group.Modifiers
                        .OrderBy(m => m.SortOrder)
                        .Select(m => new ModifierDto
                        {
                            Id = m.Id,
                            Name = m.Name,
                            Price = m.Price,
                            TaxType = m.TaxType,
                            SortOrder = m.SortOrder
                        }).ToList()
                };

                return SuccessResponse(dto, "Modifier group retrieved.");
            }
            catch (Exception ex)
            {
                return HandleException(ex, "ModifierGroups.GetById");
            }
        }

        /// <summary>
        /// Yeni modifier group oluştur.
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateModifierGroupRequest request)
        {
            try
            {
                var entity = new ProductModifierGroup
                {
                    Id = Guid.NewGuid(),
                    Name = request.Name,
                    MinSelections = request.MinSelections,
                    MaxSelections = request.MaxSelections,
                    IsRequired = request.IsRequired,
                    SortOrder = request.SortOrder,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                _context.ProductModifierGroups.Add(entity);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Modifier group created: {Id} {Name}", entity.Id, entity.Name);
                return SuccessResponse(new { id = entity.Id, name = entity.Name }, "Modifier group created.");
            }
            catch (Exception ex)
            {
                return HandleException(ex, "ModifierGroups.Create");
            }
        }

        /// <summary>
        /// Modifier group güncelle.
        /// </summary>
        [HttpPut("{id:guid}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] CreateModifierGroupRequest request)
        {
            try
            {
                var entity = await _context.ProductModifierGroups.FindAsync(id);
                if (entity == null)
                    return ErrorResponse("Modifier group not found.", 404);

                entity.Name = request.Name;
                entity.MinSelections = request.MinSelections;
                entity.MaxSelections = request.MaxSelections;
                entity.IsRequired = request.IsRequired;
                entity.SortOrder = request.SortOrder;
                entity.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                _logger.LogInformation("Modifier group updated: {Id}", id);
                return SuccessResponse(new { id = entity.Id }, "Modifier group updated.");
            }
            catch (Exception ex)
            {
                return HandleException(ex, "ModifierGroups.Update");
            }
        }

        /// <summary>
        /// Modifier group sil (soft: IsActive = false).
        /// </summary>
        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            try
            {
                var entity = await _context.ProductModifierGroups.FindAsync(id);
                if (entity == null)
                    return ErrorResponse("Modifier group not found.", 404);

                entity.IsActive = false;
                entity.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                _logger.LogInformation("Modifier group deactivated: {Id}", id);
                return SuccessResponse(new { id }, "Modifier group deleted.");
            }
            catch (Exception ex)
            {
                return HandleException(ex, "ModifierGroups.Delete");
            }
        }

        /// <summary>
        /// Gruba yeni modifier ekle.
        /// </summary>
        [HttpPost("{groupId:guid}/modifiers")]
        public async Task<IActionResult> AddModifier(Guid groupId, [FromBody] CreateModifierRequest request)
        {
            try
            {
                var group = await _context.ProductModifierGroups.FindAsync(groupId);
                if (group == null)
                    return ErrorResponse("Modifier group not found.", 404);

                var modifier = new ProductModifier
                {
                    Id = Guid.NewGuid(),
                    ModifierGroupId = groupId,
                    Name = request.Name,
                    Price = request.Price,
                    TaxType = request.TaxType,
                    SortOrder = request.SortOrder,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                _context.ProductModifiers.Add(modifier);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Modifier added to group {GroupId}: {Name}", groupId, modifier.Name);
                return SuccessResponse(new ModifierDto
                {
                    Id = modifier.Id,
                    Name = modifier.Name,
                    Price = modifier.Price,
                    TaxType = modifier.TaxType,
                    SortOrder = modifier.SortOrder
                }, "Modifier added.");
            }
            catch (Exception ex)
            {
                return HandleException(ex, "ModifierGroups.AddModifier");
            }
        }
    }
}

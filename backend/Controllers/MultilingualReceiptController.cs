using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KasseAPI_Final.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class MultilingualReceiptController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ILogger<MultilingualReceiptController> _logger;

        public MultilingualReceiptController(AppDbContext context, ILogger<MultilingualReceiptController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // GET: api/multilingualreceipt
        [HttpGet]
        public async Task<ActionResult<IEnumerable<ReceiptTemplate>>> GetReceiptTemplates()
        {
            try
            {
                var templates = await _context.ReceiptTemplates
                    .Where(t => t.IsActive)
                    .OrderBy(t => t.Language)
                    .ThenBy(t => t.TemplateName)
                    .ToListAsync();

                return Ok(templates);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting receipt templates");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        // GET: api/multilingualreceipt/{id}
        [HttpGet("{id}")]
        public async Task<ActionResult<ReceiptTemplate>> GetReceiptTemplate(Guid id)
        {
            try
            {
                var template = await _context.ReceiptTemplates.FindAsync(id);
                if (template == null || !template.IsActive)
                {
                    return NotFound(new { message = "Receipt template not found" });
                }

                return Ok(template);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting receipt template {TemplateId}", id);
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        // GET: api/multilingualreceipt/language/{language}
        [HttpGet("language/{language}")]
        public async Task<ActionResult<IEnumerable<ReceiptTemplate>>> GetReceiptTemplatesByLanguage(string language)
        {
            try
            {
                var templates = await _context.ReceiptTemplates
                    .Where(t => t.Language == language && t.IsActive)
                    .OrderBy(t => t.TemplateName)
                    .ToListAsync();

                return Ok(templates);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting receipt templates for language {Language}", language);
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        // GET: api/multilingualreceipt/type/{type}
        [HttpGet("type/{type}")]
        public async Task<ActionResult<IEnumerable<ReceiptTemplate>>> GetReceiptTemplatesByType(string type)
        {
            try
            {
                var templates = await _context.ReceiptTemplates
                    .Where(t => t.TemplateType == type && t.IsActive)
                    .OrderBy(t => t.Language)
                    .ThenBy(t => t.TemplateName)
                    .ToListAsync();

                return Ok(templates);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting receipt templates for type {Type}", type);
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        // POST: api/multilingualreceipt
        [HttpPost]
        [Authorize(Roles = "Administrator")]
        public async Task<ActionResult<ReceiptTemplate>> CreateReceiptTemplate([FromBody] CreateReceiptTemplateRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                // Aynı dil ve türde template var mı kontrol et
                var existingTemplate = await _context.ReceiptTemplates
                    .FirstOrDefaultAsync(t => t.Language == request.Language && 
                                           t.TemplateType == request.TemplateType && 
                                           t.TemplateName == request.TemplateName);

                if (existingTemplate != null)
                {
                    return BadRequest(new { message = "Template already exists for this language and type" });
                }

                var template = new ReceiptTemplate
                {
                    TemplateName = request.TemplateName,
                    TemplateType = request.TemplateType,
                    Language = request.Language,
                    HeaderTemplate = request.HeaderTemplate,
                    FooterTemplate = request.FooterTemplate,
                    ItemTemplate = request.ItemTemplate,
                    TaxTemplate = request.TaxTemplate,
                    TotalTemplate = request.TotalTemplate,
                    PaymentTemplate = request.PaymentTemplate,
                    CustomerTemplate = request.CustomerTemplate,
                    CompanyTemplate = request.CompanyTemplate,
                    CustomFields = request.CustomFields,
                    IsDefault = request.IsDefault,
                    IsActive = true
                };

                // Eğer bu template varsayılan olarak işaretlendiyse, diğerlerini varsayılan olmaktan çıkar
                if (request.IsDefault)
                {
                    var defaultTemplates = await _context.ReceiptTemplates
                        .Where(t => t.Language == request.Language && 
                                  t.TemplateType == request.TemplateType && 
                                  t.IsDefault)
                        .ToListAsync();

                    foreach (var defaultTemplate in defaultTemplates)
                    {
                        defaultTemplate.IsDefault = false;
                    }
                }

                _context.ReceiptTemplates.Add(template);
                await _context.SaveChangesAsync();

                return CreatedAtAction(nameof(GetReceiptTemplate), new { id = template.Id }, template);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating receipt template");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        // PUT: api/multilingualreceipt/{id}
        [HttpPut("{id}")]
        [Authorize(Roles = "Administrator")]
        public async Task<IActionResult> UpdateReceiptTemplate(Guid id, [FromBody] UpdateReceiptTemplateRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var template = await _context.ReceiptTemplates.FindAsync(id);
                if (template == null || !template.IsActive)
                {
                    return NotFound(new { message = "Receipt template not found" });
                }

                // Template bilgilerini güncelle
                template.TemplateName = request.TemplateName;
                template.HeaderTemplate = request.HeaderTemplate;
                template.FooterTemplate = request.FooterTemplate;
                template.ItemTemplate = request.ItemTemplate;
                template.TaxTemplate = request.TaxTemplate;
                template.TotalTemplate = request.TotalTemplate;
                template.PaymentTemplate = request.PaymentTemplate;
                template.CustomerTemplate = request.CustomerTemplate;
                template.CompanyTemplate = request.CompanyTemplate;
                template.CustomFields = request.CustomFields;
                template.IsDefault = request.IsDefault;
                template.UpdatedAt = DateTime.UtcNow;

                // Eğer bu template varsayılan olarak işaretlendiyse, diğerlerini varsayılan olmaktan çıkar
                if (request.IsDefault)
                {
                    var defaultTemplates = await _context.ReceiptTemplates
                        .Where(t => t.Language == template.Language && 
                                  t.TemplateType == template.TemplateType && 
                                  t.IsDefault && 
                                  t.Id != id)
                        .ToListAsync();

                    foreach (var defaultTemplate in defaultTemplates)
                    {
                        defaultTemplate.IsDefault = false;
                    }
                }

                await _context.SaveChangesAsync();

                return Ok(new { message = "Receipt template updated successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating receipt template {TemplateId}", id);
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        // DELETE: api/multilingualreceipt/{id}
        [HttpDelete("{id}")]
        [Authorize(Roles = "Administrator")]
        public async Task<IActionResult> DeleteReceiptTemplate(Guid id)
        {
            try
            {
                var template = await _context.ReceiptTemplates.FindAsync(id);
                if (template == null)
                {
                    return NotFound(new { message = "Receipt template not found" });
                }

                // Varsayılan template silinemez
                if (template.IsDefault)
                {
                    return BadRequest(new { message = "Cannot delete default template" });
                }

                // Soft delete
                template.IsActive = false;
                template.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                return Ok(new { message = "Receipt template deleted successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting receipt template {TemplateId}", id);
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        // POST: api/multilingualreceipt/generate
        [HttpPost("generate")]
        public async Task<ActionResult<GeneratedReceipt>> GenerateReceipt([FromBody] GenerateReceiptRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                // Template'i bul
                var template = await _context.ReceiptTemplates
                    .FirstOrDefaultAsync(t => t.Language == request.Language && 
                                           t.TemplateType == request.TemplateType && 
                                           t.IsActive);

                if (template == null)
                {
                    // Varsayılan template'i bul
                    template = await _context.ReceiptTemplates
                        .FirstOrDefaultAsync(t => t.Language == request.Language && 
                                               t.TemplateType == request.TemplateType && 
                                               t.IsDefault && 
                                               t.IsActive);
                }

                if (template == null)
                {
                    return NotFound(new { message = "Receipt template not found for specified language and type" });
                }

                // Receipt'i oluştur
                var generatedReceipt = new GeneratedReceipt
                {
                    TemplateId = template.Id,
                    Language = template.Language,
                    TemplateType = template.TemplateType,
                    GeneratedContent = GenerateReceiptContent(template, request),
                    GeneratedAt = DateTime.UtcNow,
                    IsActive = true
                };

                _context.GeneratedReceipts.Add(generatedReceipt);
                await _context.SaveChangesAsync();

                return Ok(generatedReceipt);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating receipt");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        // GET: api/multilingualreceipt/preview/{id}
        [HttpGet("preview/{id}")]
        public async Task<ActionResult<ReceiptPreview>> PreviewReceiptTemplate(Guid id)
        {
            try
            {
                var template = await _context.ReceiptTemplates.FindAsync(id);
                if (template == null || !template.IsActive)
                {
                    return NotFound(new { message = "Receipt template not found" });
                }

                // Örnek veri ile preview oluştur
                var sampleData = new GenerateReceiptRequest
                {
                    Language = template.Language,
                    TemplateType = template.TemplateType,
                    CompanyInfo = new CompanyInfo
                    {
                        Name = "Sample Company",
                        Address = "Sample Address",
                        Phone = "Sample Phone",
                        Email = "sample@company.com",
                        TaxNumber = "ATU00000000"
                    },
                    CustomerInfo = new CustomerInfo
                    {
                        Name = "Sample Customer",
                        Address = "Sample Customer Address",
                        Phone = "Sample Customer Phone"
                    },
                    Items = new List<ReceiptItem>
                    {
                        new ReceiptItem
                        {
                            Name = "Sample Product",
                            Quantity = 2,
                            UnitPrice = 10.00m,
                            TotalPrice = 20.00m
                        }
                    },
                    TaxAmount = 4.00m,
                    TotalAmount = 24.00m,
                    PaymentMethod = "Cash"
                };

                var preview = new ReceiptPreview
                {
                    TemplateId = template.Id,
                    TemplateName = template.TemplateName,
                    Language = template.Language,
                    TemplateType = template.TemplateType,
                    PreviewContent = GenerateReceiptContent(template, sampleData),
                    GeneratedAt = DateTime.UtcNow
                };

                return Ok(preview);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error previewing receipt template {TemplateId}", id);
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        // GET: api/multilingualreceipt/export
        [HttpGet("export")]
        [Authorize(Roles = "Administrator")]
        public async Task<IActionResult> ExportReceiptTemplates()
        {
            try
            {
                var templates = await _context.ReceiptTemplates
                    .Where(t => t.IsActive)
                    .OrderBy(t => t.Language)
                    .ThenBy(t => t.TemplateType)
                    .ThenBy(t => t.TemplateName)
                    .ToListAsync();

                var json = System.Text.Json.JsonSerializer.Serialize(templates, new System.Text.Json.JsonSerializerOptions
                {
                    WriteIndented = true
                });

                var bytes = System.Text.Encoding.UTF8.GetBytes(json);
                return File(bytes, "application/json", "receipt_templates.json");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting receipt templates");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        private string GenerateReceiptContent(ReceiptTemplate template, GenerateReceiptRequest request)
        {
            var content = template.HeaderTemplate ?? "";

            // Company bilgilerini ekle
            if (request.CompanyInfo != null)
            {
                content = content.Replace("{{CompanyName}}", request.CompanyInfo.Name ?? "")
                               .Replace("{{CompanyAddress}}", request.CompanyInfo.Address ?? "")
                               .Replace("{{CompanyPhone}}", request.CompanyInfo.Phone ?? "")
                               .Replace("{{CompanyEmail}}", request.CompanyInfo.Email ?? "")
                               .Replace("{{CompanyTaxNumber}}", request.CompanyInfo.TaxNumber ?? "");
            }

            // Customer bilgilerini ekle
            if (request.CustomerInfo != null)
            {
                content = content.Replace("{{CustomerName}}", request.CustomerInfo.Name ?? "")
                               .Replace("{{CustomerAddress}}", request.CustomerInfo.Address ?? "")
                               .Replace("{{CustomerPhone}}", request.CustomerInfo.Phone ?? "");
            }

            // Tarih ve saat bilgilerini ekle
            content = content.Replace("{{CurrentDate}}", DateTime.Now.ToString("dd.MM.yyyy"))
                           .Replace("{{CurrentTime}}", DateTime.Now.ToString("HH:mm:ss"));

            // Items ekle
            if (request.Items != null && request.Items.Any())
            {
                var itemsContent = "";
                foreach (var item in request.Items)
                {
                    var itemContent = template.ItemTemplate ?? "";
                    itemContent = itemContent.Replace("{{ItemName}}", item.Name ?? "")
                                          .Replace("{{ItemQuantity}}", item.Quantity.ToString())
                                          .Replace("{{ItemUnitPrice}}", item.UnitPrice.ToString("F2"))
                                          .Replace("{{ItemTotalPrice}}", item.TotalPrice.ToString("F2"));
                    itemsContent += itemContent + "\n";
                }
                content = content.Replace("{{Items}}", itemsContent);
            }

            // Vergi ve toplam bilgilerini ekle
            content = content.Replace("{{TaxAmount}}", request.TaxAmount.ToString("F2"))
                           .Replace("{{TotalAmount}}", request.TotalAmount.ToString("F2"))
                           .Replace("{{PaymentMethod}}", request.PaymentMethod ?? "");

            // Footer ekle
            if (!string.IsNullOrEmpty(template.FooterTemplate))
            {
                content += "\n" + template.FooterTemplate;
            }

            return content;
        }
    }

    // DTOs
    public class CreateReceiptTemplateRequest
    {
        [Required]
        [MaxLength(100)]
        public string TemplateName { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string TemplateType { get; set; } = string.Empty;

        [Required]
        [MaxLength(10)]
        public string Language { get; set; } = string.Empty;

        [MaxLength(2000)]
        public string? HeaderTemplate { get; set; }

        [MaxLength(2000)]
        public string? FooterTemplate { get; set; }

        [MaxLength(1000)]
        public string? ItemTemplate { get; set; }

        [MaxLength(500)]
        public string? TaxTemplate { get; set; }

        [MaxLength(500)]
        public string? TotalTemplate { get; set; }

        [MaxLength(500)]
        public string? PaymentTemplate { get; set; }

        [MaxLength(1000)]
        public string? CustomerTemplate { get; set; }

        [MaxLength(1000)]
        public string? CompanyTemplate { get; set; }

        [Column(TypeName = "jsonb")]
        public Dictionary<string, string>? CustomFields { get; set; }

        public bool IsDefault { get; set; }
    }

    public class UpdateReceiptTemplateRequest
    {
        [Required]
        [MaxLength(100)]
        public string TemplateName { get; set; } = string.Empty;

        [MaxLength(2000)]
        public string? HeaderTemplate { get; set; }

        [MaxLength(2000)]
        public string? FooterTemplate { get; set; }

        [MaxLength(1000)]
        public string? ItemTemplate { get; set; }

        [MaxLength(500)]
        public string? TaxTemplate { get; set; }

        [MaxLength(500)]
        public string? TotalTemplate { get; set; }

        [MaxLength(500)]
        public string? PaymentTemplate { get; set; }

        [MaxLength(1000)]
        public string? CustomerTemplate { get; set; }

        [MaxLength(1000)]
        public string? CompanyTemplate { get; set; }

        [Column(TypeName = "jsonb")]
        public Dictionary<string, string>? CustomFields { get; set; }

        public bool IsDefault { get; set; }
    }

    public class GenerateReceiptRequest
    {
        [Required]
        [MaxLength(10)]
        public string Language { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string TemplateType { get; set; } = string.Empty;

        public CompanyInfo? CompanyInfo { get; set; }
        public CustomerInfo? CustomerInfo { get; set; }
        public List<ReceiptItem>? Items { get; set; }
        public decimal TaxAmount { get; set; }
        public decimal TotalAmount { get; set; }
        public string? PaymentMethod { get; set; }
    }

    public class CompanyInfo
    {
        public string? Name { get; set; }
        public string? Address { get; set; }
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public string? TaxNumber { get; set; }
    }

    public class CustomerInfo
    {
        public string? Name { get; set; }
        public string? Address { get; set; }
        public string? Phone { get; set; }
    }

    public class ReceiptItem
    {
        public string? Name { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal TotalPrice { get; set; }
    }

    public class ReceiptPreview
    {
        public Guid TemplateId { get; set; }
        public string TemplateName { get; set; } = string.Empty;
        public string Language { get; set; } = string.Empty;
        public string TemplateType { get; set; } = string.Empty;
        public string PreviewContent { get; set; } = string.Empty;
        public DateTime GeneratedAt { get; set; }
    }
}

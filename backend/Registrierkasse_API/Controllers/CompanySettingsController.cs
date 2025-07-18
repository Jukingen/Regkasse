using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Registrierkasse_API.Data;
using Microsoft.EntityFrameworkCore;

namespace Registrierkasse_API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class CompanySettingsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public CompanySettingsController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetCompanySettings()
        {
            try
            {
                var settings = await _context.CompanySettings
                    .Select(cs => new
                    {
                        id = cs.Id,
                        companyName = cs.CompanyName,
                        taxNumber = cs.TaxNumber,
                        vatNumber = cs.VATNumber,
                        address = cs.Address,
                        city = cs.City,
                        postalCode = cs.PostalCode,
                        country = cs.Country,
                        phone = cs.Phone,
                        email = cs.Email,
                        website = cs.Website,
                        bankName = cs.BankName,
                        bankAccount = cs.BankAccount,
                        iban = cs.IBAN,
                        bic = cs.BIC,
                        logo = cs.Logo,
                        invoiceFooter = cs.InvoiceFooter,
                        receiptFooter = cs.ReceiptFooter,
                        defaultCurrency = cs.DefaultCurrency,
                        defaultTaxRate = cs.DefaultTaxRate,
                        industry = cs.Industry,
                        isFinanceOnlineEnabled = cs.IsFinanceOnlineEnabled,
                        financeOnlineUsername = cs.FinanceOnlineUsername,
                        financeOnlinePassword = cs.FinanceOnlinePassword,
                        signatureCertificate = cs.SignatureCertificate
                    })
                    .FirstOrDefaultAsync();

                return Ok(settings);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Failed to retrieve company settings", details = ex.Message });
            }
        }
    }
} 

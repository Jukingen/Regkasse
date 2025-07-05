using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Registrierkasse.Data;
using Microsoft.EntityFrameworkCore;

namespace Registrierkasse.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class CustomersController : ControllerBase
    {
        private readonly AppDbContext _context;

        public CustomersController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetCustomers()
        {
            try
            {
                var customers = await _context.Customers
                    .Select(c => new
                    {
                        id = c.Id,
                        customerNumber = c.CustomerNumber,
                        firstName = c.FirstName,
                        lastName = c.LastName,
                        email = c.Email,
                        phone = c.Phone,
                        address = c.Address,
                        city = c.City,
                        postalCode = c.PostalCode,
                        country = c.Country,
                        taxNumber = c.TaxNumber,
                        companyName = c.CompanyName,
                        isActive = true // Demo için
                    })
                    .ToListAsync();

                return Ok(customers);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Failed to retrieve customers", details = ex.Message });
            }
        }
    }
} 
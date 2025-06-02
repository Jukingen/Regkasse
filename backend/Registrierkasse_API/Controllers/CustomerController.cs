using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Registrierkasse.Data;
using Registrierkasse.Models;
using System;
using System.Threading.Tasks;

namespace Registrierkasse.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class CustomerController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ILogger<CustomerController> _logger;

        public CustomerController(AppDbContext context, ILogger<CustomerController> logger)
        {
            _context = context;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> GetCustomers()
        {
            try
            {
                var customers = await _context.Customers
                    .AsNoTracking()
                    .ToListAsync();

                return Ok(new { message = "Müşteriler başarıyla getirildi", customers });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Müşteriler getirilirken bir hata oluştu");
                return StatusCode(500, new { message = "Müşteriler getirilirken bir hata oluştu", error = ex.Message });
            }
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetCustomer(Guid id)
        {
            try
            {
                var customer = await _context.Customers
                    .Include(c => c.Orders)
                    .Include(c => c.Invoices)
                    .FirstOrDefaultAsync(c => c.Id == id);

                if (customer == null)
                {
                    return NotFound(new { message = "Müşteri bulunamadı" });
                }

                return Ok(new { message = "Müşteri başarıyla getirildi", customer });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"ID: {id} olan müşteri getirilirken bir hata oluştu");
                return StatusCode(500, new { message = "Müşteri getirilirken bir hata oluştu", error = ex.Message });
            }
        }

        [HttpPost]
        [Authorize(Roles = "Administrator,Manager")]
        public async Task<IActionResult> CreateCustomer([FromBody] Customer customer)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                customer.CustomerNumber = await GenerateCustomerNumber();
                _context.Customers.Add(customer);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Yeni müşteri oluşturuldu: {customer.FirstName} {customer.LastName}");

                return CreatedAtAction(nameof(GetCustomer), new { id = customer.Id },
                    new { message = "Müşteri başarıyla oluşturuldu", customer });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Müşteri oluşturulurken bir hata oluştu");
                return StatusCode(500, new { message = "Müşteri oluşturulurken bir hata oluştu", error = ex.Message });
            }
        }

        [HttpPut("{id}")]
        [Authorize(Roles = "Administrator,Manager")]
        public async Task<IActionResult> UpdateCustomer(Guid id, [FromBody] UpdateCustomerModel model)
        {
            try
            {
                var customer = await _context.Customers.FindAsync(id);
                if (customer == null)
                {
                    return NotFound(new { message = "Müşteri bulunamadı" });
                }

                customer.FirstName = model.FirstName;
                customer.LastName = model.LastName;
                customer.Email = model.Email;
                customer.Phone = model.Phone;
                customer.Address = model.Address;
                customer.City = model.City;
                customer.PostalCode = model.PostalCode;
                customer.Country = model.Country;
                customer.TaxNumber = model.TaxNumber;

                await _context.SaveChangesAsync();

                return Ok(new { message = "Müşteri başarıyla güncellendi", customer });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"ID: {id} olan müşteri güncellenirken bir hata oluştu");
                return StatusCode(500, new { message = "Müşteri güncellenirken bir hata oluştu", error = ex.Message });
            }
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Administrator")]
        public async Task<IActionResult> DeleteCustomer(Guid id)
        {
            try
            {
                var customer = await _context.Customers.FindAsync(id);
                if (customer == null)
                {
                    return NotFound(new { message = "Müşteri bulunamadı" });
                }

                _context.Customers.Remove(customer);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Müşteri silindi: {customer.FirstName} {customer.LastName}");

                return Ok(new { message = "Müşteri başarıyla silindi" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"ID: {id} olan müşteri silinirken bir hata oluştu");
                return StatusCode(500, new { message = "Müşteri silinirken bir hata oluştu", error = ex.Message });
            }
        }

        [HttpGet("search")]
        public async Task<IActionResult> SearchCustomers([FromQuery] string query)
        {
            try
            {
                var customers = await _context.Customers
                    .Where(c =>
                        c.FirstName.Contains(query) ||
                        c.LastName.Contains(query) ||
                        c.CompanyName.Contains(query) ||
                        c.CustomerNumber.Contains(query) ||
                        c.Email.Contains(query) ||
                        c.Phone.Contains(query))
                    .AsNoTracking()
                    .ToListAsync();

                return Ok(new { message = "Müşteriler başarıyla arandı", customers });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Müşteriler aranırken bir hata oluştu");
                return StatusCode(500, new { message = "Müşteriler aranırken bir hata oluştu", error = ex.Message });
            }
        }

        [HttpGet("{id}/orders")]
        public async Task<IActionResult> GetCustomerOrders(Guid id)
        {
            try
            {
                var orders = await _context.Orders
                    .Include(o => o.Customer)
                    .Include(o => o.OrderItems)
                    .Where(o => o.CustomerId == id && o.IsActive)
                    .ToListAsync();

                return Ok(new { message = "Müşteri siparişleri başarıyla getirildi", orders });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"ID: {id} olan müşterinin siparişleri getirilirken bir hata oluştu");
                return StatusCode(500, new { message = "Müşteri siparişleri getirilirken bir hata oluştu", error = ex.Message });
            }
        }

        private async Task<string> GenerateCustomerNumber()
        {
            var lastCustomer = await _context.Customers
                .OrderByDescending(c => c.Id)
                .FirstOrDefaultAsync();

            int nextNumber = 1;
            if (lastCustomer != null && lastCustomer.CustomerNumber != null)
            {
                if (int.TryParse(lastCustomer.CustomerNumber.Replace("CUST", ""), out int lastNumber))
                {
                    nextNumber = lastNumber + 1;
                }
            }

            return $"CUST{nextNumber:D6}";
        }

        private bool CustomerExists(Guid id)
        {
            return _context.Customers.Any(e => e.Id == id);
        }
    }

    public class UpdateCustomerModel
    {
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Email { get; set; }
        public string Phone { get; set; }
        public string Address { get; set; }
        public string City { get; set; }
        public string PostalCode { get; set; }
        public string Country { get; set; }
        public string TaxNumber { get; set; }
    }
} 
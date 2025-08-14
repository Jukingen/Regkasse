using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using Microsoft.AspNetCore.Authorization;

namespace KasseAPI_Final.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class CustomerController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ILogger<CustomerController> _logger;

        public CustomerController(AppDbContext context, ILogger<CustomerController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // GET: api/Customer
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Customer>>> GetCustomers()
        {
            try
            {
                var customers = await _context.Customers
                    .Where(c => c.IsActive)
                    .OrderBy(c => c.Name)
                    .ToListAsync();

                _logger.LogInformation("Retrieved {Count} customers", customers.Count);
                return Ok(customers);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving customers");
                return StatusCode(500, "Internal server error");
            }
        }

        // GET: api/Customer/5
        [HttpGet("{id}")]
        public async Task<ActionResult<Customer>> GetCustomer(Guid id)
        {
            try
            {
                var customer = await _context.Customers.FindAsync(id);

                if (customer == null || !customer.IsActive)
                {
                    return NotFound("Müşteri bulunamadı");
                }

                return Ok(customer);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving customer with ID: {Id}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        // POST: api/Customer
        [HttpPost]
        public async Task<ActionResult<Customer>> CreateCustomer(Customer customer)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                // Validation
                if (string.IsNullOrWhiteSpace(customer.Name))
                {
                    return BadRequest("Müşteri adı gerekli");
                }

                // Customer number uniqueness check
                if (!string.IsNullOrEmpty(customer.CustomerNumber))
                {
                    var existingCustomer = await _context.Customers
                        .FirstOrDefaultAsync(c => c.CustomerNumber == customer.CustomerNumber && c.IsActive);
                    
                    if (existingCustomer != null)
                    {
                        return BadRequest("Bu müşteri numarası zaten kullanılıyor");
                    }
                }

                // Email uniqueness check
                if (!string.IsNullOrEmpty(customer.Email))
                {
                    var existingCustomer = await _context.Customers
                        .FirstOrDefaultAsync(c => c.Email == customer.Email && c.IsActive);
                    
                    if (existingCustomer != null)
                    {
                        return BadRequest("Bu email adresi zaten kullanılıyor");
                    }
                }

                // Tax number uniqueness check
                if (!string.IsNullOrEmpty(customer.TaxNumber))
                {
                    var existingCustomer = await _context.Customers
                        .FirstOrDefaultAsync(c => c.TaxNumber == customer.TaxNumber && c.IsActive);
                    
                    if (existingCustomer != null)
                    {
                        return BadRequest("Bu vergi numarası zaten kullanılıyor");
                    }
                }

                customer.Id = Guid.NewGuid();
                customer.CreatedAt = DateTime.UtcNow;
                customer.IsActive = true;

                _context.Customers.Add(customer);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Customer created with ID: {Id}", customer.Id);
                return CreatedAtAction(nameof(GetCustomer), new { id = customer.Id }, customer);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating customer");
                return StatusCode(500, "Internal server error");
            }
        }

        // PUT: api/Customer/5
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateCustomer(Guid id, Customer customer)
        {
            try
            {
                if (id != customer.Id)
                {
                    return BadRequest("ID uyuşmazlığı");
                }

                var existingCustomer = await _context.Customers.FindAsync(id);
                if (existingCustomer == null || !existingCustomer.IsActive)
                {
                    return NotFound("Müşteri bulunamadı");
                }

                // Validation
                if (string.IsNullOrWhiteSpace(customer.Name))
                {
                    return BadRequest("Müşteri adı gerekli");
                }

                // Customer number uniqueness check (excluding current customer)
                if (!string.IsNullOrEmpty(customer.CustomerNumber))
                {
                    var duplicateCustomer = await _context.Customers
                        .FirstOrDefaultAsync(c => c.CustomerNumber == customer.CustomerNumber && c.Id != id && c.IsActive);
                    
                    if (duplicateCustomer != null)
                    {
                        return BadRequest("Bu müşteri numarası zaten kullanılıyor");
                    }
                }

                // Email uniqueness check (excluding current customer)
                if (!string.IsNullOrEmpty(customer.Email))
                {
                    var duplicateCustomer = await _context.Customers
                        .FirstOrDefaultAsync(c => c.Email == customer.Email && c.Id != id && c.IsActive);
                    
                    if (duplicateCustomer != null)
                    {
                        return BadRequest("Bu email adresi zaten kullanılıyor");
                    }
                }

                // Tax number uniqueness check (excluding current customer)
                if (!string.IsNullOrEmpty(customer.TaxNumber))
                {
                    var duplicateCustomer = await _context.Customers
                        .FirstOrDefaultAsync(c => c.TaxNumber == customer.TaxNumber && c.Id != id && c.IsActive);
                    
                    if (duplicateCustomer != null)
                    {
                        return BadRequest("Bu vergi numarası zaten kullanılıyor");
                    }
                }

                // Update properties
                existingCustomer.Name = customer.Name;
                existingCustomer.CustomerNumber = customer.CustomerNumber;
                existingCustomer.Email = customer.Email;
                existingCustomer.Phone = customer.Phone;
                existingCustomer.Address = customer.Address;
                existingCustomer.TaxNumber = customer.TaxNumber;
                existingCustomer.Category = customer.Category;
                existingCustomer.LoyaltyPoints = customer.LoyaltyPoints;
                existingCustomer.TotalSpent = customer.TotalSpent;
                existingCustomer.VisitCount = customer.VisitCount;
                existingCustomer.LastVisit = customer.LastVisit;
                existingCustomer.Notes = customer.Notes;
                existingCustomer.IsVip = customer.IsVip;
                existingCustomer.DiscountPercentage = customer.DiscountPercentage;
                existingCustomer.BirthDate = customer.BirthDate;
                existingCustomer.PreferredPaymentMethod = customer.PreferredPaymentMethod;
                existingCustomer.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                _logger.LogInformation("Customer updated with ID: {Id}", id);
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating customer with ID: {Id}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        // DELETE: api/Customer/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteCustomer(Guid id)
        {
            try
            {
                var customer = await _context.Customers.FindAsync(id);
                if (customer == null || !customer.IsActive)
                {
                    return NotFound("Müşteri bulunamadı");
                }

                // Soft delete
                customer.IsActive = false;
                customer.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                _logger.LogInformation("Customer deleted (soft) with ID: {Id}", id);
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting customer with ID: {Id}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        // GET: api/Customer/search?query=...
        [HttpGet("search")]
        public async Task<ActionResult<IEnumerable<Customer>>> SearchCustomers([FromQuery] string query)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(query))
                {
                    return BadRequest("Arama sorgusu gerekli");
                }

                var customers = await _context.Customers
                    .Where(c => c.IsActive && 
                               (c.Name.Contains(query) || 
                                c.CustomerNumber.Contains(query) || 
                                c.Email.Contains(query) ||
                                c.TaxNumber.Contains(query)))
                    .OrderBy(c => c.Name)
                    .Take(20)
                    .ToListAsync();

                return Ok(customers);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching customers with query: {Query}", query);
                return StatusCode(500, "Internal server error");
            }
        }

        // GET: api/Customer/vip
        [HttpGet("vip")]
        public async Task<ActionResult<IEnumerable<Customer>>> GetVipCustomers()
        {
            try
            {
                var vipCustomers = await _context.Customers
                    .Where(c => c.IsActive && c.IsVip)
                    .OrderByDescending(c => c.TotalSpent)
                    .ToListAsync();

                return Ok(vipCustomers);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving VIP customers");
                return StatusCode(500, "Internal server error");
            }
        }
    }
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Registrierkasse_API.Data;
using Microsoft.EntityFrameworkCore;
using Registrierkasse_API.Models;

namespace Registrierkasse_API.Controllers
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

        // GET: api/customers
        [HttpGet]
        public async Task<IActionResult> GetCustomers([FromQuery] string? search = null, [FromQuery] CustomerCategory? category = null)
        {
            try
            {
                var query = _context.Customers.AsQueryable();

                // Arama filtresi
                if (!string.IsNullOrEmpty(search))
                {
                    query = query.Where(c => 
                        c.Name.Contains(search) || 
                        c.Email.Contains(search) || 
                        c.Phone.Contains(search) ||
                        c.TaxNumber.Contains(search)
                    );
                }

                // Kategori filtresi
                if (category.HasValue)
                {
                    query = query.Where(c => c.Category == category.Value);
                }

                var customers = await query
                    .Select(c => new
                    {
                        id = c.Id,
                        name = c.Name,
                        email = c.Email,
                        phone = c.Phone,
                        address = c.Address,
                        taxNumber = c.TaxNumber,
                        category = c.Category,
                        discountPercentage = c.DiscountPercentage,
                        isActive = c.IsActive,
                        createdAt = c.CreatedAt,
                        updatedAt = c.UpdatedAt,
                        notes = c.Notes
                    })
                    .OrderBy(c => c.name)
                    .ToListAsync();

                return Ok(customers);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Failed to retrieve customers", details = ex.Message });
            }
        }

        // GET: api/customers/{id}
        [HttpGet("{id}")]
        public async Task<IActionResult> GetCustomer(Guid id)
        {
            try
            {
                var customer = await _context.Customers
                    .Include(c => c.Invoices)
                    .Include(c => c.Orders)
                    .Include(c => c.CustomerDiscounts)
                    .FirstOrDefaultAsync(c => c.Id == id);

                if (customer == null)
                {
                    return NotFound(new { error = "Customer not found" });
                }

                var result = new
                {
                    id = customer.Id,
                    name = customer.Name,
                    email = customer.Email,
                    phone = customer.Phone,
                    address = customer.Address,
                    taxNumber = customer.TaxNumber,
                    category = customer.Category,
                    discountPercentage = customer.DiscountPercentage,
                    isActive = customer.IsActive,
                    createdAt = customer.CreatedAt,
                    updatedAt = customer.UpdatedAt,
                    notes = customer.Notes,
                    invoiceCount = customer.Invoices?.Count ?? 0,
                    orderCount = customer.Orders?.Count ?? 0,
                    discountCount = customer.CustomerDiscounts?.Count ?? 0
                };

                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Failed to retrieve customer", details = ex.Message });
            }
        }

        // POST: api/customers
        [HttpPost]
        public async Task<IActionResult> CreateCustomer([FromBody] CreateCustomerRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                // Email benzersizlik kontrolü
                if (await _context.Customers.AnyAsync(c => c.Email == request.Email))
                {
                    return BadRequest(new { error = "Email already exists" });
                }

                var customer = new Customer
                {
                    Name = request.Name,
                    Email = request.Email,
                    Phone = request.Phone,
                    Address = request.Address,
                    TaxNumber = request.TaxNumber,
                    Category = request.Category,
                    DiscountPercentage = request.DiscountPercentage,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    Notes = request.Notes
                };

                _context.Customers.Add(customer);
                await _context.SaveChangesAsync();

                return CreatedAtAction(nameof(GetCustomer), new { id = customer.Id }, new
                {
                    id = customer.Id,
                    name = customer.Name,
                    email = customer.Email,
                    phone = customer.Phone,
                    address = customer.Address,
                    taxNumber = customer.TaxNumber,
                    category = customer.Category,
                    discountPercentage = customer.DiscountPercentage,
                    isActive = customer.IsActive,
                    createdAt = customer.CreatedAt,
                    updatedAt = customer.UpdatedAt,
                    notes = customer.Notes
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Failed to create customer", details = ex.Message });
            }
        }

        // PUT: api/customers/{id}
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateCustomer(Guid id, [FromBody] UpdateCustomerRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var customer = await _context.Customers.FindAsync(id);
                if (customer == null)
                {
                    return NotFound(new { error = "Customer not found" });
                }

                // Email benzersizlik kontrolü (kendisi hariç)
                if (await _context.Customers.AnyAsync(c => c.Email == request.Email && c.Id != id))
                {
                    return BadRequest(new { error = "Email already exists" });
                }

                customer.Name = request.Name;
                customer.Email = request.Email;
                customer.Phone = request.Phone;
                customer.Address = request.Address;
                customer.TaxNumber = request.TaxNumber;
                customer.Category = request.Category;
                customer.DiscountPercentage = request.DiscountPercentage;
                customer.Notes = request.Notes;
                customer.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                return Ok(new
                {
                    id = customer.Id,
                    name = customer.Name,
                    email = customer.Email,
                    phone = customer.Phone,
                    address = customer.Address,
                    taxNumber = customer.TaxNumber,
                    category = customer.Category,
                    discountPercentage = customer.DiscountPercentage,
                    isActive = customer.IsActive,
                    createdAt = customer.CreatedAt,
                    updatedAt = customer.UpdatedAt,
                    notes = customer.Notes
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Failed to update customer", details = ex.Message });
            }
        }

        // DELETE: api/customers/{id}
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteCustomer(Guid id)
        {
            try
            {
                var customer = await _context.Customers.FindAsync(id);
                if (customer == null)
                {
                    return NotFound(new { error = "Customer not found" });
                }

                // Müşterinin faturaları veya siparişleri varsa silme
                var hasInvoices = await _context.Invoices.AnyAsync(i => i.CustomerId == id.ToString());
                var hasOrders = await _context.Orders.AnyAsync(o => o.CustomerId == id.ToString());

                if (hasInvoices || hasOrders)
                {
                    return BadRequest(new { error = "Cannot delete customer with existing invoices or orders" });
                }

                _context.Customers.Remove(customer);
                await _context.SaveChangesAsync();

                return Ok(new { message = "Customer deleted successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Failed to delete customer", details = ex.Message });
            }
        }

        // PATCH: api/customers/{id}/status
        [HttpPatch("{id}/status")]
        public async Task<IActionResult> UpdateCustomerStatus(Guid id, [FromBody] UpdateCustomerStatusRequest request)
        {
            try
            {
                var customer = await _context.Customers.FindAsync(id);
                if (customer == null)
                {
                    return NotFound(new { error = "Customer not found" });
                }

                customer.IsActive = request.IsActive;
                customer.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                return Ok(new
                {
                    id = customer.Id,
                    isActive = customer.IsActive,
                    updatedAt = customer.UpdatedAt
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Failed to update customer status", details = ex.Message });
            }
        }
    }

    // Request Models
    public class CreateCustomerRequest
    {
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string TaxNumber { get; set; } = string.Empty;
        public CustomerCategory Category { get; set; }
        public decimal DiscountPercentage { get; set; }
        public string? Notes { get; set; }
    }

    public class UpdateCustomerRequest
    {
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string TaxNumber { get; set; } = string.Empty;
        public CustomerCategory Category { get; set; }
        public decimal DiscountPercentage { get; set; }
        public string? Notes { get; set; }
    }

    public class UpdateCustomerStatusRequest
    {
        public bool IsActive { get; set; }
    }
} 

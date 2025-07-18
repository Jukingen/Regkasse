using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Registrierkasse_API.Models;
using Registrierkasse_API.Services;
using System.Security.Claims;

namespace Registrierkasse_API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class CustomerController : ControllerBase
    {
        private readonly ICustomerService _customerService;

        public CustomerController(ICustomerService customerService)
        {
            _customerService = customerService;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Customer>>> GetAllCustomers()
        {
            try
            {
                var customers = await _customerService.GetAllCustomersAsync();
                return Ok(customers);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Failed to retrieve customers", message = ex.Message });
            }
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<Customer>> GetCustomer(Guid id)
        {
            try
            {
                var customer = await _customerService.GetCustomerByIdAsync(id);
                
                if (customer == null)
                    return NotFound(new { error = "Customer not found" });

                return Ok(customer);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Failed to retrieve customer", message = ex.Message });
            }
        }

        [HttpGet("email/{email}")]
        public async Task<ActionResult<Customer>> GetCustomerByEmail(string email)
        {
            try
            {
                var customer = await _customerService.GetCustomerByEmailAsync(email);
                
                if (customer == null)
                    return NotFound(new { error = "Customer not found" });

                return Ok(customer);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Failed to retrieve customer", message = ex.Message });
            }
        }

        [HttpGet("category/{category}")]
        public async Task<ActionResult<IEnumerable<Customer>>> GetCustomersByCategory(CustomerCategory category)
        {
            try
            {
                var customers = await _customerService.GetCustomersByCategoryAsync(category);
                return Ok(customers);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Failed to retrieve customers by category", message = ex.Message });
            }
        }

        [HttpGet("search")]
        public async Task<ActionResult<IEnumerable<Customer>>> SearchCustomers([FromQuery] string q)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(q))
                    return BadRequest(new { error = "Search term is required" });

                var customers = await _customerService.SearchCustomersAsync(q);
                return Ok(customers);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Failed to search customers", message = ex.Message });
            }
        }

        [HttpPost]
        public async Task<ActionResult<Customer>> CreateCustomer([FromBody] Customer customer)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(customer.Name))
                    return BadRequest(new { error = "Customer name is required" });

                if (!string.IsNullOrWhiteSpace(customer.Email))
                {
                    var existingCustomer = await _customerService.GetCustomerByEmailAsync(customer.Email);
                    if (existingCustomer != null)
                        return BadRequest(new { error = "Customer with this email already exists" });
                }

                var createdCustomer = await _customerService.CreateCustomerAsync(customer);
                return CreatedAtAction(nameof(GetCustomer), new { id = createdCustomer.Id }, createdCustomer);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Failed to create customer", message = ex.Message });
            }
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<Customer>> UpdateCustomer(Guid id, [FromBody] Customer customer)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(customer.Name))
                    return BadRequest(new { error = "Customer name is required" });

                var updatedCustomer = await _customerService.UpdateCustomerAsync(id, customer);
                return Ok(updatedCustomer);
            }
            catch (InvalidOperationException ex)
            {
                return NotFound(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Failed to update customer", message = ex.Message });
            }
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin,Manager")]
        public async Task<ActionResult> DeleteCustomer(Guid id)
        {
            try
            {
                var success = await _customerService.DeleteCustomerAsync(id);
                
                if (!success)
                    return NotFound(new { error = "Customer not found" });

                return NoContent();
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Failed to delete customer", message = ex.Message });
            }
        }

        [HttpPost("{id}/loyalty-points")]
        public async Task<ActionResult<Customer>> UpdateLoyaltyPoints(Guid id, [FromBody] LoyaltyPointsRequest request)
        {
            try
            {
                var customer = await _customerService.UpdateLoyaltyPointsAsync(id, request.Points);
                return Ok(customer);
            }
            catch (InvalidOperationException ex)
            {
                return NotFound(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Failed to update loyalty points", message = ex.Message });
            }
        }

        [HttpGet("{id}/discount")]
        public async Task<ActionResult<decimal>> CalculateCustomerDiscount(Guid id, [FromQuery] decimal totalAmount)
        {
            try
            {
                if (totalAmount <= 0)
                    return BadRequest(new { error = "Total amount must be greater than 0" });

                var discount = await _customerService.CalculateCustomerDiscountAsync(id, totalAmount);
                return Ok(new { customerId = id, totalAmount, discount });
            }
            catch (InvalidOperationException ex)
            {
                return NotFound(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Failed to calculate customer discount", message = ex.Message });
            }
        }

        [HttpGet("{id}/discounts")]
        public async Task<ActionResult<IEnumerable<CustomerDiscount>>> GetCustomerDiscounts(Guid id)
        {
            try
            {
                var discounts = await _customerService.GetCustomerDiscountsAsync(id);
                return Ok(discounts);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Failed to retrieve customer discounts", message = ex.Message });
            }
        }

        [HttpPost("{id}/discounts")]
        [Authorize(Roles = "Admin,Manager")]
        public async Task<ActionResult<CustomerDiscount>> AddCustomerDiscount(Guid id, [FromBody] CustomerDiscount discount)
        {
            try
            {
                discount.CustomerId = id;
                
                if (discount.DiscountValue <= 0)
                    return BadRequest(new { error = "Discount value must be greater than 0" });

                var createdDiscount = await _customerService.AddCustomerDiscountAsync(discount);
                return CreatedAtAction(nameof(GetCustomerDiscounts), new { id }, createdDiscount);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Failed to add customer discount", message = ex.Message });
            }
        }

        [HttpDelete("discounts/{discountId}")]
        [Authorize(Roles = "Admin,Manager")]
        public async Task<ActionResult> RemoveCustomerDiscount(Guid discountId)
        {
            try
            {
                var success = await _customerService.RemoveCustomerDiscountAsync(discountId);
                
                if (!success)
                    return NotFound(new { error = "Customer discount not found" });

                return NoContent();
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Failed to remove customer discount", message = ex.Message });
            }
        }

        [HttpGet("{id}/statistics")]
        public async Task<ActionResult<CustomerStatistics>> GetCustomerStatistics(Guid id)
        {
            try
            {
                var statistics = await _customerService.GetCustomerStatisticsAsync(id);
                return Ok(statistics);
            }
            catch (InvalidOperationException ex)
            {
                return NotFound(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Failed to retrieve customer statistics", message = ex.Message });
            }
        }

        [HttpGet("categories")]
        public ActionResult<IEnumerable<object>> GetCustomerCategories()
        {
            var categories = Enum.GetValues(typeof(CustomerCategory))
                .Cast<CustomerCategory>()
                .Select(c => new { 
                    value = (int)c, 
                    name = c.ToString(),
                    displayName = GetCategoryDisplayName(c)
                });

            return Ok(categories);
        }

        private string GetCategoryDisplayName(CustomerCategory category)
        {
            return category switch
            {
                CustomerCategory.Regular => "Düzenli",
                CustomerCategory.VIP => "VIP",
                CustomerCategory.Premium => "Premium",
                CustomerCategory.Corporate => "Kurumsal",
                CustomerCategory.Student => "Öğrenci",
                CustomerCategory.Senior => "Yaşlı",
                _ => category.ToString()
            };
        }
    }

    public class LoyaltyPointsRequest
    {
        public int Points { get; set; }
    }
} 

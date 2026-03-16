using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using Microsoft.AspNetCore.Authorization;
using KasseAPI_Final.Controllers.Base;
using KasseAPI_Final.Data.Repositories;

namespace KasseAPI_Final.Controllers
{
    /// <summary>
    /// Customer management API. Access controlled by customer.view (read) and customer.manage (write).
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    [HasPermission(AppPermissions.CustomerView)]
    public class CustomerController : EntityController<Customer>
    {
        private readonly AppDbContext _context;
        private readonly IGenericRepository<Customer> _customerRepository;

        public CustomerController(
            AppDbContext context, 
            IGenericRepository<Customer> customerRepository,
            ILogger<CustomerController> logger) : base(customerRepository, logger)
        {
            _context = context;
            _customerRepository = customerRepository;
        }

        /// <summary>
        /// Get customer by customer number.
        /// </summary>
        [HttpGet("number/{customerNumber}")]
        public async Task<IActionResult> GetByCustomerNumber(string customerNumber)
        {
            try
            {
                var customer = await _context.Customers
                    .FirstOrDefaultAsync(c => c.CustomerNumber == customerNumber && c.IsActive);

                if (customer == null)
                {
                    return ErrorResponse($"Customer with number {customerNumber} not found", 404);
                }

                return SuccessResponse(customer, "Customer retrieved successfully");
            }
            catch (Exception ex)
            {
                return HandleException(ex, $"GetByCustomerNumber with number {customerNumber}");
            }
        }

        /// <summary>
        /// Get customer by email.
        /// </summary>
        [HttpGet("email/{email}")]
        public async Task<IActionResult> GetByEmail(string email)
        {
            try
            {
                var customer = await _context.Customers
                    .FirstOrDefaultAsync(c => c.Email == email && c.IsActive);

                if (customer == null)
                {
                    return ErrorResponse($"Customer with email {email} not found", 404);
                }

                return SuccessResponse(customer, "Customer retrieved successfully");
            }
            catch (Exception ex)
            {
                return HandleException(ex, $"GetByEmail with email {email}");
            }
        }

        /// <summary>
        /// Get customer by tax number.
        /// </summary>
        [HttpGet("tax/{taxNumber}")]
        public async Task<IActionResult> GetByTaxNumber(string taxNumber)
        {
            try
            {
                var customer = await _context.Customers
                    .FirstOrDefaultAsync(c => c.TaxNumber == taxNumber && c.IsActive);

                if (customer == null)
                {
                    return ErrorResponse($"Customer with tax number {taxNumber} not found", 404);
                }

                return SuccessResponse(customer, "Customer retrieved successfully");
            }
            catch (Exception ex)
            {
                return HandleException(ex, $"GetByTaxNumber with tax number {taxNumber}");
            }
        }

        /// <summary>
        /// Create customer (with custom validation).
        /// </summary>
        [HttpPost]
        [HasPermission(AppPermissions.CustomerManage)]
        public override async Task<IActionResult> Create([FromBody] Customer customer)
        {
            try
            {
                var validationResult = ValidateModel();
                if (validationResult != null)
                {
                    return validationResult;
                }

                // Custom validation
                var validationErrors = await ValidateCustomerAsync(customer);
                if (validationErrors.Any())
                {
                    return ErrorResponse("Validation failed", 400, validationErrors);
                }

                var createdCustomer = await _customerRepository.AddAsync(customer);
                
                return CreatedAtAction(nameof(GetById), new { id = createdCustomer.Id }, 
                    SuccessResponse(createdCustomer, "Customer created successfully"));
            }
            catch (Exception ex)
            {
                return HandleException(ex, "Create Customer");
            }
        }

        /// <summary>
        /// Update customer (with custom validation).
        /// </summary>
        [HttpPut("{id}")]
        [HasPermission(AppPermissions.CustomerManage)]
        public override async Task<IActionResult> Update(Guid id, [FromBody] Customer customer)
        {
            try
            {
                var validationResult = ValidateModel();
                if (validationResult != null)
                {
                    return validationResult;
                }

                if (id != customer.Id)
                {
                    return ErrorResponse("ID mismatch between URL and request body", 400);
                }

                // Custom validation
                var validationErrors = await ValidateCustomerAsync(customer, id);
                if (validationErrors.Any())
                {
                    return ErrorResponse("Validation failed", 400, validationErrors);
                }

                var updatedCustomer = await _customerRepository.UpdateAsync(customer);
                
                return SuccessResponse(updatedCustomer, "Customer updated successfully");
            }
            catch (Exception ex)
            {
                return HandleException(ex, $"Update Customer with ID {id}");
            }
        }

        /// <summary>
        /// Validates customer (name required, unique customer number/email/tax number).
        /// </summary>
        private async Task<List<string>> ValidateCustomerAsync(Customer customer, Guid? excludeId = null)
        {
            var errors = new List<string>();

            if (string.IsNullOrWhiteSpace(customer.Name))
            {
                errors.Add("Customer name is required");
            }

            // Unique customer number
            if (!string.IsNullOrEmpty(customer.CustomerNumber))
            {
                var existingCustomer = await _context.Customers
                    .FirstOrDefaultAsync(c => c.CustomerNumber == customer.CustomerNumber && 
                                            c.IsActive && 
                                            c.Id != excludeId);
                
                if (existingCustomer != null)
                {
                    errors.Add("This customer number is already in use");
                }
            }

            // Unique email
            if (!string.IsNullOrEmpty(customer.Email))
            {
                var existingCustomer = await _context.Customers
                    .FirstOrDefaultAsync(c => c.Email == customer.Email && 
                                            c.IsActive && 
                                            c.Id != excludeId);
                
                if (existingCustomer != null)
                {
                    errors.Add("This email address is already in use");
                }
            }

            // Unique tax number
            if (!string.IsNullOrEmpty(customer.TaxNumber))
            {
                var existingCustomer = await _context.Customers
                    .FirstOrDefaultAsync(c => c.TaxNumber == customer.TaxNumber && 
                                            c.IsActive && 
                                            c.Id != excludeId);
                
                if (existingCustomer != null)
                {
                    errors.Add("This tax number is already in use");
                }
            }

            return errors;
        }

        /// <summary>
        /// Soft-delete customer. Requires customer.manage.
        /// </summary>
        [HttpDelete("{id}")]
        [HasPermission(AppPermissions.CustomerManage)]
        public override async Task<IActionResult> Delete(Guid id)
        {
            return await base.Delete(id);
        }

        /// <summary>
        /// Returns a minimal benefit summary for the customer (assigned active benefit count).
        /// For POS eligibility/preview preparation; no eligibility logic, read-only.
        /// </summary>
        [HttpGet("{id}/benefit-summary")]
        public async Task<IActionResult> GetBenefitSummary(Guid id)
        {
            try
            {
                var customerExists = await _context.Customers.AnyAsync(c => c.Id == id && c.IsActive);
                if (!customerExists)
                    return ErrorResponse("Customer not found or inactive", 404);

                var now = DateTime.UtcNow;
                var count = await _context.BenefitAssignments
                    .CountAsync(ba => ba.CustomerId == id && ba.IsActive
                        && ba.ValidFrom <= now && (ba.ValidTo == null || ba.ValidTo >= now));

                return SuccessResponse(new { assignedBenefitCount = count }, "Benefit summary retrieved");
            }
            catch (Exception ex)
            {
                return HandleException(ex, $"GetBenefitSummary for customer {id}");
            }
        }

        /// <summary>
        /// Search customers by name, email, or phone.
        /// </summary>
        [HttpGet("search")]
        public async Task<IActionResult> Search([FromQuery] string? name, [FromQuery] string? email, [FromQuery] string? phone)
        {
            try
            {
                var query = _context.Customers.Where(c => c.IsActive);

                if (!string.IsNullOrWhiteSpace(name))
                {
                    query = query.Where(c => c.Name.Contains(name));
                }

                if (!string.IsNullOrWhiteSpace(email))
                {
                    query = query.Where(c => c.Email != null && c.Email.Contains(email));
                }

                if (!string.IsNullOrWhiteSpace(phone))
                {
                    query = query.Where(c => c.Phone != null && c.Phone.Contains(phone));
                }

                var customers = await query
                    .OrderBy(c => c.Name)
                    .ToListAsync();

                return SuccessResponse(customers, $"Found {customers.Count} customers matching search criteria");
            }
            catch (Exception ex)
            {
                return HandleException(ex, "Search Customers");
            }
        }
    }
}

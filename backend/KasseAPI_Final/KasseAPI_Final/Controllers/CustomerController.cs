using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using Microsoft.AspNetCore.Authorization;
using KasseAPI_Final.Controllers.Base;
using KasseAPI_Final.Data.Repositories;

namespace KasseAPI_Final.Controllers
{
    /// <summary>
    /// Müşteri yönetimi için controller
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
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
        /// Müşteri numarasına göre müşteri getir
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
        /// Email'e göre müşteri getir
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
        /// Vergi numarasına göre müşteri getir
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
        /// Müşteri oluştur (özel validation ile)
        /// </summary>
        [HttpPost]
        public override async Task<IActionResult> Create([FromBody] Customer customer)
        {
            try
            {
                var validationResult = ValidateModel();
                if (validationResult != null)
                {
                    return validationResult;
                }

                // Özel validation
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
        /// Müşteri güncelle (özel validation ile)
        /// </summary>
        [HttpPut("{id}")]
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

                // Özel validation
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
        /// Müşteri validation
        /// </summary>
        private async Task<List<string>> ValidateCustomerAsync(Customer customer, Guid? excludeId = null)
        {
            var errors = new List<string>();

            // Müşteri adı kontrolü
            if (string.IsNullOrWhiteSpace(customer.Name))
            {
                errors.Add("Customer name is required");
            }

            // Müşteri numarası benzersizlik kontrolü
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

            // Email benzersizlik kontrolü
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

            // Vergi numarası benzersizlik kontrolü
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
        /// Müşteri arama
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

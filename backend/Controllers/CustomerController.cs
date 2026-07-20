using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Constants;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Loyalty;
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
        private readonly IPaymentService _paymentService;
        private readonly ICustomerService _customerService;
        private readonly ILoyaltyService _loyaltyService;

        public CustomerController(
            AppDbContext context,
            IGenericRepository<Customer> customerRepository,
            IPaymentService paymentService,
            ICustomerService customerService,
            ILoyaltyService loyaltyService,
            ILogger<CustomerController> logger) : base(customerRepository, logger)
        {
            _context = context;
            _customerRepository = customerRepository;
            _paymentService = paymentService;
            _customerService = customerService;
            _loyaltyService = loyaltyService;
        }

        private bool IsCurrentUserSuperAdmin() => HasRole(Roles.SuperAdmin);

        private async Task<IActionResult?> RejectSystemCustomerMutationAsync(Guid customerId)
        {
            if (IsCurrentUserSuperAdmin())
                return null;

            if (!await _customerService.CanModifyCustomerAsync(customerId))
                return ErrorResponse("System customers cannot be modified or deleted", 403);

            return null;
        }

        private async Task<IActionResult?> RejectSystemCustomerDeleteAsync(Guid customerId)
        {
            if (IsCurrentUserSuperAdmin())
                return null;

            if (!await _customerService.CanDeleteCustomerAsync(customerId))
                return ErrorResponse("System customers cannot be modified or deleted", 403);

            return null;
        }

        /// <summary>
        /// List customers. System customers are hidden from non–Super Admin users.
        /// </summary>
        [HttpGet]
        public override async Task<IActionResult> GetAll([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20)
        {
            try
            {
                var (validPageNumber, validPageSize) = ValidatePagination(pageNumber, pageSize);
                var query = _context.Customers.AsQueryable();

                if (IsCurrentUserSuperAdmin())
                    // Super Admin operates cross-tenant: bypass the tenant query filter for deployment-wide visibility.
                    query = query.IgnoreQueryFilters();
                else
                    query = query.Where(c => !c.IsSystem && c.Id != WalkInCustomerConstants.GuestCustomerId);

                query = query.Where(c => c.IsActive);

                var totalCount = await query.CountAsync();
                var items = await query
                    .OrderByDescending(c => c.CreatedAt)
                    .Skip((validPageNumber - 1) * validPageSize)
                    .Take(validPageSize)
                    .ToListAsync();

                var response = new
                {
                    items,
                    pagination = new
                    {
                        pageNumber = validPageNumber,
                        pageSize = validPageSize,
                        totalCount,
                        totalPages = (int)Math.Ceiling((double)totalCount / validPageSize)
                    }
                };

                return SuccessResponse(response, $"Retrieved {items.Count} Customer entities");
            }
            catch (Exception ex)
            {
                return HandleException(ex, "GetAll Customer");
            }
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

        [HttpGet("walk-in")]
        public IActionResult GetWalkInCustomerId()
        {
            return SuccessResponse(
                new { customerId = WalkInCustomerConstants.GuestCustomerId },
                "Walk-in customer id");
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

                customer.IsSystem = false;

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

                var existingCustomer = await _customerService.GetCustomerAsync(id);
                if (existingCustomer == null)
                    return ErrorResponse($"Customer with ID {id} not found", 404);

                var systemGuard = await RejectSystemCustomerMutationAsync(id);
                if (systemGuard != null)
                    return systemGuard;

                if (!IsCurrentUserSuperAdmin())
                    customer.IsSystem = existingCustomer.IsSystem;

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
            try
            {
                var existingCustomer = await _customerService.GetCustomerAsync(id);
                if (existingCustomer == null)
                    return ErrorResponse($"Customer with ID {id} not found", 404);

                var systemGuard = await RejectSystemCustomerDeleteAsync(id);
                if (systemGuard != null)
                    return systemGuard;

                return await base.Delete(id);
            }
            catch (Exception ex)
            {
                return HandleException(ex, $"Delete Customer with ID {id}");
            }
        }

        /// <summary>
        /// Returns the count of benefit assignments that are active and within their validity window (ValidFrom/ValidTo).
        /// Assignment-level only; does not reflect payment-time applicability. Consumed by two distinct intents:
        /// admin = assignment visibility for display; POS = preview (POS may attach eligibility semantics separately).
        /// Read-only.
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

                return SuccessResponse(new { assignedBenefitCount = count, activeAssignmentCount = count }, "Benefit assignment count (active, in validity window) retrieved");
            }
            catch (Exception ex)
            {
                return HandleException(ex, $"GetBenefitSummary for customer {id}");
            }
        }

        /// <summary>
        /// Eligibility preview for POS: which benefits would apply for this customer and cart, and which are blocked (reasons).
        /// Read-only; does not create payment or update BenefitDailyUsage. Distinct from GET benefit-summary (assignment count only).
        /// </summary>
        [HttpPost("{id}/benefit-eligibility-preview")]
        public async Task<IActionResult> BenefitEligibilityPreview(Guid id, [FromBody] BenefitEligibilityPreviewRequest? body)
        {
            try
            {
                if (body == null)
                    return ErrorResponse("Request body is required", 400);
                body.CustomerId = id;
                if (body.Items == null)
                    body.Items = new List<BenefitEligibilityPreviewItemRequest>();

                var result = await _paymentService.ComputeBenefitEligibilityPreviewAsync(body);
                if (result == null)
                    return ErrorResponse("Customer not found or inactive", 404);

                return SuccessResponse(result, "Eligibility preview computed");
            }
            catch (Exception ex)
            {
                return HandleException(ex, $"BenefitEligibilityPreview for customer {id}");
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
                var query = _context.Customers.AsQueryable();

                if (IsCurrentUserSuperAdmin())
                    // Super Admin operates cross-tenant: bypass the tenant query filter for deployment-wide visibility.
                    query = query.IgnoreQueryFilters();
                else
                    query = query.Where(c => !c.IsSystem && c.Id != WalkInCustomerConstants.GuestCustomerId);

                query = query.Where(c => c.IsActive);

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

        /// <summary>Current loyalty point balance for a customer.</summary>
        [HttpGet("{id:guid}/loyalty")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetLoyaltyBalance(Guid id, CancellationToken cancellationToken)
        {
            try
            {
                var balance = await _loyaltyService.GetBalanceAsync(id, cancellationToken);
                if (balance is null)
                    return ErrorResponse($"Customer with ID {id} not found", 404);

                return SuccessResponse(
                    new LoyaltyBalanceDto { CustomerId = id, LoyaltyPoints = balance.Value },
                    "Loyalty balance retrieved");
            }
            catch (Exception ex)
            {
                return HandleException(ex, $"GetLoyaltyBalance for customer {id}");
            }
        }

        /// <summary>
        /// Redeem loyalty points for an EUR discount (100 points = 1 EUR). Not a fiscal payment.
        /// </summary>
        [HttpPost("{id:guid}/loyalty/redeem")]
        [HasPermission(AppPermissions.CustomerManage)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> RedeemLoyaltyPoints(
            Guid id,
            [FromBody] RedeemLoyaltyPointsRequest request,
            CancellationToken cancellationToken)
        {
            try
            {
                var systemGuard = await RejectSystemCustomerMutationAsync(id);
                if (systemGuard != null)
                    return systemGuard;

                var result = await _loyaltyService.RedeemPointsAsync(id, request.Points, cancellationToken);
                if (!result.Succeeded)
                {
                    var status = result.Code == LoyaltyService.NotFoundCode ? 404 : 400;
                    return ErrorResponse(result.Message ?? "Loyalty redeem failed", status, new List<string> { result.Code ?? "LOYALTY_ERROR" });
                }

                return SuccessResponse(
                    new RedeemLoyaltyPointsResponse
                    {
                        CustomerId = id,
                        PointsRedeemed = request.Points,
                        DiscountEuro = result.Value,
                        LoyaltyPointsBalance = result.Balance
                    },
                    "Loyalty points redeemed");
            }
            catch (Exception ex)
            {
                return HandleException(ex, $"RedeemLoyaltyPoints for customer {id}");
            }
        }
    }
}

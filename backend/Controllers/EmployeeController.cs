using KasseAPI_Final.Authorization;
using KasseAPI_Final.Controllers.Base;
using KasseAPI_Final.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Controllers
{
    /// <summary>
    /// POS employee identification: lookup by EmployeeNumber and resolve to benefit-bearing Customer identity.
    /// Does not replace auth; used only for attaching staff identity to a sale for benefit application.
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    [HasPermission(AppPermissions.OrderCreate)]
    public class EmployeeController : BaseController
    {
        private readonly AppDbContext _context;

        public EmployeeController(AppDbContext context, ILogger<EmployeeController> logger)
            : base(logger)
        {
            _context = context;
        }

        /// <summary>
        /// Look up employee by EmployeeNumber and return the linked Customer record used for benefit application.
        /// Mapping: ApplicationUser (by EmployeeNumber) -> Customer where ApplicationUserId == user.Id (explicit FK).
        /// </summary>
        [HttpGet("by-number/{employeeNumber}")]
        public async Task<IActionResult> GetByEmployeeNumber(string employeeNumber)
        {
            var trimmed = (employeeNumber ?? "").Trim();
            if (string.IsNullOrEmpty(trimmed))
            {
                return ErrorResponse("Employee number is required.", 400);
            }

            try
            {
                var user = await _context.Users
                    .AsNoTracking()
                    .FirstOrDefaultAsync(u => u.EmployeeNumber == trimmed && u.IsActive);

                if (user == null)
                {
                    return ErrorResponse("Employee not found or inactive.", 404);
                }

                var customer = await _context.Customers
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c => c.ApplicationUserId == user.Id && c.IsActive);

                if (customer == null)
                {
                    _logger.LogWarning("Employee {EmployeeNumber} (UserId {UserId}) has no linked Customer record for benefits.", trimmed, user.Id);
                    return ErrorResponse("Employee has no linked benefit identity.", 404);
                }

                return SuccessResponse(customer, "Employee resolved successfully.");
            }
            catch (Exception ex)
            {
                return HandleException(ex, "GetByEmployeeNumber");
            }
        }

        /// <summary>
        /// List active employees that have a linked Customer (benefit identity). For POS "Aus Liste wählen".
        /// </summary>
        [HttpGet("list")]
        public async Task<IActionResult> GetList()
        {
            try
            {
                var employees = await _context.Users
                    .AsNoTracking()
                    .Where(u => u.IsActive && u.EmployeeNumber != null && u.EmployeeNumber != "")
                    .Join(
                        _context.Customers.AsNoTracking().Where(c => c.IsActive),
                        u => u.Id,
                        c => c.ApplicationUserId,
                        (u, c) => new
                        {
                            EmployeeNumber = u.EmployeeNumber ?? "",
                            Name = u.FirstName + " " + u.LastName,
                            UserId = u.Id,
                            CustomerId = c.Id
                        })
                    .OrderBy(x => x.Name)
                    .ToListAsync();

                return SuccessResponse(employees, "Employee list retrieved successfully.");
            }
            catch (Exception ex)
            {
                return HandleException(ex, "GetList");
            }
        }
    }
}

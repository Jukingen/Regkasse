using Microsoft.EntityFrameworkCore;
using Registrierkasse_API.Data;
using Registrierkasse_API.Models;
using System.Security.Claims;

namespace Registrierkasse_API.Services
{
    public interface ICustomerService
    {
        Task<IEnumerable<Customer>> GetAllCustomersAsync();
        Task<Customer> GetCustomerByIdAsync(Guid id);
        Task<Customer> GetCustomerByEmailAsync(string email);
        Task<Customer> CreateCustomerAsync(Customer customer);
        Task<Customer> UpdateCustomerAsync(Guid id, Customer customer);
        Task<bool> DeleteCustomerAsync(Guid id);
        Task<IEnumerable<Customer>> GetCustomersByCategoryAsync(CustomerCategory category);
        Task<Customer> UpdateLoyaltyPointsAsync(Guid customerId, int points);
        Task<decimal> CalculateCustomerDiscountAsync(Guid customerId, decimal totalAmount);
        Task<IEnumerable<CustomerDiscount>> GetCustomerDiscountsAsync(Guid customerId);
        Task<CustomerDiscount> AddCustomerDiscountAsync(CustomerDiscount discount);
        Task<bool> RemoveCustomerDiscountAsync(Guid discountId);
        Task<IEnumerable<Customer>> SearchCustomersAsync(string searchTerm);
        Task<CustomerStatistics> GetCustomerStatisticsAsync(Guid customerId);
    }

    public class CustomerService : ICustomerService
    {
        private readonly AppDbContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public CustomerService(AppDbContext context, IHttpContextAccessor httpContextAccessor)
        {
            _context = context;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task<IEnumerable<Customer>> GetAllCustomersAsync()
        {
            return await _context.Customers
                .Include(c => c.CustomerDiscounts.Where(cd => cd.IsActive))
                .OrderBy(c => c.Name)
                .ToListAsync();
        }

        public async Task<Customer> GetCustomerByIdAsync(Guid id)
        {
            return await _context.Customers
                .Include(c => c.CustomerDiscounts.Where(cd => cd.IsActive))
                .Include(c => c.Invoices.OrderByDescending(i => i.CreatedAt).Take(10))
                .Include(c => c.Orders.OrderByDescending(o => o.CreatedAt).Take(10))
                .FirstOrDefaultAsync(c => c.Id == id);
        }

        public async Task<Customer> GetCustomerByEmailAsync(string email)
        {
            return await _context.Customers
                .Include(c => c.CustomerDiscounts.Where(cd => cd.IsActive))
                .FirstOrDefaultAsync(c => c.Email == email);
        }

        public async Task<Customer> CreateCustomerAsync(Customer customer)
        {
            customer.CreatedAt = DateTime.UtcNow;
            customer.IsActive = true;
            customer.LoyaltyPoints = 0;
            customer.TotalSpent = 0;
            customer.VisitCount = 0;

            _context.Customers.Add(customer);
            await _context.SaveChangesAsync();

            return customer;
        }

        public async Task<Customer> UpdateCustomerAsync(Guid id, Customer customer)
        {
            var existingCustomer = await _context.Customers.FindAsync(id);
            if (existingCustomer == null)
                throw new InvalidOperationException("Müşteri bulunamadı.");

            existingCustomer.Name = customer.Name;
            existingCustomer.Email = customer.Email;
            existingCustomer.Phone = customer.Phone;
            existingCustomer.Address = customer.Address;
            existingCustomer.TaxNumber = customer.TaxNumber;
            existingCustomer.Category = customer.Category;
            existingCustomer.IsVip = customer.IsVip;
            existingCustomer.DiscountPercentage = customer.DiscountPercentage;
            existingCustomer.BirthDate = customer.BirthDate;
            existingCustomer.PreferredPaymentMethod = customer.PreferredPaymentMethod;
            existingCustomer.Notes = customer.Notes;
            existingCustomer.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return existingCustomer;
        }

        public async Task<bool> DeleteCustomerAsync(Guid id)
        {
            var customer = await _context.Customers.FindAsync(id);
            if (customer == null)
                return false;

            customer.IsActive = false;
            customer.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return true;
        }

        public async Task<IEnumerable<Customer>> GetCustomersByCategoryAsync(CustomerCategory category)
        {
            return await _context.Customers
                .Where(c => c.Category == category && c.IsActive)
                .OrderBy(c => c.Name)
                .ToListAsync();
        }

        public async Task<Customer> UpdateLoyaltyPointsAsync(Guid customerId, int points)
        {
            var customer = await _context.Customers.FindAsync(customerId);
            if (customer == null)
                throw new InvalidOperationException("Müşteri bulunamadı.");

            customer.LoyaltyPoints += points;
            customer.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return customer;
        }

        public async Task<decimal> CalculateCustomerDiscountAsync(Guid customerId, decimal totalAmount)
        {
            var customer = await _context.Customers
                .Include(c => c.CustomerDiscounts.Where(cd => cd.IsActive && 
                    cd.ValidFrom <= DateTime.UtcNow && 
                    (cd.ValidUntil == null || cd.ValidUntil >= DateTime.UtcNow)))
                .FirstOrDefaultAsync(c => c.Id == customerId);

            if (customer == null)
                return 0;

            decimal totalDiscount = 0;

            // Müşteri kategorisine göre indirim
            if (customer.DiscountPercentage > 0)
            {
                totalDiscount += totalAmount * (customer.DiscountPercentage / 100);
            }

            // VIP müşteri ek indirimi
            if (customer.IsVip)
            {
                totalDiscount += totalAmount * 0.05m; // %5 ek VIP indirimi
            }

            // Özel müşteri indirimleri
            foreach (var discount in customer.CustomerDiscounts)
            {
                if (totalAmount >= discount.MinimumAmount)
                {
                    switch (discount.DiscountType)
                    {
                        case DiscountType.Percentage:
                            totalDiscount += totalAmount * (discount.DiscountValue / 100);
                            break;
                        case DiscountType.FixedAmount:
                            totalDiscount += discount.DiscountValue;
                            break;
                    }
                }
            }

            return Math.Min(totalDiscount, totalAmount); // İndirim tutarı toplam tutarı aşamaz
        }

        public async Task<IEnumerable<CustomerDiscount>> GetCustomerDiscountsAsync(Guid customerId)
        {
            return await _context.CustomerDiscounts
                .Where(cd => cd.CustomerId == customerId && cd.IsActive)
                .OrderByDescending(cd => cd.ValidFrom)
                .ToListAsync();
        }

        public async Task<CustomerDiscount> AddCustomerDiscountAsync(CustomerDiscount discount)
        {
            discount.CreatedBy = GetCurrentUserId();
            discount.CreatedAt = DateTime.UtcNow;
            discount.IsActive = true;
            discount.UsedCount = 0;

            _context.CustomerDiscounts.Add(discount);
            await _context.SaveChangesAsync();

            return discount;
        }

        public async Task<bool> RemoveCustomerDiscountAsync(Guid discountId)
        {
            var discount = await _context.CustomerDiscounts.FindAsync(discountId);
            if (discount == null)
                return false;

            discount.IsActive = false;
            discount.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return true;
        }

        public async Task<IEnumerable<Customer>> SearchCustomersAsync(string searchTerm)
        {
            return await _context.Customers
                .Where(c => c.IsActive && (
                    c.Name.Contains(searchTerm) ||
                    c.Email.Contains(searchTerm) ||
                    c.Phone.Contains(searchTerm) ||
                    c.TaxNumber.Contains(searchTerm)
                ))
                .OrderBy(c => c.Name)
                .ToListAsync();
        }

        public async Task<CustomerStatistics> GetCustomerStatisticsAsync(Guid customerId)
        {
            var customer = await _context.Customers
                .Include(c => c.Invoices)
                .Include(c => c.Orders)
                .FirstOrDefaultAsync(c => c.Id == customerId);

            if (customer == null)
                throw new InvalidOperationException("Müşteri bulunamadı.");

            var totalInvoices = customer.Invoices.Count;
            var totalOrders = customer.Orders.Count;
            var averageInvoiceAmount = totalInvoices > 0 ? customer.Invoices.Average(i => i.TotalAmount) : 0;
            var averageOrderAmount = totalOrders > 0 ? customer.Orders.Average(o => o.TotalAmount) : 0;

            var lastInvoice = customer.Invoices.OrderByDescending(i => i.CreatedAt).FirstOrDefault();
            var lastOrder = customer.Orders.OrderByDescending(o => o.CreatedAt).FirstOrDefault();

            return new CustomerStatistics
            {
                CustomerId = customerId,
                TotalSpent = customer.TotalSpent,
                LoyaltyPoints = customer.LoyaltyPoints,
                VisitCount = customer.VisitCount,
                TotalInvoices = totalInvoices,
                TotalOrders = totalOrders,
                AverageInvoiceAmount = averageInvoiceAmount,
                AverageOrderAmount = averageOrderAmount,
                LastVisit = customer.LastVisit,
                LastInvoice = lastInvoice?.CreatedAt,
                LastOrder = lastOrder?.CreatedAt,
                Category = customer.Category,
                IsVip = customer.IsVip,
                DiscountPercentage = customer.DiscountPercentage
            };
        }

        private string GetCurrentUserId()
        {
            return _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "system";
        }
    }

    public class CustomerStatistics
    {
        public Guid CustomerId { get; set; }
        public decimal TotalSpent { get; set; }
        public int LoyaltyPoints { get; set; }
        public int VisitCount { get; set; }
        public int TotalInvoices { get; set; }
        public int TotalOrders { get; set; }
        public decimal AverageInvoiceAmount { get; set; }
        public decimal AverageOrderAmount { get; set; }
        public DateTime? LastVisit { get; set; }
        public DateTime? LastInvoice { get; set; }
        public DateTime? LastOrder { get; set; }
        public CustomerCategory Category { get; set; }
        public bool IsVip { get; set; }
        public decimal DiscountPercentage { get; set; }
    }
} 
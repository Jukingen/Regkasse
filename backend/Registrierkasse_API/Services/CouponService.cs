using Microsoft.EntityFrameworkCore;
using Registrierkasse_API.Data;
using Registrierkasse_API.Models;
using System.Security.Claims;

namespace Registrierkasse_API.Services
{
    public interface ICouponService
    {
        Task<CouponValidationResult> ValidateCouponAsync(string code, decimal totalAmount, Guid? customerId = null);
        Task<CouponUsage> UseCouponAsync(string code, decimal discountAmount, Guid? customerId = null, Guid? invoiceId = null, Guid? orderId = null);
        Task<IEnumerable<Coupon>> GetActiveCouponsAsync();
        Task<Coupon> CreateCouponAsync(Coupon coupon, string createdBy);
        Task<Coupon> UpdateCouponAsync(Guid id, Coupon coupon);
        Task<bool> DeleteCouponAsync(Guid id);
        Task<IEnumerable<CouponUsage>> GetCouponUsageHistoryAsync(Guid couponId);
    }

    public class CouponService : ICouponService
    {
        private readonly AppDbContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public CouponService(AppDbContext context, IHttpContextAccessor httpContextAccessor)
        {
            _context = context;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task<CouponValidationResult> ValidateCouponAsync(string code, decimal totalAmount, Guid? customerId = null)
        {
            var coupon = await _context.Coupons
                .FirstOrDefaultAsync(c => c.Code.ToUpper() == code.ToUpper() && c.IsActive);

            if (coupon == null)
            {
                return new CouponValidationResult
                {
                    IsValid = false,
                    ErrorMessage = "Kupon bulunamadı veya aktif değil."
                };
            }

            // Geçerlilik tarihi kontrolü
            if (DateTime.UtcNow < coupon.ValidFrom || DateTime.UtcNow > coupon.ValidUntil)
            {
                return new CouponValidationResult
                {
                    IsValid = false,
                    ErrorMessage = "Kupon geçerlilik tarihi dışında."
                };
            }

            // Minimum tutar kontrolü
            if (totalAmount < coupon.MinimumAmount)
            {
                return new CouponValidationResult
                {
                    IsValid = false,
                    ErrorMessage = $"Minimum tutar: {coupon.MinimumAmount:C} gereklidir."
                };
            }

            // Kullanım limiti kontrolü
            if (coupon.UsageLimit > 0 && coupon.UsedCount >= coupon.UsageLimit)
            {
                return new CouponValidationResult
                {
                    IsValid = false,
                    ErrorMessage = "Kupon kullanım limiti dolmuş."
                };
            }

            // Müşteri kategorisi kısıtlaması
            if (coupon.CustomerCategoryRestriction.HasValue && customerId.HasValue)
            {
                var customer = await _context.Customers.FindAsync(customerId.Value);
                if (customer?.Category != coupon.CustomerCategoryRestriction.Value)
                {
                    return new CouponValidationResult
                    {
                        IsValid = false,
                        ErrorMessage = "Bu kupon sadece belirli müşteri kategorileri için geçerlidir."
                    };
                }
            }

            // Tek kullanımlık kupon kontrolü
            if (coupon.IsSingleUse && customerId.HasValue)
            {
                var hasUsed = await _context.CouponUsages
                    .AnyAsync(cu => cu.CouponId == coupon.Id && cu.CustomerId == customerId.Value);
                
                if (hasUsed)
                {
                    return new CouponValidationResult
                    {
                        IsValid = false,
                        ErrorMessage = "Bu kupon daha önce kullanılmış."
                    };
                }
            }

            // İndirim tutarını hesapla
            decimal discountAmount = CalculateDiscount(coupon, totalAmount);

            return new CouponValidationResult
            {
                IsValid = true,
                Coupon = coupon,
                DiscountAmount = discountAmount,
                Message = "Kupon geçerli."
            };
        }

        public async Task<CouponUsage> UseCouponAsync(string code, decimal discountAmount, Guid? customerId = null, Guid? invoiceId = null, Guid? orderId = null)
        {
            var coupon = await _context.Coupons
                .FirstOrDefaultAsync(c => c.Code.ToUpper() == code.ToUpper() && c.IsActive);

            if (coupon == null)
                throw new InvalidOperationException("Kupon bulunamadı.");

            // Kupon kullanımını kaydet
            var usage = new CouponUsage
            {
                CouponId = coupon.Id,
                CustomerId = customerId,
                InvoiceId = invoiceId,
                OrderId = orderId,
                DiscountAmount = discountAmount,
                UsedAt = DateTime.UtcNow,
                UsedBy = GetCurrentUserId(),
                SessionId = GetCurrentSessionId()
            };

            _context.CouponUsages.Add(usage);

            // Kupon kullanım sayısını artır
            coupon.UsedCount++;

            await _context.SaveChangesAsync();

            return usage;
        }

        public async Task<IEnumerable<Coupon>> GetActiveCouponsAsync()
        {
            return await _context.Coupons
                .Where(c => c.IsActive && c.ValidFrom <= DateTime.UtcNow && c.ValidUntil >= DateTime.UtcNow)
                .OrderBy(c => c.Name)
                .ToListAsync();
        }

        public async Task<Coupon> CreateCouponAsync(Coupon coupon, string createdBy)
        {
            coupon.CreatedBy = createdBy;
            coupon.CreatedAt = DateTime.UtcNow;
            coupon.IsActive = true;

            _context.Coupons.Add(coupon);
            await _context.SaveChangesAsync();

            return coupon;
        }

        public async Task<Coupon> UpdateCouponAsync(Guid id, Coupon coupon)
        {
            var existingCoupon = await _context.Coupons.FindAsync(id);
            if (existingCoupon == null)
                throw new InvalidOperationException("Kupon bulunamadı.");

            existingCoupon.Name = coupon.Name;
            existingCoupon.Description = coupon.Description;
            existingCoupon.DiscountType = coupon.DiscountType;
            existingCoupon.DiscountValue = coupon.DiscountValue;
            existingCoupon.MinimumAmount = coupon.MinimumAmount;
            existingCoupon.MaximumDiscount = coupon.MaximumDiscount;
            existingCoupon.ValidFrom = coupon.ValidFrom;
            existingCoupon.ValidUntil = coupon.ValidUntil;
            existingCoupon.UsageLimit = coupon.UsageLimit;
            existingCoupon.IsActive = coupon.IsActive;
            existingCoupon.IsSingleUse = coupon.IsSingleUse;
            existingCoupon.CustomerCategoryRestriction = coupon.CustomerCategoryRestriction;
            existingCoupon.ProductCategoryRestriction = coupon.ProductCategoryRestriction;
            existingCoupon.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return existingCoupon;
        }

        public async Task<bool> DeleteCouponAsync(Guid id)
        {
            var coupon = await _context.Coupons.FindAsync(id);
            if (coupon == null)
                return false;

            _context.Coupons.Remove(coupon);
            await _context.SaveChangesAsync();

            return true;
        }

        public async Task<IEnumerable<CouponUsage>> GetCouponUsageHistoryAsync(Guid couponId)
        {
            return await _context.CouponUsages
                .Where(cu => cu.CouponId == couponId)
                .Include(cu => cu.Customer)
                .Include(cu => cu.Invoice)
                .Include(cu => cu.Order)
                .OrderByDescending(cu => cu.UsedAt)
                .ToListAsync();
        }

        private decimal CalculateDiscount(Coupon coupon, decimal totalAmount)
        {
            decimal discount = 0;

            switch (coupon.DiscountType)
            {
                case DiscountType.Percentage:
                    discount = totalAmount * (coupon.DiscountValue / 100);
                    break;
                case DiscountType.FixedAmount:
                    discount = coupon.DiscountValue;
                    break;
                case DiscountType.BuyOneGetOne:
                    // BOGO mantığı burada implement edilebilir
                    discount = 0;
                    break;
                case DiscountType.FreeShipping:
                    // Kargo ücreti hesaplama mantığı
                    discount = 0;
                    break;
            }

            // Maksimum indirim kontrolü
            if (coupon.MaximumDiscount > 0 && discount > coupon.MaximumDiscount)
            {
                discount = coupon.MaximumDiscount;
            }

            return Math.Min(discount, totalAmount); // İndirim tutarı toplam tutarı aşamaz
        }

        private string GetCurrentUserId()
        {
            return _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "system";
        }

        private string GetCurrentSessionId()
        {
            return _httpContextAccessor.HttpContext?.Session?.Id ?? Guid.NewGuid().ToString();
        }
    }

    public class CouponValidationResult
    {
        public bool IsValid { get; set; }
        public string ErrorMessage { get; set; }
        public string Message { get; set; }
        public Coupon Coupon { get; set; }
        public decimal DiscountAmount { get; set; }
    }
} 
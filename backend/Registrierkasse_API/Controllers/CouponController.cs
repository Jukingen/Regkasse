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
    public class CouponController : ControllerBase
    {
        private readonly ICouponService _couponService;

        public CouponController(ICouponService couponService)
        {
            _couponService = couponService;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Coupon>>> GetActiveCoupons()
        {
            try
            {
                var coupons = await _couponService.GetActiveCouponsAsync();
                return Ok(coupons);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Failed to retrieve coupons", message = ex.Message });
            }
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<Coupon>> GetCoupon(Guid id)
        {
            try
            {
                var coupons = await _couponService.GetActiveCouponsAsync();
                var coupon = coupons.FirstOrDefault(c => c.Id == id);
                
                if (coupon == null)
                    return NotFound(new { error = "Coupon not found" });

                return Ok(coupon);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Failed to retrieve coupon", message = ex.Message });
            }
        }

        [HttpPost("validate")]
        public async Task<ActionResult<CouponValidationResult>> ValidateCoupon([FromBody] CouponValidationRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.Code))
                    return BadRequest(new { error = "Coupon code is required" });

                if (request.TotalAmount <= 0)
                    return BadRequest(new { error = "Total amount must be greater than 0" });

                var result = await _couponService.ValidateCouponAsync(
                    request.Code, 
                    request.TotalAmount, 
                    request.CustomerId);

                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Failed to validate coupon", message = ex.Message });
            }
        }

        [HttpPost("use")]
        public async Task<ActionResult<CouponUsage>> UseCoupon([FromBody] CouponUsageRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.Code))
                    return BadRequest(new { error = "Coupon code is required" });

                if (request.DiscountAmount <= 0)
                    return BadRequest(new { error = "Discount amount must be greater than 0" });

                var usage = await _couponService.UseCouponAsync(
                    request.Code,
                    request.DiscountAmount,
                    request.CustomerId,
                    request.InvoiceId,
                    request.OrderId);

                return Ok(usage);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Failed to use coupon", message = ex.Message });
            }
        }

        [HttpPost]
        [Authorize(Roles = "Admin,Manager")]
        public async Task<ActionResult<Coupon>> CreateCoupon([FromBody] Coupon coupon)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(coupon.Code))
                    return BadRequest(new { error = "Coupon code is required" });

                if (string.IsNullOrWhiteSpace(coupon.Name))
                    return BadRequest(new { error = "Coupon name is required" });

                if (coupon.DiscountValue <= 0)
                    return BadRequest(new { error = "Discount value must be greater than 0" });

                var createdBy = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "system";
                var createdCoupon = await _couponService.CreateCouponAsync(coupon, createdBy);

                return CreatedAtAction(nameof(GetCoupon), new { id = createdCoupon.Id }, createdCoupon);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Failed to create coupon", message = ex.Message });
            }
        }

        [HttpPut("{id}")]
        [Authorize(Roles = "Admin,Manager")]
        public async Task<ActionResult<Coupon>> UpdateCoupon(Guid id, [FromBody] Coupon coupon)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(coupon.Name))
                    return BadRequest(new { error = "Coupon name is required" });

                if (coupon.DiscountValue <= 0)
                    return BadRequest(new { error = "Discount value must be greater than 0" });

                var updatedCoupon = await _couponService.UpdateCouponAsync(id, coupon);
                return Ok(updatedCoupon);
            }
            catch (InvalidOperationException ex)
            {
                return NotFound(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Failed to update coupon", message = ex.Message });
            }
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin,Manager")]
        public async Task<ActionResult> DeleteCoupon(Guid id)
        {
            try
            {
                var success = await _couponService.DeleteCouponAsync(id);
                
                if (!success)
                    return NotFound(new { error = "Coupon not found" });

                return NoContent();
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Failed to delete coupon", message = ex.Message });
            }
        }

        [HttpGet("{id}/usage-history")]
        [Authorize(Roles = "Admin,Manager")]
        public async Task<ActionResult<IEnumerable<CouponUsage>>> GetCouponUsageHistory(Guid id)
        {
            try
            {
                var usageHistory = await _couponService.GetCouponUsageHistoryAsync(id);
                return Ok(usageHistory);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Failed to retrieve coupon usage history", message = ex.Message });
            }
        }
    }

    public class CouponValidationRequest
    {
        public string Code { get; set; }
        public decimal TotalAmount { get; set; }
        public Guid? CustomerId { get; set; }
    }

    public class CouponUsageRequest
    {
        public string Code { get; set; }
        public decimal DiscountAmount { get; set; }
        public Guid? CustomerId { get; set; }
        public Guid? InvoiceId { get; set; }
        public Guid? OrderId { get; set; }
    }
} 
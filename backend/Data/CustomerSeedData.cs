using KasseAPI_Final.Constants;
using KasseAPI_Final.Models;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Data
{
    public static class CustomerSeedData
    {
        public static readonly Guid GUEST_CUSTOMER_ID = WalkInCustomerConstants.GuestCustomerId;
        
        public static async Task SeedGuestCustomerAsync(AppDbContext context)
        {
            // Check if guest customer already exists
            var existingGuest = await context.Customers
                .FirstOrDefaultAsync(c => c.Id == WalkInCustomerConstants.GuestCustomerId);
            
            if (existingGuest != null)
            {
                if (!existingGuest.IsSystem)
                {
                    existingGuest.IsSystem = true;
                    existingGuest.UpdatedAt = DateTime.UtcNow;
                    await context.SaveChangesAsync();
                }
                return;
            }
            
            var guestCustomer = new Customer
            {
                Id = WalkInCustomerConstants.GuestCustomerId,
                Name = "Walk-in Customer",
                CustomerNumber = "GUEST-000",
                Email = "walkin@system.local",
                IsSystem = true,
                Phone = "",
                Address = "",
                TaxNumber = "",
                Category = CustomerCategory.Regular,
                LoyaltyPoints = 0,
                TotalSpent = 0,
                VisitCount = 0,
                Notes = "Default guest customer for walk-in sales",
                IsVip = false,
                DiscountPercentage = 0,
                PreferredPaymentMethod = CustomerPaymentMethod.Cash,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            
            await context.Customers.AddAsync(guestCustomer);
            await context.SaveChangesAsync();
        }
    }
}

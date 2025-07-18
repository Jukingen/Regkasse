using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Registrierkasse_API.Models;
using Registrierkasse_API.Models;

namespace Registrierkasse_API.Data
{
    public class AppDbContext : IdentityDbContext<ApplicationUser>
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        // DbSet properties
        public DbSet<Product> Products { get; set; }
        public DbSet<Category> Categories { get; set; }
        public DbSet<Customer> Customers { get; set; }
        public DbSet<Receipt> Receipts { get; set; }
        public DbSet<ReceiptItem> ReceiptItems { get; set; }
        public DbSet<Transaction> Transactions { get; set; }
        public DbSet<AuditLog> AuditLogs { get; set; }
        public DbSet<CashRegister> CashRegisters { get; set; }
        public DbSet<CashRegisterTransaction> CashRegisterTransactions { get; set; }
        public DbSet<DailyReport> DailyReports { get; set; }
        public DbSet<Invoice> Invoices { get; set; }
        public DbSet<InvoiceTemplate> InvoiceTemplates { get; set; }
        public DbSet<Order> Orders { get; set; }
        public DbSet<OrderItem> OrderItems { get; set; }
        public DbSet<Inventory> Inventories { get; set; }
        public DbSet<InventoryTransaction> InventoryTransactions { get; set; }
        public DbSet<TaxSummary> TaxSummaries { get; set; }
        public DbSet<PaymentDetails> PaymentDetailsSet { get; set; }
        public DbSet<CustomerDetails> CustomerDetailsSet { get; set; }
        public DbSet<SystemConfiguration> SystemConfigurations { get; set; }
        public DbSet<UserSession> UserSessions { get; set; }
        public DbSet<UserSettings> UserSettings { get; set; }
        public DbSet<CompanySettings> CompanySettings { get; set; }
        public DbSet<InvoiceItem> InvoiceItems { get; set; }
        public DbSet<Hardware> Hardware { get; set; }
        public DbSet<FinanceOnline> FinanceOnline { get; set; }
        public DbSet<Discount> Discounts { get; set; }
        public DbSet<Table> Tables { get; set; }
        public DbSet<Coupon> Coupons { get; set; }
        public DbSet<CouponUsage> CouponUsages { get; set; }
        public DbSet<CustomerDiscount> CustomerDiscounts { get; set; }
        public DbSet<Cart> Carts { get; set; }
        public DbSet<CartItem> CartItems { get; set; }

        // Rol sistemi DbSet'leri
        public DbSet<Role> Roles { get; set; }
        public DbSet<Permission> Permissions { get; set; }
        public DbSet<UserRole> UserRoles { get; set; }
        public DbSet<RolePermission> RolePermissions { get; set; }
        public DbSet<DemoUserLog> DemoUserLogs { get; set; }
        public DbSet<OperationLog> OperationLogs { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // Product configuration
            builder.Entity<Product>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
                entity.Property(e => e.Description).HasMaxLength(500);
                entity.Property(e => e.Barcode).HasMaxLength(50);
                entity.Property(e => e.Price).HasColumnType("decimal(18,2)");
                entity.Property(e => e.Cost).HasColumnType("decimal(18,2)");
                entity.Property(e => e.TaxRate).HasColumnType("decimal(5,2)");
                
                entity.HasIndex(e => e.Barcode).IsUnique();
                entity.HasIndex(e => e.Name);
                entity.HasIndex(e => e.CategoryId);
            });

            // Category configuration
            builder.Entity<Category>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
                entity.Property(e => e.Description).HasMaxLength(300);
                
                entity.HasIndex(e => e.Name).IsUnique();
            });

            // Customer configuration
            builder.Entity<Customer>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
                entity.Property(e => e.Email).HasMaxLength(100);
                entity.Property(e => e.Phone).HasMaxLength(20);
                entity.Property(e => e.Address).HasMaxLength(200);
                entity.Property(e => e.TaxNumber).HasMaxLength(20);
                entity.Property(e => e.Category).HasConversion<string>();
                entity.Property(e => e.LoyaltyPoints).HasDefaultValue(0);
                entity.Property(e => e.TotalSpent).HasColumnType("decimal(18,2)").HasDefaultValue(0);
                entity.Property(e => e.VisitCount).HasDefaultValue(0);
                entity.Property(e => e.DiscountPercentage).HasColumnType("decimal(5,2)").HasDefaultValue(0);
                entity.Property(e => e.PreferredPaymentMethod).HasConversion<string>();
                
                entity.HasIndex(e => e.Email);
                entity.HasIndex(e => e.Phone);
                entity.HasIndex(e => e.TaxNumber);
                entity.HasIndex(e => e.Category);
            });

            // Receipt configuration
            builder.Entity<Receipt>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.ReceiptNumber).IsRequired().HasMaxLength(50);
                entity.Property(e => e.TotalAmount).HasColumnType("decimal(18,2)");
                entity.Property(e => e.TseSignature).HasMaxLength(500);
                entity.Property(e => e.KassenId).HasMaxLength(50);
                
                entity.HasIndex(e => e.ReceiptNumber).IsUnique();
                entity.HasIndex(e => e.TseSignature);
                entity.HasIndex(e => e.CreatedAt);
            });

            // ReceiptItem configuration
            builder.Entity<ReceiptItem>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.ProductName).IsRequired().HasMaxLength(200);
                entity.Property(e => e.UnitPrice).HasColumnType("decimal(18,2)");
                entity.Property(e => e.TotalAmount).HasColumnType("decimal(18,2)");
                entity.Property(e => e.TaxAmount).HasColumnType("decimal(18,2)");
                entity.Property(e => e.TaxType).HasMaxLength(20);
                
                entity.HasOne(e => e.Receipt)
                    .WithMany(e => e.Items)
                    .HasForeignKey(e => e.ReceiptId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // Transaction configuration
            builder.Entity<Transaction>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.TransactionNumber).IsRequired().HasMaxLength(50);
                entity.Property(e => e.Amount).HasColumnType("decimal(18,2)");
                entity.Property(e => e.PaymentMethod).HasMaxLength(20);
                entity.Property(e => e.Reference).HasMaxLength(100);
                
                entity.HasIndex(e => e.TransactionNumber).IsUnique();
                entity.HasIndex(e => e.CreatedAt);
                entity.HasIndex(e => e.PaymentMethod);
            });

            // AuditLog configuration
            builder.Entity<AuditLog>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Action).IsRequired().HasMaxLength(50);
                entity.Property(e => e.EntityName).IsRequired().HasMaxLength(100);
                entity.Property(e => e.EntityId).IsRequired().HasMaxLength(50);
                entity.Property(e => e.OldValues).HasColumnType("jsonb");
                entity.Property(e => e.NewValues).HasColumnType("jsonb");
                entity.Property(e => e.UserId).HasMaxLength(450);
                entity.Property(e => e.IpAddress).HasMaxLength(45);
                entity.Property(e => e.UserAgent).HasMaxLength(500);
                
                entity.HasIndex(e => e.CreatedAt);
                entity.HasIndex(e => e.Action);
                entity.HasIndex(e => e.EntityName);
                entity.HasIndex(e => e.UserId);
            });

            // CashRegister configuration
            builder.Entity<CashRegister>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.RegisterNumber).IsRequired().HasMaxLength(20);
                entity.Property(e => e.Location).HasMaxLength(200);
                entity.Property(e => e.TseId).HasMaxLength(50);
                entity.Property(e => e.KassenId).HasMaxLength(50);
                entity.Property(e => e.Status).HasMaxLength(20);
                
                entity.HasIndex(e => e.TseId).IsUnique();
                entity.HasIndex(e => e.KassenId).IsUnique();
            });

            // DailyReport configuration
            builder.Entity<DailyReport>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.TotalSales).HasColumnType("decimal(18,2)");
                entity.Property(e => e.CashPayments).HasColumnType("decimal(18,2)");
                entity.Property(e => e.CardPayments).HasColumnType("decimal(18,2)");
                entity.Property(e => e.TseSignature).HasMaxLength(500);
                entity.Property(e => e.KassenId).HasMaxLength(50);
                
                entity.HasIndex(e => e.ReportDate);
                entity.HasIndex(e => e.TseSignature);
            });

            // Invoice configuration
            builder.Entity<Invoice>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.InvoiceNumber).IsRequired().HasMaxLength(50);
                entity.Property(e => e.CustomerName).HasMaxLength(200);
                entity.Property(e => e.CustomerEmail).HasMaxLength(100);
                entity.Property(e => e.CustomerPhone).HasMaxLength(20);
                entity.Property(e => e.CustomerAddress).HasMaxLength(500);
                entity.Property(e => e.CustomerTaxNumber).HasMaxLength(50);
                entity.Property(e => e.Subtotal).HasColumnType("decimal(18,2)");
                entity.Property(e => e.TaxAmount).HasColumnType("decimal(18,2)");
                entity.Property(e => e.TotalAmount).HasColumnType("decimal(18,2)");
                entity.Property(e => e.PaidAmount).HasColumnType("decimal(18,2)");
                entity.Property(e => e.RemainingAmount).HasColumnType("decimal(18,2)");
                entity.Property(e => e.PaymentMethod).HasMaxLength(20);
                entity.Property(e => e.PaymentReference).HasMaxLength(100);
                entity.Property(e => e.CancelledReason).HasMaxLength(500);
                entity.Property(e => e.TseSignature).HasMaxLength(500);
                entity.Property(e => e.KassenId).HasMaxLength(50);
                entity.Property(e => e.CompanyName).HasMaxLength(200);
                entity.Property(e => e.CompanyAddress).HasMaxLength(500);
                entity.Property(e => e.CompanyPhone).HasMaxLength(20);
                entity.Property(e => e.CompanyEmail).HasMaxLength(100);
                entity.Property(e => e.CompanyTaxNumber).HasMaxLength(50);
                entity.Property(e => e.TermsAndConditions).HasColumnType("text");
                entity.Property(e => e.Notes).HasColumnType("text");
                entity.Property(e => e.InvoiceItems).HasColumnType("jsonb");
                entity.Property(e => e.TaxDetails).HasColumnType("jsonb");
                
                entity.HasIndex(e => e.InvoiceNumber).IsUnique();
                entity.HasIndex(e => e.ReceiptNumber).IsUnique();
                entity.HasIndex(e => e.CustomerEmail);
                entity.HasIndex(e => e.Status);
                entity.HasIndex(e => e.InvoiceDate);
                entity.HasIndex(e => e.DueDate);
                entity.HasIndex(e => e.TseSignature);
                entity.HasOne(e => e.Customer)
                    .WithMany()
                    .HasForeignKey(e => e.CustomerId)
                    .OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(e => e.CreatedBy)
                    .WithMany()
                    .HasForeignKey(e => e.CreatedById)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // InvoiceTemplate configuration
            builder.Entity<InvoiceTemplate>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
                entity.Property(e => e.CompanyName).HasMaxLength(200);
                entity.Property(e => e.CompanyTaxNumber).HasMaxLength(50);
                entity.Property(e => e.CompanyAddress).HasMaxLength(500);
                entity.Property(e => e.PrimaryColor).HasMaxLength(7);
                entity.Property(e => e.SecondaryColor).HasMaxLength(7);
                entity.Property(e => e.FontFamily).HasMaxLength(50);
                entity.Property(e => e.InvoiceTitle).HasMaxLength(100);
                entity.Property(e => e.InvoiceNumberLabel).HasMaxLength(50);
                entity.Property(e => e.InvoiceDateLabel).HasMaxLength(50);
                entity.Property(e => e.DueDateLabel).HasMaxLength(50);
                entity.Property(e => e.CustomerSectionTitle).HasMaxLength(100);
                entity.Property(e => e.CustomerNameLabel).HasMaxLength(50);
                entity.Property(e => e.CustomerEmailLabel).HasMaxLength(50);
                entity.Property(e => e.CustomerPhoneLabel).HasMaxLength(50);
                entity.Property(e => e.CustomerAddressLabel).HasMaxLength(50);
                entity.Property(e => e.CustomerTaxNumberLabel).HasMaxLength(50);
                entity.Property(e => e.ItemHeader).HasMaxLength(50);
                entity.Property(e => e.DescriptionHeader).HasMaxLength(50);
                entity.Property(e => e.QuantityHeader).HasMaxLength(50);
                entity.Property(e => e.UnitPriceHeader).HasMaxLength(50);
                entity.Property(e => e.TaxHeader).HasMaxLength(50);
                entity.Property(e => e.TotalHeader).HasMaxLength(50);
                entity.Property(e => e.SubtotalLabel).HasMaxLength(50);
                entity.Property(e => e.TaxLabel).HasMaxLength(50);
                entity.Property(e => e.TotalLabel).HasMaxLength(50);
                entity.Property(e => e.PaidLabel).HasMaxLength(50);
                entity.Property(e => e.RemainingLabel).HasMaxLength(50);
                entity.Property(e => e.PaymentSectionTitle).HasMaxLength(100);
                entity.Property(e => e.PaymentMethodLabel).HasMaxLength(50);
                entity.Property(e => e.PaymentReferenceLabel).HasMaxLength(50);
                entity.Property(e => e.PaymentDateLabel).HasMaxLength(50);
                entity.Property(e => e.TseSignatureLabel).HasMaxLength(50);
                entity.Property(e => e.KassenIdLabel).HasMaxLength(50);
                entity.Property(e => e.TseTimestampLabel).HasMaxLength(50);
                entity.Property(e => e.PageSize).HasMaxLength(10);
                entity.Property(e => e.FooterText).HasColumnType("text");
                
                entity.HasIndex(e => e.Name).IsUnique();
            });

            builder.Entity<Product>()
                .HasOne(p => p.Inventory)
                .WithOne(i => i.Product)
                .HasForeignKey<Inventory>(i => i.ProductId)
                .OnDelete(DeleteBehavior.Cascade);

            // Coupon configuration
            builder.Entity<Coupon>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Code).IsRequired().HasMaxLength(20);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
                entity.Property(e => e.Description).HasMaxLength(500);
                entity.Property(e => e.DiscountType).HasConversion<string>();
                entity.Property(e => e.DiscountValue).HasColumnType("decimal(18,2)");
                entity.Property(e => e.MinimumAmount).HasColumnType("decimal(18,2)").HasDefaultValue(0);
                entity.Property(e => e.MaximumDiscount).HasColumnType("decimal(18,2)").HasDefaultValue(0);
                entity.Property(e => e.UsageLimit).HasDefaultValue(0);
                entity.Property(e => e.UsedCount).HasDefaultValue(0);
                entity.Property(e => e.IsActive).HasDefaultValue(true);
                entity.Property(e => e.IsSingleUse).HasDefaultValue(false);
                entity.Property(e => e.CustomerCategoryRestriction).HasConversion<string>();
                entity.Property(e => e.ProductCategoryRestriction).HasMaxLength(100);
                entity.Property(e => e.CreatedBy).HasMaxLength(450);
                
                entity.HasIndex(e => e.Code).IsUnique();
                entity.HasIndex(e => e.IsActive);
                entity.HasIndex(e => e.ValidFrom);
                entity.HasIndex(e => e.ValidUntil);
            });

            // CouponUsage configuration
            builder.Entity<CouponUsage>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.CouponId).IsRequired();
                entity.Property(e => e.DiscountAmount).HasColumnType("decimal(18,2)");
                entity.Property(e => e.UsedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
                entity.Property(e => e.UsedBy).HasMaxLength(450);
                entity.Property(e => e.SessionId).HasMaxLength(100);
                
                entity.HasOne(e => e.Coupon)
                    .WithMany(e => e.CouponUsages)
                    .HasForeignKey(e => e.CouponId)
                    .OnDelete(DeleteBehavior.Restrict);
                
                entity.HasOne(e => e.Customer)
                    .WithMany()
                    .HasForeignKey(e => e.CustomerId)
                    .OnDelete(DeleteBehavior.Restrict);
                
                entity.HasOne(e => e.Invoice)
                    .WithMany()
                    .HasForeignKey(e => e.InvoiceId)
                    .OnDelete(DeleteBehavior.Restrict);
                
                entity.HasOne(e => e.Order)
                    .WithMany()
                    .HasForeignKey(e => e.OrderId)
                    .OnDelete(DeleteBehavior.Restrict);
                
                entity.HasIndex(e => e.UsedAt);
                entity.HasIndex(e => e.CouponId);
                entity.HasIndex(e => e.CustomerId);
            });

            // CustomerDiscount configuration
            builder.Entity<CustomerDiscount>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.CustomerId).IsRequired();
                entity.Property(e => e.DiscountType).HasConversion<string>();
                entity.Property(e => e.DiscountValue).HasColumnType("decimal(18,2)");
                entity.Property(e => e.Description).HasMaxLength(500);
                entity.Property(e => e.UsageLimit).HasDefaultValue(0);
                entity.Property(e => e.UsedCount).HasDefaultValue(0);
                entity.Property(e => e.IsActive).HasDefaultValue(true);
                entity.Property(e => e.ProductCategoryRestriction).HasMaxLength(100);
                entity.Property(e => e.MinimumAmount).HasColumnType("decimal(18,2)").HasDefaultValue(0);
                entity.Property(e => e.CreatedBy).HasMaxLength(450);
                
                entity.HasOne(e => e.Customer)
                    .WithMany(e => e.CustomerDiscounts)
                    .HasForeignKey(e => e.CustomerId)
                    .OnDelete(DeleteBehavior.Cascade);
                
                entity.HasIndex(e => e.CustomerId);
                entity.HasIndex(e => e.IsActive);
                entity.HasIndex(e => e.ValidFrom);
                entity.HasIndex(e => e.ValidUntil);
            });

            // Cart configuration
            builder.Entity<Cart>(entity =>
            {
                entity.HasKey(e => e.CartId);
                entity.Property(e => e.CartId).IsRequired().HasMaxLength(50);
                entity.Property(e => e.TableNumber).HasMaxLength(50);
                entity.Property(e => e.WaiterName).HasMaxLength(50);
                entity.Property(e => e.Subtotal).HasColumnType("decimal(18,2)");
                entity.Property(e => e.TaxAmount).HasColumnType("decimal(18,2)");
                entity.Property(e => e.DiscountAmount).HasColumnType("decimal(18,2)");
                entity.Property(e => e.TotalAmount).HasColumnType("decimal(18,2)");
                entity.Property(e => e.ServiceChargeAmount).HasColumnType("decimal(18,2)").HasDefaultValue(0);
                entity.Property(e => e.TipAmount).HasColumnType("decimal(18,2)").HasDefaultValue(0);
                entity.Property(e => e.SplitCount).HasDefaultValue(1);
                entity.Property(e => e.SplitAmount).HasColumnType("decimal(18,2)").HasDefaultValue(0);
                entity.Property(e => e.PaymentMethods).HasMaxLength(500);
                
                entity.Property(e => e.Notes).HasMaxLength(500);
                entity.Property(e => e.Status).HasConversion<string>();
                entity.Property(e => e.ExpiresAt);
                
                // Table ilişkisi eklendi
                entity.HasOne(e => e.Table)
                    .WithMany(e => e.CartHistory)
                    .HasForeignKey(e => e.TableId)
                    .OnDelete(DeleteBehavior.SetNull);
                
                entity.HasIndex(e => e.CartId).IsUnique();
                entity.HasIndex(e => e.Status);
                entity.HasIndex(e => e.ExpiresAt);
                entity.HasIndex(e => e.CreatedAt);
                entity.HasIndex(e => e.TableId);
                
                entity.HasOne(e => e.Customer)
                    .WithMany()
                    .HasForeignKey(e => e.CustomerId)
                    .OnDelete(DeleteBehavior.Restrict);
                
                entity.HasOne(e => e.User)
                    .WithMany()
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Restrict);
                
                entity.HasOne(e => e.CashRegister)
                    .WithMany()
                    .HasForeignKey(e => e.CashRegisterId)
                    .OnDelete(DeleteBehavior.Restrict);
                
                entity.HasOne(e => e.AppliedCoupon)
                    .WithMany()
                    .HasForeignKey(e => e.AppliedCouponId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // CartItem configuration
            builder.Entity<CartItem>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.CartId).IsRequired().HasMaxLength(50);
                entity.Property(e => e.ProductId).IsRequired();
                entity.Property(e => e.ProductName).IsRequired().HasMaxLength(200);
                entity.Property(e => e.Quantity).IsRequired();
                entity.Property(e => e.UnitPrice).HasColumnType("decimal(18,2)");
                entity.Property(e => e.TaxRate).HasColumnType("decimal(5,2)");
                entity.Property(e => e.DiscountAmount).HasColumnType("decimal(18,2)");
                entity.Property(e => e.TaxAmount).HasColumnType("decimal(18,2)");
                entity.Property(e => e.TotalAmount).HasColumnType("decimal(18,2)");
                entity.Property(e => e.Notes).HasMaxLength(200);
                entity.Property(e => e.IsModified).HasDefaultValue(false);
                entity.Property(e => e.ModifiedAt);
                entity.Property(e => e.OriginalUnitPrice).HasColumnType("decimal(18,2)");
                entity.Property(e => e.OriginalQuantity).HasDefaultValue(0);
                
                entity.HasOne(e => e.Cart)
                    .WithMany(e => e.Items)
                    .HasForeignKey(e => e.CartId)
                    .OnDelete(DeleteBehavior.Cascade);
                
                entity.HasOne(e => e.Product)
                    .WithMany()
                    .HasForeignKey(e => e.ProductId)
                    .OnDelete(DeleteBehavior.Restrict);
                
                entity.HasIndex(e => e.CartId);
                entity.HasIndex(e => e.ProductId);
            });

            // Table configuration - EKSİK OLAN KONFİGÜRASYON
            builder.Entity<Table>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Number).IsRequired();
                entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
                entity.Property(e => e.Capacity).IsRequired();
                entity.Property(e => e.Location).IsRequired().HasMaxLength(100);
                entity.Property(e => e.Status).HasMaxLength(20).HasDefaultValue("empty");
                entity.Property(e => e.CustomerName).HasMaxLength(100);
                entity.Property(e => e.StartTime);
                entity.Property(e => e.LastOrderTime);
                entity.Property(e => e.TotalPaid).HasColumnType("decimal(18,2)").HasDefaultValue(0);
                entity.Property(e => e.CurrentTotal).HasColumnType("decimal(18,2)").HasDefaultValue(0);
                
                // Yeni eklenen alanlar
                entity.Property(e => e.ServiceChargePercentage).HasColumnType("decimal(5,2)").HasDefaultValue(0);
                entity.Property(e => e.TipPercentage).HasColumnType("decimal(5,2)").HasDefaultValue(0);
                entity.Property(e => e.SplitBillEnabled).HasDefaultValue(true);
                entity.Property(e => e.MaxSplitCount).HasDefaultValue(4);
                entity.Property(e => e.Notes).HasMaxLength(500);
                
                // Table-Cart ilişkisi
                entity.HasOne(e => e.CurrentCart)
                    .WithMany()
                    .HasForeignKey(e => e.CurrentCartId)
                    .OnDelete(DeleteBehavior.SetNull);
                
                entity.HasOne(e => e.CurrentOrder)
                    .WithMany()
                    .HasForeignKey(e => e.CurrentOrderId)
                    .OnDelete(DeleteBehavior.SetNull);
                
                entity.HasIndex(e => e.Number).IsUnique();
                entity.HasIndex(e => e.Status);
                entity.HasIndex(e => e.CurrentCartId);
            });
        }
    }
}

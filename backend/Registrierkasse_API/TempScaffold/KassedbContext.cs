using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace Registrierkasse_API.TempScaffold;

public partial class KassedbContext : DbContext
{
    public KassedbContext()
    {
    }

    public KassedbContext(DbContextOptions<KassedbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<AspNetRole> AspNetRoles { get; set; }

    public virtual DbSet<AspNetRoleClaim> AspNetRoleClaims { get; set; }

    public virtual DbSet<AspNetUser> AspNetUsers { get; set; }

    public virtual DbSet<AspNetUserClaim> AspNetUserClaims { get; set; }

    public virtual DbSet<AspNetUserLogin> AspNetUserLogins { get; set; }

    public virtual DbSet<AspNetUserToken> AspNetUserTokens { get; set; }

    public virtual DbSet<AuditLog> AuditLogs { get; set; }

    public virtual DbSet<Cart> Carts { get; set; }

    public virtual DbSet<CartItem> CartItems { get; set; }

    public virtual DbSet<CashRegister> CashRegisters { get; set; }

    public virtual DbSet<CashRegisterTransaction> CashRegisterTransactions { get; set; }

    public virtual DbSet<Category> Categories { get; set; }

    public virtual DbSet<CompanySetting> CompanySettings { get; set; }

    public virtual DbSet<Coupon> Coupons { get; set; }

    public virtual DbSet<CouponUsage> CouponUsages { get; set; }

    public virtual DbSet<Customer> Customers { get; set; }

    public virtual DbSet<CustomerDetail> CustomerDetails { get; set; }

    public virtual DbSet<CustomerDiscount> CustomerDiscounts { get; set; }

    public virtual DbSet<DailyReport> DailyReports { get; set; }

    public virtual DbSet<Discount> Discounts { get; set; }

    public virtual DbSet<FinanceOnline> FinanceOnlines { get; set; }

    public virtual DbSet<Hardware> Hardwares { get; set; }

    public virtual DbSet<Inventory> Inventories { get; set; }

    public virtual DbSet<InventoryTransaction> InventoryTransactions { get; set; }

    public virtual DbSet<Invoice> Invoices { get; set; }

    public virtual DbSet<InvoiceHistory> InvoiceHistories { get; set; }

    public virtual DbSet<InvoiceItem> InvoiceItems { get; set; }

    public virtual DbSet<InvoiceTemplate> InvoiceTemplates { get; set; }

    public virtual DbSet<Order> Orders { get; set; }

    public virtual DbSet<OrderItem> OrderItems { get; set; }

    public virtual DbSet<PaymentDetail> PaymentDetails { get; set; }

    public virtual DbSet<Product> Products { get; set; }

    public virtual DbSet<Receipt> Receipts { get; set; }

    public virtual DbSet<ReceiptItem> ReceiptItems { get; set; }

    public virtual DbSet<SystemConfiguration> SystemConfigurations { get; set; }

    public virtual DbSet<Table> Tables { get; set; }

    public virtual DbSet<TableReservation> TableReservations { get; set; }

    public virtual DbSet<TaxSummary> TaxSummaries { get; set; }

    public virtual DbSet<Transaction> Transactions { get; set; }

    public virtual DbSet<UserSession> UserSessions { get; set; }

    public virtual DbSet<UserSetting> UserSettings { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
#warning To protect potentially sensitive information in your connection string, you should move it out of source code. You can avoid scaffolding the connection string by using the Name= syntax to read it from configuration - see https://go.microsoft.com/fwlink/?linkid=2131148. For more guidance on storing connection strings, see https://go.microsoft.com/fwlink/?LinkId=723263.
        => optionsBuilder.UseNpgsql("Host=localhost;Database=kassedb;Username=postgres;Password=Juke1034#;Port=5432");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AspNetRole>(entity =>
        {
            entity.HasIndex(e => e.NormalizedName, "RoleNameIndex").IsUnique();

            entity.Property(e => e.Name).HasMaxLength(256);
            entity.Property(e => e.NormalizedName).HasMaxLength(256);
        });

        modelBuilder.Entity<AspNetRoleClaim>(entity =>
        {
            entity.HasIndex(e => e.RoleId, "IX_AspNetRoleClaims_RoleId");

            entity.HasOne(d => d.Role).WithMany(p => p.AspNetRoleClaims).HasForeignKey(d => d.RoleId);
        });

        modelBuilder.Entity<AspNetUser>(entity =>
        {
            entity.HasIndex(e => e.NormalizedEmail, "EmailIndex");

            entity.HasIndex(e => e.NormalizedUserName, "UserNameIndex").IsUnique();

            entity.Property(e => e.Email).HasMaxLength(256);
            entity.Property(e => e.EmployeeNumber)
                .HasMaxLength(20)
                .HasColumnName("employee_number");
            entity.Property(e => e.FirstName)
                .HasMaxLength(50)
                .HasColumnName("first_name");
            entity.Property(e => e.IsActive).HasColumnName("is_active");
            entity.Property(e => e.LastLogin).HasColumnName("last_login");
            entity.Property(e => e.LastName)
                .HasMaxLength(50)
                .HasColumnName("last_name");
            entity.Property(e => e.NormalizedEmail).HasMaxLength(256);
            entity.Property(e => e.NormalizedUserName).HasMaxLength(256);
            entity.Property(e => e.Notes)
                .HasMaxLength(500)
                .HasColumnName("notes");
            entity.Property(e => e.Role)
                .HasMaxLength(20)
                .HasColumnName("role");
            entity.Property(e => e.TaxNumber)
                .HasMaxLength(20)
                .HasColumnName("tax_number");
            entity.Property(e => e.UserName).HasMaxLength(256);

            entity.HasMany(d => d.Roles).WithMany(p => p.Users)
                .UsingEntity<Dictionary<string, object>>(
                    "AspNetUserRole",
                    r => r.HasOne<AspNetRole>().WithMany().HasForeignKey("RoleId"),
                    l => l.HasOne<AspNetUser>().WithMany().HasForeignKey("UserId"),
                    j =>
                    {
                        j.HasKey("UserId", "RoleId");
                        j.ToTable("AspNetUserRoles");
                        j.HasIndex(new[] { "RoleId" }, "IX_AspNetUserRoles_RoleId");
                    });
        });

        modelBuilder.Entity<AspNetUserClaim>(entity =>
        {
            entity.HasIndex(e => e.UserId, "IX_AspNetUserClaims_UserId");

            entity.HasOne(d => d.User).WithMany(p => p.AspNetUserClaims).HasForeignKey(d => d.UserId);
        });

        modelBuilder.Entity<AspNetUserLogin>(entity =>
        {
            entity.HasKey(e => new { e.LoginProvider, e.ProviderKey });

            entity.HasIndex(e => e.UserId, "IX_AspNetUserLogins_UserId");

            entity.HasOne(d => d.User).WithMany(p => p.AspNetUserLogins).HasForeignKey(d => d.UserId);
        });

        modelBuilder.Entity<AspNetUserToken>(entity =>
        {
            entity.HasKey(e => new { e.UserId, e.LoginProvider, e.Name });

            entity.HasOne(d => d.User).WithMany(p => p.AspNetUserTokens).HasForeignKey(d => d.UserId);
        });

        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.HasIndex(e => e.Action, "IX_AuditLogs_Action");

            entity.HasIndex(e => e.EntityName, "IX_AuditLogs_EntityName");

            entity.HasIndex(e => e.UserId, "IX_AuditLogs_UserId");

            entity.HasIndex(e => e.CreatedAt, "IX_AuditLogs_created_at");

            entity.Property(e => e.Id)
                .ValueGeneratedNever()
                .HasColumnName("id");
            entity.Property(e => e.Action).HasMaxLength(50);
            entity.Property(e => e.AdditionalData).HasColumnType("jsonb");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.CreatedBy)
                .HasMaxLength(450)
                .HasColumnName("created_by");
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.Property(e => e.EntityId).HasMaxLength(50);
            entity.Property(e => e.EntityName).HasMaxLength(100);
            entity.Property(e => e.EntityType).HasMaxLength(100);
            entity.Property(e => e.ErrorMessage).HasMaxLength(500);
            entity.Property(e => e.IpAddress).HasMaxLength(45);
            entity.Property(e => e.IsActive).HasColumnName("is_active");
            entity.Property(e => e.NewValues).HasColumnType("jsonb");
            entity.Property(e => e.OldValues).HasColumnType("jsonb");
            entity.Property(e => e.Status).HasMaxLength(20);
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy)
                .HasMaxLength(450)
                .HasColumnName("updated_by");
            entity.Property(e => e.UserAgent).HasMaxLength(500);
            entity.Property(e => e.UserId).HasMaxLength(450);
            entity.Property(e => e.UserName).HasMaxLength(100);
            entity.Property(e => e.UserRole).HasMaxLength(50);
        });

        modelBuilder.Entity<Cart>(entity =>
        {
            entity.HasIndex(e => e.AppliedCouponId, "IX_Carts_AppliedCouponId");

            entity.HasIndex(e => e.CartId, "IX_Carts_CartId").IsUnique();

            entity.HasIndex(e => e.CashRegisterId, "IX_Carts_CashRegisterId");

            entity.HasIndex(e => e.CreatedAt, "IX_Carts_CreatedAt");

            entity.HasIndex(e => e.CustomerId, "IX_Carts_CustomerId");

            entity.HasIndex(e => e.ExpiresAt, "IX_Carts_ExpiresAt");

            entity.HasIndex(e => e.Status, "IX_Carts_Status");

            entity.HasIndex(e => e.UserId, "IX_Carts_UserId");

            entity.Property(e => e.CartId).HasMaxLength(50);
            entity.Property(e => e.DiscountAmount).HasPrecision(18, 2);
            entity.Property(e => e.Notes).HasMaxLength(500);
            entity.Property(e => e.Subtotal).HasPrecision(18, 2);
            entity.Property(e => e.TableNumber).HasMaxLength(50);
            entity.Property(e => e.TaxAmount).HasPrecision(18, 2);
            entity.Property(e => e.TotalAmount).HasPrecision(18, 2);
            entity.Property(e => e.WaiterName).HasMaxLength(50);

            entity.HasOne(d => d.AppliedCoupon).WithMany(p => p.Carts)
                .HasForeignKey(d => d.AppliedCouponId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(d => d.CashRegister).WithMany(p => p.Carts)
                .HasForeignKey(d => d.CashRegisterId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(d => d.Customer).WithMany(p => p.Carts)
                .HasForeignKey(d => d.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(d => d.User).WithMany(p => p.Carts)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<CartItem>(entity =>
        {
            entity.HasIndex(e => e.CartId, "IX_CartItems_CartId");

            entity.HasIndex(e => e.ProductId, "IX_CartItems_ProductId");

            entity.Property(e => e.Id)
                .ValueGeneratedNever()
                .HasColumnName("id");
            entity.Property(e => e.CartId).HasMaxLength(50);
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.CreatedBy)
                .HasMaxLength(450)
                .HasColumnName("created_by");
            entity.Property(e => e.DiscountAmount).HasPrecision(18, 2);
            entity.Property(e => e.IsActive).HasColumnName("is_active");
            entity.Property(e => e.IsModified).HasDefaultValue(false);
            entity.Property(e => e.Notes).HasMaxLength(200);
            entity.Property(e => e.OriginalQuantity).HasDefaultValue(0);
            entity.Property(e => e.OriginalUnitPrice).HasPrecision(18, 2);
            entity.Property(e => e.ProductName).HasMaxLength(200);
            entity.Property(e => e.TaxAmount).HasPrecision(18, 2);
            entity.Property(e => e.TaxRate).HasPrecision(5, 2);
            entity.Property(e => e.TotalAmount).HasPrecision(18, 2);
            entity.Property(e => e.UnitPrice).HasPrecision(18, 2);
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy)
                .HasMaxLength(450)
                .HasColumnName("updated_by");

            entity.HasOne(d => d.Cart).WithMany(p => p.CartItems).HasForeignKey(d => d.CartId);

            entity.HasOne(d => d.Product).WithMany(p => p.CartItems)
                .HasForeignKey(d => d.ProductId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<CashRegister>(entity =>
        {
            entity.ToTable("cash_registers");

            entity.HasIndex(e => e.CurrentUserId, "IX_cash_registers_current_user_id");

            entity.HasIndex(e => e.KassenId, "IX_cash_registers_kassen_id").IsUnique();

            entity.HasIndex(e => e.TseId, "IX_cash_registers_tse_id").IsUnique();

            entity.Property(e => e.Id)
                .ValueGeneratedNever()
                .HasColumnName("id");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.CreatedBy)
                .HasMaxLength(450)
                .HasColumnName("created_by");
            entity.Property(e => e.CurrentBalance).HasColumnName("current_balance");
            entity.Property(e => e.CurrentUserId).HasColumnName("current_user_id");
            entity.Property(e => e.IsActive).HasColumnName("is_active");
            entity.Property(e => e.KassenId)
                .HasMaxLength(50)
                .HasColumnName("kassen_id");
            entity.Property(e => e.LastBalanceUpdate).HasColumnName("last_balance_update");
            entity.Property(e => e.LastClosingAmount).HasColumnName("last_closing_amount");
            entity.Property(e => e.LastClosingDate).HasColumnName("last_closing_date");
            entity.Property(e => e.Location)
                .HasMaxLength(200)
                .HasColumnName("location");
            entity.Property(e => e.Notes)
                .HasMaxLength(500)
                .HasColumnName("notes");
            entity.Property(e => e.RegisterNumber)
                .HasMaxLength(20)
                .HasColumnName("register_number");
            entity.Property(e => e.StartingBalance).HasColumnName("starting_balance");
            entity.Property(e => e.Status).HasColumnName("status");
            entity.Property(e => e.TseId)
                .HasMaxLength(50)
                .HasColumnName("tse_id");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy)
                .HasMaxLength(450)
                .HasColumnName("updated_by");

            entity.HasOne(d => d.CurrentUser).WithMany(p => p.CashRegisters).HasForeignKey(d => d.CurrentUserId);
        });

        modelBuilder.Entity<CashRegisterTransaction>(entity =>
        {
            entity.HasIndex(e => e.CashRegisterId1, "IX_CashRegisterTransactions_CashRegisterId1");

            entity.HasIndex(e => e.UserId, "IX_CashRegisterTransactions_UserId");

            entity.Property(e => e.Id)
                .ValueGeneratedNever()
                .HasColumnName("id");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.CreatedBy)
                .HasMaxLength(450)
                .HasColumnName("created_by");
            entity.Property(e => e.Description).HasMaxLength(200);
            entity.Property(e => e.IsActive).HasColumnName("is_active");
            entity.Property(e => e.Reference).HasMaxLength(50);
            entity.Property(e => e.Tsesignature).HasColumnName("TSESignature");
            entity.Property(e => e.TsesignatureCounter).HasColumnName("TSESignatureCounter");
            entity.Property(e => e.Tsetime).HasColumnName("TSETime");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy)
                .HasMaxLength(450)
                .HasColumnName("updated_by");

            entity.HasOne(d => d.CashRegisterId1Navigation).WithMany(p => p.CashRegisterTransactions).HasForeignKey(d => d.CashRegisterId1);

            entity.HasOne(d => d.User).WithMany(p => p.CashRegisterTransactions).HasForeignKey(d => d.UserId);
        });

        modelBuilder.Entity<Category>(entity =>
        {
            entity.HasIndex(e => e.Name, "IX_Categories_Name").IsUnique();

            entity.Property(e => e.Id)
                .ValueGeneratedNever()
                .HasColumnName("id");
            entity.Property(e => e.Color).HasMaxLength(20);
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.CreatedBy)
                .HasMaxLength(450)
                .HasColumnName("created_by");
            entity.Property(e => e.Description).HasMaxLength(300);
            entity.Property(e => e.Icon).HasMaxLength(50);
            entity.Property(e => e.Name).HasMaxLength(100);
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy)
                .HasMaxLength(450)
                .HasColumnName("updated_by");
        });

        modelBuilder.Entity<CompanySetting>(entity =>
        {
            entity.Property(e => e.Id)
                .ValueGeneratedNever()
                .HasColumnName("id");
            entity.Property(e => e.Bic).HasColumnName("BIC");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.CreatedBy)
                .HasMaxLength(450)
                .HasColumnName("created_by");
            entity.Property(e => e.Iban).HasColumnName("IBAN");
            entity.Property(e => e.IsActive).HasColumnName("is_active");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy)
                .HasMaxLength(450)
                .HasColumnName("updated_by");
            entity.Property(e => e.Vatnumber).HasColumnName("VATNumber");
        });

        modelBuilder.Entity<Coupon>(entity =>
        {
            entity.ToTable("coupons");

            entity.HasIndex(e => e.Code, "IX_coupons_code").IsUnique();

            entity.HasIndex(e => e.IsActive, "IX_coupons_is_active");

            entity.HasIndex(e => e.ValidFrom, "IX_coupons_valid_from");

            entity.HasIndex(e => e.ValidUntil, "IX_coupons_valid_until");

            entity.Property(e => e.Id)
                .ValueGeneratedNever()
                .HasColumnName("id");
            entity.Property(e => e.Code)
                .HasMaxLength(20)
                .HasColumnName("code");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.CreatedBy)
                .HasMaxLength(450)
                .HasColumnName("created_by");
            entity.Property(e => e.CustomerCategoryRestriction).HasColumnName("customer_category_restriction");
            entity.Property(e => e.Description)
                .HasMaxLength(500)
                .HasColumnName("description");
            entity.Property(e => e.DiscountType).HasColumnName("discount_type");
            entity.Property(e => e.DiscountValue)
                .HasPrecision(18, 2)
                .HasColumnName("discount_value");
            entity.Property(e => e.IsActive)
                .HasDefaultValue(true)
                .HasColumnName("is_active");
            entity.Property(e => e.IsSingleUse)
                .HasDefaultValue(false)
                .HasColumnName("is_single_use");
            entity.Property(e => e.MaximumDiscount)
                .HasPrecision(18, 2)
                .HasColumnName("maximum_discount");
            entity.Property(e => e.MinimumAmount)
                .HasPrecision(18, 2)
                .HasColumnName("minimum_amount");
            entity.Property(e => e.Name)
                .HasMaxLength(100)
                .HasColumnName("name");
            entity.Property(e => e.ProductCategoryRestriction)
                .HasMaxLength(100)
                .HasColumnName("product_category_restriction");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy)
                .HasMaxLength(450)
                .HasColumnName("updated_by");
            entity.Property(e => e.UsageLimit)
                .HasDefaultValue(0)
                .HasColumnName("usage_limit");
            entity.Property(e => e.UsedCount)
                .HasDefaultValue(0)
                .HasColumnName("used_count");
            entity.Property(e => e.ValidFrom).HasColumnName("valid_from");
            entity.Property(e => e.ValidUntil).HasColumnName("valid_until");
        });

        modelBuilder.Entity<CouponUsage>(entity =>
        {
            entity.ToTable("coupon_usages");

            entity.HasIndex(e => e.CouponId, "IX_coupon_usages_coupon_id");

            entity.HasIndex(e => e.CustomerId, "IX_coupon_usages_customer_id");

            entity.HasIndex(e => e.InvoiceId, "IX_coupon_usages_invoice_id");

            entity.HasIndex(e => e.OrderId, "IX_coupon_usages_order_id");

            entity.HasIndex(e => e.UsedAt, "IX_coupon_usages_used_at");

            entity.Property(e => e.Id)
                .ValueGeneratedNever()
                .HasColumnName("id");
            entity.Property(e => e.CouponId).HasColumnName("coupon_id");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.CreatedBy)
                .HasMaxLength(450)
                .HasColumnName("created_by");
            entity.Property(e => e.CustomerId).HasColumnName("customer_id");
            entity.Property(e => e.DiscountAmount)
                .HasPrecision(18, 2)
                .HasColumnName("discount_amount");
            entity.Property(e => e.InvoiceId).HasColumnName("invoice_id");
            entity.Property(e => e.IsActive).HasColumnName("is_active");
            entity.Property(e => e.OrderId).HasColumnName("order_id");
            entity.Property(e => e.SessionId)
                .HasMaxLength(100)
                .HasColumnName("session_id");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy)
                .HasMaxLength(450)
                .HasColumnName("updated_by");
            entity.Property(e => e.UsedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnName("used_at");
            entity.Property(e => e.UsedBy)
                .HasMaxLength(450)
                .HasColumnName("used_by");

            entity.HasOne(d => d.Coupon).WithMany(p => p.CouponUsages)
                .HasForeignKey(d => d.CouponId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(d => d.Customer).WithMany(p => p.CouponUsages)
                .HasForeignKey(d => d.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(d => d.Invoice).WithMany(p => p.CouponUsages)
                .HasForeignKey(d => d.InvoiceId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(d => d.Order).WithMany(p => p.CouponUsages)
                .HasForeignKey(d => d.OrderId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Customer>(entity =>
        {
            entity.ToTable("customers");

            entity.HasIndex(e => e.CustomerCategory, "IX_customers_customer_category");

            entity.HasIndex(e => e.Email, "IX_customers_email");

            entity.HasIndex(e => e.Phone, "IX_customers_phone");

            entity.HasIndex(e => e.TaxNumber, "IX_customers_tax_number");

            entity.Property(e => e.Id)
                .ValueGeneratedNever()
                .HasColumnName("id");
            entity.Property(e => e.Address)
                .HasMaxLength(200)
                .HasColumnName("address");
            entity.Property(e => e.BirthDate).HasColumnName("birth_date");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.CreatedBy)
                .HasMaxLength(450)
                .HasColumnName("created_by");
            entity.Property(e => e.CustomerCategory).HasColumnName("customer_category");
            entity.Property(e => e.CustomerNumber)
                .HasMaxLength(20)
                .HasColumnName("customer_number");
            entity.Property(e => e.DiscountPercentage)
                .HasPrecision(5, 2)
                .HasColumnName("discount_percentage");
            entity.Property(e => e.Email)
                .HasMaxLength(100)
                .HasColumnName("email");
            entity.Property(e => e.IsActive).HasColumnName("is_active");
            entity.Property(e => e.IsVip).HasColumnName("is_vip");
            entity.Property(e => e.LastVisit).HasColumnName("last_visit");
            entity.Property(e => e.LoyaltyPoints)
                .HasDefaultValue(0)
                .HasColumnName("loyalty_points");
            entity.Property(e => e.Name)
                .HasMaxLength(100)
                .HasColumnName("name");
            entity.Property(e => e.Notes).HasColumnName("notes");
            entity.Property(e => e.Phone)
                .HasMaxLength(20)
                .HasColumnName("phone");
            entity.Property(e => e.PreferredPaymentMethod).HasColumnName("preferred_payment_method");
            entity.Property(e => e.TaxNumber)
                .HasMaxLength(20)
                .HasColumnName("tax_number");
            entity.Property(e => e.TotalSpent)
                .HasPrecision(18, 2)
                .HasColumnName("total_spent");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy)
                .HasMaxLength(450)
                .HasColumnName("updated_by");
            entity.Property(e => e.VisitCount)
                .HasDefaultValue(0)
                .HasColumnName("visit_count");
        });

        modelBuilder.Entity<CustomerDetail>(entity =>
        {
            entity.Property(e => e.Id)
                .ValueGeneratedNever()
                .HasColumnName("id");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.CreatedBy)
                .HasMaxLength(450)
                .HasColumnName("created_by");
            entity.Property(e => e.IsActive).HasColumnName("is_active");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy)
                .HasMaxLength(450)
                .HasColumnName("updated_by");
        });

        modelBuilder.Entity<CustomerDiscount>(entity =>
        {
            entity.ToTable("customer_discounts");

            entity.HasIndex(e => e.CustomerId, "IX_customer_discounts_customer_id");

            entity.HasIndex(e => e.IsActive, "IX_customer_discounts_is_active");

            entity.HasIndex(e => e.ValidFrom, "IX_customer_discounts_valid_from");

            entity.HasIndex(e => e.ValidUntil, "IX_customer_discounts_valid_until");

            entity.Property(e => e.Id)
                .ValueGeneratedNever()
                .HasColumnName("id");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.CreatedBy)
                .HasMaxLength(450)
                .HasColumnName("created_by");
            entity.Property(e => e.CustomerId).HasColumnName("customer_id");
            entity.Property(e => e.Description)
                .HasMaxLength(500)
                .HasColumnName("description");
            entity.Property(e => e.DiscountType).HasColumnName("discount_type");
            entity.Property(e => e.DiscountValue)
                .HasPrecision(18, 2)
                .HasColumnName("discount_value");
            entity.Property(e => e.IsActive)
                .HasDefaultValue(true)
                .HasColumnName("is_active");
            entity.Property(e => e.MinimumAmount)
                .HasPrecision(18, 2)
                .HasColumnName("minimum_amount");
            entity.Property(e => e.ProductCategoryRestriction)
                .HasMaxLength(100)
                .HasColumnName("product_category_restriction");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy)
                .HasMaxLength(450)
                .HasColumnName("updated_by");
            entity.Property(e => e.UsageLimit)
                .HasDefaultValue(0)
                .HasColumnName("usage_limit");
            entity.Property(e => e.UsedCount)
                .HasDefaultValue(0)
                .HasColumnName("used_count");
            entity.Property(e => e.ValidFrom).HasColumnName("valid_from");
            entity.Property(e => e.ValidUntil).HasColumnName("valid_until");

            entity.HasOne(d => d.Customer).WithMany(p => p.CustomerDiscounts).HasForeignKey(d => d.CustomerId);
        });

        modelBuilder.Entity<DailyReport>(entity =>
        {
            entity.HasIndex(e => e.ReportDate, "IX_DailyReports_ReportDate");

            entity.HasIndex(e => e.TseSignature, "IX_DailyReports_TseSignature");

            entity.Property(e => e.Id)
                .ValueGeneratedNever()
                .HasColumnName("id");
            entity.Property(e => e.CardPayments).HasPrecision(18, 2);
            entity.Property(e => e.CashPayments).HasPrecision(18, 2);
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.CreatedBy)
                .HasMaxLength(450)
                .HasColumnName("created_by");
            entity.Property(e => e.IsActive).HasColumnName("is_active");
            entity.Property(e => e.KassenId).HasMaxLength(50);
            entity.Property(e => e.TotalSales).HasPrecision(18, 2);
            entity.Property(e => e.TseSignature).HasMaxLength(500);
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy)
                .HasMaxLength(450)
                .HasColumnName("updated_by");
        });

        modelBuilder.Entity<Discount>(entity =>
        {
            entity.ToTable("discounts");

            entity.Property(e => e.Id)
                .ValueGeneratedNever()
                .HasColumnName("id");
            entity.Property(e => e.Code)
                .HasMaxLength(50)
                .HasColumnName("code");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.CreatedBy)
                .HasMaxLength(450)
                .HasColumnName("created_by");
            entity.Property(e => e.DiscountType).HasColumnName("discount_type");
            entity.Property(e => e.EndDate).HasColumnName("end_date");
            entity.Property(e => e.IsActive).HasColumnName("is_active");
            entity.Property(e => e.MaxDiscountAmount).HasColumnName("max_discount_amount");
            entity.Property(e => e.MinPurchaseAmount).HasColumnName("min_purchase_amount");
            entity.Property(e => e.Name)
                .HasMaxLength(100)
                .HasColumnName("name");
            entity.Property(e => e.StartDate).HasColumnName("start_date");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy)
                .HasMaxLength(450)
                .HasColumnName("updated_by");
            entity.Property(e => e.Value).HasColumnName("value");
        });

        modelBuilder.Entity<FinanceOnline>(entity =>
        {
            entity.ToTable("finance_online");

            entity.HasIndex(e => e.InvoiceId, "IX_finance_online_invoice_id");

            entity.Property(e => e.Id)
                .ValueGeneratedNever()
                .HasColumnName("id");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.CreatedBy)
                .HasMaxLength(450)
                .HasColumnName("created_by");
            entity.Property(e => e.InvoiceId).HasColumnName("invoice_id");
            entity.Property(e => e.IsActive).HasColumnName("is_active");
            entity.Property(e => e.QrCode).HasColumnName("qr_code");
            entity.Property(e => e.ResponseCode)
                .HasMaxLength(10)
                .HasColumnName("response_code");
            entity.Property(e => e.ResponseMessage).HasColumnName("response_message");
            entity.Property(e => e.SignatureCertificate).HasColumnName("signature_certificate");
            entity.Property(e => e.SignatureValue).HasColumnName("signature_value");
            entity.Property(e => e.TransactionNumber)
                .HasMaxLength(50)
                .HasColumnName("transaction_number");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy)
                .HasMaxLength(450)
                .HasColumnName("updated_by");

            entity.HasOne(d => d.Invoice).WithMany(p => p.FinanceOnlines).HasForeignKey(d => d.InvoiceId);
        });

        modelBuilder.Entity<Hardware>(entity =>
        {
            entity.ToTable("Hardware");

            entity.Property(e => e.Id)
                .ValueGeneratedNever()
                .HasColumnName("id");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.CreatedBy)
                .HasMaxLength(450)
                .HasColumnName("created_by");
            entity.Property(e => e.Ipaddress).HasColumnName("IPAddress");
            entity.Property(e => e.IsActive).HasColumnName("is_active");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy)
                .HasMaxLength(450)
                .HasColumnName("updated_by");
        });

        modelBuilder.Entity<Inventory>(entity =>
        {
            entity.HasIndex(e => e.ProductId, "IX_Inventories_ProductId").IsUnique();

            entity.Property(e => e.Id)
                .ValueGeneratedNever()
                .HasColumnName("id");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.CreatedBy)
                .HasMaxLength(450)
                .HasColumnName("created_by");
            entity.Property(e => e.IsActive).HasColumnName("is_active");
            entity.Property(e => e.Location).HasMaxLength(100);
            entity.Property(e => e.Notes).HasMaxLength(200);
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy)
                .HasMaxLength(450)
                .HasColumnName("updated_by");

            entity.HasOne(d => d.Product).WithOne(p => p.Inventory).HasForeignKey<Inventory>(d => d.ProductId);
        });

        modelBuilder.Entity<InventoryTransaction>(entity =>
        {
            entity.HasIndex(e => e.ApplicationUserId, "IX_InventoryTransactions_ApplicationUserId");

            entity.HasIndex(e => e.InventoryId1, "IX_InventoryTransactions_InventoryId1");

            entity.Property(e => e.Id)
                .ValueGeneratedNever()
                .HasColumnName("id");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.CreatedBy)
                .HasMaxLength(450)
                .HasColumnName("created_by");
            entity.Property(e => e.IsActive).HasColumnName("is_active");
            entity.Property(e => e.Reference).HasMaxLength(100);
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy)
                .HasMaxLength(450)
                .HasColumnName("updated_by");

            entity.HasOne(d => d.ApplicationUser).WithMany(p => p.InventoryTransactions).HasForeignKey(d => d.ApplicationUserId);

            entity.HasOne(d => d.InventoryId1Navigation).WithMany(p => p.InventoryTransactions).HasForeignKey(d => d.InventoryId1);
        });

        modelBuilder.Entity<Invoice>(entity =>
        {
            entity.HasIndex(e => e.CashRegisterId1, "IX_Invoices_CashRegisterId1");

            entity.HasIndex(e => e.CreatedById, "IX_Invoices_CreatedById");

            entity.HasIndex(e => e.CustomerDetailsId, "IX_Invoices_CustomerDetailsId");

            entity.HasIndex(e => e.CustomerEmail, "IX_Invoices_CustomerEmail");

            entity.HasIndex(e => e.CustomerId, "IX_Invoices_CustomerId");

            entity.HasIndex(e => e.CustomerId1, "IX_Invoices_CustomerId1");

            entity.HasIndex(e => e.DueDate, "IX_Invoices_DueDate");

            entity.HasIndex(e => e.InvoiceDate, "IX_Invoices_InvoiceDate");

            entity.HasIndex(e => e.InvoiceNumber, "IX_Invoices_InvoiceNumber").IsUnique();

            entity.HasIndex(e => e.InvoiceTemplateId, "IX_Invoices_InvoiceTemplateId");

            entity.HasIndex(e => e.PaymentDetailsId, "IX_Invoices_PaymentDetailsId");

            entity.HasIndex(e => e.ReceiptNumber, "IX_Invoices_ReceiptNumber").IsUnique();

            entity.HasIndex(e => e.Status, "IX_Invoices_Status");

            entity.HasIndex(e => e.TaxSummaryId, "IX_Invoices_TaxSummaryId");

            entity.HasIndex(e => e.TseSignature, "IX_Invoices_TseSignature");

            entity.Property(e => e.Id)
                .ValueGeneratedNever()
                .HasColumnName("id");
            entity.Property(e => e.CancelledReason).HasMaxLength(500);
            entity.Property(e => e.CashRegisterId).HasMaxLength(50);
            entity.Property(e => e.CompanyAddress).HasMaxLength(500);
            entity.Property(e => e.CompanyEmail).HasMaxLength(100);
            entity.Property(e => e.CompanyName).HasMaxLength(200);
            entity.Property(e => e.CompanyPhone).HasMaxLength(20);
            entity.Property(e => e.CompanyTaxNumber).HasMaxLength(50);
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.CustomerAddress).HasMaxLength(500);
            entity.Property(e => e.CustomerEmail).HasMaxLength(100);
            entity.Property(e => e.CustomerName).HasMaxLength(200);
            entity.Property(e => e.CustomerPhone).HasMaxLength(20);
            entity.Property(e => e.CustomerTaxNumber).HasMaxLength(50);
            entity.Property(e => e.FinanzOnlineReference).HasMaxLength(100);
            entity.Property(e => e.InvoiceItems).HasColumnType("jsonb");
            entity.Property(e => e.InvoiceNumber).HasMaxLength(50);
            entity.Property(e => e.InvoiceType).HasMaxLength(50);
            entity.Property(e => e.IsActive).HasColumnName("is_active");
            entity.Property(e => e.KassenId).HasMaxLength(50);
            entity.Property(e => e.PaidAmount).HasPrecision(18, 2);
            entity.Property(e => e.PaymentReference).HasMaxLength(100);
            entity.Property(e => e.ReceiptNumber).HasMaxLength(50);
            entity.Property(e => e.RemainingAmount).HasPrecision(18, 2);
            entity.Property(e => e.Subtotal).HasPrecision(18, 2);
            entity.Property(e => e.TaxAmount).HasPrecision(18, 2);
            entity.Property(e => e.TaxDetails).HasColumnType("jsonb");
            entity.Property(e => e.TotalAmount).HasPrecision(18, 2);
            entity.Property(e => e.TseSignature).HasMaxLength(500);
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy)
                .HasMaxLength(450)
                .HasColumnName("updated_by");

            entity.HasOne(d => d.CashRegisterId1Navigation).WithMany(p => p.Invoices).HasForeignKey(d => d.CashRegisterId1);

            entity.HasOne(d => d.CreatedBy).WithMany(p => p.InvoiceCreatedBies)
                .HasForeignKey(d => d.CreatedById)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(d => d.CustomerDetails).WithMany(p => p.Invoices).HasForeignKey(d => d.CustomerDetailsId);

            entity.HasOne(d => d.Customer).WithMany(p => p.InvoiceCustomers)
                .HasForeignKey(d => d.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(d => d.CustomerId1Navigation).WithMany(p => p.Invoices).HasForeignKey(d => d.CustomerId1);

            entity.HasOne(d => d.InvoiceTemplate).WithMany(p => p.Invoices).HasForeignKey(d => d.InvoiceTemplateId);

            entity.HasOne(d => d.PaymentDetails).WithMany(p => p.Invoices).HasForeignKey(d => d.PaymentDetailsId);

            entity.HasOne(d => d.TaxSummary).WithMany(p => p.Invoices).HasForeignKey(d => d.TaxSummaryId);
        });

        modelBuilder.Entity<InvoiceHistory>(entity =>
        {
            entity.ToTable("InvoiceHistory");

            entity.HasIndex(e => e.InvoiceId, "IX_InvoiceHistory_InvoiceId");

            entity.HasIndex(e => e.PerformedById, "IX_InvoiceHistory_PerformedById");

            entity.Property(e => e.Id)
                .ValueGeneratedNever()
                .HasColumnName("id");
            entity.Property(e => e.Action).HasMaxLength(50);
            entity.Property(e => e.Changes).HasColumnType("jsonb");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.CreatedBy)
                .HasMaxLength(450)
                .HasColumnName("created_by");
            entity.Property(e => e.Description).HasMaxLength(200);
            entity.Property(e => e.IsActive).HasColumnName("is_active");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy)
                .HasMaxLength(450)
                .HasColumnName("updated_by");

            entity.HasOne(d => d.Invoice).WithMany(p => p.InvoiceHistories).HasForeignKey(d => d.InvoiceId);

            entity.HasOne(d => d.PerformedBy).WithMany(p => p.InvoiceHistories).HasForeignKey(d => d.PerformedById);
        });

        modelBuilder.Entity<InvoiceItem>(entity =>
        {
            entity.HasIndex(e => e.InvoiceId, "IX_InvoiceItems_InvoiceId");

            entity.HasIndex(e => e.ProductId, "IX_InvoiceItems_ProductId");

            entity.Property(e => e.Id)
                .ValueGeneratedNever()
                .HasColumnName("id");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.CreatedBy)
                .HasMaxLength(450)
                .HasColumnName("created_by");
            entity.Property(e => e.IsActive).HasColumnName("is_active");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy)
                .HasMaxLength(450)
                .HasColumnName("updated_by");

            entity.HasOne(d => d.Invoice).WithMany(p => p.InvoiceItemsNavigation).HasForeignKey(d => d.InvoiceId);

            entity.HasOne(d => d.ProductNavigation).WithMany(p => p.InvoiceItems).HasForeignKey(d => d.ProductId);
        });

        modelBuilder.Entity<InvoiceTemplate>(entity =>
        {
            entity.HasIndex(e => e.CreatedById, "IX_InvoiceTemplates_CreatedById");

            entity.HasIndex(e => e.Name, "IX_InvoiceTemplates_Name").IsUnique();

            entity.Property(e => e.Id)
                .ValueGeneratedNever()
                .HasColumnName("id");
            entity.Property(e => e.CompanyAddress).HasMaxLength(500);
            entity.Property(e => e.CompanyEmail).HasMaxLength(100);
            entity.Property(e => e.CompanyName).HasMaxLength(200);
            entity.Property(e => e.CompanyPhone).HasMaxLength(20);
            entity.Property(e => e.CompanyTaxNumber).HasMaxLength(50);
            entity.Property(e => e.CompanyWebsite).HasMaxLength(100);
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.CustomerAddressLabel).HasMaxLength(50);
            entity.Property(e => e.CustomerEmailLabel).HasMaxLength(50);
            entity.Property(e => e.CustomerNameLabel).HasMaxLength(50);
            entity.Property(e => e.CustomerPhoneLabel).HasMaxLength(50);
            entity.Property(e => e.CustomerSectionTitle).HasMaxLength(100);
            entity.Property(e => e.CustomerTaxNumberLabel).HasMaxLength(50);
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.DescriptionHeader).HasMaxLength(50);
            entity.Property(e => e.DueDateLabel).HasMaxLength(50);
            entity.Property(e => e.FontFamily).HasMaxLength(50);
            entity.Property(e => e.InvoiceDateLabel).HasMaxLength(50);
            entity.Property(e => e.InvoiceNumberLabel).HasMaxLength(50);
            entity.Property(e => e.InvoiceTitle).HasMaxLength(100);
            entity.Property(e => e.ItemHeader).HasMaxLength(50);
            entity.Property(e => e.KassenIdLabel).HasMaxLength(50);
            entity.Property(e => e.LogoUrl).HasMaxLength(500);
            entity.Property(e => e.Name).HasMaxLength(100);
            entity.Property(e => e.Notes).HasMaxLength(500);
            entity.Property(e => e.PageSize).HasMaxLength(10);
            entity.Property(e => e.PaidLabel).HasMaxLength(50);
            entity.Property(e => e.PaymentDateLabel).HasMaxLength(50);
            entity.Property(e => e.PaymentMethodLabel).HasMaxLength(50);
            entity.Property(e => e.PaymentReferenceLabel).HasMaxLength(50);
            entity.Property(e => e.PaymentSectionTitle).HasMaxLength(100);
            entity.Property(e => e.PrimaryColor).HasMaxLength(7);
            entity.Property(e => e.QuantityHeader).HasMaxLength(50);
            entity.Property(e => e.RemainingLabel).HasMaxLength(50);
            entity.Property(e => e.SecondaryColor).HasMaxLength(7);
            entity.Property(e => e.SubtotalLabel).HasMaxLength(50);
            entity.Property(e => e.TaxHeader).HasMaxLength(50);
            entity.Property(e => e.TaxLabel).HasMaxLength(50);
            entity.Property(e => e.TermsAndConditions).HasMaxLength(500);
            entity.Property(e => e.TotalHeader).HasMaxLength(50);
            entity.Property(e => e.TotalLabel).HasMaxLength(50);
            entity.Property(e => e.TseSignatureLabel).HasMaxLength(50);
            entity.Property(e => e.TseTimestampLabel).HasMaxLength(50);
            entity.Property(e => e.UnitPriceHeader).HasMaxLength(50);
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy)
                .HasMaxLength(450)
                .HasColumnName("updated_by");

            entity.HasOne(d => d.CreatedBy).WithMany(p => p.InvoiceTemplates).HasForeignKey(d => d.CreatedById);
        });

        modelBuilder.Entity<Order>(entity =>
        {
            entity.HasIndex(e => e.ApplicationUserId, "IX_Orders_ApplicationUserId");

            entity.HasIndex(e => e.CustomerId1, "IX_Orders_CustomerId1");

            entity.HasIndex(e => e.TableId, "IX_Orders_TableId");

            entity.Property(e => e.Id)
                .ValueGeneratedNever()
                .HasColumnName("id");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.CreatedBy)
                .HasMaxLength(450)
                .HasColumnName("created_by");
            entity.Property(e => e.IsActive).HasColumnName("is_active");
            entity.Property(e => e.Notes).HasMaxLength(200);
            entity.Property(e => e.OrderNumber).HasMaxLength(50);
            entity.Property(e => e.TableNumber).HasMaxLength(10);
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy)
                .HasMaxLength(450)
                .HasColumnName("updated_by");
            entity.Property(e => e.WaiterName).HasMaxLength(50);

            entity.HasOne(d => d.ApplicationUser).WithMany(p => p.Orders).HasForeignKey(d => d.ApplicationUserId);

            entity.HasOne(d => d.CustomerId1Navigation).WithMany(p => p.Orders).HasForeignKey(d => d.CustomerId1);

            entity.HasOne(d => d.Table).WithMany(p => p.Orders).HasForeignKey(d => d.TableId);
        });

        modelBuilder.Entity<OrderItem>(entity =>
        {
            entity.HasIndex(e => e.OrderId1, "IX_OrderItems_OrderId1");

            entity.HasIndex(e => e.ProductId, "IX_OrderItems_ProductId");

            entity.Property(e => e.Id)
                .ValueGeneratedNever()
                .HasColumnName("id");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.CreatedBy)
                .HasMaxLength(450)
                .HasColumnName("created_by");
            entity.Property(e => e.IsActive).HasColumnName("is_active");
            entity.Property(e => e.Notes).HasMaxLength(200);
            entity.Property(e => e.TotalAmount).HasPrecision(18, 2);
            entity.Property(e => e.UnitPrice).HasPrecision(18, 2);
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy)
                .HasMaxLength(450)
                .HasColumnName("updated_by");

            entity.HasOne(d => d.OrderId1Navigation).WithMany(p => p.OrderItems).HasForeignKey(d => d.OrderId1);

            entity.HasOne(d => d.Product).WithMany(p => p.OrderItems).HasForeignKey(d => d.ProductId);
        });

        modelBuilder.Entity<PaymentDetail>(entity =>
        {
            entity.Property(e => e.Id)
                .ValueGeneratedNever()
                .HasColumnName("id");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.CreatedBy)
                .HasMaxLength(450)
                .HasColumnName("created_by");
            entity.Property(e => e.IsActive).HasColumnName("is_active");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy)
                .HasMaxLength(450)
                .HasColumnName("updated_by");
        });

        modelBuilder.Entity<Product>(entity =>
        {
            entity.ToTable("products");

            entity.HasIndex(e => e.CategoryId, "IX_products_CategoryId");

            entity.HasIndex(e => e.CategoryId1, "IX_products_CategoryId1");

            entity.HasIndex(e => e.Barcode, "IX_products_barcode").IsUnique();

            entity.HasIndex(e => e.Name, "IX_products_name");

            entity.Property(e => e.Id)
                .ValueGeneratedNever()
                .HasColumnName("id");
            entity.Property(e => e.Barcode)
                .HasMaxLength(50)
                .HasColumnName("barcode");
            entity.Property(e => e.Category)
                .HasMaxLength(50)
                .HasColumnName("category");
            entity.Property(e => e.Cost).HasPrecision(18, 2);
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.CreatedBy)
                .HasMaxLength(450)
                .HasColumnName("created_by");
            entity.Property(e => e.Description)
                .HasMaxLength(500)
                .HasColumnName("description");
            entity.Property(e => e.ImageUrl).HasColumnName("image_url");
            entity.Property(e => e.IsActive).HasColumnName("is_active");
            entity.Property(e => e.MinStockLevel).HasColumnName("min_stock_level");
            entity.Property(e => e.Name)
                .HasMaxLength(200)
                .HasColumnName("name");
            entity.Property(e => e.Price)
                .HasPrecision(18, 2)
                .HasColumnName("price");
            entity.Property(e => e.StockQuantity).HasColumnName("stock_quantity");
            entity.Property(e => e.TaxRate).HasPrecision(5, 2);
            entity.Property(e => e.TaxType).HasColumnName("tax_type");
            entity.Property(e => e.Unit)
                .HasMaxLength(20)
                .HasColumnName("unit");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy)
                .HasMaxLength(450)
                .HasColumnName("updated_by");

            entity.HasOne(d => d.CategoryId1Navigation).WithMany(p => p.Products).HasForeignKey(d => d.CategoryId1);
        });

        modelBuilder.Entity<Receipt>(entity =>
        {
            entity.HasIndex(e => e.CashRegisterId1, "IX_Receipts_CashRegisterId1");

            entity.HasIndex(e => e.ReceiptNumber, "IX_Receipts_ReceiptNumber").IsUnique();

            entity.HasIndex(e => e.TseSignature, "IX_Receipts_TseSignature");

            entity.HasIndex(e => e.UserId, "IX_Receipts_UserId");

            entity.HasIndex(e => e.CreatedAt, "IX_Receipts_created_at");

            entity.Property(e => e.Id)
                .ValueGeneratedNever()
                .HasColumnName("id");
            entity.Property(e => e.CancellationReason).HasMaxLength(500);
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.CreatedBy)
                .HasMaxLength(450)
                .HasColumnName("created_by");
            entity.Property(e => e.IsActive).HasColumnName("is_active");
            entity.Property(e => e.KassenId).HasMaxLength(50);
            entity.Property(e => e.PaymentMethod).HasMaxLength(20);
            entity.Property(e => e.ReceiptNumber).HasMaxLength(50);
            entity.Property(e => e.Subtotal).HasPrecision(18, 2);
            entity.Property(e => e.TaxAmount).HasPrecision(18, 2);
            entity.Property(e => e.TotalAmount).HasPrecision(18, 2);
            entity.Property(e => e.TseSignature).HasMaxLength(500);
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy)
                .HasMaxLength(450)
                .HasColumnName("updated_by");

            entity.HasOne(d => d.CashRegisterId1Navigation).WithMany(p => p.Receipts).HasForeignKey(d => d.CashRegisterId1);

            entity.HasOne(d => d.User).WithMany(p => p.Receipts).HasForeignKey(d => d.UserId);
        });

        modelBuilder.Entity<ReceiptItem>(entity =>
        {
            entity.HasIndex(e => e.ProductId, "IX_ReceiptItems_ProductId");

            entity.HasIndex(e => e.ReceiptId, "IX_ReceiptItems_ReceiptId");

            entity.Property(e => e.Id)
                .ValueGeneratedNever()
                .HasColumnName("id");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.CreatedBy)
                .HasMaxLength(450)
                .HasColumnName("created_by");
            entity.Property(e => e.IsActive).HasColumnName("is_active");
            entity.Property(e => e.Name).HasMaxLength(200);
            entity.Property(e => e.ProductName).HasMaxLength(200);
            entity.Property(e => e.TaxAmount).HasPrecision(18, 2);
            entity.Property(e => e.TaxRate).HasPrecision(5, 2);
            entity.Property(e => e.TotalAmount).HasPrecision(18, 2);
            entity.Property(e => e.UnitPrice).HasPrecision(18, 2);
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy)
                .HasMaxLength(450)
                .HasColumnName("updated_by");

            entity.HasOne(d => d.Product).WithMany(p => p.ReceiptItems).HasForeignKey(d => d.ProductId);

            entity.HasOne(d => d.Receipt).WithMany(p => p.ReceiptItems).HasForeignKey(d => d.ReceiptId);
        });

        modelBuilder.Entity<SystemConfiguration>(entity =>
        {
            entity.Property(e => e.Id)
                .ValueGeneratedNever()
                .HasColumnName("id");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.CreatedBy)
                .HasMaxLength(450)
                .HasColumnName("created_by");
            entity.Property(e => e.IsActive).HasColumnName("is_active");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy)
                .HasMaxLength(450)
                .HasColumnName("updated_by");
        });

        modelBuilder.Entity<Table>(entity =>
        {
            entity.ToTable("tables");

            entity.HasIndex(e => e.CurrentOrderId, "IX_tables_current_order_id");

            entity.Property(e => e.Id)
                .ValueGeneratedNever()
                .HasColumnName("id");
            entity.Property(e => e.Capacity).HasColumnName("capacity");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.CreatedBy)
                .HasMaxLength(450)
                .HasColumnName("created_by");
            entity.Property(e => e.CurrentOrderId).HasColumnName("current_order_id");
            entity.Property(e => e.CurrentTotal).HasColumnName("current_total");
            entity.Property(e => e.CustomerName)
                .HasMaxLength(100)
                .HasColumnName("customer_name");
            entity.Property(e => e.IsActive).HasColumnName("is_active");
            entity.Property(e => e.LastOrderTime).HasColumnName("last_order_time");
            entity.Property(e => e.Location)
                .HasMaxLength(100)
                .HasColumnName("location");
            entity.Property(e => e.Name)
                .HasMaxLength(100)
                .HasColumnName("name");
            entity.Property(e => e.Number).HasColumnName("number");
            entity.Property(e => e.StartTime).HasColumnName("start_time");
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .HasColumnName("status");
            entity.Property(e => e.TotalPaid).HasColumnName("total_paid");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy)
                .HasMaxLength(450)
                .HasColumnName("updated_by");

            entity.HasOne(d => d.CurrentOrder).WithMany(p => p.Tables).HasForeignKey(d => d.CurrentOrderId);
        });

        modelBuilder.Entity<TableReservation>(entity =>
        {
            entity.ToTable("table_reservations");

            entity.HasIndex(e => e.TableId, "IX_table_reservations_table_id");

            entity.Property(e => e.Id)
                .ValueGeneratedNever()
                .HasColumnName("id");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.CreatedBy)
                .HasMaxLength(450)
                .HasColumnName("created_by");
            entity.Property(e => e.CustomerName)
                .HasMaxLength(100)
                .HasColumnName("customer_name");
            entity.Property(e => e.CustomerPhone)
                .HasMaxLength(20)
                .HasColumnName("customer_phone");
            entity.Property(e => e.GuestCount).HasColumnName("guest_count");
            entity.Property(e => e.IsActive).HasColumnName("is_active");
            entity.Property(e => e.Notes)
                .HasMaxLength(500)
                .HasColumnName("notes");
            entity.Property(e => e.ReservationTime).HasColumnName("reservation_time");
            entity.Property(e => e.Status).HasColumnName("status");
            entity.Property(e => e.TableId).HasColumnName("table_id");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy)
                .HasMaxLength(450)
                .HasColumnName("updated_by");

            entity.HasOne(d => d.Table).WithMany(p => p.TableReservations).HasForeignKey(d => d.TableId);
        });

        modelBuilder.Entity<TaxSummary>(entity =>
        {
            entity.Property(e => e.Id)
                .ValueGeneratedNever()
                .HasColumnName("id");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.CreatedBy)
                .HasMaxLength(450)
                .HasColumnName("created_by");
            entity.Property(e => e.IsActive).HasColumnName("is_active");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy)
                .HasMaxLength(450)
                .HasColumnName("updated_by");
        });

        modelBuilder.Entity<Transaction>(entity =>
        {
            entity.HasIndex(e => e.CashRegisterId1, "IX_Transactions_CashRegisterId1");

            entity.HasIndex(e => e.InvoiceId, "IX_Transactions_InvoiceId");

            entity.HasIndex(e => e.PaymentMethod, "IX_Transactions_PaymentMethod");

            entity.HasIndex(e => e.ReceiptId, "IX_Transactions_ReceiptId");

            entity.HasIndex(e => e.TransactionNumber, "IX_Transactions_TransactionNumber").IsUnique();

            entity.HasIndex(e => e.UserId, "IX_Transactions_UserId");

            entity.HasIndex(e => e.CreatedAt, "IX_Transactions_created_at");

            entity.Property(e => e.Id)
                .ValueGeneratedNever()
                .HasColumnName("id");
            entity.Property(e => e.Amount).HasPrecision(18, 2);
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.CreatedBy)
                .HasMaxLength(450)
                .HasColumnName("created_by");
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.IsActive).HasColumnName("is_active");
            entity.Property(e => e.PaymentMethod).HasMaxLength(20);
            entity.Property(e => e.Reference).HasMaxLength(100);
            entity.Property(e => e.Status).HasMaxLength(20);
            entity.Property(e => e.TransactionNumber).HasMaxLength(50);
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy)
                .HasMaxLength(450)
                .HasColumnName("updated_by");

            entity.HasOne(d => d.CashRegisterId1Navigation).WithMany(p => p.Transactions).HasForeignKey(d => d.CashRegisterId1);

            entity.HasOne(d => d.Invoice).WithMany(p => p.Transactions).HasForeignKey(d => d.InvoiceId);

            entity.HasOne(d => d.Receipt).WithMany(p => p.Transactions).HasForeignKey(d => d.ReceiptId);

            entity.HasOne(d => d.User).WithMany(p => p.Transactions).HasForeignKey(d => d.UserId);
        });

        modelBuilder.Entity<UserSession>(entity =>
        {
            entity.HasIndex(e => e.UserId, "IX_UserSessions_UserId");

            entity.Property(e => e.Id)
                .ValueGeneratedNever()
                .HasColumnName("id");
            entity.Property(e => e.CreatedBy)
                .HasMaxLength(450)
                .HasColumnName("created_by");
            entity.Property(e => e.IsActive).HasColumnName("is_active");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy)
                .HasMaxLength(450)
                .HasColumnName("updated_by");

            entity.HasOne(d => d.User).WithMany(p => p.UserSessions).HasForeignKey(d => d.UserId);
        });

        modelBuilder.Entity<UserSetting>(entity =>
        {
            entity.HasIndex(e => e.UserId, "IX_UserSettings_UserId").IsUnique();

            entity.Property(e => e.Id)
                .ValueGeneratedNever()
                .HasColumnName("id");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.CreatedBy)
                .HasMaxLength(450)
                .HasColumnName("created_by");
            entity.Property(e => e.IsActive).HasColumnName("is_active");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy)
                .HasMaxLength(450)
                .HasColumnName("updated_by");

            entity.HasOne(d => d.User).WithOne(p => p.UserSetting).HasForeignKey<UserSetting>(d => d.UserId);
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}

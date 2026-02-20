using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using KasseAPI_Final.Models;

namespace KasseAPI_Final.Data
{
    public class AppDbContext : IdentityDbContext<ApplicationUser>
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        // DbSet properties
        public DbSet<Product> Products { get; set; }
        public DbSet<Customer> Customers { get; set; }
        public DbSet<Invoice> Invoices { get; set; }
        public DbSet<Cart> Carts { get; set; }
        public DbSet<CartItem> CartItems { get; set; }
        public DbSet<Order> Orders { get; set; }
        public DbSet<OrderItem> OrderItems { get; set; }
        public DbSet<CashRegister> CashRegisters { get; set; }
        public DbSet<CashRegisterTransaction> CashRegisterTransactions { get; set; }
        public DbSet<Category> Categories { get; set; }
        public DbSet<PaymentDetails> PaymentDetails { get; set; }
        public DbSet<PaymentItem> PaymentItems { get; set; }
        public DbSet<InventoryItem> Inventory { get; set; }
        public DbSet<InventoryTransaction> InventoryTransactions { get; set; }
        public DbSet<SystemSettings> SystemSettings { get; set; }
        public DbSet<UserSettings> UserSettings { get; set; }
        public DbSet<AuditLog> AuditLogs { get; set; }
        public DbSet<CompanySettings> CompanySettings { get; set; }
        public DbSet<LocalizationSettings> LocalizationSettings { get; set; }
        public DbSet<ReceiptTemplate> ReceiptTemplates { get; set; }
        public DbSet<GeneratedReceipt> GeneratedReceipts { get; set; }
        public DbSet<TseDevice> TseDevices { get; set; }
        public DbSet<DailyClosing> DailyClosings { get; set; }
        public DbSet<TseSignature> TseSignatures { get; set; }
        public DbSet<FinanzOnlineError> FinanzOnlineErrors { get; set; }
        public DbSet<PaymentLogEntry> PaymentLogs { get; set; }
        public DbSet<PaymentSession> PaymentSessions { get; set; }
        public DbSet<PaymentMetrics> PaymentMetrics { get; set; }
        
        // Masa siparişleri için yeni tablolar
        public DbSet<TableOrder> TableOrders { get; set; }
        public DbSet<TableOrderItem> TableOrderItems { get; set; }

        // FinanzOnline Audit
        public DbSet<FinanzOnlineSubmission> FinanzOnlineSubmissions { get; set; }

        // RKSV Receipt tables
        public DbSet<Receipt> Receipts { get; set; }
        public DbSet<ReceiptItem> ReceiptItems { get; set; }
        public DbSet<ReceiptTaxLine> ReceiptTaxLines { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // Enable pg_trgm extension for GIN indexes
            builder.HasPostgresExtension("pg_trgm");

            // ApplicationUser configuration
            builder.Entity<ApplicationUser>(entity =>
            {
                entity.Property(e => e.FirstName).IsRequired().HasMaxLength(50);
                entity.Property(e => e.LastName).IsRequired().HasMaxLength(50);
                entity.Property(e => e.EmployeeNumber).HasMaxLength(20);
                entity.Property(e => e.Role).HasMaxLength(20);
                entity.Property(e => e.TaxNumber).HasMaxLength(20);
                entity.Property(e => e.Notes).HasMaxLength(500);
                
                entity.HasIndex(e => e.EmployeeNumber).IsUnique();
                entity.HasIndex(e => e.TaxNumber).IsUnique();
            });

            // Product configuration - RKSV uyumlu güncellenmiş yapı
            builder.Entity<Product>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
                entity.Property(e => e.Description).HasColumnType("text");
                entity.Property(e => e.Price).HasColumnType("decimal(18,2)");
                entity.Property(e => e.Cost).HasColumnType("decimal(18,2)");
                entity.Property(e => e.TaxRate).HasColumnType("decimal(5,2)");
                entity.Property(e => e.TaxType).HasMaxLength(20);
                entity.Property(e => e.Category).HasMaxLength(100);
                // entity.Property(e => e.CategoryId).IsRequired(false); // Geçici olarak kapatıldı
                entity.Property(e => e.ImageUrl).HasMaxLength(500);
                entity.Property(e => e.StockQuantity).IsRequired();
                entity.Property(e => e.MinStockLevel).IsRequired();
                entity.Property(e => e.Unit).HasMaxLength(20);
                entity.Property(e => e.CreatedAt).IsRequired();
                entity.Property(e => e.UpdatedAt).IsRequired();
                entity.Property(e => e.CreatedBy).HasMaxLength(100);
                entity.Property(e => e.UpdatedBy).HasMaxLength(100);
                entity.Property(e => e.IsActive).IsRequired();
                
                // Indexes
                entity.HasIndex(e => e.Name);
                entity.HasIndex(e => e.Category);
                entity.HasIndex(e => e.TaxType);
                // entity.HasIndex(e => e.CategoryId); // Geçici olarak kapatıldı
                
                // Navigation property relationship - CategoryId foreign key
                // entity.HasOne<Category>()
                //       .WithMany(c => c.Products)
                //       .HasForeignKey(p => p.CategoryId)
                //       .OnDelete(DeleteBehavior.SetNull)
                //       .IsRequired(false); // Geçici olarak kapatıldı
                
                // Constraints
                entity.ToTable("products", t => 
                {
                    t.HasCheckConstraint("CK_products_price_positive", "\"Price\" >= 0");
                    t.HasCheckConstraint("CK_products_stock_quantity_non_negative", "\"StockQuantity\" >= 0");
                    t.HasCheckConstraint("CK_products_min_stock_level_non_negative", "\"MinStockLevel\" >= 0");
                    t.HasCheckConstraint("CK_products_cost_non_negative", "\"Cost\" >= 0");
                    t.HasCheckConstraint("CK_products_tax_rate_range", "\"TaxRate\" >= 0 AND \"TaxRate\" <= 100");
                });
            });

            // Customer configuration - Sadece mevcut sütunlar
            builder.Entity<Customer>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
                entity.Property(e => e.CustomerNumber).HasMaxLength(20);
                entity.Property(e => e.Email).HasMaxLength(100);
                entity.Property(e => e.Phone).HasMaxLength(20);
                entity.Property(e => e.Address).HasMaxLength(200);
                entity.Property(e => e.TaxNumber).HasMaxLength(20);
                entity.Property(e => e.Notes).HasMaxLength(500);
                
                entity.HasIndex(e => e.CustomerNumber).IsUnique();
                entity.HasIndex(e => e.Email).IsUnique();
                entity.HasIndex(e => e.TaxNumber).IsUnique();
            });

            // Invoice configuration - Sadece mevcut sütunlar
            builder.Entity<Invoice>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.InvoiceNumber).IsRequired().HasMaxLength(50);
                entity.Property(e => e.CustomerName).HasMaxLength(100);
                entity.Property(e => e.CustomerEmail).HasMaxLength(100);
                entity.Property(e => e.CustomerPhone).HasMaxLength(20);
                entity.Property(e => e.CustomerAddress).HasMaxLength(200);
                entity.Property(e => e.CustomerTaxNumber).HasMaxLength(20);
                entity.Property(e => e.CompanyName).IsRequired().HasMaxLength(100);
                entity.Property(e => e.CompanyTaxNumber).IsRequired().HasMaxLength(20);
                entity.Property(e => e.CompanyAddress).IsRequired().HasMaxLength(200);
                entity.Property(e => e.CompanyPhone).HasMaxLength(20);
                entity.Property(e => e.CompanyEmail).HasMaxLength(100);
                entity.Property(e => e.TseSignature).IsRequired().HasMaxLength(500);
                entity.Property(e => e.KassenId).IsRequired().HasMaxLength(50);
                entity.Property(e => e.PaymentReference).HasMaxLength(50);
                entity.Property(e => e.Subtotal).HasColumnType("decimal(18,2)");
                entity.Property(e => e.TaxAmount).HasColumnType("decimal(18,2)");
                entity.Property(e => e.TotalAmount).HasColumnType("decimal(18,2)");
                entity.Property(e => e.PaidAmount).HasColumnType("decimal(18,2)");
                entity.Property(e => e.RemainingAmount).HasColumnType("decimal(18,2)");
                
                entity.HasIndex(e => e.InvoiceNumber).IsUnique();
                // entity.HasIndex(e => e.InvoiceDate); // Covered by composite index
                // entity.HasIndex(e => e.Status); // Covered by composite index
                entity.HasIndex(e => e.TseSignature).IsUnique();

                // Composite Indexes for Pagination
                entity.HasIndex(e => new { e.IsActive, e.InvoiceDate }); // Default ASC/ASC, checking descending support...
                // EF Core 7+ supports IsDescending on HasIndex(). 
                // However, simple HasIndex can be optimized later if syntax is complex. 
                // Let's use standard indexes first or raw SQL in migration.
                // Re-reading Plan: "invoices(is_active, invoice_date desc)"
                // Using .IsDescending(false, true)
                entity.HasIndex(e => new { e.IsActive, e.InvoiceDate }).IsDescending(false, true);
                entity.HasIndex(e => new { e.Status, e.InvoiceDate }).IsDescending(false, true);

                // GIN Indexes for ILIKE search
                entity.HasIndex(e => e.InvoiceNumber).HasMethod("GIN").HasOperators("gin_trgm_ops");
                entity.HasIndex(e => e.CustomerName).HasMethod("GIN").HasOperators("gin_trgm_ops");
                entity.HasIndex(e => e.CompanyName).HasMethod("GIN").HasOperators("gin_trgm_ops");
            });

            // CashRegister configuration
            builder.Entity<CashRegister>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.RegisterNumber).IsRequired().HasMaxLength(20);
                entity.Property(e => e.Location).IsRequired().HasMaxLength(100);
                entity.Property(e => e.StartingBalance).HasColumnType("decimal(18,2)");
                entity.Property(e => e.CurrentBalance).HasColumnType("decimal(18,2)");
                entity.Property(e => e.LastBalanceUpdate).IsRequired();
                entity.Property(e => e.Status).IsRequired();
                entity.Property(e => e.CurrentUserId).HasMaxLength(450);
                
                entity.HasIndex(e => e.RegisterNumber).IsUnique();
                entity.HasIndex(e => e.Status);
                entity.HasIndex(e => e.CurrentUserId);
            });

            // CashRegisterTransaction configuration
            builder.Entity<CashRegisterTransaction>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.CashRegisterId).IsRequired().HasMaxLength(50);
                entity.Property(e => e.TransactionType).IsRequired();
                entity.Property(e => e.Amount).HasColumnType("decimal(18,2)");
                entity.Property(e => e.Description).IsRequired().HasMaxLength(500);
                entity.Property(e => e.UserId).IsRequired().HasMaxLength(450);
                entity.Property(e => e.TransactionDate).IsRequired();
            });

            // Category configuration
            builder.Entity<Category>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
                entity.Property(e => e.Description).HasMaxLength(500);
                entity.Property(e => e.Color).HasMaxLength(20);
                entity.Property(e => e.Icon).HasMaxLength(50);
                entity.Property(e => e.SortOrder);
                
                entity.HasIndex(e => e.Name).IsUnique();
                entity.HasIndex(e => e.SortOrder);
            });

            // PaymentDetails configuration
            builder.Entity<PaymentDetails>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.CustomerId).IsRequired();
                entity.Property(e => e.CustomerName).IsRequired().HasMaxLength(100);
                entity.Property(e => e.TotalAmount).HasColumnType("decimal(18,2)");
                entity.Property(e => e.TaxAmount).HasColumnType("decimal(18,2)");
                
                // Map PaymentMethodRaw to DB varchar column "PaymentMethod"
                entity.Property(e => e.PaymentMethodRaw)
                    .IsRequired()
                    .HasMaxLength(50)
                    .HasColumnName("PaymentMethod")
                    .HasColumnType("character varying");
                
                // Ignore the NotMapped enum helper property
                entity.Ignore(e => e.PaymentMethod);
                
                entity.Property(e => e.Notes).HasMaxLength(500);
                entity.Property(e => e.TransactionId).HasMaxLength(100);
                entity.Property(e => e.TseSignature).HasMaxLength(100);
                entity.Property(e => e.TaxDetails).HasColumnType("jsonb");
                entity.Property(e => e.PaymentItems).HasColumnType("jsonb");
                
                entity.HasIndex(e => e.CustomerId);
                entity.HasIndex(e => e.PaymentMethodRaw); // Index on raw property
                entity.HasIndex(e => e.CreatedAt);
                entity.HasIndex(e => e.TseSignature);
            });

            // PaymentItem configuration
            builder.Entity<PaymentItem>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.ProductId).IsRequired();
                entity.Property(e => e.ProductName).IsRequired().HasMaxLength(100);
                entity.Property(e => e.Quantity).IsRequired();
                entity.Property(e => e.UnitPrice).HasColumnType("decimal(18,2)");
                entity.Property(e => e.TotalPrice).HasColumnType("decimal(18,2)");
                entity.Property(e => e.TaxType).IsRequired().HasMaxLength(50);
                entity.Property(e => e.TaxRate).HasColumnType("decimal(5,4)");
                entity.Property(e => e.TaxAmount).HasColumnType("decimal(18,2)");
            });

            // InventoryItem configuration
            builder.Entity<InventoryItem>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.ProductId).IsRequired();
                entity.Property(e => e.CurrentStock).IsRequired();
                entity.Property(e => e.MinStockLevel).IsRequired();
                entity.Property(e => e.MaxStockLevel);
                entity.Property(e => e.ReorderPoint);
                entity.Property(e => e.UnitCost).HasColumnType("decimal(18,2)");
                entity.Property(e => e.LastRestocked);
                entity.Property(e => e.Notes).HasMaxLength(500);
                
                entity.HasIndex(e => e.ProductId).IsUnique();
                entity.HasIndex(e => e.CurrentStock);
                entity.HasIndex(e => e.MinStockLevel);
            });

            // InventoryTransaction configuration
            builder.Entity<InventoryTransaction>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.InventoryId).IsRequired();
                entity.Property(e => e.TransactionType).IsRequired();
                entity.Property(e => e.Quantity).IsRequired();
                entity.Property(e => e.UnitCost).HasColumnType("decimal(18,2)");
                entity.Property(e => e.TotalCost).HasColumnType("decimal(18,2)");
                entity.Property(e => e.Notes).HasMaxLength(500);
                entity.Property(e => e.TransactionDate).IsRequired();
                
                entity.HasIndex(e => e.InventoryId);
                entity.HasIndex(e => e.TransactionType);
                entity.HasIndex(e => e.TransactionDate);
            });

            // SystemSettings configuration
            builder.Entity<SystemSettings>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.CompanyName).IsRequired().HasMaxLength(100);
                entity.Property(e => e.CompanyAddress).IsRequired().HasMaxLength(200);
                entity.Property(e => e.CompanyPhone).HasMaxLength(20);
                entity.Property(e => e.CompanyEmail).HasMaxLength(100);
                entity.Property(e => e.CompanyTaxNumber).IsRequired().HasMaxLength(20);
                entity.Property(e => e.DefaultLanguage).IsRequired().HasMaxLength(10);
                entity.Property(e => e.DefaultCurrency).IsRequired().HasMaxLength(3);
                entity.Property(e => e.TimeZone).IsRequired().HasMaxLength(50);
                entity.Property(e => e.DateFormat).IsRequired().HasMaxLength(20);
                entity.Property(e => e.TimeFormat).IsRequired().HasMaxLength(20);
                entity.Property(e => e.DecimalPlaces).IsRequired();
                entity.Property(e => e.TaxRates).HasColumnType("jsonb");
                entity.Property(e => e.ReceiptTemplate).HasMaxLength(50);
                entity.Property(e => e.InvoicePrefix).HasMaxLength(10);
                entity.Property(e => e.ReceiptPrefix).HasMaxLength(10);
                entity.Property(e => e.AutoBackup).IsRequired();
                entity.Property(e => e.BackupFrequency).IsRequired();
                entity.Property(e => e.MaxBackupFiles).IsRequired();
                entity.Property(e => e.LastBackup);
                entity.Property(e => e.EmailNotifications).IsRequired();
                entity.Property(e => e.SmsNotifications).IsRequired();
                entity.Property(e => e.EmailSettings).HasColumnType("jsonb");
                entity.Property(e => e.SmsSettings).HasColumnType("jsonb");
                
                entity.HasIndex(e => e.CompanyTaxNumber).IsUnique();
            });

            // AuditLog configuration
            builder.Entity<AuditLog>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Action).IsRequired().HasMaxLength(50);
                entity.Property(e => e.EntityType).IsRequired().HasMaxLength(100);
                entity.Property(e => e.EntityId).IsRequired().HasMaxLength(100);
                entity.Property(e => e.UserId).IsRequired().HasMaxLength(450);
                entity.Property(e => e.OldValues).HasMaxLength(4000);
                entity.Property(e => e.NewValues).HasMaxLength(4000);
                entity.Property(e => e.IpAddress).HasMaxLength(45);
                entity.Property(e => e.UserAgent).HasMaxLength(500);
                entity.Property(e => e.Timestamp).IsRequired();
                
                entity.HasIndex(e => e.Timestamp);
                entity.HasIndex(e => e.Action);
                entity.HasIndex(e => e.EntityType);
                entity.HasIndex(e => e.EntityId);
                entity.HasIndex(e => e.UserId);
            });

            // CompanySettings configuration
            builder.Entity<CompanySettings>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.CompanyName).IsRequired().HasMaxLength(100);
                entity.Property(e => e.CompanyAddress).IsRequired().HasMaxLength(200);
                entity.Property(e => e.CompanyPhone).HasMaxLength(20);
                entity.Property(e => e.CompanyEmail).HasMaxLength(100);
                entity.Property(e => e.CompanyWebsite).HasMaxLength(100);
                entity.Property(e => e.CompanyTaxNumber).IsRequired().HasMaxLength(20);
                entity.Property(e => e.CompanyRegistrationNumber).HasMaxLength(20);
                entity.Property(e => e.CompanyVatNumber).HasMaxLength(20);
                entity.Property(e => e.CompanyLogo).HasMaxLength(100);
                entity.Property(e => e.CompanyDescription).HasMaxLength(500);
                entity.Property(e => e.BusinessHours).HasColumnType("jsonb");
                entity.Property(e => e.ContactPerson).HasMaxLength(100);
                entity.Property(e => e.ContactPhone).HasMaxLength(20);
                entity.Property(e => e.ContactEmail).HasMaxLength(100);
                entity.Property(e => e.BankName).HasMaxLength(100);
                entity.Property(e => e.BankAccountNumber).HasMaxLength(50);
                entity.Property(e => e.BankRoutingNumber).HasMaxLength(50);
                entity.Property(e => e.BankSwiftCode).HasMaxLength(20);
                entity.Property(e => e.PaymentTerms).HasMaxLength(50);
                entity.Property(e => e.Currency).IsRequired().HasMaxLength(3);
                entity.Property(e => e.Language).IsRequired().HasMaxLength(10);
                entity.Property(e => e.TimeZone).IsRequired().HasMaxLength(50);
                entity.Property(e => e.DateFormat).IsRequired().HasMaxLength(20);
                entity.Property(e => e.TimeFormat).IsRequired().HasMaxLength(20);
                entity.Property(e => e.DecimalPlaces).IsRequired();
                entity.Property(e => e.TaxCalculationMethod).IsRequired().HasMaxLength(50);
                entity.Property(e => e.InvoiceNumbering).IsRequired().HasMaxLength(50);
                entity.Property(e => e.ReceiptNumbering).IsRequired().HasMaxLength(50);
                entity.Property(e => e.DefaultPaymentMethod).IsRequired().HasMaxLength(50);
                
                entity.HasIndex(e => e.CompanyTaxNumber).IsUnique();
                entity.HasIndex(e => e.CompanyRegistrationNumber).IsUnique();
                entity.HasIndex(e => e.CompanyVatNumber).IsUnique();
            });

            // LocalizationSettings configuration
            builder.Entity<LocalizationSettings>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.DefaultLanguage).IsRequired().HasMaxLength(10);
                entity.Property(e => e.SupportedLanguages).HasColumnType("jsonb");
                entity.Property(e => e.DefaultCurrency).IsRequired().HasMaxLength(3);
                entity.Property(e => e.SupportedCurrencies).HasColumnType("jsonb");
                entity.Property(e => e.DefaultTimeZone).IsRequired().HasMaxLength(50);
                entity.Property(e => e.SupportedTimeZones).HasColumnType("jsonb");
                entity.Property(e => e.DefaultDateFormat).IsRequired().HasMaxLength(20);
                entity.Property(e => e.DefaultTimeFormat).IsRequired().HasMaxLength(20);
                entity.Property(e => e.DefaultDecimalPlaces).IsRequired();
                entity.Property(e => e.NumberFormat).IsRequired().HasMaxLength(50);
                entity.Property(e => e.DateFormatOptions).HasColumnType("jsonb");
                entity.Property(e => e.TimeFormatOptions).HasColumnType("jsonb");
                entity.Property(e => e.CurrencySymbols).HasColumnType("jsonb");
                
                entity.HasIndex(e => e.DefaultLanguage);
                entity.HasIndex(e => e.DefaultCurrency);
            });

            // ReceiptTemplate configuration
            builder.Entity<ReceiptTemplate>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.TemplateName).IsRequired().HasMaxLength(100);
                entity.Property(e => e.TemplateType).IsRequired().HasMaxLength(50);
                entity.Property(e => e.Language).IsRequired().HasMaxLength(10);
                entity.Property(e => e.HeaderTemplate).HasMaxLength(2000);
                entity.Property(e => e.FooterTemplate).HasMaxLength(2000);
                entity.Property(e => e.ItemTemplate).HasMaxLength(1000);
                entity.Property(e => e.TaxTemplate).HasMaxLength(500);
                entity.Property(e => e.TotalTemplate).HasMaxLength(500);
                entity.Property(e => e.PaymentTemplate).HasMaxLength(500);
                entity.Property(e => e.CustomerTemplate).HasMaxLength(1000);
                entity.Property(e => e.CompanyTemplate).HasMaxLength(1000);
                entity.Property(e => e.CustomFields).HasColumnType("jsonb");
                entity.Property(e => e.IsDefault).IsRequired();
                
                entity.HasIndex(e => e.Language);
                entity.HasIndex(e => e.TemplateType);
                entity.HasIndex(e => e.TemplateName);
                entity.HasIndex(e => e.IsDefault);
            });

            // UserSettings configuration
            builder.Entity<UserSettings>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.UserId).IsRequired().HasMaxLength(450);
                entity.Property(e => e.Language).IsRequired().HasMaxLength(10);
                entity.Property(e => e.Currency).IsRequired().HasMaxLength(3);
                entity.Property(e => e.DateFormat).IsRequired().HasMaxLength(20);
                entity.Property(e => e.TimeFormat).IsRequired().HasMaxLength(10);
                entity.Property(e => e.CashRegisterId).HasMaxLength(100);
                entity.Property(e => e.DefaultTaxRate).IsRequired();
                entity.Property(e => e.EnableDiscounts).IsRequired();
                entity.Property(e => e.EnableCoupons).IsRequired();
                entity.Property(e => e.AutoPrintReceipts).IsRequired();
                entity.Property(e => e.ReceiptHeader).HasMaxLength(200);
                entity.Property(e => e.TseDeviceId).HasMaxLength(100);
                entity.Property(e => e.FinanzOnlineEnabled).IsRequired();
                entity.Property(e => e.FinanzOnlineUsername).HasMaxLength(100);
                entity.Property(e => e.SessionTimeout).IsRequired();
                entity.Property(e => e.RequirePinForRefunds).IsRequired();
                entity.Property(e => e.MaxDiscountPercentage).IsRequired();
                entity.Property(e => e.Theme).IsRequired().HasMaxLength(10);
                entity.Property(e => e.CompactMode).IsRequired();
                entity.Property(e => e.ShowProductImages).IsRequired();
                entity.Property(e => e.EnableNotifications).IsRequired();
                entity.Property(e => e.LowStockAlert).IsRequired();
                entity.Property(e => e.DailyReportEmail).HasMaxLength(255);
                entity.Property(e => e.DefaultPaymentMethod).IsRequired().HasMaxLength(20);
                entity.Property(e => e.DefaultTableNumber).HasMaxLength(10);
                entity.Property(e => e.DefaultWaiterName).HasMaxLength(100);
                entity.Property(e => e.CreatedAt).IsRequired();
                entity.Property(e => e.UpdatedAt).IsRequired();
                
                entity.HasIndex(e => e.UserId).IsUnique();
                entity.HasIndex(e => e.Language);
                entity.HasIndex(e => e.Currency);
                
                // Foreign key relationship
                entity.HasOne(e => e.User)
                      .WithMany()
                      .HasForeignKey(e => e.UserId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // GeneratedReceipt configuration
            builder.Entity<GeneratedReceipt>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.TemplateId).IsRequired();
                entity.Property(e => e.Language).IsRequired().HasMaxLength(10);
                entity.Property(e => e.TemplateType).IsRequired().HasMaxLength(50);
                entity.Property(e => e.GeneratedContent).IsRequired().HasColumnType("text");
                entity.Property(e => e.GeneratedAt).IsRequired();
                
                entity.HasIndex(e => e.TemplateId);
                entity.HasIndex(e => e.Language);
                entity.HasIndex(e => e.TemplateType);
                entity.HasIndex(e => e.GeneratedAt);
                
                // Foreign key relationship
                entity.HasOne(e => e.Template)
                      .WithMany()
                      .HasForeignKey(e => e.TemplateId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // TSE Device configuration
            builder.Entity<TseDevice>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.SerialNumber).IsRequired().HasMaxLength(100);
                entity.Property(e => e.DeviceType).IsRequired().HasMaxLength(50);
                entity.Property(e => e.CertificateStatus).HasMaxLength(50);
                entity.Property(e => e.MemoryStatus).HasMaxLength(50);
                entity.Property(e => e.KassenId).HasMaxLength(50);
                entity.Property(e => e.FinanzOnlineUsername).HasMaxLength(100);
                entity.HasIndex(e => e.SerialNumber).IsUnique();
            });

            // DailyClosing configuration
            builder.Entity<DailyClosing>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.CashRegisterId).IsRequired().HasMaxLength(50);
                entity.Property(e => e.UserId).IsRequired();
                entity.Property(e => e.ClosingType).IsRequired().HasMaxLength(20);
                entity.Property(e => e.TotalAmount).HasColumnType("decimal(18,2)");
                entity.Property(e => e.TotalTaxAmount).HasColumnType("decimal(18,2)");
                entity.Property(e => e.TseSignature).IsRequired().HasMaxLength(500);
                entity.Property(e => e.Status).IsRequired().HasMaxLength(20);
                entity.Property(e => e.FinanzOnlineStatus).HasMaxLength(20);
                entity.Property(e => e.FinanzOnlineError).HasMaxLength(500);
                entity.Property(e => e.FinanzOnlineReferenceId).HasMaxLength(100);
                entity.HasIndex(e => new { e.CashRegisterId, e.ClosingDate, e.ClosingType });
                entity.HasOne(e => e.User).WithMany().HasForeignKey(e => e.UserId);
                entity.HasOne(e => e.CashRegister).WithMany().HasForeignKey(e => e.CashRegisterId);
            });

            // TseSignature configuration
            builder.Entity<TseSignature>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Signature).IsRequired().HasMaxLength(500);
                entity.Property(e => e.CashRegisterId).IsRequired().HasMaxLength(50);
                entity.Property(e => e.InvoiceNumber).IsRequired().HasMaxLength(100);
                entity.Property(e => e.Amount).HasColumnType("decimal(18,2)");
                entity.Property(e => e.SignatureType).IsRequired().HasMaxLength(50);
                entity.Property(e => e.TseDeviceId).HasMaxLength(100);
                entity.Property(e => e.CertificateNumber).HasMaxLength(100);
                entity.HasIndex(e => e.Signature).IsUnique();
                entity.HasIndex(e => new { e.CashRegisterId, e.CreatedAt });
                entity.HasOne(e => e.CashRegister).WithMany().HasForeignKey(e => e.CashRegisterId);
            });

            // FinanzOnlineError configuration
            builder.Entity<FinanzOnlineError>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.ErrorType).IsRequired().HasMaxLength(50);
                entity.Property(e => e.ErrorMessage).IsRequired().HasMaxLength(500);
                entity.Property(e => e.ReferenceId).HasMaxLength(100);
                entity.Property(e => e.ResolvedBy).HasMaxLength(100);
                entity.Property(e => e.ResolutionNotes).HasMaxLength(500);
                entity.Property(e => e.CashRegisterId).HasMaxLength(50);
                entity.Property(e => e.InvoiceNumber).HasMaxLength(100);
                entity.Property(e => e.Status).HasMaxLength(20);
                entity.HasIndex(e => new { e.ErrorType, e.OccurredAt });
                entity.HasIndex(e => e.ReferenceId);
                entity.HasOne(e => e.CashRegister).WithMany().HasForeignKey(e => e.CashRegisterId);
            });

            // Cart configuration - Güvenlik ve performans için index'ler
            builder.Entity<Cart>(entity =>
            {
                entity.HasKey(e => e.CartId);
                entity.Property(e => e.CartId).IsRequired().HasMaxLength(50);
                entity.Property(e => e.TableNumber);
                entity.Property(e => e.WaiterName).HasMaxLength(100);
                entity.Property(e => e.CustomerId);
                entity.Property(e => e.UserId).IsRequired().HasMaxLength(450);
                entity.Property(e => e.Notes).HasMaxLength(500);
                entity.Property(e => e.ExpiresAt).IsRequired();
                entity.Property(e => e.Status).IsRequired();
                
                // Foreign key relationships - Model'de ForeignKey attribute kullanıldığı için sadece User konfigürasyonu
                entity.HasOne(e => e.User)
                    .WithMany()
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
                
                // Customer navigation property model'de ForeignKey attribute ile konfigüre edildi
                // Bu şekilde shadow property oluşmaz
            });

            // CartItem configuration
            builder.Entity<CartItem>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.CartId).IsRequired().HasMaxLength(50);
                entity.Property(e => e.ProductId).IsRequired();
                entity.Property(e => e.Quantity).IsRequired();
                entity.Property(e => e.UnitPrice).IsRequired().HasColumnType("decimal(18,2)");
                entity.Property(e => e.Notes).HasMaxLength(500);
                
                // Foreign key relationships
                entity.HasOne(e => e.Cart)
                    .WithMany(c => c.Items)
                    .HasForeignKey(e => e.CartId)
                    .OnDelete(DeleteBehavior.Cascade);
                
                // Product relationship removed to prevent shadow property conflicts
                // entity.HasOne(e => e.Product)
                //     .WithMany()
                //     .HasForeignKey(e => e.ProductId)
                //     .OnDelete(DeleteBehavior.Restrict);
            });

            // Order configuration
            builder.Entity<Order>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.OrderId).IsRequired().HasMaxLength(50);
                entity.Property(e => e.TableNumber);
                entity.Property(e => e.WaiterName).HasMaxLength(100);
                entity.Property(e => e.CustomerName).HasMaxLength(100);
                entity.Property(e => e.CustomerPhone).HasMaxLength(20);
                entity.Property(e => e.Notes).HasMaxLength(500);
                entity.Property(e => e.OrderDate).IsRequired();
                entity.Property(e => e.Status).IsRequired();
                entity.Property(e => e.Subtotal).HasColumnType("decimal(18,2)");
                entity.Property(e => e.TaxAmount).HasColumnType("decimal(18,2)");
                entity.Property(e => e.DiscountAmount).HasColumnType("decimal(18,2)");
                entity.Property(e => e.TotalAmount).HasColumnType("decimal(18,2)");
                entity.Property(e => e.CustomerId);
            });

            // OrderItem configuration
            builder.Entity<OrderItem>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.OrderId).IsRequired().HasMaxLength(50);
                entity.Property(e => e.ProductId).IsRequired();
                entity.Property(e => e.ProductName).IsRequired().HasMaxLength(100);
                entity.Property(e => e.Quantity).IsRequired();
                entity.Property(e => e.UnitPrice).HasColumnType("decimal(18,2)");
                entity.Property(e => e.TaxRate).HasColumnType("decimal(5,2)");
                entity.Property(e => e.TaxAmount).HasColumnType("decimal(18,2)");
                entity.Property(e => e.DiscountAmount).HasColumnType("decimal(18,2)");
                entity.Property(e => e.TotalAmount).HasColumnType("decimal(18,2)");
                entity.Property(e => e.SpecialNotes).HasMaxLength(500);
                entity.Property(e => e.ProductDescription).HasMaxLength(500);
                entity.Property(e => e.ProductCategory).HasMaxLength(100);
            });

            // TableOrder configuration - Masa siparişleri için - Basit konfigürasyon
            builder.Entity<TableOrder>(entity =>
            {
                entity.HasKey(e => e.TableOrderId); // TableOrderId'yi primary key yap
                entity.Ignore(e => e.Id); // BaseEntity'den gelen Id property'sini ignore et
                entity.Property(e => e.CreatedAt).HasColumnName("CreatedAt"); // BaseEntity'den gelen CreatedAt'i CreatedAt sütununa map et
                entity.Property(e => e.UpdatedAt).HasColumnName("UpdatedAt"); // BaseEntity'den gelen UpdatedAt'i UpdatedAt sütununa map et
                entity.Property(e => e.CreatedBy).HasColumnName("CreatedBy"); // BaseEntity'den gelen CreatedBy'i CreatedBy sütununa map et
                entity.Property(e => e.UpdatedBy).HasColumnName("UpdatedBy"); // BaseEntity'den gelen UpdatedBy'i UpdatedBy sütununa map et
                entity.Property(e => e.IsActive).HasColumnName("IsActive"); // BaseEntity'den gelen IsActive'i IsActive sütununa map et
                entity.Property(e => e.TableOrderId).IsRequired().HasMaxLength(50);
                entity.Property(e => e.TableNumber).IsRequired();
                entity.Property(e => e.UserId).IsRequired().HasMaxLength(450);
                entity.Property(e => e.WaiterName).HasMaxLength(100);
                entity.Property(e => e.CustomerName).HasMaxLength(100);
                entity.Property(e => e.CustomerPhone).HasMaxLength(20);
                entity.Property(e => e.Notes).HasMaxLength(500);
                entity.Property(e => e.Status).IsRequired();
                entity.Property(e => e.Subtotal).HasColumnType("decimal(18,2)");
                entity.Property(e => e.TaxAmount).HasColumnType("decimal(18,2)");
                entity.Property(e => e.TotalAmount).HasColumnType("decimal(18,2)");
                entity.Property(e => e.OrderStartTime).IsRequired();
                entity.Property(e => e.CartId).HasMaxLength(50);
                entity.Property(e => e.SessionId).HasMaxLength(100);
                entity.Property(e => e.StatusHistory).HasMaxLength(1000);
            });

            // TableOrderItem configuration - Basit konfigürasyon
            builder.Entity<TableOrderItem>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasColumnName("Id"); // Id property'sini Id sütununa map et (PostgreSQL case sensitivity için)
                entity.Property(e => e.CreatedAt).HasColumnName("CreatedAt"); // BaseEntity'den gelen CreatedAt'i CreatedAt sütununa map et
                entity.Property(e => e.UpdatedAt).HasColumnName("UpdatedAt"); // BaseEntity'den gelen UpdatedAt'i UpdatedAt sütununa map et
                entity.Property(e => e.CreatedBy).HasColumnName("CreatedBy"); // BaseEntity'den gelen CreatedBy'i CreatedBy sütununa map et
                entity.Property(e => e.UpdatedBy).HasColumnName("UpdatedBy"); // BaseEntity'den gelen UpdatedBy'i UpdatedBy sütununa map et
                entity.Property(e => e.IsActive).HasColumnName("IsActive"); // BaseEntity'den gelen IsActive'i IsActive sütununa map et
                entity.Property(e => e.TableOrderId).IsRequired().HasMaxLength(50);
                entity.Property(e => e.ProductId).IsRequired();
                entity.Property(e => e.ProductName).IsRequired().HasMaxLength(200);
                entity.Property(e => e.Quantity).IsRequired();
                entity.Property(e => e.UnitPrice).HasColumnType("decimal(18,2)");
                entity.Property(e => e.TotalPrice).HasColumnType("decimal(18,2)");
                entity.Property(e => e.Notes).HasMaxLength(500);
                entity.Property(e => e.TaxType).IsRequired().HasMaxLength(20);
                entity.Property(e => e.TaxRate).HasColumnType("decimal(5,2)");
                entity.Property(e => e.Status).IsRequired();
                
                // Foreign key ilişkisi - TableOrderId string olarak tanımlanmış
                entity.HasOne(e => e.TableOrder)
                      .WithMany(o => o.Items)
                      .HasForeignKey(e => e.TableOrderId)
                      .HasPrincipalKey(o => o.TableOrderId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // Receipt configuration
            builder.Entity<Receipt>(entity =>
            {
               entity.HasIndex(e => e.ReceiptNumber).IsUnique();
               entity.HasIndex(e => e.PaymentId);
            });

            Console.WriteLine("AppDbContext model configuration completed with TableOrder support");
        }
    }
}

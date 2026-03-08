using System.Text.Json;
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
        public DbSet<CartItemModifier> CartItemModifiers { get; set; }
        public DbSet<Order> Orders { get; set; }
        public DbSet<OrderItem> OrderItems { get; set; }
        public DbSet<CashRegister> CashRegisters { get; set; }
        public DbSet<CashRegisterTransaction> CashRegisterTransactions { get; set; }
        public DbSet<Category> Categories { get; set; }
        public DbSet<PaymentDetails> PaymentDetails { get; set; }
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
        public DbSet<TableOrderItemModifier> TableOrderItemModifiers { get; set; }

        // FinanzOnline Audit
        public DbSet<FinanzOnlineSubmission> FinanzOnlineSubmissions { get; set; }

        // RKSV Receipt tables
        public DbSet<Receipt> Receipts { get; set; }
        public DbSet<ReceiptItem> ReceiptItems { get; set; }
        public DbSet<ReceiptTaxLine> ReceiptTaxLines { get; set; }

        // Extra Zutaten (Add-on groups and assignments; add-on products in addon_group_products)
        public DbSet<ProductModifierGroup> ProductModifierGroups { get; set; }
        public DbSet<ProductModifierGroupAssignment> ProductModifierGroupAssignments { get; set; }
        /// <summary>Faz 1: Grup içi product referansları (suggested add-on); fiyat Product'ta.</summary>
        public DbSet<AddOnGroupProduct> AddOnGroupProducts { get; set; }

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
                entity.Property(e => e.DeactivatedBy).HasMaxLength(450);
                entity.Property(e => e.DeactivationReason).HasMaxLength(500);
                
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
                entity.Property(e => e.TaxType).IsRequired(); // Map to integer column
                entity.Property(e => e.Category).HasMaxLength(100);
                entity.Property(e => e.CategoryId).HasColumnName("category_id").HasColumnType("uuid").IsRequired();
                entity.Property(e => e.ImageUrl).HasMaxLength(500);
                entity.Property(e => e.StockQuantity).IsRequired();
                entity.Property(e => e.MinStockLevel).IsRequired();
                entity.Property(e => e.Unit).HasMaxLength(20);
                entity.Property(e => e.CreatedAt).IsRequired();
                entity.Property(e => e.UpdatedAt).IsRequired();
                entity.Property(e => e.CreatedBy).HasMaxLength(100);
                entity.Property(e => e.UpdatedBy).HasMaxLength(100);
                entity.Property(e => e.IsActive).IsRequired();
                entity.Property(e => e.IsSellableAddOn).HasColumnName("is_sellable_addon").HasDefaultValue(false);
                
                // Indexes
                entity.HasIndex(e => e.Name);
                entity.HasIndex(e => e.Category);
                entity.HasIndex(e => e.TaxType);
                entity.HasIndex(e => e.CategoryId);
                entity.HasIndex(e => e.Barcode).IsUnique();
                
                // Navigation property relationship - CategoryId foreign key (required)
                entity.HasOne(p => p.CategoryNavigation)
                      .WithMany(c => c.Products)
                      .HasForeignKey(p => p.CategoryId)
                      .OnDelete(DeleteBehavior.Restrict)
                      .IsRequired();
                
                // Constraints
                entity.ToTable("products", t => 
                {
                    t.HasCheckConstraint("CK_products_price_positive", "price >= 0");
                    t.HasCheckConstraint("CK_products_stock_quantity_non_negative", "stock_quantity >= 0");
                    t.HasCheckConstraint("CK_products_min_stock_level_non_negative", "min_stock_level >= 0");
                    t.HasCheckConstraint("CK_products_cost_non_negative", "cost >= 0");
                    t.HasCheckConstraint("CK_products_tax_rate_range", "tax_rate >= 0 AND tax_rate <= 100");
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
                entity.Property(e => e.InvoiceItems)
                    .HasColumnType("jsonb")
                    .HasConversion(
                        v => v == null ? "[]" : v.RootElement.GetRawText(),
                        v => string.IsNullOrEmpty(v) ? JsonDocument.Parse("[]") : JsonDocument.Parse(v));
                entity.Property(e => e.TaxDetails)
                    .HasColumnType("jsonb")
                    .HasConversion(
                        v => v.RootElement.GetRawText(),
                        v => string.IsNullOrEmpty(v) ? JsonDocument.Parse("{}") : JsonDocument.Parse(v));
                
                entity.HasIndex(e => e.InvoiceNumber).IsUnique();
                // entity.HasIndex(e => e.InvoiceDate); // Covered by composite index
                // entity.HasIndex(e => e.Status); // Covered by composite index
                entity.HasIndex(e => e.TseSignature)
                    .IsUnique()
                    .HasFilter("\"TseSignature\" != ''");

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

                // Partial unique index: one Invoice per source payment (nulls allowed for manual invoices)
                entity.HasIndex(e => e.SourcePaymentId)
                    .IsUnique()
                    .HasFilter("\"SourcePaymentId\" IS NOT NULL");
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
                entity.Property(e => e.Id).HasColumnType("uuid").HasColumnName("id");
                entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
                entity.Property(e => e.Description).HasMaxLength(500);
                entity.Property(e => e.Color).HasMaxLength(20);
                entity.Property(e => e.Icon).HasMaxLength(50);
                entity.Property(e => e.SortOrder);
                entity.Property(e => e.VatRate).HasColumnType("decimal(5,2)").IsRequired();
                
                entity.HasIndex(e => e.Name).IsUnique();
                entity.HasIndex(e => e.SortOrder);
            });

            // ProductModifierGroup configuration (Extra Zutaten)
            builder.Entity<ProductModifierGroup>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
                entity.Property(e => e.MinSelections);
                entity.Property(e => e.MaxSelections);
                entity.Property(e => e.IsRequired);
                entity.Property(e => e.SortOrder);
                entity.ToTable("product_modifier_groups");
            });

            // ProductModifierGroupAssignment (Product M:N ModifierGroup)
            builder.Entity<ProductModifierGroupAssignment>(entity =>
            {
                entity.HasKey(e => new { e.ProductId, e.ModifierGroupId });
                entity.HasOne(e => e.Product)
                    .WithMany(p => p.ModifierGroupAssignments)
                    .HasForeignKey(e => e.ProductId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(e => e.ModifierGroup)
                    .WithMany(g => g.ProductAssignments)
                    .HasForeignKey(e => e.ModifierGroupId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.ToTable("product_modifier_group_assignments");
            });

            // AddOnGroupProduct (Faz 1: group -> product ref; price on Product)
            builder.Entity<AddOnGroupProduct>(entity =>
            {
                entity.HasKey(e => new { e.ModifierGroupId, e.ProductId });
                entity.Property(e => e.ModifierGroupId).HasColumnName("modifier_group_id").HasColumnType("uuid").IsRequired();
                entity.Property(e => e.ProductId).HasColumnName("product_id").HasColumnType("uuid").IsRequired();
                entity.Property(e => e.SortOrder).HasColumnName("sort_order").IsRequired();
                entity.HasOne(e => e.ModifierGroup)
                    .WithMany(g => g.AddOnGroupProducts)
                    .HasForeignKey(e => e.ModifierGroupId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(e => e.Product)
                    .WithMany()
                    .HasForeignKey(e => e.ProductId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.ToTable("addon_group_products");
                entity.HasIndex(e => e.ProductId);
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
                entity.Property(e => e.TseSignature).HasMaxLength(2000);
                entity.Property(e => e.PrevSignatureValueUsed).HasMaxLength(2000);
                entity.Property(e => e.TaxDetails)
                    .HasColumnType("jsonb")
                    .HasConversion(
                        v => v.RootElement.GetRawText(),
                        v => string.IsNullOrEmpty(v) ? JsonDocument.Parse("{}") : JsonDocument.Parse(v));
                entity.Property(e => e.PaymentItems)
                    .HasColumnType("jsonb")
                    .HasConversion(
                        v => v.RootElement.GetRawText(),
                        v => string.IsNullOrEmpty(v) ? JsonDocument.Parse("[]") : JsonDocument.Parse(v));
                
                entity.HasIndex(e => e.CustomerId);
                entity.HasIndex(e => e.PaymentMethodRaw); // Index on raw property
                entity.HasIndex(e => e.CreatedAt);
                entity.HasIndex(e => e.TseSignature);
            });

            // PaymentItem: not mapped; single source of truth is payment_details.PaymentItems (JSON).
            builder.Ignore<PaymentItem>();

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
                entity.Property(e => e.TaxRates)
                    .HasColumnType("jsonb")
                    .HasConversion(
                        v => JsonSerializer.Serialize(v),
                        v => string.IsNullOrEmpty(v) ? new Dictionary<string, decimal>() : JsonSerializer.Deserialize<Dictionary<string, decimal>>(v)!);
                entity.Property(e => e.ReceiptTemplate).HasMaxLength(50);
                entity.Property(e => e.InvoicePrefix).HasMaxLength(10);
                entity.Property(e => e.ReceiptPrefix).HasMaxLength(10);
                entity.Property(e => e.AutoBackup).IsRequired();
                entity.Property(e => e.BackupFrequency).IsRequired();
                entity.Property(e => e.MaxBackupFiles).IsRequired();
                entity.Property(e => e.LastBackup);
                entity.Property(e => e.EmailNotifications).IsRequired();
                entity.Property(e => e.SmsNotifications).IsRequired();
                entity.Property(e => e.EmailSettings)
                    .HasColumnType("jsonb")
                    .HasConversion(
                        v => v == null ? "{}" : JsonSerializer.Serialize(v),
                        v => string.IsNullOrEmpty(v) ? null : JsonSerializer.Deserialize<Dictionary<string, string>>(v));
                entity.Property(e => e.SmsSettings)
                    .HasColumnType("jsonb")
                    .HasConversion(
                        v => v == null ? "{}" : JsonSerializer.Serialize(v),
                        v => string.IsNullOrEmpty(v) ? null : JsonSerializer.Deserialize<Dictionary<string, string>>(v));
                
                entity.HasIndex(e => e.CompanyTaxNumber).IsUnique();
            });

            // AuditLog: table audit_logs with snake_case columns everywhere (PostgreSQL default; no quoted identifiers).
            // User navigation ignored so EF never joins AspNetUsers.
            builder.Entity<AuditLog>(entity =>
            {
                entity.ToTable("audit_logs");
                entity.Ignore(e => e.User);
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasColumnName("id");
                entity.Property(e => e.CreatedAt).HasColumnName("created_at");
                entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
                entity.Property(e => e.CreatedBy).HasColumnName("created_by");
                entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");
                entity.Property(e => e.IsActive).HasColumnName("is_active");
                entity.Property(e => e.Action).IsRequired().HasMaxLength(50).HasColumnName("action");
                entity.Property(e => e.Amount).HasColumnType("decimal(18,2)").HasColumnName("amount");
                entity.Property(e => e.CorrelationId).HasMaxLength(100).HasColumnName("correlation_id");
                entity.Property(e => e.Description).HasMaxLength(500).HasColumnName("description");
                entity.Property(e => e.Endpoint).HasMaxLength(100).HasColumnName("endpoint");
                entity.Property(e => e.EntityId).IsRequired(false).HasColumnName("entity_id");
                entity.Property(e => e.EntityName).HasMaxLength(100).HasColumnName("entity_name");
                entity.Property(e => e.EntityType).IsRequired().HasMaxLength(100).HasColumnName("entity_type");
                entity.Property(e => e.ErrorDetails).HasMaxLength(500).HasColumnName("error_details");
                entity.Property(e => e.HttpMethod).HasMaxLength(10).HasColumnName("http_method");
                entity.Property(e => e.HttpStatusCode).HasColumnName("http_status_code");
                entity.Property(e => e.IpAddress).HasMaxLength(45).HasColumnName("ip_address");
                entity.Property(e => e.NewValues).HasMaxLength(4000).HasColumnName("new_values");
                entity.Property(e => e.Notes).HasMaxLength(500).HasColumnName("notes");
                entity.Property(e => e.OldValues).HasMaxLength(4000).HasColumnName("old_values");
                entity.Property(e => e.PaymentMethod).HasMaxLength(50).HasColumnName("payment_method");
                entity.Property(e => e.ProcessingTimeMs).HasColumnName("processing_time_ms");
                entity.Property(e => e.RequestData).HasMaxLength(4000).HasColumnName("request_data");
                entity.Property(e => e.ResponseData).HasMaxLength(4000).HasColumnName("response_data");
                entity.Property(e => e.SessionId).IsRequired().HasMaxLength(100).HasColumnName("session_id");
                entity.Property(e => e.Status).IsRequired().HasColumnName("status");
                entity.Property(e => e.Timestamp).IsRequired().HasColumnName("timestamp");
                entity.Property(e => e.TransactionId).HasMaxLength(100).HasColumnName("transaction_id");
                entity.Property(e => e.TseSignature).HasMaxLength(500).HasColumnName("tse_signature");
                entity.Property(e => e.UserAgent).HasMaxLength(500).HasColumnName("user_agent");
                entity.Property(e => e.UserId).IsRequired().HasMaxLength(450).HasColumnName("user_id");
                entity.Property(e => e.UserRole).IsRequired().HasMaxLength(50).HasColumnName("user_role");

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
                entity.Property(e => e.BusinessHours)
                    .HasColumnType("jsonb")
                    .HasConversion(
                        v => JsonSerializer.Serialize(v),
                        v => string.IsNullOrEmpty(v) ? new Dictionary<string, string>() : JsonSerializer.Deserialize<Dictionary<string, string>>(v)!);
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
                entity.Property(e => e.SupportedLanguages)
                    .HasColumnType("jsonb")
                    .HasConversion(
                        v => JsonSerializer.Serialize(v),
                        v => string.IsNullOrEmpty(v) ? new List<string>() : JsonSerializer.Deserialize<List<string>>(v)!);
                entity.Property(e => e.DefaultCurrency).IsRequired().HasMaxLength(3);
                entity.Property(e => e.SupportedCurrencies)
                    .HasColumnType("jsonb")
                    .HasConversion(
                        v => JsonSerializer.Serialize(v),
                        v => string.IsNullOrEmpty(v) ? new List<string>() : JsonSerializer.Deserialize<List<string>>(v)!);
                entity.Property(e => e.DefaultTimeZone).IsRequired().HasMaxLength(50);
                entity.Property(e => e.SupportedTimeZones)
                    .HasColumnType("jsonb")
                    .HasConversion(
                        v => JsonSerializer.Serialize(v),
                        v => string.IsNullOrEmpty(v) ? new List<string>() : JsonSerializer.Deserialize<List<string>>(v)!);
                entity.Property(e => e.DefaultDateFormat).IsRequired().HasMaxLength(20);
                entity.Property(e => e.DefaultTimeFormat).IsRequired().HasMaxLength(20);
                entity.Property(e => e.DefaultDecimalPlaces).IsRequired();
                entity.Property(e => e.NumberFormat).IsRequired().HasMaxLength(50);
                entity.Property(e => e.DateFormatOptions)
                    .HasColumnType("jsonb")
                    .HasConversion(
                        v => JsonSerializer.Serialize(v),
                        v => string.IsNullOrEmpty(v) ? new Dictionary<string, string>() : JsonSerializer.Deserialize<Dictionary<string, string>>(v)!);
                entity.Property(e => e.TimeFormatOptions)
                    .HasColumnType("jsonb")
                    .HasConversion(
                        v => JsonSerializer.Serialize(v),
                        v => string.IsNullOrEmpty(v) ? new Dictionary<string, string>() : JsonSerializer.Deserialize<Dictionary<string, string>>(v)!);
                entity.Property(e => e.CurrencySymbols)
                    .HasColumnType("jsonb")
                    .HasConversion(
                        v => JsonSerializer.Serialize(v),
                        v => string.IsNullOrEmpty(v) ? new Dictionary<string, string>() : JsonSerializer.Deserialize<Dictionary<string, string>>(v)!);
                
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
                entity.Property(e => e.CustomFields)
                    .HasColumnType("jsonb")
                    .HasConversion(
                        v => v == null ? "{}" : JsonSerializer.Serialize(v),
                        v => string.IsNullOrEmpty(v) ? null : JsonSerializer.Deserialize<Dictionary<string, string>>(v));
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

            builder.Entity<CartItemModifier>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.CartItemId).IsRequired();
                entity.Property(e => e.ModifierId).IsRequired();
                entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
                entity.Property(e => e.Price).HasColumnType("decimal(18,2)");
                entity.Property(e => e.Quantity).IsRequired();
                entity.HasOne(e => e.CartItem)
                    .WithMany(ci => ci.Modifiers)
                    .HasForeignKey(e => e.CartItemId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.ToTable("cart_item_modifiers");
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
                entity.Property(e => e.ProductCategory).HasMaxLength(100);
                
                // Add mapping to prevent shadow OrderId1 property
                entity.HasOne(e => e.Order)
                      .WithMany(o => o.Items)
                      .HasForeignKey(e => e.OrderId)
                      .HasPrincipalKey(o => o.OrderId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // TableOrder configuration - Masa siparişleri için - Basit konfigürasyon
            builder.Entity<TableOrder>(entity =>
            {
                entity.HasKey(e => e.TableOrderId); // TableOrderId'yi primary key yap
                entity.Property(e => e.Id).HasColumnName("Id"); // PostgreSQL: migration "Id" oluşturdu, BaseEntity "id" bekliyordu
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
                entity.Property(e => e.TaxType).IsRequired();
                entity.Property(e => e.TaxRate).HasColumnType("decimal(5,2)");
                entity.Property(e => e.Status).IsRequired();
                
                // Foreign key ilişkisi - TableOrderId string olarak tanımlanmış
                entity.HasOne(e => e.TableOrder)
                      .WithMany(o => o.Items)
                      .HasForeignKey(e => e.TableOrderId)
                      .HasPrincipalKey(o => o.TableOrderId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<TableOrderItemModifier>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.TableOrderItemId).IsRequired();
                entity.Property(e => e.ModifierId).IsRequired();
                entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
                entity.Property(e => e.Price).HasColumnType("decimal(18,2)");
                entity.Property(e => e.Quantity).IsRequired();
                entity.HasOne(e => e.TableOrderItem)
                    .WithMany(ti => ti.Modifiers)
                    .HasForeignKey(e => e.TableOrderItemId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.ToTable("table_order_item_modifiers");
            });

            // Receipt configuration
            builder.Entity<Receipt>(entity =>
            {
               entity.HasIndex(e => e.ReceiptNumber).IsUnique();
               entity.HasIndex(e => e.PaymentId);
            });

            Console.WriteLine("AppDbContext model configuration completed with TableOrder support");
        }

        /// <summary>
        /// Compliance: Audit log is append-only. Any attempt to UPDATE existing AuditLog rows is rejected (anti-tamper).
        /// DELETE is only allowed via the dedicated retention method DeleteAuditLogsOlderThanAsync.
        /// </summary>
        public override int SaveChanges()
        {
            EnforceAuditLogAppendOnly();
            return base.SaveChanges();
        }

        /// <summary>
        /// Async variant: enforces audit log append-only (no updates to existing audit rows).
        /// </summary>
        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            EnforceAuditLogAppendOnly();
            return await base.SaveChangesAsync(cancellationToken);
        }

        private void EnforceAuditLogAppendOnly()
        {
            var modifiedAuditLogs = ChangeTracker.Entries<AuditLog>()
                .Where(e => e.State == EntityState.Modified)
                .ToList();
            if (modifiedAuditLogs.Any())
            {
                throw new InvalidOperationException(
                    "Audit log is append-only. Updating existing audit records is not allowed for compliance. " +
                    $"Attempted to modify {modifiedAuditLogs.Count} audit log entry/entries.");
            }
        }
    }
}

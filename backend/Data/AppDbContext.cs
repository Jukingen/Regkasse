using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.Backup;
using KasseAPI_Final.Models.RestoreVerification;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Activity;
using KasseAPI_Final.Tenancy;
using KasseAPI_Final.Time;

namespace KasseAPI_Final.Data
{
    public class AppDbContext : IdentityDbContext<ApplicationUser>
    {
        private readonly ICurrentTenantAccessor _tenantAccessor;
        private readonly ILogger<AppDbContext> _logger;

        /// <summary>EF Core design-time only (migrations, <see cref="DesignTimeDbContextFactory"/>). Tenant query filters disabled.</summary>
        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options)
        {
            _tenantAccessor = NullCurrentTenantAccessor.Instance;
            _logger = NullLogger<AppDbContext>.Instance;
        }

        /// <summary>Runtime DI and tests with an explicit tenant accessor.</summary>
        [ActivatorUtilitiesConstructor]
        public AppDbContext(
            DbContextOptions<AppDbContext> options,
            ICurrentTenantAccessor tenantAccessor,
            ILogger<AppDbContext>? logger = null)
            : base(options)
        {
            _tenantAccessor = tenantAccessor ?? throw new ArgumentNullException(nameof(tenantAccessor));
            _logger = logger ?? NullLogger<AppDbContext>.Instance;
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            // Provider/connection are set in ApplicationHost, DesignTimeDbContextFactory, or test options — never override here.
            if (optionsBuilder.IsConfigured)
                return;
        }

        // DbSet properties
        public DbSet<Product> Products { get; set; }
        public DbSet<Customer> Customers { get; set; }
        public DbSet<BenefitDefinition> BenefitDefinitions { get; set; }
        public DbSet<PaymentMethodDefinition> PaymentMethodDefinitions { get; set; }
        public DbSet<BenefitAssignment> BenefitAssignments { get; set; }
        public DbSet<BenefitDailyUsage> BenefitDailyUsages { get; set; }
        public DbSet<Invoice> Invoices { get; set; }
        public DbSet<Cart> Carts { get; set; }
        public DbSet<CartItem> CartItems { get; set; }
        public DbSet<CartItemModifier> CartItemModifiers { get; set; }
        public DbSet<Order> Orders { get; set; }
        public DbSet<OrderItem> OrderItems { get; set; }
        public DbSet<CashRegister> CashRegisters { get; set; }
        public DbSet<CashRegisterTransaction> CashRegisterTransactions { get; set; }
        public DbSet<CashierShift> CashierShifts { get; set; }
        public DbSet<CashierFavorite> CashierFavorites { get; set; }
        public DbSet<SplitSession> SplitSessions { get; set; }
        public DbSet<SplitItem> SplitItems { get; set; }
        public DbSet<Category> Categories { get; set; }
        public DbSet<PaymentDetails> PaymentDetails { get; set; }
        public DbSet<PaymentReversalApproval> PaymentReversalApprovals { get; set; }
        public DbSet<SuspiciousTransactionAlert> SuspiciousTransactionAlerts { get; set; }
        public DbSet<OfflineTransaction> OfflineTransactions { get; set; }
        public DbSet<CardPaymentTransaction> CardPaymentTransactions { get; set; }
        /// <summary>Observability: DeviceId/ClientSequence coverage per replayed offline intent (no domain impact).</summary>
        public DbSet<OfflineIntentCoverageSample> OfflineIntentCoverageSamples { get; set; }
        public DbSet<InventoryItem> Inventory { get; set; }
        public DbSet<InventoryTransaction> InventoryTransactions { get; set; }
        public DbSet<SystemSettings> SystemSettings { get; set; }
        public DbSet<CashRegisterSettings> CashRegisterSettings { get; set; }

        /// <summary>RKSV: NTP vs system clock audit samples.</summary>
        public DbSet<SystemTimeSyncLog> SystemTimeSyncLogs { get; set; }

        /// <summary>Singleton (Id=1): admin NTP auto-sync and drift threshold overrides.</summary>
        public DbSet<NtpAdminSettings> NtpAdminSettings { get; set; }
        public DbSet<UserSettings> UserSettings { get; set; }
        public DbSet<AuditLog> AuditLogs { get; set; }
        public DbSet<CompanySettings> CompanySettings { get; set; }
        public DbSet<LocalizationSettings> LocalizationSettings { get; set; }
        public DbSet<Tenant> Tenants { get; set; }
        public DbSet<ReceiptTemplate> ReceiptTemplates { get; set; }
        public DbSet<GeneratedReceipt> GeneratedReceipts { get; set; }
        public DbSet<TseDevice> TseDevices { get; set; }

        /// <summary>Append-only log when TSE operational health classification changes.</summary>
        public DbSet<TseHealthAuditLog> TseHealthAuditLogs { get; set; }
        public DbSet<DailyClosing> DailyClosings { get; set; }
        public DbSet<TagesberichtReport> TagesberichtReports { get; set; }
        public DbSet<MonatsberichtReport> MonatsberichtReports { get; set; }
        public DbSet<JahresberichtReport> JahresberichtReports { get; set; }
        public DbSet<PeriodenberichtRun> PeriodenberichtRuns { get; set; }
        public DbSet<TseSignature> TseSignatures { get; set; }
        public DbSet<FinanzOnlineError> FinanzOnlineErrors { get; set; }
        public DbSet<PaymentLogEntry> PaymentLogs { get; set; }
        public DbSet<PaymentSession> PaymentSessions { get; set; }
        public DbSet<PaymentMetrics> PaymentMetrics { get; set; }
        public DbSet<AuthSession> AuthSessions { get; set; }
        public DbSet<RefreshToken> RefreshTokens { get; set; }
        public DbSet<UserTenantMembership> UserTenantMemberships { get; set; }
        public DbSet<UserPermissionOverride> UserPermissionOverrides { get; set; }
        public DbSet<UserUsernameHistory> UserUsernameHistories { get; set; }
        public DbSet<DashboardPreferences> DashboardPreferences { get; set; }
        public DbSet<UserPreferences> UserPreferences { get; set; }
        public DbSet<AuditReportSchedule> AuditReportSchedules { get; set; }
        public DbSet<OperationalReportSchedule> OperationalReportSchedules { get; set; }
        public DbSet<DepExportHistory> DepExportHistories { get; set; }
        public DbSet<DepExportSchedule> DepExportSchedules { get; set; }
        public DbSet<ActivityEvent> ActivityEvents { get; set; }
        public DbSet<ActivityEventRead> ActivityEventReads { get; set; }
        public DbSet<TenantNotificationConfig> TenantNotificationConfigs { get; set; }

        /// <summary>Tenant-scoped vouchers (Gutscheine); codes stored hashed only.</summary>
        public DbSet<Voucher> Vouchers { get; set; }

        /// <summary>Append-only ledger for voucher balance changes.</summary>
        public DbSet<VoucherLedgerEntry> VoucherLedgerEntries { get; set; }
        
        // Masa siparişleri için yeni tablolar
        public DbSet<TableOrder> TableOrders { get; set; }
        public DbSet<TableOrderItem> TableOrderItems { get; set; }
        public DbSet<TableOrderItemModifier> TableOrderItemModifiers { get; set; }

        // FinanzOnline Audit
        public DbSet<FinanzOnlineSubmission> FinanzOnlineSubmissions { get; set; }
        public DbSet<FinanzOnlineOutboxMessage> FinanzOnlineOutboxMessages { get; set; }

        /// <summary>RKSV Startbeleg/Jahresbeleg FinanzOnline submission tracking (no secrets).</summary>
        public DbSet<RksvSpecialReceiptFinanzOnlineSubmission> RksvSpecialReceiptFinanzOnlineSubmissions { get; set; }

        // RKSV Receipt tables
        public DbSet<Receipt> Receipts { get; set; }
        public DbSet<ReceiptItem> ReceiptItems { get; set; }
        public DbSet<ReceiptTaxLine> ReceiptTaxLines { get; set; }
        /// <summary>Per-register per-day sequence for BelegNr allocation (Sprint 1).</summary>
        public DbSet<ReceiptSequence> ReceiptSequences { get; set; }
        /// <summary>Per-register TSE signature chain state; locked (FOR UPDATE) when generating signatures to avoid races.</summary>
        public DbSet<SignatureChainState> SignatureChainState { get; set; }
        /// <summary>Sprint 5: Legal hold on audit date ranges; cleanup skips records in active holds.</summary>
        public DbSet<LegalHold> LegalHolds { get; set; }

        /// <summary>Phase 1: backup orchestration metadata (not fiscal domain).</summary>
        public DbSet<BackupRun> BackupRuns { get; set; }
        public DbSet<BackupArtifact> BackupArtifacts { get; set; }
        public DbSet<BackupVerification> BackupVerifications { get; set; }

        /// <summary>Tek satır: admin yedek çalıştırma modu (Fake / PgDump / yapılandırmayı izle).</summary>
        public DbSet<BackupRuntimeExecutionPreference> BackupRuntimeExecutionPreferences { get; set; }

        /// <summary>Singleton (Id=1): scheduled backup UTC cron + retention (see migration seed).</summary>
        public DbSet<BackupSettings> BackupSettings { get; set; }

        /// <summary>Per-tenant backup automation schedule (UTC cron + retention).</summary>
        public DbSet<BackupScheduleConfiguration> BackupScheduleConfigurations { get; set; }

        /// <summary>Singleton (Id=1): persisted development-mode toggles (see migration seed).</summary>
        public DbSet<DevelopmentModeSettings> DevelopmentModeSettings { get; set; }

        /// <summary>Restore drill metadata (pg_restore --list + optional fiscal SQL + integrity); not artifact verification.</summary>
        public DbSet<RestoreVerificationRun> RestoreVerificationRuns { get; set; }

        /// <summary>Super-admin manual restore approval requests (validation-only).</summary>
        public DbSet<ManualRestoreRequest> ManualRestoreRequests { get; set; }

        // Extra Zutaten (Add-on groups and assignments; add-on products in addon_group_products)
        public DbSet<ProductModifierGroup> ProductModifierGroups { get; set; }
        public DbSet<ProductModifierGroupAssignment> ProductModifierGroupAssignments { get; set; }
        /// <summary>Faz 1: Grup içi product referansları (suggested add-on); fiyat Product'ta.</summary>
        public DbSet<AddOnGroupProduct> AddOnGroupProducts { get; set; }

        /// <summary>Hospitality: saat/gün/kasa kapsamında fiyat kuralları (Happy Hour vb.).</summary>
        public DbSet<PricingRule> PricingRules { get; set; }

        /// <summary>Audit trail of admin-issued offline licenses (REGK key + signed JWT). Indexed unique on license_key.</summary>
        public DbSet<IssuedLicense> IssuedLicenses { get; set; }

        /// <summary>Per-machine activation rows (survives API restarts; used with encrypted license file).</summary>
        public DbSet<ActivatedLicense> ActivatedLicenses { get; set; }

        public DbSet<LicenseActivationAttempt> LicenseActivationAttempts { get; set; }

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
                entity.Property(e => e.TaxNumber).HasMaxLength(20).IsRequired(false);
                entity.Property(e => e.Notes).HasMaxLength(500);
                entity.Property(e => e.DeactivatedBy).HasMaxLength(450);
                entity.Property(e => e.DeactivationReason).HasMaxLength(500);
                
                entity.HasIndex(e => e.EmployeeNumber).IsUnique();
                entity.HasIndex(e => e.TaxNumber)
                    .IsUnique()
                    .HasFilter("tax_number IS NOT NULL AND tax_number <> ''");
            });

            builder.Entity<AuthSession>(entity =>
            {
                entity.ToTable("auth_sessions");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.UserId).HasColumnName("user_id").IsRequired().HasMaxLength(450);
                entity.Property(e => e.ClientApp).HasColumnName("client_app").IsRequired().HasMaxLength(20);
                entity.Property(e => e.TenantId).HasColumnName("tenant_id");
                entity.Property(e => e.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
                entity.Property(e => e.RevokedAtUtc).HasColumnName("revoked_at_utc");
                entity.Property(e => e.RevokedReason).HasColumnName("revoked_reason").HasMaxLength(200);
                entity.Property(e => e.LastActivityAtUtc).HasColumnName("last_activity_at_utc");
                entity.Property(e => e.DeviceId).HasColumnName("device_id").HasMaxLength(200);
                entity.Property(e => e.IpAddress).HasColumnName("ip_address").HasMaxLength(45);
                entity.Property(e => e.UserAgent).HasColumnName("user_agent").HasMaxLength(500);
                entity.HasOne(e => e.Tenant)
                    .WithMany()
                    .HasForeignKey(e => e.TenantId)
                    .OnDelete(DeleteBehavior.Restrict);
                entity.HasIndex(e => new { e.UserId, e.RevokedAtUtc });
                entity.HasIndex(e => e.CreatedAtUtc);
            });

            builder.Entity<RefreshToken>(entity =>
            {
                entity.ToTable("refresh_tokens");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.UserId).HasColumnName("user_id").IsRequired().HasMaxLength(450);
                entity.Property(e => e.SessionId).HasColumnName("session_id").IsRequired();
                entity.Property(e => e.TokenHash).HasColumnName("token_hash").IsRequired().HasMaxLength(128);
                entity.Property(e => e.AccessJti).HasColumnName("access_jti").IsRequired().HasMaxLength(64);
                entity.Property(e => e.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
                entity.Property(e => e.ExpiresAtUtc).HasColumnName("expires_at_utc").IsRequired();
                entity.Property(e => e.ConsumedAtUtc).HasColumnName("consumed_at_utc");
                entity.Property(e => e.RevokedAtUtc).HasColumnName("revoked_at_utc");
                entity.Property(e => e.ReplacedByTokenId).HasColumnName("replaced_by_token_id");
                entity.Property(e => e.RevokeReason).HasColumnName("revoke_reason").HasMaxLength(200);

                entity.HasIndex(e => e.TokenHash).IsUnique();
                entity.HasIndex(e => new { e.SessionId, e.RevokedAtUtc, e.ExpiresAtUtc });
                entity.HasIndex(e => new { e.UserId, e.CreatedAtUtc });
                entity.HasOne(e => e.Session)
                    .WithMany(s => s.RefreshTokens)
                    .HasForeignKey(e => e.SessionId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<UserTenantMembership>(entity =>
            {
                entity.ToTable("user_tenant_memberships");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.UserId).HasColumnName("user_id").IsRequired().HasMaxLength(450);
                entity.Property(e => e.TenantId).HasColumnName("tenant_id").IsRequired();
                entity.Property(e => e.IsActive).HasColumnName("is_active").IsRequired();
                entity.Property(e => e.IsOwner).HasColumnName("is_owner").IsRequired();
                entity.Property(e => e.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
                entity.Property(e => e.UpdatedAtUtc).HasColumnName("updated_at_utc");

                // Composite unique: one membership row per (user, tenant); user may belong to many tenants.
                entity.HasIndex(e => new { e.UserId, e.TenantId }).IsUnique();
                entity.HasIndex(e => e.TenantId);
                entity.HasIndex(e => e.UserId);

                entity.HasOne(e => e.User)
                    .WithMany(u => u.UserTenantMemberships)
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.Tenant)
                    .WithMany()
                    .HasForeignKey(e => e.TenantId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<UserPermissionOverride>(entity =>
            {
                entity.ToTable("user_permission_overrides");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.UserId).HasColumnName("user_id").IsRequired().HasMaxLength(450);
                entity.Property(e => e.TenantId).HasColumnName("tenant_id");
                entity.Property(e => e.Permission).HasColumnName("permission").IsRequired().HasMaxLength(128);
                entity.Property(e => e.IsGranted).HasColumnName("is_granted").IsRequired();
                entity.Property(e => e.Reason).HasColumnName("reason").HasMaxLength(500);
                entity.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();
                entity.Property(e => e.CreatedByUserId).HasColumnName("created_by_user_id").HasMaxLength(450);
                entity.Property(e => e.ExpiresAt).HasColumnName("expires_at");

                entity.HasIndex(e => e.UserId);
                entity.HasIndex(e => e.TenantId);
                entity.HasIndex(e => new { e.UserId, e.TenantId, e.Permission });

                entity.HasOne(e => e.User)
                    .WithMany()
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<UserUsernameHistory>(entity =>
            {
                entity.ToTable("user_username_history");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.UserId).HasColumnName("user_id").IsRequired().HasMaxLength(450);
                entity.Property(e => e.OldUsername).HasColumnName("old_username").HasMaxLength(50);
                entity.Property(e => e.NewUsername).HasColumnName("new_username").IsRequired().HasMaxLength(50);
                entity.Property(e => e.ChangedByUserId).HasColumnName("changed_by_user_id").HasMaxLength(450);
                entity.Property(e => e.ChangedAtUtc).HasColumnName("changed_at_utc").IsRequired();
                entity.Property(e => e.Reason).HasColumnName("reason").HasMaxLength(500);

                entity.HasIndex(e => e.UserId);
                entity.HasIndex(e => e.ChangedAtUtc);
                entity.HasIndex(e => new { e.UserId, e.ChangedAtUtc });

                entity.HasOne(e => e.User)
                    .WithMany()
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.ChangedByUser)
                    .WithMany()
                    .HasForeignKey(e => e.ChangedByUserId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            // Product configuration - RKSV uyumlu güncellenmiş yapı
            builder.Entity<Product>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.TenantId).HasColumnName("tenant_id").IsRequired();
                entity.HasOne(e => e.Tenant)
                    .WithMany()
                    .HasForeignKey(e => e.TenantId)
                    .OnDelete(DeleteBehavior.Restrict);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
                entity.Property(e => e.NameDe).HasColumnName("name_de").HasMaxLength(200);
                entity.Property(e => e.NameEn).HasColumnName("name_en").HasMaxLength(200);
                entity.Property(e => e.NameTr).HasColumnName("name_tr").HasMaxLength(200);
                entity.Property(e => e.Description).HasColumnType("text");
                entity.Property(e => e.DescriptionDe).HasColumnName("description_de").HasColumnType("text");
                entity.Property(e => e.DescriptionEn).HasColumnName("description_en").HasColumnType("text");
                entity.Property(e => e.DescriptionTr).HasColumnName("description_tr").HasColumnType("text");
                entity.Property(e => e.Price).HasColumnType("decimal(18,2)");
                entity.Property(e => e.Cost).HasColumnType("decimal(18,2)");
                entity.Property(e => e.TaxRate).HasColumnType("decimal(5,2)");
                entity.Property(e => e.TaxType).IsRequired(); // Map to integer column
                entity.Property(e => e.Category).HasMaxLength(100);
                entity.Property(e => e.CategoryId).HasColumnName("category_id").HasColumnType("uuid").IsRequired();
                entity.Property(e => e.ImageUrl).HasMaxLength(500);
                entity.Property(e => e.StockQuantity).IsRequired();
                entity.Property(e => e.MinStockLevel).IsRequired();
                entity.Property(e => e.MaxStockLevel).HasColumnName("max_stock_level");
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
                entity.HasIndex(e => e.TenantId);
                entity.HasIndex(e => new { e.TenantId, e.Barcode })
                    .IsUnique()
                    .HasFilter("barcode IS NOT NULL AND barcode <> ''");
                
                // Composite FK: product category must belong to the same tenant
                entity.HasOne(p => p.CategoryNavigation)
                      .WithMany(c => c.Products)
                      .HasForeignKey(p => new { p.CategoryId, p.TenantId })
                      .HasPrincipalKey(c => new { c.Id, c.TenantId })
                      .OnDelete(DeleteBehavior.Restrict)
                      .IsRequired();

                // Enables composite FKs from modifier join tables (id + tenant must match parent rows).
                entity.HasAlternateKey(e => new { e.Id, e.TenantId });
                
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
                entity.Property(e => e.ApplicationUserId).HasMaxLength(450).IsRequired(false);
                
                entity.HasIndex(e => e.CustomerNumber).IsUnique();
                entity.HasIndex(e => e.Email).IsUnique();
                entity.HasIndex(e => e.TaxNumber).IsUnique();
                entity.HasIndex(e => e.ApplicationUserId);
                
                entity.HasOne(c => c.ApplicationUser)
                    .WithMany()
                    .HasForeignKey(c => c.ApplicationUserId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // BenefitDefinition configuration
            builder.Entity<BenefitDefinition>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Code).IsRequired().HasMaxLength(50);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
                entity.Property(e => e.BenefitKind).IsRequired();
                entity.Property(e => e.PercentageValue).HasColumnType("decimal(5,2)");
                entity.Property(e => e.AllowanceScope).HasMaxLength(50);
                entity.Property(e => e.AllowanceCategoryId);
                entity.HasOne(e => e.AllowanceCategory)
                    .WithMany()
                    .HasForeignKey(e => e.AllowanceCategoryId)
                    .OnDelete(DeleteBehavior.SetNull);
                entity.HasIndex(e => e.Code).IsUnique();
            });

            builder.Entity<PaymentMethodDefinition>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.MetadataJson).HasColumnType("text");
                entity.Property(e => e.TenantId).HasColumnName("tenant_id").IsRequired();
                entity.Property(e => e.CashRegisterId).HasColumnName("cash_register_id").IsRequired();
                entity.HasOne(e => e.Tenant)
                    .WithMany()
                    .HasForeignKey(e => e.TenantId)
                    .OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(e => e.CashRegister)
                    .WithMany()
                    .HasForeignKey(e => e.CashRegisterId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasIndex(e => new { e.CashRegisterId, e.Code }).IsUnique();
                entity.HasIndex(e => new { e.TenantId, e.CashRegisterId, e.IsActive, e.DisplayOrder });
            });

            // BenefitDailyUsage configuration
            builder.Entity<BenefitDailyUsage>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.CustomerId).IsRequired();
                entity.Property(e => e.BenefitDefinitionId).IsRequired();
                entity.Property(e => e.UsageDate).IsRequired();
                entity.Property(e => e.QuantityUsed).IsRequired();
                entity.Property(e => e.Version).IsConcurrencyToken();
                entity.HasOne(e => e.Customer)
                    .WithMany()
                    .HasForeignKey(e => e.CustomerId)
                    .OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(e => e.BenefitDefinition)
                    .WithMany()
                    .HasForeignKey(e => e.BenefitDefinitionId)
                    .OnDelete(DeleteBehavior.Restrict);
                entity.HasIndex(e => new { e.CustomerId, e.BenefitDefinitionId, e.UsageDate }).IsUnique();
            });

            // BenefitAssignment configuration
            builder.Entity<BenefitAssignment>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.BenefitDefinitionId).IsRequired();
                entity.Property(e => e.CustomerId).IsRequired();
                entity.Property(e => e.ValidFrom).IsRequired();
                entity.HasOne(e => e.BenefitDefinition)
                    .WithMany(b => b.Assignments)
                    .HasForeignKey(e => e.BenefitDefinitionId)
                    .OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(e => e.Customer)
                    .WithMany(c => c.BenefitAssignments)
                    .HasForeignKey(e => e.CustomerId)
                    .OnDelete(DeleteBehavior.Restrict);
                entity.HasIndex(e => e.BenefitDefinitionId);
                entity.HasIndex(e => e.CustomerId);
                entity.HasIndex(e => new { e.CustomerId, e.ValidFrom, e.ValidTo });
            });

            // Invoice configuration - Sadece mevcut sütunlar
            builder.Entity<Invoice>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.TenantId).HasColumnName("tenant_id").IsRequired();
                entity.HasOne(e => e.Tenant)
                    .WithMany()
                    .HasForeignKey(e => e.TenantId)
                    .OnDelete(DeleteBehavior.Restrict);
                entity.HasIndex(e => e.TenantId);
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
                entity.Property(e => e.TseSignature).IsRequired().HasColumnType("text");
                entity.Property(e => e.JwsHeader).HasColumnType("text");
                entity.Property(e => e.JwsPayload).HasColumnType("text");
                entity.Property(e => e.JwsSignature).HasColumnType("text");
                entity.Property(e => e.StornoReasonText).HasColumnType("text");
                entity.Property(e => e.KassenId).IsRequired().HasMaxLength(50);
                entity.Property(e => e.CashRegisterId).IsRequired();
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
                // Dropped IX_invoices_TseSignature: B-tree cannot index full JWS strings (>2704 bytes per row in PostgreSQL).

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
                entity.Property(e => e.TenantId).HasColumnName("tenant_id").IsRequired();
                entity.Property(e => e.RegisterNumber).IsRequired().HasMaxLength(20);
                entity.Property(e => e.Location).IsRequired().HasMaxLength(100);
                entity.Property(e => e.StartingBalance).HasColumnType("decimal(18,2)");
                entity.Property(e => e.CurrentBalance).HasColumnType("decimal(18,2)");
                entity.Property(e => e.LastBalanceUpdate).IsRequired();
                entity.Property(e => e.Status).IsRequired();
                entity.Property(e => e.CurrentUserId).HasMaxLength(450);
                entity.HasOne(e => e.Tenant)
                    .WithMany()
                    .HasForeignKey(e => e.TenantId)
                    .OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(e => e.CurrentUser)
                    .WithMany(u => u.CashRegisters)
                    .HasForeignKey(e => e.CurrentUserId)
                    .IsRequired(false)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.HasIndex(e => new { e.TenantId, e.RegisterNumber }).IsUnique();
                entity.HasIndex(e => e.TenantId)
                    .IsUnique()
                    .HasFilter("\"is_default_for_tenant\" = true");
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

            builder.Entity<CashierShift>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.TenantId).HasColumnName("tenant_id").IsRequired();
                entity.Property(e => e.CashRegisterId).HasColumnName("cash_register_id").IsRequired();
                entity.Property(e => e.CashierId).HasColumnName("cashier_id").IsRequired().HasMaxLength(450);
                entity.Property(e => e.CashierName).HasColumnName("cashier_name").IsRequired().HasMaxLength(200);
                entity.Property(e => e.StartBalance).HasColumnName("start_balance").HasColumnType("decimal(18,2)");
                entity.Property(e => e.EndBalance).HasColumnName("end_balance").HasColumnType("decimal(18,2)");
                entity.Property(e => e.TotalSales).HasColumnName("total_sales").HasColumnType("decimal(18,2)");
                entity.Property(e => e.TotalCash).HasColumnName("total_cash").HasColumnType("decimal(18,2)");
                entity.Property(e => e.TotalCard).HasColumnName("total_card").HasColumnType("decimal(18,2)");
                entity.Property(e => e.Difference).HasColumnName("difference").HasColumnType("decimal(18,2)");
                entity.Property(e => e.StartedAt).HasColumnName("started_at").HasColumnType("timestamptz");
                entity.Property(e => e.EndedAt).HasColumnName("ended_at").HasColumnType("timestamptz");
                entity.Property(e => e.Status).HasColumnName("status").IsRequired().HasMaxLength(20);
                entity.Property(e => e.Notes).HasColumnName("notes").HasColumnType("text");
                entity.Property(e => e.DailyClosingId).HasColumnName("daily_closing_id");
                entity.Property(e => e.CashCount).HasColumnName("cash_count").HasColumnType("decimal(18,2)");
                entity.HasOne(e => e.DailyClosing)
                    .WithMany()
                    .HasForeignKey(e => e.DailyClosingId)
                    .IsRequired(false)
                    .OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(e => e.Tenant)
                    .WithMany()
                    .HasForeignKey(e => e.TenantId)
                    .OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(e => e.CashRegister)
                    .WithMany()
                    .HasForeignKey(e => e.CashRegisterId)
                    .OnDelete(DeleteBehavior.Restrict);
                entity.HasIndex(e => new { e.TenantId, e.CashierId, e.Status });
                entity.HasIndex(e => new { e.CashRegisterId, e.StartedAt });
            });

            builder.Entity<CashierFavorite>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.TenantId).HasColumnName("tenant_id").IsRequired();
                entity.Property(e => e.CashierId).HasColumnName("cashier_id").IsRequired().HasMaxLength(450);
                entity.Property(e => e.ProductId).HasColumnName("product_id").IsRequired();
                entity.Property(e => e.SortOrder).HasColumnName("sort_order").IsRequired();
                entity.HasOne(e => e.Tenant)
                    .WithMany()
                    .HasForeignKey(e => e.TenantId)
                    .OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(e => e.Product)
                    .WithMany()
                    .HasForeignKey(e => e.ProductId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasIndex(e => new { e.TenantId, e.CashierId, e.ProductId }).IsUnique();
                entity.HasIndex(e => new { e.TenantId, e.CashierId, e.SortOrder });
            });

            builder.Entity<SplitSession>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.TenantId).HasColumnName("tenant_id").IsRequired();
                entity.Property(e => e.OriginalCartId).HasColumnName("original_cart_id").IsRequired();
                entity.Property(e => e.CashierId).HasColumnName("cashier_id").IsRequired().HasMaxLength(450);
                entity.Property(e => e.IsCompleted).HasColumnName("is_completed").IsRequired();
                entity.HasOne(e => e.Tenant)
                    .WithMany()
                    .HasForeignKey(e => e.TenantId)
                    .OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(e => e.OriginalCart)
                    .WithMany()
                    .HasForeignKey(e => e.OriginalCartId)
                    .HasPrincipalKey(c => c.Id)
                    .OnDelete(DeleteBehavior.Restrict);
                entity.HasIndex(e => new { e.TenantId, e.OriginalCartId, e.IsCompleted });
                entity.HasIndex(e => new { e.TenantId, e.CashierId, e.CreatedAt });
            });

            builder.Entity<SplitItem>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.SplitSessionId).HasColumnName("split_session_id").IsRequired();
                entity.Property(e => e.ProductId).HasColumnName("product_id").IsRequired();
                entity.Property(e => e.SourceCartItemId).HasColumnName("source_cart_item_id");
                entity.Property(e => e.Quantity).HasColumnName("quantity").IsRequired();
                entity.Property(e => e.Price).HasColumnName("price").HasColumnType("decimal(18,2)");
                entity.Property(e => e.CustomerName).HasColumnName("customer_name").IsRequired().HasMaxLength(200);
                entity.Property(e => e.SeatNumber).HasColumnName("seat_number").IsRequired();
                entity.HasOne(e => e.SplitSession)
                    .WithMany(s => s.SplitItems)
                    .HasForeignKey(e => e.SplitSessionId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(e => e.Product)
                    .WithMany()
                    .HasForeignKey(e => e.ProductId)
                    .OnDelete(DeleteBehavior.Restrict);
                entity.HasIndex(e => e.SplitSessionId);
                entity.HasIndex(e => new { e.SplitSessionId, e.SeatNumber });
            });

            // Category configuration
            builder.Entity<Category>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasColumnType("uuid").HasColumnName("id");
                entity.Property(e => e.TenantId).HasColumnName("tenant_id").IsRequired();
                entity.HasOne(e => e.Tenant)
                    .WithMany()
                    .HasForeignKey(e => e.TenantId)
                    .OnDelete(DeleteBehavior.Restrict);
                entity.Property(e => e.Key).IsRequired().HasMaxLength(100).HasColumnName("category_key");
                entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
                entity.Property(e => e.OriginalDemoName).HasMaxLength(100).HasColumnName("original_demo_name");
                entity.Property(e => e.Description).HasMaxLength(500);
                entity.Property(e => e.Color).HasMaxLength(20);
                entity.Property(e => e.Icon).HasMaxLength(50);
                entity.Property(e => e.SortOrder);
                entity.Property(e => e.VatRate).HasColumnType("decimal(5,2)").IsRequired();
                entity.Property(e => e.FiscalCategory).HasColumnName("fiscal_category").HasConversion<int>().IsRequired();
                entity.Property(e => e.IsSystemCategory).HasColumnName("is_system_category").HasDefaultValue(false);
                entity.HasAlternateKey(e => new { e.Id, e.TenantId });
                entity.HasIndex(e => new { e.TenantId, e.Key }).IsUnique();
                // Case-insensitive unique (tenant_id, name) enforced in PostgreSQL via
                // IX_categories_tenant_id_Name_ci — see migration CaseInsensitiveCategoryNameUniqueIndex.
                entity.HasIndex(e => e.SortOrder);
            });

            // ProductModifierGroup configuration (Extra Zutaten)
            builder.Entity<ProductModifierGroup>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.TenantId).HasColumnName("tenant_id").IsRequired();
                entity.HasOne(e => e.Tenant)
                    .WithMany()
                    .HasForeignKey(e => e.TenantId)
                    .OnDelete(DeleteBehavior.Restrict);
                entity.HasAlternateKey(e => new { e.Id, e.TenantId });
                entity.HasIndex(e => e.TenantId);
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
                entity.Property(e => e.TenantId).HasColumnName("tenant_id").IsRequired();
                entity.HasOne(e => e.Product)
                    .WithMany(p => p.ModifierGroupAssignments)
                    .HasForeignKey(e => new { e.ProductId, e.TenantId })
                    .HasPrincipalKey(p => new { p.Id, p.TenantId })
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(e => e.ModifierGroup)
                    .WithMany(g => g.ProductAssignments)
                    .HasForeignKey(e => new { e.ModifierGroupId, e.TenantId })
                    .HasPrincipalKey(g => new { g.Id, g.TenantId })
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasIndex(e => e.TenantId);
                entity.ToTable("product_modifier_group_assignments");
            });

            // AddOnGroupProduct (Faz 1: group -> product ref; price on Product)
            builder.Entity<AddOnGroupProduct>(entity =>
            {
                entity.HasKey(e => new { e.ModifierGroupId, e.ProductId });
                entity.Property(e => e.TenantId).HasColumnName("tenant_id").IsRequired();
                entity.Property(e => e.ModifierGroupId).HasColumnName("modifier_group_id").HasColumnType("uuid").IsRequired();
                entity.Property(e => e.ProductId).HasColumnName("product_id").HasColumnType("uuid").IsRequired();
                entity.Property(e => e.SortOrder).HasColumnName("sort_order").IsRequired();
                entity.HasOne(e => e.ModifierGroup)
                    .WithMany(g => g.AddOnGroupProducts)
                    .HasForeignKey(e => new { e.ModifierGroupId, e.TenantId })
                    .HasPrincipalKey(g => new { g.Id, g.TenantId })
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(e => e.Product)
                    .WithMany()
                    .HasForeignKey(e => new { e.ProductId, e.TenantId })
                    .HasPrincipalKey(p => new { p.Id, p.TenantId })
                    .OnDelete(DeleteBehavior.Cascade);
                entity.ToTable("addon_group_products");
                entity.HasIndex(e => e.ProductId);
                entity.HasIndex(e => e.TenantId);
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
                
                entity.Property(e => e.Notes).HasColumnType("text");
                entity.Property(e => e.ReceiptNumber).IsRequired().HasColumnType("text");
                entity.Property(e => e.CancellationReason).HasColumnType("text");
                entity.Property(e => e.TransactionId).HasMaxLength(100);
                entity.Property(e => e.TseSignature).IsRequired().HasColumnType("text");
                entity.Property(e => e.CertificateThumbprint)
                    .HasMaxLength(64)
                    .HasColumnName("certificate_thumbprint");
                entity.HasIndex(e => e.CertificateThumbprint)
                    .HasFilter("\"certificate_thumbprint\" IS NOT NULL");
                entity.Property(e => e.CompanyName).HasMaxLength(100);
                entity.Property(e => e.CompanyAddress).HasMaxLength(200);
                entity.Property(e => e.PrevSignatureValueUsed).HasColumnType("text");
                entity.Property(e => e.JwsHeader).HasColumnType("text");
                entity.Property(e => e.JwsPayload).HasColumnType("text");
                entity.Property(e => e.JwsSignature).HasColumnType("text");
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
                entity.Property(e => e.AppliedBenefitsSnapshot)
                    .HasColumnType("jsonb")
                    .HasConversion(
                        v => v == null ? (string?)null : v.RootElement.GetRawText(),
                        v => string.IsNullOrEmpty(v) ? null : JsonDocument.Parse(v));
                
                entity.HasIndex(e => e.CustomerId);
                entity.HasIndex(e => e.PaymentMethodRaw); // Index on raw property
                entity.HasIndex(e => e.CreatedAt);
                entity.HasIndex(e => e.TseSignature);
                entity.Property(e => e.IdempotencyKey).HasMaxLength(64);
                entity.HasIndex(e => e.IdempotencyKey).IsUnique().HasFilter("\"idempotency_key\" IS NOT NULL");
                entity.Property(e => e.CancelIdempotencyKey).HasMaxLength(64);
                entity.HasIndex(e => e.CancelIdempotencyKey).IsUnique().HasFilter("\"cancel_idempotency_key\" IS NOT NULL");
                
                // Strengthen data integrity for POS/receipt numbering.
                // Unique over real receipt numbers only (ignore empty/draft values).
                entity.HasIndex(e => e.ReceiptNumber)
                    .IsUnique()
                    .HasFilter("\"receipt_number\" IS NOT NULL AND \"receipt_number\" <> ''");
                entity.Property(e => e.CashRegisterId).IsRequired().HasColumnName("cash_register_id");
                entity.HasOne(e => e.CashRegister)
                    .WithMany()
                    .HasForeignKey(e => e.CashRegisterId)
                    .OnDelete(DeleteBehavior.Restrict);
                entity.HasIndex(e => e.CashRegisterId);
                entity.HasIndex(e => new { e.CashRegisterId, e.CreatedAt })
                    .IsDescending(false, true);
                entity.Property(e => e.OriginalReceiptId).HasColumnName("original_receipt_id");
                entity.HasOne<Receipt>()
                    .WithMany()
                    .HasForeignKey(e => e.OriginalReceiptId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.Property(e => e.OfflineTransactionId).HasColumnName("offline_transaction_id");
                entity.HasOne(e => e.OfflineTransaction)
                    .WithMany()
                    .HasForeignKey(e => e.OfflineTransactionId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.HasIndex(e => e.OfflineTransactionId);
                entity.Property(e => e.FinanzOnlineStatus).HasMaxLength(30);
                entity.Property(e => e.FinanzOnlineError).HasColumnType("text");
                entity.Property(e => e.FinanzOnlineReferenceId).HasMaxLength(100);
                entity.HasIndex(e => e.FinanzOnlineStatus).HasFilter("\"finanz_online_status\" IS NOT NULL");

                entity.Property(e => e.RksvSpecialReceiptKind).HasMaxLength(20).HasColumnName("rksv_special_receipt_kind");
                entity.Property(e => e.RksvSpecialReceiptYear).HasColumnName("rksv_special_receipt_year");
                entity.Property(e => e.RksvSpecialReceiptMonth).HasColumnName("rksv_special_receipt_month");
                entity.Property(e => e.RksvNullbelegActsAsJahresbeleg).HasColumnName("rksv_nullbeleg_acts_as_jahresbeleg");
                entity.Property(e => e.TimeSyncWarning).HasColumnName("time_sync_warning");

                entity.Property(e => e.StornoReason)
                    .HasColumnName("storno_reason")
                    .HasConversion<int>();

                entity.HasIndex(e => new { e.CashRegisterId, e.RksvSpecialReceiptYear, e.RksvSpecialReceiptMonth })
                    .IsUnique()
                    .HasDatabaseName("ix_payment_details_nullbeleg_per_register_month")
                    .HasFilter("\"rksv_special_receipt_kind\" = 'Nullbeleg' AND \"is_active\" = true");

                entity.HasIndex(e => e.CashRegisterId)
                    .IsUnique()
                    .HasDatabaseName("ix_payment_details_startbeleg_per_register")
                    .HasFilter("\"rksv_special_receipt_kind\" = 'Startbeleg' AND \"is_active\" = true");

                entity.HasIndex(e => new { e.CashRegisterId, e.RksvSpecialReceiptYear, e.RksvSpecialReceiptMonth })
                    .IsUnique()
                    .HasDatabaseName("ix_payment_details_monatsbeleg_per_register_month")
                    .HasFilter("\"rksv_special_receipt_kind\" = 'Monatsbeleg' AND \"is_active\" = true");

                entity.HasIndex(e => new { e.CashRegisterId, e.RksvSpecialReceiptYear })
                    .IsUnique()
                    .HasDatabaseName("ix_payment_details_jahresbeleg_per_register_year")
                    .HasFilter(
                        "\"rksv_special_receipt_kind\" = 'Jahresbeleg' AND \"is_active\" = true AND \"rksv_special_receipt_year\" IS NOT NULL");

                // Composite so EF does not treat this as the same index as Startbeleg (both would be CashRegisterId-only otherwise).
                entity.HasIndex(e => new { e.CashRegisterId, e.RksvSpecialReceiptKind })
                    .IsUnique()
                    .HasDatabaseName("ix_payment_details_schlussbeleg_per_register")
                    .HasFilter("\"rksv_special_receipt_kind\" = 'Schlussbeleg' AND \"is_active\" = true");
            });

            // OfflineTransaction configuration (non-fiscal intent; replay creates the canonical Payment + Receipt)
            builder.Entity<OfflineTransaction>(entity =>
            {
                entity.ToTable("offline_transactions");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.TenantId).HasColumnName("tenant_id").IsRequired();
                entity.HasOne(e => e.Tenant)
                    .WithMany()
                    .HasForeignKey(e => e.TenantId)
                    .OnDelete(DeleteBehavior.Restrict);
                entity.HasIndex(e => e.TenantId);
                entity.Property(e => e.CashRegisterId).IsRequired();
                entity.Property(e => e.PayloadJson).IsRequired().HasColumnType("jsonb");
                entity.Property(e => e.PayloadSecretsProtected).HasColumnName("payload_secrets_protected").HasColumnType("text");
                entity.Property(e => e.PayloadHash).HasMaxLength(64).HasColumnName("payload_hash");
                entity.Property(e => e.ServerReceivedAtUtc).IsRequired().HasColumnName("server_received_at_utc");
                entity.Property(e => e.DeviceId).HasMaxLength(128).HasColumnName("device_id");
                entity.Property(e => e.ClientSequenceNumber).HasColumnName("client_sequence_number");
                entity.Property(e => e.ClockDriftWarning).IsRequired().HasColumnName("clock_drift_warning");
                entity.Property(e => e.SequenceGapDetected).IsRequired().HasColumnName("sequence_gap_detected");
                entity.Property(e => e.SequenceDuplicateDetected).IsRequired().HasColumnName("sequence_duplicate_detected");
                entity.Property(e => e.Status)
                    .IsRequired()
                    .HasConversion<string>()
                    .HasMaxLength(20);

                entity.Property(e => e.SyncedPaymentId);
                entity.HasIndex(e => e.CashRegisterId);
                entity.HasIndex(e => e.Status);
                entity.HasIndex(e => e.SyncedPaymentId);

                // Dedup across offline IDs by content hash (for new inserts where PayloadHash is set)
                entity.HasIndex(e => new { e.CashRegisterId, e.PayloadHash })
                    .IsUnique();

                // Enforce monotonic sequence identity per device (nulls are allowed multiple times by Postgres)
                entity.HasIndex(e => new { e.CashRegisterId, e.DeviceId, e.ClientSequenceNumber })
                    .IsUnique();
            });

            builder.Entity<CardPaymentTransaction>(entity =>
            {
                entity.ToTable("card_payment_transactions");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.TenantId).HasColumnName("tenant_id").IsRequired();
                entity.HasOne(e => e.Tenant)
                    .WithMany()
                    .HasForeignKey(e => e.TenantId)
                    .OnDelete(DeleteBehavior.Restrict);
                entity.HasIndex(e => e.TenantId);
                entity.Property(e => e.CashRegisterId).HasColumnName("cash_register_id").IsRequired();
                entity.Property(e => e.PaymentId).HasColumnName("payment_id");
                entity.HasOne(e => e.Payment)
                    .WithMany()
                    .HasForeignKey(e => e.PaymentId)
                    .OnDelete(DeleteBehavior.SetNull);
                entity.Property(e => e.Amount).HasColumnType("decimal(18,2)").IsRequired();
                entity.Property(e => e.Currency).HasMaxLength(3).IsRequired();
                entity.Property(e => e.Gateway).HasColumnName("gateway").HasMaxLength(20).IsRequired();
                entity.Property(e => e.GatewayPaymentIntentId).HasColumnName("gateway_payment_intent_id").HasMaxLength(128);
                entity.Property(e => e.GatewayTransactionId).HasColumnName("gateway_transaction_id").HasMaxLength(100);
                entity.Property(e => e.ClientSecret).HasColumnName("client_secret").HasMaxLength(128);
                entity.Property(e => e.Status).HasMaxLength(20).IsRequired();
                entity.Property(e => e.CardBrand).HasColumnName("card_brand").HasMaxLength(20);
                entity.Property(e => e.CardLast4).HasColumnName("card_last4").HasMaxLength(4);
                entity.Property(e => e.ErrorMessage).HasColumnName("error_message");
                entity.Property(e => e.CompletedAt).HasColumnName("completed_at");
                entity.Property(e => e.RefundedAtUtc).HasColumnName("refunded_at_utc");
                entity.Property(e => e.RefundedAmount).HasColumnName("refunded_amount").HasColumnType("decimal(18,2)");
                entity.Property(e => e.CreatedByUserId).HasColumnName("created_by_user_id").HasMaxLength(450);
                entity.Property(e => e.MetadataJson).HasColumnName("metadata_json").HasColumnType("jsonb");
                entity.HasIndex(e => e.CashRegisterId);
                entity.HasIndex(e => e.Status);
                entity.HasIndex(e => e.PaymentId);
                entity.HasIndex(e => e.CreatedAt);
            });

            // OfflineIntentCoverageSample: observability only; one row per replayed intent for coverage metrics
            builder.Entity<OfflineIntentCoverageSample>(entity =>
            {
                entity.ToTable("offline_intent_coverage_samples");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.CreatedAtUtc).IsRequired();
                entity.Property(e => e.CashRegisterId).IsRequired();
                entity.Property(e => e.HasDeviceId).IsRequired();
                entity.Property(e => e.HasClientSequence).IsRequired();
                entity.HasIndex(e => e.CreatedAtUtc);
                entity.HasIndex(e => e.CashRegisterId);
                entity.HasIndex(e => e.ReplayBatchCorrelationId);
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

            // Tenant (Wave 0–1 SaaS root; single seeded row for legacy deployments)
            builder.Entity<Tenant>(entity =>
            {
                entity.ToTable("tenants");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
                entity.Property(e => e.Slug).IsRequired().HasMaxLength(64);
                entity.Property(e => e.Email).HasMaxLength(200);
                entity.Property(e => e.Phone).HasMaxLength(50);
                entity.Property(e => e.Address);
                entity.Property(e => e.Status).IsRequired().HasMaxLength(20).HasDefaultValue(TenantStatuses.Active);
                entity.Property(e => e.LicenseKey).HasMaxLength(100);
                entity.Property(e => e.LicenseValidUntilUtc);
                entity.Property(e => e.LicenseGracePeriodStartedAt);
                entity.Property(e => e.LicenseGracePeriodUsedDays).HasDefaultValue(0);
                entity.Property(e => e.DeletedAtUtc);
                entity.Property(e => e.DeletedByUserId).HasMaxLength(450);
                entity.HasIndex(e => e.Slug).IsUnique();
                entity.HasIndex(e => e.Status);
                entity.HasIndex(e => e.LicenseValidUntilUtc)
                    .HasDatabaseName("IX_tenants_license_valid_until");
            });

            builder.Entity<SystemTimeSyncLog>(entity =>
            {
                entity.ToTable("system_time_sync_logs");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.SyncTimeUtc).HasColumnName("sync_time_utc").IsRequired();
                entity.Property(e => e.SystemTimeUtc).HasColumnName("system_time_utc").IsRequired();
                entity.Property(e => e.NtpTimeUtc).HasColumnName("ntp_time_utc").IsRequired();
                entity.Property(e => e.OffsetSeconds).HasColumnName("offset_seconds").IsRequired();
                entity.Property(e => e.NtpServerUsed).HasColumnName("ntp_server_used").HasMaxLength(512).IsRequired();
                entity.Property(e => e.IsSuccess).HasColumnName("is_success").IsRequired();
                entity.Property(e => e.ErrorMessage).HasColumnName("error_message").HasMaxLength(2000);
                entity.HasIndex(e => e.SyncTimeUtc);
            });

            builder.Entity<NtpAdminSettings>(entity =>
            {
                entity.ToTable("ntp_admin_settings");
                entity.HasKey(e => e.Id);
            });

            // Admin-issued offline licenses: unique on the display key, indexed for audit lookups.
            builder.Entity<IssuedLicense>(entity =>
            {
                entity.ToTable("issued_licenses");
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.LicenseKey).IsUnique();
                entity.HasIndex(e => e.IssuedAtUtc);
                entity.HasIndex(e => e.ExpiryAtUtc);
                entity.HasIndex(e => e.IsRevoked).HasFilter("is_revoked = TRUE");
                entity.HasIndex(e => e.IsDeleted).HasFilter("is_deleted = TRUE");
                entity.HasIndex(e => e.IsCancelled).HasFilter("is_cancelled = TRUE");
                entity.Property(e => e.IsCancelled).IsRequired();
                entity.Property(e => e.IsDeleted).IsRequired();
                entity.HasIndex(e => e.SupersededByLicenseId);
                entity.HasOne(e => e.SupersededByLicense)
                    .WithMany()
                    .HasForeignKey(e => e.SupersededByLicenseId)
                    .OnDelete(DeleteBehavior.Restrict);
                entity.HasIndex(e => e.TransferredToLicenseId);
                entity.HasOne<IssuedLicense>()
                    .WithMany()
                    .HasForeignKey(e => e.TransferredToLicenseId)
                    .OnDelete(DeleteBehavior.Restrict);
                entity.Property(e => e.LicenseKey).IsRequired();
                entity.Property(e => e.CustomerName).IsRequired();
                entity.Property(e => e.SignedJwt).IsRequired();
                entity.Property(e => e.IssuedAtUtc).IsRequired();
                entity.Property(e => e.ExpiryAtUtc).IsRequired();
                entity.Property(e => e.FeaturesJson).HasMaxLength(4096);
            });

            builder.Entity<ActivatedLicense>(entity =>
            {
                entity.ToTable("activated_licenses");
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => new { e.MachineFingerprint, e.LicenseKey }).IsUnique();
                entity.HasIndex(e => e.ValidUntilUtc);
                entity.HasIndex(e => e.ActivatedAtUtc);
                entity.HasIndex(e => e.LastSeenAtUtc);
                entity.HasIndex(e => e.IsActive);
                entity.Property(e => e.LicenseKey).IsRequired().HasMaxLength(64);
                entity.Property(e => e.CustomerName).HasMaxLength(256);
                entity.Property(e => e.MachineFingerprint).HasMaxLength(128);
                entity.Property(e => e.ValidUntilUtc).IsRequired();
                entity.Property(e => e.ActivatedAtUtc).IsRequired();
                entity.Property(e => e.LastSeenAtUtc).IsRequired();
                entity.Property(e => e.IsActive).IsRequired();
                entity.Property(e => e.CreatedByUserId);
                entity.Property(e => e.FeaturesJson).HasMaxLength(4096);
            });

            builder.Entity<LicenseActivationAttempt>(entity =>
            {
                entity.ToTable("license_activation_attempts");
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.ActivatedAtUtc);
                entity.HasIndex(e => e.ActivationStatus);
                entity.HasIndex(e => new { e.LicenseKey, e.ActivatedAtUtc });
                entity.HasIndex(e => e.MachineFingerprint);
                entity.Property(e => e.LicenseKey).IsRequired().HasMaxLength(64);
                entity.Property(e => e.MachineFingerprint).IsRequired().HasMaxLength(128);
                entity.Property(e => e.ActivationStatus).IsRequired();
                entity.Property(e => e.FailureReason).HasMaxLength(4000);
                entity.Property(e => e.ClientIp).HasMaxLength(45);
                entity.Property(e => e.UserAgent).HasMaxLength(500);
                entity.Property(e => e.ActivatedAtUtc).IsRequired();
            });

            // SystemSettings configuration
            builder.Entity<SystemSettings>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.TenantId).IsRequired();
                entity.HasOne(e => e.Tenant)
                    .WithMany()
                    .HasForeignKey(e => e.TenantId)
                    .OnDelete(DeleteBehavior.Restrict);
                entity.HasIndex(e => e.TenantId).IsUnique();
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
                entity.Property(e => e.SessionTimeoutMinutes).HasColumnName("session_timeout_minutes").IsRequired();
                entity.Property(e => e.SessionWarningBeforeTimeoutMinutes).HasColumnName("session_warning_before_timeout_minutes").IsRequired();
                entity.Property(e => e.KeepCartAfterTimeout).HasColumnName("keep_cart_after_timeout").IsRequired();
                entity.Property(e => e.SessionIdleTimeoutEnabled).HasColumnName("session_idle_timeout_enabled").IsRequired();
                
                entity.HasIndex(e => new { e.TenantId, e.CompanyTaxNumber }).IsUnique();
            });

            builder.Entity<CashRegisterSettings>(entity =>
            {
                entity.HasKey(e => e.TenantId);
                entity.HasOne(e => e.Tenant)
                    .WithMany()
                    .HasForeignKey(e => e.TenantId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.Property(e => e.EffectiveDefaultOnPosEntry).IsRequired().HasDefaultValue(true);
                entity.Property(e => e.AutoOpenSoleClosedRegister).IsRequired().HasDefaultValue(false);
                entity.Property(e => e.AutoOpenAssignedClosedRegister).IsRequired().HasDefaultValue(false);
                entity.Property(e => e.DefaultAutoOpenOpeningBalance).IsRequired().HasColumnType("decimal(18,2)").HasDefaultValue(0m);
                entity.Property(e => e.UpdatedAtUtc).IsRequired();
            });

            // AuditLog: table audit_logs with snake_case columns everywhere (PostgreSQL default; no quoted identifiers).
            // User navigation ignored so EF never joins AspNetUsers.
            builder.Entity<AuditLog>(entity =>
            {
                entity.ToTable("audit_logs");
                entity.Ignore(e => e.User);
                entity.HasKey(e => e.Id);
                entity.Property(e => e.TenantId).HasColumnName("tenant_id").IsRequired();
                entity.HasOne(e => e.Tenant)
                    .WithMany()
                    .HasForeignKey(e => e.TenantId)
                    .OnDelete(DeleteBehavior.Restrict);
                entity.HasIndex(e => e.TenantId);
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
                entity.Property(e => e.ActorDisplayName).HasMaxLength(200).HasColumnName("actor_display_name");
                entity.Property(e => e.Changes).HasColumnType("jsonb").HasColumnName("changes");
                entity.Property(e => e.Metadata).HasColumnType("jsonb").HasColumnName("metadata");
                entity.Property(e => e.ActionType).HasColumnName("action_type");
                entity.Property(e => e.ImpersonatedBy).HasMaxLength(450).HasColumnName("impersonated_by");
                entity.Property(e => e.ImpersonatedTenantId).HasColumnName("impersonated_tenant");

                entity.HasIndex(e => e.ImpersonatedTenantId);
                entity.HasIndex(e => e.ImpersonatedBy);

                entity.HasIndex(e => e.Timestamp);
                entity.HasIndex(e => e.Action);
                entity.HasIndex(e => e.EntityType);
                entity.HasIndex(e => e.EntityId);
                entity.HasIndex(e => e.UserId);

                // Tenant-scoped list + user lifecycle queries (ORDER BY timestamp DESC)
                entity.HasIndex(e => new { e.TenantId, e.Timestamp })
                    .IsDescending(false, true);
                entity.HasIndex(e => new { e.EntityType, e.EntityName, e.Timestamp })
                    .IsDescending(false, false, true);
                entity.HasIndex(e => new { e.EntityType, e.EntityId, e.Timestamp })
                    .IsDescending(false, false, true);
            });

            // CompanySettings configuration
            builder.Entity<CompanySettings>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.TenantId).IsRequired();
                entity.HasOne(e => e.Tenant)
                    .WithMany()
                    .HasForeignKey(e => e.TenantId)
                    .OnDelete(DeleteBehavior.Restrict);
                entity.HasIndex(e => e.TenantId).IsUnique();
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
                
                entity.HasIndex(e => new { e.TenantId, e.CompanyTaxNumber }).IsUnique();
                entity.HasIndex(e => new { e.TenantId, e.CompanyRegistrationNumber }).IsUnique();
                entity.HasIndex(e => new { e.TenantId, e.CompanyVatNumber }).IsUnique();
            });

            // LocalizationSettings configuration
            builder.Entity<LocalizationSettings>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.TenantId).IsRequired();
                entity.HasOne(e => e.Tenant)
                    .WithMany()
                    .HasForeignKey(e => e.TenantId)
                    .OnDelete(DeleteBehavior.Restrict);
                entity.HasIndex(e => e.TenantId).IsUnique();
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

            builder.Entity<DashboardPreferences>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.UserId).IsRequired().HasMaxLength(450);
                entity.Property(e => e.TenantId).IsRequired();
                entity.Property(e => e.UpdatedAtUtc).IsRequired();
                entity.Property(e => e.Widgets)
                    .HasColumnType("jsonb")
                    .HasConversion(
                        v => JsonSerializer.Serialize(v),
                        v => string.IsNullOrEmpty(v)
                            ? new List<DashboardWidget>()
                            : JsonSerializer.Deserialize<List<DashboardWidget>>(v) ?? new List<DashboardWidget>());
                entity.HasIndex(e => new { e.UserId, e.TenantId }).IsUnique();
                entity.HasOne<ApplicationUser>()
                    .WithMany()
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<UserPreferences>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.UserId).IsRequired().HasMaxLength(450);
                entity.Property(e => e.ThemeMode).IsRequired().HasMaxLength(10);
                entity.Property(e => e.DensityMode).IsRequired().HasMaxLength(20);
                entity.Property(e => e.DefaultPage).IsRequired().HasMaxLength(200);
                entity.Property(e => e.DateFormat).HasMaxLength(20);
                entity.Property(e => e.TimeFormat).HasMaxLength(10);
                entity.Property(e => e.UpdatedAtUtc).IsRequired();
                entity.HasIndex(e => e.UserId).IsUnique();
                entity.HasOne<ApplicationUser>()
                    .WithMany()
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<ActivityEvent>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Title).IsRequired().HasMaxLength(200);
                entity.Property(e => e.Description).HasMaxLength(2000);
                entity.Property(e => e.Severity).IsRequired().HasMaxLength(20);
                entity.Property(e => e.DedupKey).HasMaxLength(120);
                entity.Property(e => e.ActorUserId).HasMaxLength(450);
                entity.Property(e => e.ActorName).HasMaxLength(200);
                entity.Property(e => e.EntityType).HasMaxLength(100);
                entity.Property(e => e.EntityId).HasMaxLength(100);
                entity.HasIndex(e => new { e.TenantId, e.CreatedAtUtc });
                entity.HasIndex(e => new { e.TenantId, e.Severity });
                entity.HasIndex(e => new { e.TenantId, e.DedupKey })
                    .IsUnique()
                    .HasFilter("\"dedup_key\" IS NOT NULL");
            });

            builder.Entity<ActivityEventRead>(entity =>
            {
                entity.HasKey(e => new { e.ActivityEventId, e.UserId });
                entity.Property(e => e.UserId).IsRequired().HasMaxLength(450);
                entity.HasOne(e => e.ActivityEvent)
                    .WithMany()
                    .HasForeignKey(e => e.ActivityEventId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<TenantNotificationConfig>(entity =>
            {
                entity.HasKey(e => e.TenantId);
                entity.Property(e => e.UpdatedAtUtc).IsRequired();
                entity.Property(e => e.Config)
                    .HasColumnType("jsonb")
                    .HasConversion(
                        v => NotificationConfigJson.Serialize(v),
                        v => NotificationConfigJson.Deserialize(v));
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

            // TSE Device configuration — explicit column names only (no schema change); avoids convention drift and BaseEntity is_active vs IsActive mismatch on this legacy table.
            builder.Entity<TseDevice>(entity =>
            {
                entity.ToTable("TseDevices");
                entity.HasKey(e => e.Id);

                // BaseEntity columns - explicit mapping for this mixed-schema legacy table
                entity.Property(e => e.Id)
                    .HasColumnName("id");

                entity.Property(e => e.CreatedAt)
                    .HasColumnName("created_at");

                entity.Property(e => e.UpdatedAt)
                    .HasColumnName("updated_at");

                entity.Property(e => e.CreatedBy)
                    .HasColumnName("created_by")
                    .HasMaxLength(450);

                entity.Property(e => e.UpdatedBy)
                    .HasColumnName("updated_by")
                    .HasMaxLength(450);

                // Legacy PascalCase column in TseDevices table (overrides BaseEntity [Column("is_active")])
                entity.Property(e => e.IsActive)
                    .HasColumnName("IsActive");

                // TseDevice-specific columns - explicit names to avoid future naming-convention drift
                entity.Property(e => e.SerialNumber)
                    .HasColumnName("SerialNumber")
                    .IsRequired()
                    .HasMaxLength(100);

                entity.Property(e => e.DeviceType)
                    .HasColumnName("DeviceType")
                    .IsRequired()
                    .HasMaxLength(50);

                entity.Property(e => e.VendorId)
                    .HasColumnName("VendorId");

                entity.Property(e => e.ProductId)
                    .HasColumnName("ProductId");

                entity.Property(e => e.IsConnected)
                    .HasColumnName("IsConnected");

                entity.Property(e => e.LastConnectionTime)
                    .HasColumnName("LastConnectionTime");

                entity.Property(e => e.LastSignatureTime)
                    .HasColumnName("LastSignatureTime");

                entity.Property(e => e.CertificateStatus)
                    .HasColumnName("CertificateStatus")
                    .HasMaxLength(50);

                entity.Property(e => e.MemoryStatus)
                    .HasColumnName("MemoryStatus")
                    .HasMaxLength(50);

                entity.Property(e => e.CanCreateInvoices)
                    .HasColumnName("CanCreateInvoices");

                entity.Property(e => e.ErrorMessage)
                    .HasColumnName("ErrorMessage")
                    .HasMaxLength(500);

                entity.Property(e => e.TimeoutSeconds)
                    .HasColumnName("TimeoutSeconds");

                entity.Property(e => e.KassenId)
                    .HasColumnName("KassenId");

                entity.Property(e => e.FinanzOnlineUsername)
                    .HasColumnName("FinanzOnlineUsername")
                    .HasMaxLength(100);

                entity.Property(e => e.FinanzOnlineEnabled)
                    .HasColumnName("FinanzOnlineEnabled");

                entity.Property(e => e.LastFinanzOnlineSync)
                    .HasColumnName("LastFinanzOnlineSync");

                entity.Property(e => e.PendingInvoices)
                    .HasColumnName("PendingInvoices");

                entity.Property(e => e.PendingReports)
                    .HasColumnName("PendingReports");

                entity.HasIndex(e => e.SerialNumber)
                    .IsUnique();
            });

            // DailyClosing configuration
            builder.Entity<DailyClosing>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.TenantId).HasColumnName("tenant_id").IsRequired();
                entity.HasOne(e => e.Tenant)
                    .WithMany()
                    .HasForeignKey(e => e.TenantId)
                    .OnDelete(DeleteBehavior.Restrict);
                entity.HasIndex(e => e.TenantId);
                entity.Property(e => e.CashRegisterId).IsRequired().HasMaxLength(50);
                entity.Property(e => e.UserId).IsRequired();
                entity.Property(e => e.ClosingType).IsRequired().HasMaxLength(20);
                entity.Property(e => e.TotalAmount).HasColumnType("decimal(18,2)");
                entity.Property(e => e.TotalTaxAmount).HasColumnType("decimal(18,2)");
                entity.Property(e => e.TseSignature).IsRequired().HasColumnType("text");
                entity.Property(e => e.CertificateThumbprint)
                    .HasMaxLength(64)
                    .HasColumnName("certificate_thumbprint");
                entity.HasIndex(e => e.CertificateThumbprint)
                    .HasFilter("\"certificate_thumbprint\" IS NOT NULL");
                entity.Property(e => e.JwsHeader).HasColumnType("text");
                entity.Property(e => e.JwsPayload).HasColumnType("text");
                entity.Property(e => e.JwsSignature).HasColumnType("text");
                entity.Property(e => e.Status).IsRequired().HasMaxLength(20);
                entity.Property(e => e.FinanzOnlineStatus).HasMaxLength(20);
                entity.Property(e => e.FinanzOnlineError).HasMaxLength(500);
                entity.Property(e => e.FinanzOnlineReferenceId).HasMaxLength(100);
                entity.HasIndex(e => new { e.CashRegisterId, e.ClosingDate, e.ClosingType });
                entity.HasOne(e => e.User).WithMany().HasForeignKey(e => e.UserId);
                entity.HasOne(e => e.CashRegister).WithMany().HasForeignKey(e => e.CashRegisterId);
            });

            builder.Entity<TagesberichtReport>(entity =>
            {
                entity.ToTable("tagesbericht_reports");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.ViennaBusinessDate).HasColumnType("date");
                entity.Property(e => e.SnapshotJson).HasColumnType("jsonb").IsRequired();
                entity.Property(e => e.SnapshotHash).IsRequired().HasMaxLength(64);
                entity.Property(e => e.SnapshotSchemaVersion).IsRequired().HasMaxLength(20);
                entity.Property(e => e.ReportStatus).IsRequired().HasMaxLength(30);
                entity.Property(e => e.CorrectionKind).IsRequired().HasMaxLength(30);
                entity.Property(e => e.ReportRevisionReason).HasMaxLength(200);
                entity.Property(e => e.RebuildCause).HasMaxLength(80);
                entity.Property(e => e.CorrectionType).IsRequired().HasMaxLength(40);
                entity.Property(e => e.SubmissionImpact).IsRequired().HasMaxLength(40);
                entity.Property(e => e.StoreLabel).HasMaxLength(200);
                entity.Property(e => e.OperatorUserIdScope).HasMaxLength(450);
                entity.Property(e => e.CreatedByUserId).IsRequired().HasMaxLength(450);
                entity.Property(e => e.FinalizedByUserId).HasMaxLength(450);
                entity.Property(e => e.LastSubmissionStatusCode).HasMaxLength(40);
                entity.Property(e => e.LastSubmissionError).HasMaxLength(500);
                entity.Property(e => e.SnapshotGrossSalesAmount).HasColumnType("decimal(18,2)");
                entity.HasIndex(e => new { e.ViennaBusinessDate, e.CashRegisterId, e.ReportStatus });
                entity.HasIndex(e => new { e.OriginalReportId, e.ReportVersion });
                entity.HasOne(e => e.CashRegister).WithMany().HasForeignKey(e => e.CashRegisterId);
            });

            builder.Entity<MonatsberichtReport>(entity =>
            {
                entity.ToTable("monatsbericht_reports");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.ViennaMonthStart).HasColumnType("date");
                entity.Property(e => e.SnapshotJson).HasColumnType("jsonb").IsRequired();
                entity.Property(e => e.SnapshotHash).IsRequired().HasMaxLength(64);
                entity.Property(e => e.SnapshotSchemaVersion).IsRequired().HasMaxLength(20);
                entity.Property(e => e.ScopeKind).IsRequired().HasMaxLength(20);
                entity.Property(e => e.ReportStatus).IsRequired().HasMaxLength(30);
                entity.Property(e => e.CorrectionKind).IsRequired().HasMaxLength(30);
                entity.Property(e => e.ReportRevisionReason).HasMaxLength(200);
                entity.Property(e => e.RebuildCause).HasMaxLength(80);
                entity.Property(e => e.CorrectionType).IsRequired().HasMaxLength(40);
                entity.Property(e => e.SubmissionImpact).IsRequired().HasMaxLength(40);
                entity.Property(e => e.StoreLabel).HasMaxLength(200);
                entity.Property(e => e.CreatedByUserId).IsRequired().HasMaxLength(450);
                entity.Property(e => e.FinalizedByUserId).HasMaxLength(450);
                entity.Property(e => e.LastSubmissionStatusCode).HasMaxLength(40);
                entity.Property(e => e.LastSubmissionError).HasMaxLength(500);
                entity.Property(e => e.SnapshotGrossSalesAmount).HasColumnType("decimal(18,2)");
                entity.Property(e => e.UpstreamReviewRequired).HasDefaultValue(false);
                entity.Property(e => e.UpstreamReviewReasonCode).HasMaxLength(80);
                entity.HasIndex(e => new { e.ViennaMonthStart, e.ScopeKind, e.ReportStatus });
                entity.HasIndex(e => new { e.OriginalReportId, e.ReportVersion });
                entity.HasOne(e => e.CashRegister).WithMany().HasForeignKey(e => e.CashRegisterId).IsRequired(false);
            });

            builder.Entity<JahresberichtReport>(entity =>
            {
                entity.ToTable("jahresbericht_reports");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.ViennaYearStart).HasColumnType("date");
                entity.Property(e => e.SnapshotJson).HasColumnType("jsonb").IsRequired();
                entity.Property(e => e.SnapshotHash).IsRequired().HasMaxLength(64);
                entity.Property(e => e.SnapshotSchemaVersion).IsRequired().HasMaxLength(20);
                entity.Property(e => e.ScopeKind).IsRequired().HasMaxLength(20);
                entity.Property(e => e.ReportStatus).IsRequired().HasMaxLength(30);
                entity.Property(e => e.CorrectionKind).IsRequired().HasMaxLength(30);
                entity.Property(e => e.ReportRevisionReason).HasMaxLength(200);
                entity.Property(e => e.RebuildCause).HasMaxLength(80);
                entity.Property(e => e.CorrectionType).IsRequired().HasMaxLength(40);
                entity.Property(e => e.SubmissionImpact).IsRequired().HasMaxLength(40);
                entity.Property(e => e.StoreLabel).HasMaxLength(200);
                entity.Property(e => e.CreatedByUserId).IsRequired().HasMaxLength(450);
                entity.Property(e => e.FinalizedByUserId).HasMaxLength(450);
                entity.Property(e => e.LastSubmissionStatusCode).HasMaxLength(40);
                entity.Property(e => e.LastSubmissionError).HasMaxLength(500);
                entity.Property(e => e.SnapshotGrossSalesAmount).HasColumnType("decimal(18,2)");
                entity.Property(e => e.UpstreamReviewRequired).HasDefaultValue(false);
                entity.Property(e => e.UpstreamReviewReasonCode).HasMaxLength(80);
                entity.HasIndex(e => new { e.ViennaYearStart, e.ScopeKind, e.ReportStatus });
                entity.HasIndex(e => new { e.OriginalReportId, e.ReportVersion });
                entity.HasOne(e => e.CashRegister).WithMany().HasForeignKey(e => e.CashRegisterId).IsRequired(false);
            });

            builder.Entity<PeriodenberichtRun>(entity =>
            {
                entity.ToTable("periodenbericht_runs");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.PeriodPreset).IsRequired().HasMaxLength(50);
                entity.Property(e => e.PeriodStartLocalDate).HasColumnType("date");
                entity.Property(e => e.PeriodEndLocalDate).HasColumnType("date");
                entity.Property(e => e.ScopeKind).IsRequired().HasMaxLength(20);
                entity.Property(e => e.CashierId).HasMaxLength(450);
                entity.Property(e => e.QueryParametersJson).HasColumnType("jsonb").IsRequired();
                entity.Property(e => e.QueryParametersHash).IsRequired().HasMaxLength(64);
                entity.Property(e => e.SnapshotJson).HasColumnType("jsonb").IsRequired();
                entity.Property(e => e.SnapshotHash).IsRequired().HasMaxLength(64);
                entity.Property(e => e.SnapshotSchemaVersion).IsRequired().HasMaxLength(20);
                entity.Property(e => e.GrossTotalAmount).HasColumnType("decimal(18,2)");
                entity.Property(e => e.TaxTotalAmount).HasColumnType("decimal(18,2)");
                entity.Property(e => e.RefundAmountTotal).HasColumnType("decimal(18,2)");
                entity.Property(e => e.WarningsJson).HasColumnType("jsonb").IsRequired();
                entity.Property(e => e.CreatedByUserId).IsRequired().HasMaxLength(450);
                entity.Property(e => e.ExportProfileKey).HasMaxLength(50);
                entity.Property(e => e.CorrelationId).HasMaxLength(100);
                entity.HasIndex(e => e.CreatedAtUtc);
                entity.HasIndex(e => new { e.PeriodStartLocalDate, e.PeriodEndLocalDate, e.ScopeKind });
                entity.HasIndex(e => e.QueryParametersHash);
                entity.HasOne(e => e.CashRegister).WithMany().HasForeignKey(e => e.CashRegisterId).IsRequired(false);
            });

            // TseSignature configuration
            builder.Entity<TseSignature>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.TenantId).HasColumnName("tenant_id").IsRequired();
                entity.HasOne(e => e.Tenant)
                    .WithMany()
                    .HasForeignKey(e => e.TenantId)
                    .OnDelete(DeleteBehavior.Restrict);
                entity.HasIndex(e => e.TenantId);
                entity.Property(e => e.Signature).IsRequired().HasColumnType("text");
                entity.Property(e => e.CashRegisterId).IsRequired();
                entity.Property(e => e.InvoiceNumber).IsRequired().HasMaxLength(100);
                entity.Property(e => e.Amount).HasColumnType("decimal(18,2)");
                entity.Property(e => e.SignatureType).IsRequired().HasMaxLength(50);
                entity.Property(e => e.TseDeviceId);
                entity.Property(e => e.CertificateNumber).HasMaxLength(100);
                entity.Property(e => e.JwsHeader).HasColumnType("text");
                entity.Property(e => e.JwsPayload).HasColumnType("text");
                entity.Property(e => e.JwsSignature).HasColumnType("text");
                // B-tree unique index exceeds PG btree max index row size (~2704 B) for long compact JWS; hash supports equality lookups only.
                entity.HasIndex(e => e.Signature)
                    .HasDatabaseName("IX_TseSignatures_Signature_Hash")
                    .HasMethod("hash");
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

            builder.Entity<FinanzOnlineOutboxMessage>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.TenantId).HasMaxLength(64);
                entity.Property(e => e.BranchId).HasMaxLength(64);
                entity.Property(e => e.AggregateType).IsRequired().HasMaxLength(50);
                entity.Property(e => e.MessageType).IsRequired().HasMaxLength(80);
                entity.Property(e => e.BusinessKey).IsRequired().HasMaxLength(120);
                entity.Property(e => e.IdempotencyKey).IsRequired().HasMaxLength(120);
                entity.Property(e => e.PayloadHash).IsRequired().HasMaxLength(128);
                entity.Property(e => e.Mode).IsRequired().HasMaxLength(10);
                entity.Property(e => e.Status).IsRequired().HasMaxLength(30);
                entity.Property(e => e.LastErrorCode).HasMaxLength(80);
                entity.Property(e => e.LastErrorMessage).HasMaxLength(500);
                entity.Property(e => e.FailureCategory).HasMaxLength(40);
                entity.Property(e => e.TransmissionId).HasMaxLength(120);
                entity.Property(e => e.ExternalReferenceId).HasMaxLength(120);
                entity.Property(e => e.ExternalStatus).HasMaxLength(40);
                entity.Property(e => e.ProtocolCode).HasMaxLength(80);
                entity.Property(e => e.ProtocolPayloadHash).HasMaxLength(128);
                entity.Property(e => e.ProtocolSummary).HasMaxLength(500);
                entity.Property(e => e.ProcessingToken).HasMaxLength(64);
                entity.Property(e => e.CorrelationId).HasMaxLength(120);

                entity.HasIndex(e => e.IdempotencyKey).IsUnique();
                entity.HasIndex(e => new { e.Status, e.NextAttemptAt });
                entity.HasIndex(e => new { e.AggregateType, e.AggregateId, e.MessageType });
                entity.HasIndex(e => new { e.TenantId, e.BranchId, e.BusinessKey, e.MessageType });
                entity.HasIndex(e => new { e.TenantId, e.BranchId, e.MessageType, e.BusinessKey, e.PayloadHash, e.Mode })
                    .IsUnique();
                entity.HasIndex(e => e.TransmissionId).HasFilter("\"TransmissionId\" IS NOT NULL");
            });

            builder.Entity<RksvSpecialReceiptFinanzOnlineSubmission>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.RawResponseSnapshot).HasColumnType("jsonb");
                entity.HasIndex(e => e.PaymentId).IsUnique();
                entity.HasIndex(e => new { e.CashRegisterId, e.Kind });

                entity.HasOne<PaymentDetails>()
                    .WithMany()
                    .HasForeignKey(e => e.PaymentId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne<Receipt>()
                    .WithMany()
                    .HasForeignKey(e => e.ReceiptId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.HasOne<CashRegister>()
                    .WithMany()
                    .HasForeignKey(e => e.CashRegisterId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // Cart configuration - Güvenlik ve performans için index'ler
            builder.Entity<Cart>(entity =>
            {
                entity.HasKey(e => e.CartId);
                entity.HasAlternateKey(e => e.Id);
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
                entity.Property(e => e.AppliedPricingRuleId).HasColumnName("applied_pricing_rule_id");

                // Foreign key relationships
                entity.HasOne(e => e.Cart)
                    .WithMany(c => c.Items)
                    .HasForeignKey(e => e.CartId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne<PricingRule>()
                    .WithMany()
                    .HasForeignKey(e => e.AppliedPricingRuleId)
                    .OnDelete(DeleteBehavior.SetNull);

                // Product relationship removed to prevent shadow property conflicts
                // entity.HasOne(e => e.Product)
                //     .WithMany()
                //     .HasForeignKey(e => e.ProductId)
                //     .OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<PricingRule>(entity =>
            {
                entity.Property(e => e.TargetScope).HasConversion<int>();
                entity.Property(e => e.ActionType).HasConversion<int>();
                entity.HasIndex(e => new { e.IsActive, e.ValidFromDate, e.ValidToDate });
                entity.HasIndex(e => e.CashRegisterId);
                entity.HasOne<CashRegister>()
                    .WithMany()
                    .HasForeignKey(e => e.CashRegisterId)
                    .OnDelete(DeleteBehavior.SetNull);
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
                entity.Property(e => e.IdempotencyKey).HasMaxLength(64);
                entity.HasIndex(e => e.IdempotencyKey).IsUnique().HasFilter("\"idempotency_key\" IS NOT NULL");
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
               entity.Property(e => e.TenantId).HasColumnName("tenant_id").IsRequired();
               entity.HasOne(e => e.Tenant)
                   .WithMany()
                   .HasForeignKey(e => e.TenantId)
                   .OnDelete(DeleteBehavior.Restrict);
               entity.HasIndex(e => e.TenantId);
               entity.HasIndex(e => e.ReceiptNumber).IsUnique();
               entity.HasIndex(e => e.PaymentId);
               entity.Property(e => e.ReceiptNumber).IsRequired().HasColumnType("text");
               entity.Property(e => e.JwsHeader).HasColumnType("text");
               entity.Property(e => e.JwsPayload).HasColumnType("text");
               entity.Property(e => e.JwsSignature).HasColumnType("text");
               entity.Property(e => e.SignatureValue).HasColumnType("text");
               entity.Property(e => e.PrevSignatureValue).HasColumnType("text");
            });

            // Receipt sequence: one row per (CashRegisterId, date).
            builder.Entity<ReceiptSequence>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => new { e.CashRegisterId, e.SequenceDate }).IsUnique();
                entity.Property(e => e.CashRegisterId).IsRequired().HasColumnName("cash_register_id");
                entity.Property(e => e.SequenceDate).IsRequired();
                entity.Property(e => e.NextSequence).IsRequired();
                entity.Property(e => e.UpdatedAt).IsRequired();
            });

            // Signature chain state: one row per cash register (UUID); FOR UPDATE when signing.
            builder.Entity<SignatureChainState>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.TenantId).HasColumnName("tenant_id").IsRequired();
                entity.HasOne(e => e.Tenant)
                    .WithMany()
                    .HasForeignKey(e => e.TenantId)
                    .OnDelete(DeleteBehavior.Restrict);
                entity.HasIndex(e => e.TenantId);
                entity.HasIndex(e => e.CashRegisterId).IsUnique();
                entity.Property(e => e.CashRegisterId).IsRequired().HasColumnName("cash_register_id");
                entity.Property(e => e.LastSignature).HasColumnType("text");
                entity.Property(e => e.LastCounter).IsRequired();
                entity.Property(e => e.LastTurnoverCounterCents).IsRequired().HasColumnName("last_turnover_counter_cents");
                entity.Property(e => e.UpdatedAt).IsRequired();
            });

            // Sprint 5: Legal hold – date range; audit cleanup must not delete logs whose date falls within an active hold.
            builder.Entity<LegalHold>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.FromDate).IsRequired();
                entity.Property(e => e.ToDate).IsRequired();
                entity.Property(e => e.IsActive).IsRequired();
                entity.Property(e => e.CreatedAt).IsRequired();
            });

            builder.Entity<AuditReportSchedule>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.TenantId).IsRequired();
                entity.Property(e => e.CreatedByUserId).IsRequired().HasMaxLength(450);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
                entity.Property(e => e.FiltersJson).IsRequired().HasColumnType("jsonb");
                entity.Property(e => e.ScheduleCron).IsRequired().HasMaxLength(100);
                entity.Property(e => e.RecipientsJson).IsRequired().HasColumnType("jsonb");
                entity.Property(e => e.Format).IsRequired().HasMaxLength(20);
                entity.Property(e => e.IsActive).IsRequired();
                entity.Property(e => e.CreatedAtUtc).IsRequired();
                entity.HasIndex(e => new { e.TenantId, e.IsActive, e.NextRunUtc });
            });

            builder.Entity<OperationalReportSchedule>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.TenantId).IsRequired();
                entity.Property(e => e.CreatedByUserId).IsRequired().HasMaxLength(450);
                entity.Property(e => e.ReportType).IsRequired().HasMaxLength(80);
                entity.Property(e => e.FiltersJson).IsRequired().HasColumnType("jsonb");
                entity.Property(e => e.ScheduleCron).IsRequired().HasMaxLength(100);
                entity.Property(e => e.RecipientsJson).IsRequired().HasColumnType("jsonb");
                entity.Property(e => e.Format).IsRequired().HasMaxLength(20);
                entity.Property(e => e.IsActive).IsRequired();
                entity.Property(e => e.CreatedAtUtc).IsRequired();
                entity.HasIndex(e => new { e.TenantId, e.IsActive, e.NextRunUtc });
            });

            builder.Entity<DepExportHistory>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.TenantId).IsRequired();
                entity.Property(e => e.CashRegisterId).IsRequired();
                entity.Property(e => e.FromUtc).IsRequired();
                entity.Property(e => e.ToUtc).IsRequired();
                entity.Property(e => e.ExportedAt).IsRequired();
                entity.Property(e => e.ExportedByUserId).IsRequired().HasMaxLength(450);
                entity.Property(e => e.FileName).IsRequired().HasMaxLength(260);
                entity.Property(e => e.FileSizeBytes).IsRequired();
                entity.Property(e => e.SignatureCount).IsRequired();
                entity.Property(e => e.GroupCount).IsRequired();
                entity.Property(e => e.Status).IsRequired().HasMaxLength(32);
                entity.Property(e => e.StoragePath).HasMaxLength(500);
                entity.HasIndex(e => new { e.TenantId, e.CashRegisterId, e.ExportedAt });
                entity.HasIndex(e => e.ScheduleId).HasFilter("\"schedule_id\" IS NOT NULL");
            });

            builder.Entity<DepExportSchedule>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.TenantId).IsRequired();
                entity.Property(e => e.CashRegisterId).IsRequired();
                entity.Property(e => e.ScheduleType).IsRequired().HasMaxLength(16);
                entity.Property(e => e.DayOfMonth).IsRequired();
                entity.Property(e => e.TimeOfDay).IsRequired().HasMaxLength(5);
                entity.Property(e => e.IsActive).IsRequired();
                entity.Property(e => e.CreatedAt).IsRequired();
                entity.HasIndex(e => new { e.TenantId, e.IsActive, e.NextRunAt });
                entity.HasIndex(e => e.CashRegisterId);
            });

            builder.Entity<BackupRuntimeExecutionPreference>(entity =>
            {
                entity.ToTable("backup_runtime_execution_preferences");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Mode).IsRequired();
                entity.Property(e => e.UpdatedAtUtc).IsRequired();
                entity.Property(e => e.UpdatedByUserId).HasMaxLength(450);
            });

            builder.Entity<BackupSettings>(entity =>
            {
                entity.ToTable("backup_settings");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.ScheduleCron).IsRequired().HasMaxLength(120);
                entity.Property(e => e.RetentionDays).IsRequired();
                entity.Property(e => e.UpdatedAtUtc).IsRequired();
            });

            builder.Entity<BackupScheduleConfiguration>(entity =>
            {
                entity.ToTable("backup_schedule_configurations");
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.TenantId).IsUnique();
                entity.Property(e => e.ScheduleCron).IsRequired().HasMaxLength(120);
                entity.Property(e => e.RetentionDays).IsRequired();
                entity.HasOne<Tenant>()
                    .WithMany()
                    .HasForeignKey(e => e.TenantId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<DevelopmentModeSettings>(entity =>
            {
                entity.ToTable("development_mode_settings");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Enabled).IsRequired();
                entity.Property(e => e.BypassLicense).IsRequired();
                entity.Property(e => e.BypassNtpCheck).IsRequired();
                entity.Property(e => e.BypassTseCheck).IsRequired();
                entity.Property(e => e.SimulateOffline).IsRequired();
                entity.Property(e => e.ForceOnline).IsRequired();
                entity.Property(e => e.ValidDays).IsRequired();
                entity.Property(e => e.Features)
                    .HasColumnType("jsonb")
                    .HasConversion(
                        v => JsonSerializer.Serialize(v),
                        v => string.IsNullOrEmpty(v) ? Array.Empty<string>() : JsonSerializer.Deserialize<string[]>(v) ?? Array.Empty<string>())
                    .IsRequired();
                entity.Property(e => e.UpdatedAtUtc).IsRequired();
                entity.HasData(new global::KasseAPI_Final.Models.DevelopmentModeSettings
                {
                    Id = global::KasseAPI_Final.Models.DevelopmentModeSettings.SingletonId,
                    Enabled = true,
                    BypassLicense = true,
                    BypassNtpCheck = true,
                    BypassTseCheck = true,
                    SimulateOffline = false,
                    ForceOnline = true,
                    ValidDays = 365,
                    Features = [],
                    UpdatedAtUtc = new DateTime(2026, 6, 11, 12, 0, 0, DateTimeKind.Utc),
                    UpdatedByUserId = null,
                });
            });

            builder.Entity<BackupRun>(entity =>
            {
                entity.ToTable("backup_runs");
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.RequestedAt);
                entity.HasIndex(e => e.Status);
                entity.HasIndex(e => e.NextRetryAtUtc)
                    .HasDatabaseName("ix_backup_runs_next_retry_at")
                    .HasFilter("next_retry_at_utc IS NOT NULL");
                entity.HasIndex(e => e.LeaseExpiresAtUtc)
                    .HasDatabaseName("ix_backup_runs_lease_expires_stale_reaper")
                    .HasFilter("status IN (1, 2)");
                entity.HasIndex(e => e.IdempotencyKey)
                    .IsUnique()
                    .HasFilter("idempotency_key IS NOT NULL");
                entity.HasMany(e => e.Artifacts)
                    .WithOne(a => a.BackupRun!)
                    .HasForeignKey(a => a.BackupRunId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasMany(e => e.Verifications)
                    .WithOne(v => v.BackupRun!)
                    .HasForeignKey(v => v.BackupRunId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<BackupArtifact>(entity =>
            {
                entity.ToTable("backup_artifacts");
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.BackupRunId);
            });

            builder.Entity<BackupVerification>(entity =>
            {
                entity.ToTable("backup_verifications");
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.BackupRunId);
                entity.HasIndex(e => e.StartedAt);
            });

            builder.Entity<RestoreVerificationRun>(entity =>
            {
                entity.ToTable("restore_verification_runs");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.IdempotencyKey).HasMaxLength(200);
                entity.HasIndex(e => e.RequestedAt);
                entity.HasIndex(e => e.Status);
                entity.HasIndex(e => e.LeaseExpiresAtUtc)
                    .HasDatabaseName("ix_restore_verification_runs_lease_expires_stale_reaper")
                    .HasFilter("status = 1");
                entity.HasIndex(e => e.SourceBackupRunId);
                entity.HasOne(e => e.SourceBackupRun)
                    .WithMany()
                    .HasForeignKey(e => e.SourceBackupRunId)
                    .OnDelete(DeleteBehavior.SetNull);
                entity.HasIndex(e => e.SourceBackupArtifactId);
                entity.HasOne(e => e.SourceBackupArtifact)
                    .WithMany()
                    .HasForeignKey(e => e.SourceBackupArtifactId)
                    .OnDelete(DeleteBehavior.SetNull);
                entity.HasIndex(e => e.IdempotencyKey)
                    .IsUnique()
                    .HasDatabaseName("ux_restore_verification_runs_idempotency_key")
                    .HasFilter("idempotency_key IS NOT NULL");
                entity.Property(e => e.PostRestoreL4ContinuityProofState).HasConversion<int>();
            });

            builder.Entity<ManualRestoreRequest>(entity =>
            {
                entity.ToTable("manual_restore_requests");
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.Status);
                entity.HasIndex(e => e.RequestedAt);
                entity.HasIndex(e => e.BackupRunId);
                entity.HasOne(e => e.BackupRun)
                    .WithMany()
                    .HasForeignKey(e => e.BackupRunId)
                    .OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(e => e.RestoreVerificationRun)
                    .WithMany()
                    .HasForeignKey(e => e.RestoreVerificationRunId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            builder.Entity<PaymentReversalApproval>(entity =>
            {
                entity.ToTable("payment_reversal_approvals");
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.TenantId);
                entity.HasIndex(e => new { e.TenantId, e.PaymentId, e.Status });
                entity.HasIndex(e => e.IdempotencyKey)
                    .HasDatabaseName("ix_payment_reversal_approvals_idempotency_key")
                    .HasFilter("idempotency_key IS NOT NULL");
                entity.Property(e => e.Operation).HasConversion<int>();
                entity.Property(e => e.Status).HasConversion<int>();
            });

            builder.Entity<SuspiciousTransactionAlert>(entity =>
            {
                entity.ToTable("suspicious_transaction_alerts");
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.TenantId);
                entity.HasIndex(e => new { e.TenantId, e.DedupKey, e.DetectedAtUtc });
                entity.HasIndex(e => new { e.TenantId, e.Status, e.DetectedAtUtc });
                entity.Property(e => e.AlertType).HasConversion<int>();
                entity.Property(e => e.Severity).HasConversion<int>();
                entity.Property(e => e.Status).HasConversion<int>();
            });

            // Vouchers (Gutscheine): configured last so FK delete behaviors are not overridden by model conventions.
            builder.Entity<Voucher>(entity =>
            {
                entity.ToTable("vouchers");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.TenantId).HasColumnName("tenant_id").IsRequired();
                entity.Property(e => e.CodeHash).HasColumnName("code_hash").IsRequired().HasMaxLength(64);
                entity.Property(e => e.MaskedCode).HasColumnName("masked_code").IsRequired().HasMaxLength(32);
                entity.Property(e => e.InitialAmount).HasColumnName("initial_amount").HasColumnType("decimal(18,2)").IsRequired();
                entity.Property(e => e.RemainingAmount).HasColumnName("remaining_amount").HasColumnType("decimal(18,2)").IsRequired();
                entity.Property(e => e.Currency).HasColumnName("currency").IsRequired().HasMaxLength(3);
                entity.Property(e => e.Status).HasColumnName("status").IsRequired().HasConversion<int>();
                entity.Property(e => e.ValidFromUtc).HasColumnName("valid_from_utc").IsRequired();
                entity.Property(e => e.ExpiresAtUtc).HasColumnName("expires_at_utc").IsRequired();
                entity.Property(e => e.CreatedByUserId).HasColumnName("created_by_user_id").IsRequired().HasMaxLength(450);
                entity.Property(e => e.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
                entity.Property(e => e.CancelledAtUtc).HasColumnName("cancelled_at_utc");
                entity.Property(e => e.CancellationReason).HasColumnName("cancellation_reason").HasMaxLength(500);
                entity.Property(e => e.InternalNote).HasColumnName("internal_note").HasMaxLength(500);
                entity.Property(e => e.CustomerId).HasColumnName("customer_id");

                entity.HasOne(e => e.Tenant)
                    .WithMany()
                    .HasForeignKey(e => e.TenantId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.Customer)
                    .WithMany()
                    .HasForeignKey(e => e.CustomerId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.HasIndex(e => e.TenantId);
                entity.HasIndex(e => e.CustomerId);
                entity.HasIndex(e => e.Status);
                entity.HasIndex(e => e.ExpiresAtUtc);
                entity.HasIndex(e => e.CodeHash);
                entity.HasIndex(e => new { e.TenantId, e.CodeHash }).IsUnique();
                entity.HasIndex(e => e.CreatedAtUtc);
            });

            builder.Entity<VoucherLedgerEntry>(entity =>
            {
                entity.ToTable("voucher_ledger_entries");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.TenantId).HasColumnName("tenant_id").IsRequired();
                entity.Property(e => e.VoucherId).HasColumnName("voucher_id").IsRequired();
                entity.Property(e => e.PaymentId).HasColumnName("payment_id");
                entity.Property(e => e.ReceiptId).HasColumnName("receipt_id");
                entity.Property(e => e.Type).HasColumnName("type").IsRequired().HasConversion<int>();
                entity.Property(e => e.Amount).HasColumnName("amount").HasColumnType("decimal(18,2)").IsRequired();
                entity.Property(e => e.BalanceAfter).HasColumnName("balance_after").HasColumnType("decimal(18,2)").IsRequired();
                entity.Property(e => e.CreatedByUserId).HasColumnName("created_by_user_id").IsRequired().HasMaxLength(450);
                entity.Property(e => e.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
                entity.Property(e => e.CorrelationId).HasColumnName("correlation_id").HasMaxLength(100);
                entity.Property(e => e.IdempotencyKey).HasColumnName("idempotency_key").HasMaxLength(128);

                entity.HasOne(e => e.Tenant)
                    .WithMany()
                    .HasForeignKey(e => e.TenantId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.Voucher)
                    .WithMany(v => v.LedgerEntries)
                    .HasForeignKey(e => e.VoucherId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.Payment)
                    .WithMany()
                    .HasForeignKey(e => e.PaymentId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.HasOne(e => e.Receipt)
                    .WithMany()
                    .HasForeignKey(e => e.ReceiptId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.HasIndex(e => e.TenantId);
                entity.HasIndex(e => e.VoucherId);
                entity.HasIndex(e => e.PaymentId);
                entity.HasIndex(e => e.ReceiptId);
                entity.HasIndex(e => e.CreatedAtUtc);
                entity.HasIndex(e => e.IdempotencyKey).IsUnique().HasFilter("\"idempotency_key\" IS NOT NULL");
            });

            builder.Entity<ReceiptItem>(entity =>
            {
                entity.Property(e => e.TenantId).HasColumnName("tenant_id").IsRequired();
                entity.HasOne(e => e.Tenant)
                    .WithMany()
                    .HasForeignKey(e => e.TenantId)
                    .OnDelete(DeleteBehavior.Restrict);
                entity.HasIndex(e => e.TenantId);
            });

            builder.Entity<ReceiptTaxLine>(entity =>
            {
                entity.Property(e => e.TenantId).HasColumnName("tenant_id").IsRequired();
                entity.HasOne(e => e.Tenant)
                    .WithMany()
                    .HasForeignKey(e => e.TenantId)
                    .OnDelete(DeleteBehavior.Restrict);
                entity.HasIndex(e => e.TenantId);
            });

            builder.Entity<FinanzOnlineSubmission>(entity =>
            {
                entity.Property(e => e.TenantId).HasColumnName("tenant_id").IsRequired();
                entity.HasOne(e => e.Tenant)
                    .WithMany()
                    .HasForeignKey(e => e.TenantId)
                    .OnDelete(DeleteBehavior.Restrict);
                entity.HasIndex(e => e.TenantId);
                entity.HasIndex(e => e.InvoiceId);
            });

            // Global query filter: fail-closed — no ambient tenant means no tenant-scoped rows (never expose all tenants).
            foreach (var entityType in builder.Model.GetEntityTypes()
                .Where(t => t.ClrType?.GetInterface(nameof(ITenantEntity)) != null))
            {
                var clrType = entityType.ClrType!;
                var filter = (LambdaExpression)typeof(AppDbContext)
                    .GetMethod(nameof(CreateTenantQueryFilter), BindingFlags.Instance | BindingFlags.NonPublic)!
                    .MakeGenericMethod(clrType)
                    .Invoke(this, null)!;
                builder.Entity(clrType).HasQueryFilter(filter);
                _logger.LogDebug("Added query filter for {EntityType}", clrType.Name);
            }

            _logger.LogDebug("AppDbContext model configuration completed with TableOrder support");
        }

        private Expression<Func<TEntity, bool>> CreateTenantQueryFilter<TEntity>()
            where TEntity : class, ITenantEntity =>
            e => _tenantAccessor.TenantId != null && e.TenantId == _tenantAccessor.TenantId;

        /// <summary>
        /// <c>timestamptz</c> columns: Npgsql rejects non-UTC <see cref="DateTime"/> on parameters and on many write paths.
        /// Before save, coerce every mapped <see cref="DateTime"/> / <see cref="DateTime?"/> on those columns to UTC.
        /// <list type="bullet">
        /// <item><description><strong>Instant semantics</strong> (default): <see cref="PostgreSqlUtcDateTime.InstantToPersistUtc"/> — Local→UTC; Unspecified→UTC kind (UTC clock ticks; use <see cref="DateTime.UtcNow"/> at source).</description></item>
        /// <item><description><strong>Vienna calendar anchor</strong> (e.g. daily closing label): <see cref="PostgreSqlUtcDateTime.ViennaCalendarAnchorToPersistUtc"/> — Unspecified date is that calendar day at 00:00 in Europe/Vienna.</description></item>
        /// <item><description>Properties mapped to PostgreSQL <c>date</c> only (not <c>timestamptz</c>) are skipped.</description></item>
        /// </list>
        /// Production registers <see cref="NpgsqlTimestamptzUtcParameterInterceptor"/> (see <c>Program.cs</c> <c>AddInterceptors</c> or <see cref="AppDbContextNpgsqlExtensions.UseAppNpgsql"/>) so ADO parameters are UTC even when the change tracker coerces <see cref="DateTime.Kind"/>.
        /// Test seam: <see cref="ApplyTimestamptzWriteNormalizationForTests"/> — the InMemory provider often resets tracked <see cref="DateTime.Kind"/> to <see cref="DateTimeKind.Unspecified"/>; assert normalized instants via ticks or use PostgreSQL integration tests for kind round-trips.
        /// </summary>
        internal void ApplyTimestamptzWriteNormalizationForTests() => NormalizeTimestamptzDateTimesBeforeSave();

        private void NormalizeTimestamptzDateTimesBeforeSave()
        {
            foreach (var entry in ChangeTracker.Entries())
            {
                if (entry.State != EntityState.Added && entry.State != EntityState.Modified)
                    continue;

                var entityClrType = entry.Metadata.ClrType;

                foreach (var meta in entry.Metadata.GetProperties())
                {
                    if (meta.IsShadowProperty())
                        continue;
                    if (!IsTimestamptzDateTimeProperty(meta))
                        continue;

                    var clr = meta.ClrType;
                    if (clr == typeof(DateTime))
                    {
                        var current = (DateTime)entry.CurrentValues[meta.Name]!;
                        entry.CurrentValues[meta.Name] = NormalizeTimestamptzDateTime(current, entityClrType, meta.Name);
                    }
                    else if (clr == typeof(DateTime?))
                    {
                        var nullable = (DateTime?)entry.CurrentValues[meta.Name];
                        if (!nullable.HasValue)
                            continue;
                        entry.CurrentValues[meta.Name] = NormalizeTimestamptzDateTime(nullable.Value, entityClrType, meta.Name);
                    }
                }
            }
        }

        /// <summary>Explicit <c>timestamptz</c> properties that store an Austria business-calendar day (00:00 Vienna), not a raw UTC instant.</summary>
        private static readonly (Type EntityType, string PropertyName)[] ViennaCalendarTimestamptzProperties =
        {
            (typeof(DailyClosing), nameof(DailyClosing.ClosingDate)),
        };

        private static bool IsViennaCalendarTimestamptzProperty(Type entityClrType, string propertyName)
        {
            foreach (var (t, p) in ViennaCalendarTimestamptzProperties)
            {
                if (p != propertyName)
                    continue;
                if (t.IsAssignableFrom(entityClrType))
                    return true;
            }

            return false;
        }

        private static DateTime NormalizeTimestamptzDateTime(DateTime value, Type entityClrType, string propertyName)
        {
            if (IsViennaCalendarTimestamptzProperty(entityClrType, propertyName))
                return PostgreSqlUtcDateTime.ViennaCalendarAnchorToPersistUtc(value);
            return PostgreSqlUtcDateTime.InstantToPersistUtc(value);
        }

        private static bool IsTimestamptzDateTimeProperty(IProperty property)
        {
            var clr = property.ClrType;
            var underlying = Nullable.GetUnderlyingType(clr) ?? clr;
            if (underlying != typeof(DateTime))
                return false;

            string? storeType = null;
            try
            {
                storeType = property.GetColumnType();
            }
            catch (InvalidCastException)
            {
                // In-memory tests: no relational type mapping; treat DateTime like Npgsql timestamptz (same normalization rules).
                return true;
            }

            // Skip only columns that are clearly not timestamptz (calendar date or local timestamp).
            if (string.Equals(storeType, "date", StringComparison.OrdinalIgnoreCase))
                return false;
            if (!string.IsNullOrWhiteSpace(storeType)
                && storeType.Contains("timestamp", StringComparison.OrdinalIgnoreCase)
                && storeType.Contains("without time zone", StringComparison.OrdinalIgnoreCase))
                return false;

            // PostgreSQL timestamptz, Npgsql defaults, InMemory (unknown/opaque store types), etc.
            return true;
        }

        /// <summary>
        /// Invariant 1–2: Audit log is append-only. Any attempt to UPDATE existing AuditLog rows is rejected (anti-tamper).
        /// No audit record may be modified. DELETE is only allowed via the dedicated retention method DeleteAuditLogsOlderThanAsync.
        /// </summary>
        public override int SaveChanges()
        {
            NormalizeTimestamptzDateTimesBeforeSave();
            StampTenantIdsOnInsert();
            NormalizeCategoriesBeforeSave();
            EnforceAuditLogAppendOnly();
            return base.SaveChanges();
        }

        /// <summary>
        /// Invariant 1–2: Async variant – enforces audit log append-only (no updates to existing audit rows).
        /// </summary>
        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            NormalizeTimestamptzDateTimesBeforeSave();
            StampTenantIdsOnInsert();
            NormalizeCategoriesBeforeSave();
            EnforceAuditLogAppendOnly();
            return await base.SaveChangesAsync(cancellationToken);
        }

        /// <summary>Auto-generates category keys on insert; blocks key/fiscal category mutation after creation.</summary>
        private void NormalizeCategoriesBeforeSave()
        {
            foreach (var entry in ChangeTracker.Entries<Category>())
            {
                if (entry.State == EntityState.Added)
                {
                    if (string.IsNullOrWhiteSpace(entry.Entity.Key))
                        entry.Entity.Key = CategoryKey.FromDisplayName(entry.Entity.Name);

                    if (entry.Entity.FiscalCategory == RksvProductCategory.Unspecified)
                        entry.Entity.FiscalCategory = CategoryKey.InferFiscalCategory(entry.Entity.Name, entry.Entity.Description);
                }
                else if (entry.State == EntityState.Modified)
                {
                    if (entry.Property(c => c.Key).IsModified)
                        entry.Property(c => c.Key).CurrentValue = entry.Property(c => c.Key).OriginalValue;

                    if (entry.Property(c => c.FiscalCategory).IsModified)
                        entry.Property(c => c.FiscalCategory).CurrentValue = entry.Property(c => c.FiscalCategory).OriginalValue;
                }
            }
        }

        /// <summary>Sets <see cref="ITenantEntity.TenantId"/> on new rows from ambient tenant or owning cash register / receipt / invoice.</summary>
        private void StampTenantIdsOnInsert()
        {
            var pending = ChangeTracker.Entries<ITenantEntity>()
                .Where(e => e.State == EntityState.Added && e.Entity.TenantId == Guid.Empty)
                .ToList();
            if (pending.Count == 0)
                return;

            if (_tenantAccessor.TenantId is Guid ambient && ambient != Guid.Empty)
            {
                foreach (var entry in pending)
                    entry.Entity.TenantId = ambient;
            }

            pending = pending.Where(e => e.Entity.TenantId == Guid.Empty).ToList();
            if (pending.Count == 0)
                return;

            var cashRegisterIds = pending
                .Select(e => TryGetCashRegisterId(e.Entity))
                .Where(id => id.HasValue)
                .Select(id => id!.Value)
                .Distinct()
                .ToList();

            if (cashRegisterIds.Count > 0)
            {
                var tenantByRegister = CashRegisters.AsNoTracking()
                    .Where(cr => cashRegisterIds.Contains(cr.Id))
                    .Select(cr => new { cr.Id, cr.TenantId })
                    .ToDictionary(x => x.Id, x => x.TenantId);

                foreach (var entry in pending)
                {
                    if (entry.Entity.TenantId != Guid.Empty)
                        continue;
                    var registerId = TryGetCashRegisterId(entry.Entity);
                    if (registerId.HasValue && tenantByRegister.TryGetValue(registerId.Value, out var tenantId))
                        entry.Entity.TenantId = tenantId;
                }
            }

            pending = pending.Where(e => e.Entity.TenantId == Guid.Empty).ToList();
            if (pending.Count == 0)
                return;

            var tenantByReceipt = ChangeTracker.Entries<Receipt>()
                .Where(e => e.Entity.TenantId != Guid.Empty)
                .ToDictionary(e => e.Entity.ReceiptId, e => e.Entity.TenantId);

            var receiptIds = pending
                .Select(e => TryGetReceiptId(e.Entity))
                .Where(id => id.HasValue)
                .Select(id => id!.Value)
                .Where(id => !tenantByReceipt.ContainsKey(id))
                .Distinct()
                .ToList();

            if (receiptIds.Count > 0)
            {
                foreach (var row in Receipts.AsNoTracking()
                             .Where(r => receiptIds.Contains(r.ReceiptId))
                             .Select(r => new { r.ReceiptId, r.TenantId }))
                {
                    tenantByReceipt[row.ReceiptId] = row.TenantId;
                }
            }

            foreach (var entry in pending)
            {
                if (entry.Entity.TenantId != Guid.Empty)
                    continue;
                var receiptId = TryGetReceiptId(entry.Entity);
                if (receiptId.HasValue && tenantByReceipt.TryGetValue(receiptId.Value, out var tenantId))
                    entry.Entity.TenantId = tenantId;
            }

            pending = pending.Where(e => e.Entity.TenantId == Guid.Empty).ToList();
            var invoiceIds = pending
                .Select(e => e.Entity)
                .OfType<FinanzOnlineSubmission>()
                .Select(f => f.InvoiceId)
                .Distinct()
                .ToList();

            if (invoiceIds.Count == 0)
                return;

            var tenantByInvoice = Invoices.AsNoTracking()
                .Where(i => invoiceIds.Contains(i.Id))
                .Select(i => new { i.Id, i.TenantId })
                .ToDictionary(x => x.Id, x => x.TenantId);

            foreach (var entry in pending)
            {
                if (entry.Entity is not FinanzOnlineSubmission submission || submission.TenantId != Guid.Empty)
                    continue;
                if (tenantByInvoice.TryGetValue(submission.InvoiceId, out var tenantId))
                    submission.TenantId = tenantId;
            }
        }

        private static Guid? TryGetCashRegisterId(ITenantEntity entity) => entity switch
        {
            CashierShift cs => cs.CashRegisterId,
            OfflineTransaction ot => ot.CashRegisterId,
            CardPaymentTransaction ct => ct.CashRegisterId,
            DailyClosing dc => dc.CashRegisterId,
            TseSignature ts => ts.CashRegisterId,
            SignatureChainState sc => sc.CashRegisterId,
            Receipt r => r.CashRegisterId,
            Invoice inv => inv.CashRegisterId,
            _ => null
        };

        private static Guid? TryGetReceiptId(ITenantEntity entity) => entity switch
        {
            ReceiptItem item => item.ReceiptId,
            ReceiptTaxLine line => line.ReceiptId,
            _ => null
        };

        /// <summary>Invariant 1–2: Reject any UPDATE to AuditLog; records are immutable after insert.</summary>
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

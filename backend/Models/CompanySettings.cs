using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KasseAPI_Final.Models
{
    /// <summary>
    /// Tenant-wide company master data (single source of truth for POS receipts, invoices, and FinanzOnline).
    /// RKSV §8 mandatory receipt fields are mapped from <see cref="CompanyName"/>, <see cref="CompanyAddress"/>,
    /// and <see cref="CompanyTaxNumber"/>; values are snapshotted onto each <see cref="PaymentDetails"/> at sale time.
    /// </summary>
    [Table("company_settings")]
    public class CompanySettings : BaseEntity, ITenantEntity
    {
        [Required]
        [Column("tenant_id")]
        public Guid TenantId { get; set; }

        public virtual Tenant? Tenant { get; set; }

        /// <summary>RKSV §8 — Firmenname / Unternehmensbezeichnung.</summary>
        [Required]
        [MaxLength(100)]
        public string CompanyName { get; set; } = string.Empty;

        /// <summary>RKSV §8 — Firmenadresse / Sitz der gewerblichen Betriebsstätte.</summary>
        [Required]
        [MaxLength(200)]
        public string CompanyAddress { get; set; } = string.Empty;

        [MaxLength(20)]
        public string? CompanyPhone { get; set; }

        [EmailAddress]
        [MaxLength(100)]
        public string? CompanyEmail { get; set; }

        [MaxLength(100)]
        public string? CompanyWebsite { get; set; }

        /// <summary>RKSV §8 — UID (Umsatzsteuer-Identifikationsnummer), format ATU12345678.</summary>
        [Required]
        [MaxLength(20)]
        public string CompanyTaxNumber { get; set; } = string.Empty;

        /// <summary>RKSV report alias for <see cref="CompanyName"/>.</summary>
        [NotMapped]
        public string Name
        {
            get => CompanyName;
            set => CompanyName = value;
        }

        /// <summary>RKSV report alias for <see cref="CompanyAddress"/>.</summary>
        [NotMapped]
        public string Address
        {
            get => CompanyAddress;
            set => CompanyAddress = value;
        }

        /// <summary>RKSV report alias for <see cref="CompanyTaxNumber"/>.</summary>
        [NotMapped]
        public string VatId
        {
            get => CompanyTaxNumber;
            set => CompanyTaxNumber = value;
        }

        /// <summary>RKSV report alias for <see cref="CompanyPhone"/>.</summary>
        [NotMapped]
        public string? Phone
        {
            get => CompanyPhone;
            set => CompanyPhone = value;
        }

        /// <summary>RKSV report alias for <see cref="CompanyEmail"/>.</summary>
        [NotMapped]
        public string? Email
        {
            get => CompanyEmail;
            set => CompanyEmail = value;
        }

        /// <summary>RKSV report alias for <see cref="CompanyWebsite"/>.</summary>
        [NotMapped]
        public string? Website
        {
            get => CompanyWebsite;
            set => CompanyWebsite = value;
        }

        [MaxLength(20)]
        public string? CompanyRegistrationNumber { get; set; }

        [MaxLength(20)]
        public string? CompanyVatNumber { get; set; }

        [MaxLength(100)]
        public string? CompanyLogo { get; set; }

        [MaxLength(500)]
        public string? CompanyDescription { get; set; }

        /// <summary>
        /// Custom POS receipt thank-you line (Dankesnachricht). Null/empty uses
        /// <see cref="ReceiptThankYouMessage.Default"/>. Printed before <see cref="CompanyDescription"/>.
        /// </summary>
        [MaxLength(ReceiptThankYouMessage.MaxLength)]
        [Column("thank_you_message")]
        public string? ThankYouMessage { get; set; }

        [Required]
        [Column(TypeName = "jsonb")]
        public Dictionary<string, string> BusinessHours { get; set; } = new();

        /// <summary>
        /// Structured restaurant working hours (Mon–Sun open/close/isClosed) plus
        /// Tagesabschluss reminder lead time. JSONB; defaults when null/empty.
        /// </summary>
        [Required]
        [Column("working_hours", TypeName = "jsonb")]
        public WorkingHoursSettings WorkingHours { get; set; } = WorkingHoursSettings.CreateDefault();

        /// <summary>
        /// Automatic Tagesabschluss fallback (Vienna local time, open-order policy).
        /// JSONB; defaults when null/empty.
        /// </summary>
        [Required]
        [Column("auto_tagesabschluss", TypeName = "jsonb")]
        public AutoTagesabschlussSettings AutoTagesabschluss { get; set; } =
            AutoTagesabschlussSettings.CreateDefault();

        [MaxLength(100)]
        public string? ContactPerson { get; set; }

        [MaxLength(20)]
        public string? ContactPhone { get; set; }

        [EmailAddress]
        [MaxLength(100)]
        public string? ContactEmail { get; set; }

        [MaxLength(100)]
        public string? BankName { get; set; }

        [MaxLength(50)]
        public string? BankAccountNumber { get; set; }

        [MaxLength(50)]
        public string? BankRoutingNumber { get; set; }

        [MaxLength(20)]
        public string? BankSwiftCode { get; set; }

        [MaxLength(50)]
        public string? PaymentTerms { get; set; }

        [Required]
        [MaxLength(3)]
        public string Currency { get; set; } = string.Empty;

        /// <summary>ISO 3166-1 alpha-2 country code (e.g. AT). Changed only via Super Admin settings approval workflow.</summary>
        [Required]
        [MaxLength(2)]
        [Column("country")]
        public string Country { get; set; } = "AT";

        /// <summary>
        /// ISO 3166-1 alpha-2 billing country when invoicing happens elsewhere than the operating
        /// <see cref="Country"/>. Null means "bill in the operating country".
        /// </summary>
        [MaxLength(2)]
        [RegularExpression(Iso3166CountryCode.OptionalPattern, ErrorMessage = Iso3166CountryCode.ValidationMessage)]
        [Column("billing_country")]
        public string? BillingCountry { get; set; }

        /// <summary>VAT regime used for tax calculation and invoice disclosures. Existing mandants are Austrian.</summary>
        [Required]
        [Column("vat_regime")]
        public VatRegime VatRegime { get; set; } = VatRegime.AT_RKSV_STANDARD;

        /// <summary>True when the mandant is exempt from VAT (small-business or equivalent relief).</summary>
        [Column("tax_exempt")]
        public bool TaxExempt { get; set; }

        [Required]
        [MaxLength(10)]
        public string Language { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string TimeZone { get; set; } = string.Empty;

        [Required]
        [MaxLength(20)]
        public string DateFormat { get; set; } = string.Empty;

        [Required]
        [MaxLength(20)]
        public string TimeFormat { get; set; } = string.Empty;

        [Range(0, 4)]
        public int DecimalPlaces { get; set; }

        [Required]
        [MaxLength(50)]
        public string TaxCalculationMethod { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string InvoiceNumbering { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string ReceiptNumbering { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string DefaultPaymentMethod { get; set; } = string.Empty;

        // FinanzOnline entegrasyonu için alanlar
        [MaxLength(500)]
        public string? FinanzOnlineApiUrl { get; set; }

        [MaxLength(100)]
        public string? FinanzOnlineUsername { get; set; }

        [MaxLength(100)]
        public string? FinanzOnlinePassword { get; set; }

        [MaxLength(12)]
        public string? FinanzOnlineTelematikId { get; set; }

        [MaxLength(24)]
        public string? FinanzOnlineHerstellerId { get; set; }

        public bool FinanzOnlineAutoSubmit { get; set; } = false;

        public int FinanzOnlineSubmitInterval { get; set; } = 60; // dakika

        public int FinanzOnlineRetryAttempts { get; set; } = 3;

        public bool FinanzOnlineEnableValidation { get; set; } = true;

        // FinanzOnline status fields
        public bool FinanzOnlineEnabled { get; set; } = false;

        public DateTime? LastFinanzOnlineSync { get; set; }

        public int? PendingInvoices { get; set; } = 0;

        // TSE cihazı ayarları
        [MaxLength(100)]
        public string? DefaultTseDeviceId { get; set; }

        public bool TseAutoConnect { get; set; } = false;

        public int TseConnectionTimeout { get; set; } = 30; // saniye

        /// <summary>
        /// When true (default), Vienna December uses Jahresbeleg flow instead of a separate Monatsbeleg row,
        /// and December Monatsbeleg may satisfy Jahresbeleg detection where configured.
        /// </summary>
        [Column("use_december_monatsbeleg_as_jahresbeleg")]
        public bool UseDecemberMonatsbelegAsJahresbeleg { get; set; } = true;

        /// <summary>
        /// POS sales blocking when the previous Vienna-month Monatsbeleg is missing:
        /// <c>Strict</c> (default), <c>GracePeriod</c>, or <c>WarningOnly</c>.
        /// </summary>
        [Required]
        [MaxLength(32)]
        [Column("monatsbeleg_blocking_mode")]
        public string MonatsbelegBlockingMode { get; set; } = MonatsbelegBlockingModeNames.Strict;

        /// <summary>
        /// When true (default), the hosted worker creates the previous-month TSE Monatsbeleg
        /// on Vienna day 1 at 00:01 (catch-up through day 7) and notifies Mandanten-Admin.
        /// </summary>
        [Column("auto_monatsbeleg_enabled")]
        public bool AutoMonatsbelegEnabled { get; set; } = true;

        /// <summary>Max Auto-Monatsbeleg attempts per register/month (1–5, default 3).</summary>
        [Range(1, 5)]
        [Column("monatsbeleg_retry_count")]
        public int MonatsbelegRetryCount { get; set; } = 3;

        /// <summary>Printed pickup window on Vorbestellung / Besorgerzettel receipts.</summary>
        [Range(PreorderPolicyDefaults.MinPickupDeadlineWeeks, PreorderPolicyDefaults.MaxPickupDeadlineWeeks)]
        [Column("preorder_pickup_deadline_weeks")]
        public int PreorderPickupDeadlineWeeks { get; set; } = PreorderPolicyDefaults.PickupDeadlineWeeks;

        /// <summary>Printed cancellation / return policy line on Besorgerzettel receipts.</summary>
        [MaxLength(500)]
        [Column("preorder_cancellation_policy_text")]
        public string? PreorderCancellationPolicyText { get; set; }

        /// <summary>Fiskaly SIGN DE TSS id. Null until a canary tenant is configured.</summary>
        [Column("de_tss_id")]
        [MaxLength(64)]
        public string? DeTssId { get; set; }

        /// <summary>Fiskaly SIGN DE client id. Null until a canary tenant is configured.</summary>
        [Column("de_client_id")]
        [MaxLength(64)]
        public string? DeClientId { get; set; }
    }
}

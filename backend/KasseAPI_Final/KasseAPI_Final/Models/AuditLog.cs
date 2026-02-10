using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KasseAPI_Final.Models
{
    // English Description: Comprehensive audit log for all payment operations and system activities
    public class AuditLog : BaseEntity
    {
        [Required]
        [MaxLength(100)]
        public string SessionId { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string UserId { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string UserRole { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string Action { get; set; } = string.Empty; // CREATE, READ, UPDATE, DELETE, PAYMENT_INITIATE, PAYMENT_CONFIRM, REFUND, etc.

        [Required]
        [MaxLength(100)]
        public string EntityType { get; set; } = string.Empty; // Payment, Invoice, Cart, Customer, etc.

        public Guid? EntityId { get; set; } // ID of the affected entity

        [MaxLength(100)]
        public string? EntityName { get; set; } // Human-readable name of the entity

        [MaxLength(4000)]
        public string? OldValues { get; set; } // Previous state of the entity (for updates)

        [MaxLength(4000)]
        public string? NewValues { get; set; } // New state of the entity

        [MaxLength(4000)]
        public string? RequestData { get; set; } // Complete request data

        [MaxLength(4000)]
        public string? ResponseData { get; set; } // Complete response data

        [Required]
        public AuditLogStatus Status { get; set; }

        [Required]
        public DateTime Timestamp { get; set; }

        [MaxLength(500)]
        public string? Description { get; set; } // Human-readable description of the action

        [MaxLength(500)]
        public string? Notes { get; set; } // Additional notes

        [MaxLength(45)]
        public string? IpAddress { get; set; }

        [MaxLength(500)]
        public string? UserAgent { get; set; }

        [MaxLength(100)]
        public string? Endpoint { get; set; } // API endpoint that was called

        [MaxLength(10)]
        public string? HttpMethod { get; set; } // GET, POST, PUT, DELETE

        public int? HttpStatusCode { get; set; } // HTTP response status code

        public double? ProcessingTimeMs { get; set; }

        [MaxLength(500)]
        public string? ErrorDetails { get; set; } // Error details if the action failed

        [MaxLength(100)]
        public string? CorrelationId { get; set; } // For tracking related operations

        [MaxLength(100)]
        public string? TransactionId { get; set; } // Payment transaction ID if applicable

        [Column(TypeName = "decimal(18,2)")]
        public decimal? Amount { get; set; } // Amount involved in the operation

        [MaxLength(50)]
        public string? PaymentMethod { get; set; } // Payment method if applicable

        [MaxLength(500)]
        public string? TseSignature { get; set; } // TSE signature if applicable

        // Navigation properties
        [ForeignKey("UserId")]
        public virtual ApplicationUser? User { get; set; }

        // Entity navigation property removed - EF Core doesn't support object type for navigation
        // Use EntityId and EntityType for entity identification instead
    }

    // Audit log statuses
    public enum AuditLogStatus
    {
        Success,        // Operation completed successfully
        Failed,         // Operation failed
        Pending,        // Operation is pending
        Cancelled,      // Operation was cancelled
        InProgress,     // Operation is in progress
        Timeout,        // Operation timed out
        ValidationError, // Operation failed due to validation
        AuthorizationError, // Operation failed due to authorization
        SystemError,    // System-level error
        Warning,        // Operation completed with warnings
        Error           // General error status
    }

    // Audit log actions
    public static class AuditLogActions
    {
        // Payment operations
        public const string PAYMENT_INITIATE = "PAYMENT_INITIATE";
        public const string PAYMENT_CONFIRM = "PAYMENT_CONFIRM";
        public const string PAYMENT_REFUND = "PAYMENT_REFUND";
        public const string PAYMENT_CANCEL = "PAYMENT_CANCEL";
        public const string PAYMENT_UPDATE = "PAYMENT_UPDATE";
        public const string PAYMENT_DELETE = "PAYMENT_DELETE";

        // Invoice operations
        public const string INVOICE_CREATE = "INVOICE_CREATE";
        public const string INVOICE_UPDATE = "INVOICE_UPDATE";
        public const string INVOICE_DELETE = "INVOICE_DELETE";
        public const string INVOICE_PRINT = "INVOICE_PRINT";

        // Receipt operations
        public const string RECEIPT_PRINTED = "RECEIPT_PRINTED";
        public const string RECEIPT_SAVED = "RECEIPT_SAVED";
        public const string RECEIPT_ERROR = "RECEIPT_ERROR";

        // Cart operations
        public const string CART_CREATE = "CART_CREATE";
        public const string CART_UPDATE = "CART_UPDATE";
        public const string CART_DELETE = "CART_DELETE";
        public const string CART_ITEM_ADD = "CART_ITEM_ADD";
        public const string CART_ITEM_REMOVE = "CART_ITEM_REMOVE";

        // Customer operations
        public const string CUSTOMER_CREATE = "CUSTOMER_CREATE";
        public const string CUSTOMER_UPDATE = "CUSTOMER_UPDATE";
        public const string CUSTOMER_DELETE = "CUSTOMER_DELETE";

        // User operations
        public const string USER_LOGIN = "USER_LOGIN";
        public const string USER_LOGOUT = "USER_LOGOUT";
        public const string USER_CREATE = "USER_CREATE";
        public const string USER_UPDATE = "USER_UPDATE";
        public const string USER_DELETE = "USER_DELETE";
        public const string USER_ROLE_CHANGE = "USER_ROLE_CHANGE";

        // System operations
        public const string SYSTEM_CONFIG_UPDATE = "SYSTEM_CONFIG_UPDATE";
        public const string TSE_STATUS_CHECK = "TSE_STATUS_CHECK";
        public const string BACKUP_CREATE = "BACKUP_CREATE";
        public const string SYSTEM_MAINTENANCE = "SYSTEM_MAINTENANCE";

        // Generic CRUD operations
        public const string CREATE = "CREATE";
        public const string READ = "READ";
        public const string UPDATE = "UPDATE";
        public const string DELETE = "DELETE";
    }

            // Entity types for audit logging
        public static class AuditLogEntityTypes
        {
            public const string PAYMENT = "Payment";
            public const string INVOICE = "Invoice";
            public const string CART = "Cart";
            public const string CART_ITEM = "CartItem";
            public const string CUSTOMER = "Customer";
            public const string USER = "User";
            public const string PAYMENT_SESSION = "PaymentSession";
            public const string PAYMENT_LOG = "PaymentLog";
            public const string AUDIT_LOG = "AuditLog";
            public const string SYSTEM_CONFIG = "SystemConfig";
            public const string TSE_DEVICE = "TseDevice";
            public const string RECEIPT = "Receipt";
        }
}

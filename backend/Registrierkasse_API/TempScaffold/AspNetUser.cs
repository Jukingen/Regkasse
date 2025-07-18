using System;
using System.Collections.Generic;

namespace Registrierkasse_API.TempScaffold;

public partial class AspNetUser
{
    public string Id { get; set; } = null!;

    public string FirstName { get; set; } = null!;

    public string LastName { get; set; } = null!;

    public string EmployeeNumber { get; set; } = null!;

    public string Role { get; set; } = null!;

    public string TaxNumber { get; set; } = null!;

    public string Notes { get; set; } = null!;

    public bool IsActive { get; set; }

    public DateTime? LastLogin { get; set; }

    public string? UserName { get; set; }

    public string? NormalizedUserName { get; set; }

    public string? Email { get; set; }

    public string? NormalizedEmail { get; set; }

    public bool EmailConfirmed { get; set; }

    public string? PasswordHash { get; set; }

    public string? SecurityStamp { get; set; }

    public string? ConcurrencyStamp { get; set; }

    public string? PhoneNumber { get; set; }

    public bool PhoneNumberConfirmed { get; set; }

    public bool TwoFactorEnabled { get; set; }

    public DateTime? LockoutEnd { get; set; }

    public bool LockoutEnabled { get; set; }

    public int AccessFailedCount { get; set; }

    public virtual ICollection<AspNetUserClaim> AspNetUserClaims { get; set; } = new List<AspNetUserClaim>();

    public virtual ICollection<AspNetUserLogin> AspNetUserLogins { get; set; } = new List<AspNetUserLogin>();

    public virtual ICollection<AspNetUserToken> AspNetUserTokens { get; set; } = new List<AspNetUserToken>();

    public virtual ICollection<Cart> Carts { get; set; } = new List<Cart>();

    public virtual ICollection<CashRegisterTransaction> CashRegisterTransactions { get; set; } = new List<CashRegisterTransaction>();

    public virtual ICollection<CashRegister> CashRegisters { get; set; } = new List<CashRegister>();

    public virtual ICollection<InventoryTransaction> InventoryTransactions { get; set; } = new List<InventoryTransaction>();

    public virtual ICollection<Invoice> InvoiceCreatedBies { get; set; } = new List<Invoice>();

    public virtual ICollection<Invoice> InvoiceCustomers { get; set; } = new List<Invoice>();

    public virtual ICollection<InvoiceHistory> InvoiceHistories { get; set; } = new List<InvoiceHistory>();

    public virtual ICollection<InvoiceTemplate> InvoiceTemplates { get; set; } = new List<InvoiceTemplate>();

    public virtual ICollection<Order> Orders { get; set; } = new List<Order>();

    public virtual ICollection<Receipt> Receipts { get; set; } = new List<Receipt>();

    public virtual ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();

    public virtual ICollection<UserSession> UserSessions { get; set; } = new List<UserSession>();

    public virtual UserSetting? UserSetting { get; set; }

    public virtual ICollection<AspNetRole> Roles { get; set; } = new List<AspNetRole>();
}

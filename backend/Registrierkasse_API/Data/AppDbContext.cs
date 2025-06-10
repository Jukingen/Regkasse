using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Registrierkasse.Models;

namespace Registrierkasse.Data
{
    public class AppDbContext : IdentityDbContext<ApplicationUser>
    {
        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options)
        {
        }

        public DbSet<Product> Products { get; set; }
        public DbSet<Customer> Customers { get; set; }
        public DbSet<Order> Orders { get; set; }
        public DbSet<OrderItem> OrderItems { get; set; }
        public DbSet<Invoice> Invoices { get; set; }
        public DbSet<InvoiceItem> InvoiceItems { get; set; }
        public DbSet<CashRegister> CashRegisters { get; set; }
        public DbSet<CashRegisterTransaction> CashRegisterTransactions { get; set; }
        public DbSet<Table> Tables { get; set; }
        public DbSet<TableReservation> TableReservations { get; set; }
        public DbSet<Inventory> Inventories { get; set; } = null!;
        public DbSet<InventoryTransaction> InventoryTransactions { get; set; } = null!;
        public DbSet<Discount> Discounts { get; set; }
        public DbSet<Voucher> Vouchers { get; set; }
        public DbSet<Hardware> Hardware { get; set; }
        public DbSet<CompanySettings> CompanySettings { get; set; }
        public DbSet<UserSettings> UserSettings { get; set; }
        public DbSet<FinanceOnline> FinanceOnline { get; set; }
        public DbSet<UserSession> UserSessions { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Product - Inventory ilişkisi
            modelBuilder.Entity<Product>()
                .HasOne(p => p.Inventory)
                .WithOne(i => i.Product)
                .HasForeignKey<Inventory>(i => i.ProductId);

            // Invoice - CashRegister ilişkisi
            modelBuilder.Entity<Invoice>()
                .HasOne(i => i.CashRegister)
                .WithMany(cr => cr.Invoices)
                .HasForeignKey(i => i.CashRegisterId);

            // Diğer ilişkiler...
            modelBuilder.Entity<Order>()
                .HasOne(o => o.Customer)
                .WithMany(c => c.Orders)
                .HasForeignKey(o => o.CustomerId);

            modelBuilder.Entity<OrderItem>()
                .HasOne(oi => oi.Order)
                .WithMany(o => o.OrderItems)
                .HasForeignKey(oi => oi.OrderId);

            modelBuilder.Entity<OrderItem>()
                .HasOne(oi => oi.Product)
                .WithMany(p => p.OrderItems)
                .HasForeignKey(oi => oi.ProductId);

            modelBuilder.Entity<Invoice>()
                .HasOne(i => i.Customer)
                .WithMany(c => c.Invoices)
                .HasForeignKey(i => i.CustomerId);

            modelBuilder.Entity<Invoice>()
                .HasMany(i => i.Items)
                .WithOne(ii => ii.Invoice)
                .HasForeignKey(ii => ii.InvoiceId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Table>()
                .HasOne(t => t.CurrentOrder)
                .WithOne()
                .HasForeignKey<Table>(t => t.CurrentOrderId);

            modelBuilder.Entity<TableReservation>()
                .HasOne(r => r.Table)
                .WithMany(t => t.Reservations)
                .HasForeignKey(r => r.TableId);

            modelBuilder.Entity<CashRegister>()
                .HasOne(cr => cr.CurrentUser)
                .WithMany(u => u.AssignedCashRegisters)
                .HasForeignKey(cr => cr.CurrentUserId);

            modelBuilder.Entity<CashRegisterTransaction>()
                .HasOne(t => t.CashRegister)
                .WithMany(cr => cr.Transactions)
                .HasForeignKey(t => t.CashRegisterId);

            modelBuilder.Entity<CashRegisterTransaction>()
                .HasOne(t => t.User)
                .WithMany(u => u.Transactions)
                .HasForeignKey(t => t.UserId);

            modelBuilder.Entity<CashRegisterTransaction>()
                .HasOne(t => t.Invoice)
                .WithMany()
                .HasForeignKey(t => t.InvoiceId);

            modelBuilder.Entity<InventoryTransaction>()
                .HasOne(t => t.Inventory)
                .WithMany(i => i.Transactions)
                .HasForeignKey(t => t.InventoryId);

            modelBuilder.Entity<InventoryTransaction>()
                .HasOne(t => t.User)
                .WithMany(u => u.InventoryTransactions)
                .HasForeignKey(t => t.UserId);

            modelBuilder.Entity<UserSettings>()
                .HasOne(s => s.User)
                .WithOne(u => u.Settings)
                .HasForeignKey<UserSettings>(s => s.UserId);

            modelBuilder.Entity<FinanceOnline>()
                .HasOne(f => f.Invoice)
                .WithOne(i => i.FinanceOnline)
                .HasForeignKey<FinanceOnline>(f => f.InvoiceId);
        }
    }
}
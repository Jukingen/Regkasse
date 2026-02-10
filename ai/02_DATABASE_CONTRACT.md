# Database Contract (PostgreSQL + EF Core)

## DbContext
- AppDbContext : IdentityDbContext<ApplicationUser>
- Mapping: Fluent API (OnModelCreating)

## DbSets (özet)
- Product, Category
- Customer, Invoice
- Cart, CartItem
- Order, OrderItem
- CashRegister, CashRegisterTransaction
- PaymentDetails, PaymentItem
- InventoryItem, InventoryTransaction
- SystemSettings, UserSettings, CompanySettings, LocalizationSettings
- AuditLog
- ReceiptTemplate, GeneratedReceipt
- TseDevice, TseSignature
- DailyClosing
- FinanzOnlineError
- PaymentLogEntry, PaymentSession, PaymentMetrics
- TableOrder, TableOrderItem

## Key & ID Notları
- Bazı entity'lerde GUID yerine string key var (örn: Cart.CartId (max 50), UserId max 450).
- Yeni tablo/kolon eklerken mevcut key stilini bozmadan ilerle.

## Money & Precision
- Varsayılan para alanları: decimal(18,2)
- Bazı oran/vergiler: decimal(5,4) veya decimal(5,2)
- Toplam/iskonto/vergilerde rounding mevcut policy ile aynı kalmalı.

## JSONB Kullanımı
- Bazı alanlar jsonb olarak map edilmiş (audit/receipt/tse payload gibi esnek data alanları).
- Yeni jsonb alan eklemeden önce gerçekten gerekli mi kontrol et.

## İlişki & Delete Behavior
- Cart -> User: Cascade delete (AppDbContext’te konfigurasyon var)
- Yeni ilişkiler eklerken mevcut delete behavior yaklaşımını takip et.

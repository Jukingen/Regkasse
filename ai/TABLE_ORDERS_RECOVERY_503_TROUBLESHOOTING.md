# Table Orders Recovery 503 - Troubleshooting

## Hata
`GET /api/cart/table-orders-recovery` → 503
```json
{
  "success": false,
  "message": "Table orders infrastructure is currently being provisioned.",
  "errorCode": "TABLE_ORDERS_MISSING",
  "isTransient": true
}
```

## Kök neden
Backend, PostgreSQL exception içinde `42P01` (undefined_table), `42703` (undefined_column) veya `relation ... does not exist` gördüğünde 503 döner.

**Yaygın sebepler:**
- `table_orders` veya `table_order_items` tablolarının eksik olması
- Migration'ların uygulanmamış olması
- **`42703: column t.id does not exist`** — PostgreSQL büyük/küçük harf uyumsuzluğu: `table_orders.Id` (PascalCase) vs BaseEntity'nin `id` (lowercase). Fix: AppDbContext'te `entity.Property(e => e.Id).HasColumnName("Id")` (TableOrder için) eklendi.
- Schema/search_path uyumsuzluğu

## Doğrulama adımları

### 1) DB tablolarını kontrol et
```sql
SELECT tablename FROM pg_tables
WHERE schemaname = 'public'
  AND tablename IN ('table_orders', 'table_order_items', 'carts', 'cart_items', 'customers');
```
Beklenen: 5 satır. Eksik tablo varsa migration uygulanmamış.

### 2) Migration durumu
```bash
cd backend
dotnet ef migrations list
```
`20260222222921_FixTableOrdersSync` listede olmalı.

### 3) Migration uygula (eksikse)
```bash
cd backend
dotnet ef database update
```

### 4) Backend log
503 aldığında backend log'da tam exception mesajı görünür:
```
⚠️ TABLE_ORDERS_MISSING: Relation does not exist - ... Message: ..., Inner: ...
```
Hangi tablo/kolon eksik oradan anlaşılır.

## Script
`scripts/db-validate-table-orders.sql` — detaylı doğrulama sorguları.

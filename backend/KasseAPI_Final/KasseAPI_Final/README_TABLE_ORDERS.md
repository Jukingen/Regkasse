# 🍽️ TableOrder Tabloları Kurulum Rehberi

## 📋 Genel Bakış
Bu rehber, masa siparişlerinin kalıcı saklanması için gerekli `table_orders` ve `table_order_items` tablolarının manuel kurulumunu açıklar.

## 🚀 Kurulum Adımları

### 1. PostgreSQL'e Bağlanın
```bash
# PostgreSQL'e bağlanın
psql -U your_username -d your_database_name
```

### 2. SQL Script'i Çalıştırın
```sql
-- CreateTableOrderTables.sql dosyasındaki tüm SQL komutlarını çalıştırın
\i CreateTableOrderTables.sql
```

### 3. Tabloları Kontrol Edin
```sql
-- Tabloların oluşturulduğunu kontrol edin
\dt table_orders
\dt table_order_items

-- Tablo yapısını kontrol edin
\d table_orders
\d table_order_items
```

## 📊 Tablo Yapısı

### table_orders
- **TableOrderId** (VARCHAR(50)): Primary Key
- **TableNumber** (INTEGER): Masa numarası
- **UserId** (VARCHAR(450)): Kullanıcı ID
- **Status** (INTEGER): Sipariş durumu
- **TotalAmount** (DECIMAL(18,2)): Toplam tutar
- **OrderStartTime** (TIMESTAMP): Sipariş başlama zamanı

### table_order_items
- **Id** (UUID): Primary Key
- **TableOrderId** (VARCHAR(50)): Foreign Key to table_orders
- **ProductId** (UUID): Ürün ID
- **ProductName** (VARCHAR(200)): Ürün adı
- **Quantity** (INTEGER): Miktar
- **UnitPrice** (DECIMAL(18,2)): Birim fiyat
- **TotalPrice** (DECIMAL(18,2)): Toplam fiyat

## 🔗 İlişkiler
- `table_order_items.TableOrderId` → `table_orders.TableOrderId`
- Cascade delete: TableOrder silindiğinde tüm TableOrderItem'lar da silinir

## 📈 İndeksler
- `idx_table_orders_table_status`: Masa ve durum bazlı arama
- `idx_table_orders_user_table`: Kullanıcı ve masa bazlı arama
- `idx_table_orders_start_time`: Zaman bazlı arama
- `idx_table_order_items_order_id`: Sipariş bazlı arama
- `idx_table_order_items_product_status`: Ürün ve durum bazlı arama

## ✅ Kurulum Sonrası Kontrol
1. Tablolar başarıyla oluşturuldu mu?
2. İndeksler oluşturuldu mu?
3. Foreign key constraint'ler doğru mu?
4. Tablo yorumları eklendi mi?

## 🚨 Sorun Giderme
- **Hata**: "relation already exists"
  - **Çözüm**: `DROP TABLE IF EXISTS table_order_items; DROP TABLE IF EXISTS table_orders;` çalıştırın

- **Hata**: "permission denied"
  - **Çözüm**: Yeterli yetkiye sahip kullanıcı ile bağlanın

- **Hata**: "Spalte t0.CreatedBy existiert nicht" (Column t0.CreatedBy does not exist)
  - **Çözüm**: Eski tabloları silin ve güncellenmiş script'i tekrar çalıştırın:
    ```sql
    DROP TABLE IF EXISTS table_order_items CASCADE;
    DROP TABLE IF EXISTS table_orders CASCADE;
    \i CreateTableOrderTables.sql
    ```

## 🔄 Sonraki Adımlar
1. Backend'de TableOrderService'i aktif edin
2. CartController'daki TableOrder referanslarını aktif edin
3. Frontend'de test edin
4. F5 recovery'yi test edin

## 📞 Destek
Herhangi bir sorun yaşarsanız, lütfen development ekibi ile iletişime geçin.

# 🗄️ Veritabanı Güncelleme - RKSV Uyumluluğu

Bu doküman, frontend güncellemelerinden sonra veritabanında yapılması gereken değişiklikleri açıklar.

## 📋 Güncelleme Gerekliliği

**Evet, veritabanında değişiklik yapmamız gerekiyor!** Çünkü:

1. **Frontend güncellendi** - Yeni API endpoint'leri ve interface'ler eklendi
2. **RKSV uyumluluğu** - Avusturya kasa sistemi standartlarına uygun hale getirildi
3. **Yeni alanlar** - Ürün yönetimi için ek özellikler eklendi
4. **Performans iyileştirmeleri** - Yeni index'ler ve constraint'ler

## 🚀 Güncelleme Seçenekleri

### Seçenek 1: Entity Framework Migration (Önerilen)

```bash
# Backend projesinde
cd backend/KasseAPI_Final/KasseAPI_Final

# Migration oluştur
dotnet ef migrations add UpdateProductTableForRKSV

# Veritabanını güncelle
dotnet ef database update
```

### Seçenek 2: Manuel SQL Script

```bash
# PostgreSQL'e bağlan
psql -h localhost -U your_username -d your_database

# SQL script'i çalıştır
\i update-products-table-rksv.sql
```

### Seçenek 3: Visual Studio Package Manager Console

```powershell
# Package Manager Console'da
Add-Migration UpdateProductTableForRKSV
Update-Database
```

## 📊 Yapılan Değişiklikler

### ✅ Yeni Alanlar Eklendi

| Alan Adı | Tip | Açıklama |
|-----------|-----|----------|
| `sku` | VARCHAR(50) | Stock Keeping Unit - Ürün stok kodu |
| `weight` | DECIMAL(10,3) | Ürün ağırlığı (kg) |
| `dimensions` | VARCHAR(100) | Ürün boyutları (LxWxH cm) |
| `is_taxable` | BOOLEAN | Vergiye tabi mi? |
| `discount_rate` | DECIMAL(5,2) | İndirim oranı (%) |
| `discount_start_date` | TIMESTAMP | İndirim başlangıç tarihi |
| `discount_end_date` | TIMESTAMP | İndirim bitiş tarihi |
| `supplier_code` | VARCHAR(50) | Tedarikçi kodu |
| `manufacturer` | VARCHAR(100) | Üretici |
| `country_of_origin` | VARCHAR(50) | Menşe ülke |
| `hs_code` | VARCHAR(20) | Harmonized System kodu |

### ✅ Mevcut Alanlar Güncellendi

| Alan Adı | Eski | Yeni |
|----------|------|------|
| `name` | VARCHAR(100) | VARCHAR(200) |
| `description` | VARCHAR(500) | TEXT |
| `category` | VARCHAR(50) | VARCHAR(100) |
| `barcode` | VARCHAR(50) | VARCHAR(100) |
| `tax_type` | VARCHAR(50) | VARCHAR(20) |

### ✅ Yeni Index'ler

- `IX_products_barcode` - Barkod araması için
- `IX_products_category` - Kategori filtreleme için
- `IX_products_sku` - SKU araması için
- `IX_products_supplier_code` - Tedarikçi kodu için
- `IX_products_tax_type` - Vergi tipi için
- `IX_products_is_active_category` - Aktif ürünler + kategori
- `IX_products_stock_quantity` - Stok durumu için

### ✅ Check Constraint'ler

- Fiyat ≥ 0
- Stok miktarı ≥ 0
- Minimum stok seviyesi ≥ 0
- İndirim oranı 0-100 arası
- Ağırlık > 0 (null değerler hariç)

## 🔍 Güncelleme Sonrası Kontrol

### 1. Tablo Yapısını Kontrol Et

```sql
-- Tablo yapısını kontrol et
SELECT column_name, data_type, is_nullable, column_default
FROM information_schema.columns 
WHERE table_name = 'products' 
ORDER BY ordinal_position;
```

### 2. Index'leri Kontrol Et

```sql
-- Index'leri kontrol et
SELECT indexname, indexdef 
FROM pg_indexes 
WHERE tablename = 'products';
```

### 3. Constraint'leri Kontrol Et

```sql
-- Constraint'leri kontrol et
SELECT conname, contype, pg_get_constraintdef(oid) 
FROM pg_constraint 
WHERE conrelid = 'products'::regclass;
```

### 4. Test Verisi Ekle

```sql
-- Test ürünü ekle
INSERT INTO products (
    name, price, tax_type, category, stock_quantity, 
    min_stock_level, unit, description, is_active
) VALUES (
    'Test Ürün - RKSV Uyumlu',
    19.99,
    'Standard',
    'Test Kategori',
    100,
    10,
    'adet',
    'RKSV uyumlu test ürünü',
    TRUE
);
```

## ⚠️ Önemli Notlar

### 1. Yedekleme
- **MUTLAKA** veritabanını yedekleyin
- Production ortamında test edin
- Rollback planı hazırlayın

### 2. Veri Kaybı Riski
- Mevcut veriler korunur
- Yeni alanlar NULL olarak eklenir
- Default değerler otomatik atanır

### 3. Uyumluluk
- Eski API'ler çalışmaya devam eder
- Yeni özellikler opsiyonel
- Geriye uyumluluk korunur

## 🚨 Sorun Giderme

### Migration Hatası

```bash
# Migration'ı sıfırla
dotnet ef database update 0

# Migration'ları sil
dotnet ef migrations remove

# Yeniden oluştur
dotnet ef migrations add UpdateProductTableForRKSV
dotnet ef database update
```

### Constraint Hatası

```sql
-- Constraint'i geçici olarak devre dışı bırak
ALTER TABLE products DISABLE TRIGGER ALL;

-- Veriyi düzelt
UPDATE products SET price = 0 WHERE price < 0;

-- Constraint'i tekrar etkinleştir
ALTER TABLE products ENABLE TRIGGER ALL;
```

### Index Hatası

```sql
-- Index'i yeniden oluştur
REINDEX INDEX IX_products_barcode;
REINDEX INDEX IX_products_category;
```

## 📞 Destek

Eğer güncelleme sırasında sorun yaşarsanız:

1. **Log'ları kontrol edin** - `logs/` klasörü
2. **Migration dosyalarını inceleyin** - `Migrations/` klasörü
3. **Veritabanı bağlantısını test edin**
4. **Rollback yapın** ve tekrar deneyin

## 🎯 Sonuç

Bu güncellemeler ile:

- ✅ **RKSV uyumluluğu** sağlanır
- ✅ **Performans** artar
- ✅ **Veri bütünlüğü** korunur
- ✅ **Yeni özellikler** eklenir
- ✅ **Frontend-Backend senkronizasyonu** tamamlanır

**Güncelleme tamamlandıktan sonra sistem tamamen RKSV uyumlu ve modern API standartlarında çalışacaktır!** 🎉

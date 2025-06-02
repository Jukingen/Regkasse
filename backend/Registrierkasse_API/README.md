# Registrierkasse - Avusturya Uyumlu Kasa Sistemi

## Özellikler

### Temel Özellikler
- ✅ Fatura Oluşturma ve Yönetimi (Rechnungen)
- ✅ Stok Yönetimi (Lagerhaltung)
- ✅ Ürün Yönetimi (Artikel)
- ✅ Müşteri Yönetimi (Kunden)
- ✅ Belge Arşivi (Belegarchiv)
- ✅ Özel Belgeler (Besondere Belege)
- ✅ İstatistikler (Statistiken)
- ✅ Hediye Çekleri (Gutscheine)
- ✅ İndirimler (Rabatte)
- ✅ Para Üstü Hesaplama (Wechselgeldberechnung)
- ✅ Teklifler (Angebote)
- ✅ Avusturya Maliye Uyumlu (Finanzamtskonform)
- ✅ Kullanıcı Yetkilendirme (Benutzerrechte) - 

### Kasa İşlemleri
- ✅ Kasa Aktivasyonu
- ✅ Yeni Belge Oluşturma
- ✅ Belge Düzenleme
- ✅ Açık Faturalar
- ✅ Sipariş Yönetimi
- ✅ Kasa Defteri
- ✅ Kasa Yönetimi
- ✅ Kasa Değiştirme

### Ayarlar ve Yönetim
- ✅ Kullanıcı Yönetimi
- ✅ Şirket Ayarları
- ✅ Kişisel Ayarlar
- ✅ Müşteri Veritabanı
- ✅ Ürün Veritabanı
- ✅ Stok Yönetimi
- ✅ FinanzOnline Entegrasyonu
- ✅ Belge Arşivi
- ✅ Fatura Düzenleyici
- ✅ İstatistikler
- ✅ Hediye Çeki Yönetimi
- ✅ Ödeme Yönetimi
- ✅ Yardım Sayfası
- ✅ Donanım Yönetimi
- ✅ Sektör Yönetimi
- ✅ Masa/Oda Planları

## Teknik Özellikler

### Backend
- .NET 7.0
- ASP.NET Core Web API
- Entity Framework Core
- PostgreSQL
- Identity Framework
- JWT Authentication

### Güvenlik
- Rol Tabanlı Yetkilendirme
- JWT Token Bazlı Kimlik Doğrulama
- Şifrelenmiş Veri İletişimi
- Güvenli Parola Politikası

### Veritabanı Tabloları
- Products (Ürünler)
- Customers (Müşteriler)
- Invoices (Faturalar)
- Orders (Siparişler)
- CashRegisters (Kasalar)
- Inventory (Stok)
- Users (Kullanıcılar)
- Vouchers (Hediye Çekleri)
- Discounts (İndirimler)
- Tables (Masalar)
- FinanceOnline (Maliye Entegrasyonu)

## Kurulum

1. PostgreSQL veritabanını kurun
2. Veritabanı bağlantı ayarlarını `appsettings.json` dosyasında yapılandırın
3. Migration'ları uygulayın:
   ```bash
   dotnet ef database update
   ```
4. Uygulamayı başlatın:
   ```bash
   dotnet run
   ```

## Kullanıcı Rolleri

- Administrator: Tam yetki
- Manager: Kasa ve personel yönetimi
- Cashier: Temel kasa işlemleri
- Accountant: Finansal raporlar ve muhasebe

## API Endpoints

### Kimlik Doğrulama
- POST /api/auth/login
- POST /api/auth/register

### Kasa İşlemleri
- GET /api/cashregister
- POST /api/cashregister/open
- POST /api/cashregister/close

### Faturalar
- GET /api/invoice
- POST /api/invoice
- PUT /api/invoice/{id}/status

### Stok
- GET /api/inventory
- POST /api/inventory/adjust
- GET /api/inventory/low-stock

### Ürünler
- GET /api/products
- POST /api/products
- PUT /api/products/{id}

### Müşteriler
- GET /api/customer
- POST /api/customer
- GET /api/customer/search

### İstatistikler
- GET /api/invoice/statistics
- GET /api/products/statistics
- GET /api/customer/statistics

## Örnek Veriler

Sistem ilk kurulumda otomatik olarak örnek veriler oluşturur:
- Demo şirket bilgileri
- Admin kullanıcısı (admin@kasse.at / Admin123!)
- Örnek ürünler
- Örnek müşteriler
- Örnek kasa

## Lisans

Bu proje [MIT lisansı](LICENSE) altında lisanslanmıştır. 
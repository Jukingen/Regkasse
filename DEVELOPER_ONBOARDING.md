# Registrierkasse - Geliştirici Onboarding Dokümanı

## 📋 İçindekiler
1. [Sistem Genel Bakış](#sistem-genel-bakış)
2. [Çok Dilli Sistem](#çok-dilli-sistem)
3. [Avusturya Yasal Zorunlulukları](#avusturya-yasal-zorunlulukları)
4. [Teknik Mimari](#teknik-mimari)
5. [Kodlama Standartları](#kodlama-standartları)
6. [Geliştirme Ortamı](#geliştirme-ortamı)
7. [Test Stratejisi](#test-stratejisi)
8. [Deployment](#deployment)

---

## 🏗️ Sistem Genel Bakış

### Registrierkasse Nedir?
Registrierkasse, Avusturya mevzuatına tam uyumlu, çok dilli POS (Point of Sale) sistemidir. Sistem hem backend (.NET Core) hem de frontend (React Native) bileşenlerinden oluşur.

### Temel Özellikler
- ✅ **Çok Dilli Destek**: Almanca (varsayılan), İngilizce, Türkçe
- ✅ **Avusturya Uyumluluğu**: RKSV, DSGVO, FinanzOnline entegrasyonu
- ✅ **Rol Tabanlı Erişim**: Admin, Manager, Cashier rolleri
- ✅ **TSE Entegrasyonu**: Teknik Güvenlik Cihazı desteği
- ✅ **Çoklu Ödeme**: Nakit, Kart, Kupon desteği
- ✅ **Detaylı Loglama**: Denetim için kapsamlı kayıtlar

---

## 🌍 Çok Dilli Sistem

### Dil Stratejisi
Sistem üç dilde çalışır:
- **Almanca (de-DE)**: Varsayılan dil, Avusturya pazarı için
- **İngilizce (en)**: Uluslararası kullanım için
- **Türkçe (tr)**: Türk kullanıcılar için

### Teknik Terim Politikası
**Önemli**: Teknik terimler her zaman İngilizce kalır:
```typescript
// ✅ Doğru - Teknik terimler İngilizce
"technical.transaction": "Transaction",
"technical.invoice": "Invoice",
"technical.receipt": "Receipt",
"technical.tse": "TSE",
"technical.finanzonline": "FinanzOnline"

// ❌ Yanlış - Teknik terimler çevrilmemeli
"technical.transaction": "İşlem", // Yanlış!
```

### Frontend Dil Yönetimi
```typescript
// LanguageContext kullanımı
const { language, setLanguage, t } = useLanguage();

// Çeviri kullanımı
const message = t('sales.new_sale'); // "Neuer Verkauf" (DE), "New Sale" (EN), "Yeni Satış" (TR)

// Parametreli çeviri
const welcomeMessage = t('common.welcome', { name: 'Ahmet' });
```

### Backend Dil Yönetimi
```csharp
// LocalizationService kullanımı
var message = _localizationService.Translate("sales.new_sale", "de-DE");
var errorMessage = _localizationService.Translate("error.unauthorized", language);
```

### Local Storage Persistence
Kullanıcının dil tercihi AsyncStorage'da saklanır:
```typescript
const LANGUAGE_STORAGE_KEY = '@registrierkasse_language';
await AsyncStorage.setItem(LANGUAGE_STORAGE_KEY, 'tr');
```

---

## ⚖️ Avusturya Yasal Zorunlulukları

### RKSV (Registrierkassensicherheitsverordnung)
**RKSV §6**: Tüm fişlerde TSE imzası zorunlu
```csharp
// Receipt modelinde TSE imzası
public string TseSignature { get; set; } = string.Empty;
```

**Zorunlu Fiş Alanları**:
- `BelegDatum` (DD.MM.YYYY)
- `Uhrzeit` (HH:MM:SS)
- `TSE-Signatur`
- `Kassen-ID`
- `Beleg-Nr.` (Benzersiz fiş numarası)

### DSGVO (Datenschutz-Grundverordnung)
**Müşteri verileri 7 yıl saklanmalı**:
```csharp
// Customer modelinde saklama süresi
public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
public DateTime? DeletedAt { get; set; } // Soft delete için
```

**Loglarda kredi kartı bilgisi tutulmamalı**:
```csharp
// Payment modelinde güvenli saklama
public string MaskedCardNumber { get; set; } = string.Empty; // "****1234"
public string CardToken { get; set; } = string.Empty; // Şifrelenmiş token
```

### FinanzOnline Entegrasyonu
**Yasal zorunluluk**: Tüm satışlar FinanzOnline'a bildirilmeli
```csharp
// FinanzOnlineService
public async Task<FinanzOnlineResponse> SubmitTransaction(Receipt receipt)
{
    // TSE imzası ile birlikte gönderim
    var request = new FinanzOnlineRequest
    {
        TransactionNumber = receipt.ReceiptNumber,
        TseSignature = receipt.TseSignature,
        Amount = receipt.TotalAmount,
        TaxAmount = receipt.TaxAmount
    };
    
    return await _finanzOnlineClient.SubmitAsync(request);
}
```

---

## 🏛️ Teknik Mimari

### Backend Mimari (.NET Core)
```
Registrierkasse_API/
├── Controllers/          # API endpoint'leri
├── Services/            # İş mantığı servisleri
├── Models/              # Veri modelleri
├── Data/                # Entity Framework context
├── Middleware/          # Yetki kontrolü, loglama
├── Resources/           # Dil dosyaları
└── Tests/               # Unit ve integration testler
```

### Frontend Mimari (React Native)
```
frontend/
├── app/                 # Ana uygulama dosyaları
├── components/          # Yeniden kullanılabilir bileşenler
├── contexts/            # Global state yönetimi
├── hooks/               # Custom React hook'ları
├── services/            # API servisleri
├── types/               # TypeScript tip tanımları
└── i18n/                # Dil dosyaları
```

### Veritabanı Şeması (PostgreSQL)
```sql
-- Ana tablolar
CREATE TABLE receipts (
    id SERIAL PRIMARY KEY,
    receipt_number VARCHAR(50) UNIQUE NOT NULL,
    tse_signature TEXT NOT NULL,
    receipt_date DATE NOT NULL,
    receipt_time TIME NOT NULL,
    cash_register_id VARCHAR(50) NOT NULL,
    total_amount DECIMAL(18,2) NOT NULL,
    tax_amount DECIMAL(18,2) NOT NULL,
    payment_method VARCHAR(20) NOT NULL,
    qr_code_data TEXT,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- Rol ve yetki tabloları
CREATE TABLE roles (
    id SERIAL PRIMARY KEY,
    name VARCHAR(50) UNIQUE NOT NULL,
    description TEXT
);

CREATE TABLE permissions (
    id SERIAL PRIMARY KEY,
    resource VARCHAR(50) NOT NULL,
    action VARCHAR(50) NOT NULL,
    description TEXT
);
```

---

## 📝 Kodlama Standartları

### 1. Dil Kullanım Kuralları
```csharp
// ✅ Doğru - Comment'ler Türkçe
/// <summary>
/// Yeni satış işlemi oluşturur ve TSE imzası ekler
/// </summary>
public async Task<Receipt> CreateSaleAsync(SaleRequest request)

// ✅ Doğru - Log mesajları İngilizce
_logger.LogInformation("Sale completed successfully for receipt {ReceiptNumber}", receiptNumber);

// ✅ Doğru - Kullanıcı mesajları dile göre
var message = _localizationService.Translate("success.sale_completed", language);
```

### 2. API Endpoint Standartları
```csharp
// ✅ Doğru - Endpoint'ler İngilizce
[HttpPost("sales")]
[HttpGet("receipts/{id}")]
[HttpPut("customers/{id}")]

// ✅ Doğru - JSON alanları İngilizce
{
    "receipt_number": "AT-DEMO-20241201-12345678",
    "total_amount": 120.50,
    "payment_method": "cash",
    "tse_signature": "DEMO-SIGNATURE-123"
}
```

### 3. Güvenlik Standartları
```csharp
// ✅ Doğru - Rol tabanlı yetki kontrolü
[Authorize(Roles = "Admin")]
[RequirePermission("receipts", "delete")]

// ✅ Doğru - Input validasyonu
[Required]
[StringLength(50)]
public string ReceiptNumber { get; set; } = string.Empty;
```

### 4. Error Handling
```csharp
// ✅ Doğru - Kapsamlı hata yönetimi
try
{
    var result = await _service.ProcessAsync(request);
    return Ok(result);
}
catch (ValidationException ex)
{
    _logger.LogWarning("Validation error: {Message}", ex.Message);
    return BadRequest(new { error = "validation_error", details = ex.Message });
}
catch (Exception ex)
{
    _logger.LogError(ex, "Unexpected error occurred");
    return StatusCode(500, new { error = "internal_server_error" });
}
```

---

## 🛠️ Geliştirme Ortamı

### Gereksinimler
- **.NET 8.0 SDK**
- **Node.js 18+**
- **PostgreSQL 14+**
- **Visual Studio 2022** veya **VS Code**
- **Android Studio** (React Native için)

### Kurulum Adımları
```bash
# 1. Repository'yi klonla
git clone https://github.com/your-org/registrierkasse.git
cd registrierkasse

# 2. Backend kurulumu
cd backend/Registrierkasse_API
dotnet restore
dotnet ef database update
dotnet run

# 3. Frontend kurulumu
cd ../../frontend
npm install
npx expo start
```

### Environment Variables
```bash
# Backend (.env)
DATABASE_CONNECTION_STRING=Host=localhost;Database=registrierkasse;Username=postgres;Password=password
JWT_SECRET=your-super-secret-jwt-key
FINANZONLINE_API_KEY=your-finanzonline-api-key
TSE_DEVICE_ID=your-tse-device-id

# Frontend (.env)
API_BASE_URL=http://localhost:5000/api
EXPO_PUBLIC_APP_NAME=Registrierkasse
```

---

## 🧪 Test Stratejisi

### Unit Tests
```csharp
[Fact]
public async Task CreateReceipt_WithValidData_ShouldReturnReceipt()
{
    // Arrange
    var request = new CreateReceiptRequest
    {
        Items = new List<ReceiptItemRequest>
        {
            new() { ProductId = 1, Quantity = 2, UnitPrice = 10.00m }
        },
        PaymentMethod = "cash"
    };

    // Act
    var result = await _receiptService.CreateReceiptAsync(request);

    // Assert
    Assert.NotNull(result);
    Assert.NotEmpty(result.TseSignature);
    Assert.Equal(20.00m, result.TotalAmount);
}
```

### Integration Tests
```csharp
[Fact]
public async Task ReceiptController_CreateReceipt_ShouldReturn201()
{
    // Arrange
    var client = _factory.CreateClient();
    var request = new { /* test data */ };

    // Act
    var response = await client.PostAsJsonAsync("/api/receipts", request);

    // Assert
    Assert.Equal(HttpStatusCode.Created, response.StatusCode);
}
```

### E2E Tests (Frontend)
```typescript
describe('Sales Flow', () => {
  it('should complete a sale and generate receipt', async () => {
    // Test adımları
    await element(by.id('add-item-button')).tap();
    await element(by.id('complete-sale-button')).tap();
    
    // Assert
    await expect(element(by.text('Sale completed'))).toBeVisible();
  });
});
```

---

## 🚀 Deployment

### Backend Deployment
```bash
# Production build
dotnet publish -c Release -o ./publish

# Docker deployment
docker build -t registrierkasse-api .
docker run -p 5000:5000 registrierkasse-api
```

### Frontend Deployment
```bash
# Expo build
eas build --platform android
eas build --platform ios

# Web deployment
expo export:web
```

### Database Migration
```bash
# Production migration
dotnet ef database update --connection "Host=prod-db;Database=registrierkasse;Username=prod-user;Password=prod-pass"
```

---

## 📚 Önemli Dosyalar ve Klasörler

### Backend
- `Controllers/` - API endpoint'leri
- `Services/` - İş mantığı
- `Models/` - Veri modelleri
- `Data/AppDbContext.cs` - Entity Framework context
- `Resources/` - Dil dosyaları
- `Tests/` - Test dosyaları

### Frontend
- `contexts/LanguageContext.tsx` - Dil yönetimi
- `components/` - UI bileşenleri
- `services/api/` - API servisleri
- `i18n/` - Dil dosyaları
- `types/` - TypeScript tipleri

### Konfigürasyon
- `appsettings.json` - Backend ayarları
- `app.json` - Expo ayarları
- `package.json` - NPM bağımlılıkları
- `*.csproj` - .NET proje dosyaları

---

## 🆘 Yardım ve Destek

### Dokümantasyon
- [API Dokümantasyonu](./API_DOCUMENTATION.md)
- [Database Schema](./DATABASE_SCHEMA.md)
- [Testing Guide](./TESTING_GUIDE.md)

### İletişim
- **Teknik Sorular**: tech-support@registrierkasse.com
- **Bug Reports**: github.com/your-org/registrierkasse/issues
- **Feature Requests**: github.com/your-org/registrierkasse/discussions

### Geliştirici Araçları
- **Swagger UI**: http://localhost:5000/swagger
- **Database Admin**: pgAdmin veya DBeaver
- **API Testing**: Postman collection mevcut

---

## 🎯 Sonraki Adımlar

1. **Sistemi Keşfet**: Demo kullanıcılarla test et
2. **Kod İncele**: Örnek implementasyonları incele
3. **Test Yaz**: Yeni özellikler için test yaz
4. **Dokümantasyon Güncelle**: Eksik kısımları tamamla
5. **Code Review**: Pull request'leri incele

**Başarılar! 🚀** 
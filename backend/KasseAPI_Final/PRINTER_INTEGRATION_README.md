# Yazıcı Entegrasyonu - Backend

## Genel Bakış

Bu dokümantasyon, KasseAPI backend'inde başarılı ödeme sonrası yazıcıya direkt çıktı alma özelliğini açıklar. Sistem, EPSON ve Star marka yazıcıları destekler ve OCRA-B font kullanarak profesyonel fiş çıktısı sağlar.

## Özellikler

### 🖨️ Desteklenen Yazıcılar
- **EPSON TM-T88VI** (Öncelikli model)
- **EPSON TM-T88V** (Alternatif model)
- **Star TSP 700** (Star yazıcı desteği)
- **Microsoft Print to PDF** (Test için fallback)

### 📄 Fiş Formatı
- OCRA-B font desteği
- 80mm kağıt genişliği
- Otomatik kesim
- TSE imza bilgileri
- Müşteri ve ürün detayları
- Vergi hesaplamaları

### 🔧 Teknik Özellikler
- Cross-platform desteği (Windows/Linux)
- Otomatik yazıcı tespiti
- Bağlantı durumu kontrolü
- Hata durumunda dosyaya kaydetme
- Detaylı loglama ve audit trail

## API Endpoints

### 1. Yazıcı Durumu Kontrolü
```http
GET /api/printer/status
Authorization: Bearer {token}
```

**Response:**
```json
{
  "availablePrinters": ["EPSON TM-T88VI", "Star TSP 700"],
  "defaultPrinter": "EPSON TM-T88VI",
  "defaultPrinterStatus": "Ready",
  "connectionTest": true,
  "lastChecked": "2025-01-15T10:30:00Z",
  "message": "Printer connection successful"
}
```

### 2. Mevcut Yazıcıları Listele
```http
GET /api/printer/printers
Authorization: Bearer {token}
```

**Response:**
```json
{
  "printers": ["EPSON TM-T88VI", "Star TSP 700"],
  "count": 2,
  "message": "Found 2 available printers"
}
```

### 3. Yazıcı Bağlantı Testi
```http
POST /api/printer/test
Authorization: Bearer {token}
Content-Type: application/json

{
  "printerName": "EPSON TM-T88VI"
}
```

**Response:**
```json
{
  "printerName": "EPSON TM-T88VI",
  "connectionSuccessful": true,
  "printerStatus": "Ready",
  "testedAt": "2025-01-15T10:30:00Z",
  "message": "Printer connection test successful"
}
```

### 4. Test Sayfası Yazdır
```http
POST /api/printer/print-test
Authorization: Bearer {token}
Content-Type: application/json

{
  "printerName": "EPSON TM-T88VI"
}
```

**Response:**
```json
{
  "printerName": "EPSON TM-T88VI",
  "printSuccessful": true,
  "testedAt": "2025-01-15T10:30:00Z",
  "message": "Test page printed successfully"
}
```

### 5. Yazıcı Entegrasyon Testi
```http
GET /api/test/printer
Authorization: Bearer {token}
```

**Response:**
```json
{
  "testType": "Printer Integration Test",
  "timestamp": "2025-01-15T10:30:00Z",
  "user": {
    "id": "user123",
    "role": "Cashier"
  },
  "printerTests": {
    "availablePrinters": ["EPSON TM-T88VI"],
    "defaultPrinterStatus": "Ready",
    "connectionTest": true,
    "message": "Printer connection successful"
  },
  "receiptTests": {
    "receiptGenerated": true,
    "digitalReceiptGenerated": true,
    "receiptLength": 1250,
    "digitalReceiptLength": 3200
  },
  "testStatus": "PASSED",
  "message": "Printer integration test completed successfully"
}
```

## Otomatik Fiş Yazdırma

### Başarılı Ödeme Sonrası
Sistem, `ConfirmPayment` endpoint'i çağrıldığında otomatik olarak:

1. **Fiş İçeriği Oluşturma**
   - Ödeme detayları
   - Ürün listesi
   - Vergi hesaplamaları
   - TSE imza bilgileri

2. **Yazıcı Durumu Kontrolü**
   - Varsayılan yazıcı tespiti
   - Bağlantı durumu kontrolü
   - Kağıt durumu kontrolü

3. **Fiş Yazdırma**
   - Direkt yazıcıya gönderim
   - Başarılı yazdırma loglama
   - Audit trail kaydı

4. **Hata Durumunda Fallback**
   - Dosyaya kaydetme
   - Uyarı loglama
   - Kullanıcıya bildirim

### Loglama ve Audit
```csharp
// Başarılı yazdırma
await _auditLogService.LogPaymentOperationAsync(
    AuditLogActions.RECEIPT_PRINTED,
    AuditLogEntityTypes.RECEIPT,
    paymentDetails.Id,
    userId,
    userRole,
    amount,
    "PRINT_SUCCESS",
    // ... diğer parametreler
);

// Fallback dosya kaydı
await _auditLogService.LogPaymentOperationAsync(
    AuditLogActions.RECEIPT_SAVED,
    AuditLogEntityTypes.RECEIPT,
    paymentDetails.Id,
    userId,
    userRole,
    amount,
    "FILE_SAVE",
    // ... diğer parametreler
);
```

## Servis Yapısı

### IReceiptService Interface
```csharp
public interface IReceiptService
{
    Task<string> GenerateReceiptAsync(PaymentDetails payment, Invoice invoice, Cart cart);
    Task<bool> PrintReceiptAsync(string receiptContent, string printerName = null);
    Task<string> GenerateDigitalReceiptAsync(PaymentDetails payment, Invoice invoice, Cart cart);
    Task<bool> SaveReceiptToFileAsync(string receiptContent, string fileName);
    List<string> GetAvailablePrinters();
    Task<bool> TestPrinterConnectionAsync(string printerName = null);
    Task<PrinterStatus> GetPrinterStatusAsync(string printerName = null);
}
```

### PrinterStatus Enum
```csharp
public enum PrinterStatus
{
    Ready,      // Yazıcı hazır
    Offline,    // Yazıcı çevrimdışı
    PaperOut,   // Kağıt yok
    Error,      // Hata durumu
    Unknown     // Bilinmeyen durum
}
```

## Kurulum ve Konfigürasyon

### 1. Gerekli Paketler
```xml
<PackageReference Include="System.Runtime.InteropServices" Version="9.0.0" />
```

### 2. Dependency Injection
```csharp
// Program.cs
builder.Services.AddScoped<IReceiptService, ReceiptService>();
```

### 3. Yazıcı Konfigürasyonu
```csharp
// ReceiptService constructor
public ReceiptService(ILogger<ReceiptService> logger)
{
    _logger = logger;
    _receiptsDirectory = Path.Combine(Directory.GetCurrentDirectory(), "Receipts");
    _defaultPrinterName = GetDefaultPrinter(); // Otomatik tespit
    
    if (!Directory.Exists(_receiptsDirectory))
    {
        Directory.CreateDirectory(_receiptsDirectory);
    }
}
```

## Kullanım Örnekleri

### Basit Fiş Yazdırma
```csharp
// PaymentController'da
var receiptContent = await _receiptService.GenerateReceiptAsync(paymentDetails, invoice, cart);
var printSuccess = await _receiptService.PrintReceiptAsync(receiptContent);

if (printSuccess)
{
    _logger.LogInformation("Receipt printed successfully");
}
else
{
    // Fallback: dosyaya kaydet
    await _receiptService.SaveReceiptToFileAsync(receiptContent, $"receipt_{DateTime.UtcNow:yyyyMMdd_HHmmss}.txt");
}
```

### Belirli Yazıcıya Yazdırma
```csharp
var printSuccess = await _receiptService.PrintReceiptAsync(receiptContent, "EPSON TM-T88VI");
```

### Yazıcı Durumu Kontrolü
```csharp
var printerStatus = await _receiptService.GetPrinterStatusAsync();
if (printerStatus == PrinterStatus.Ready)
{
    // Yazdırma işlemi
}
```

## Hata Yönetimi

### Yazıcı Bağlantı Hatası
```csharp
try
{
    var printSuccess = await _receiptService.PrintReceiptAsync(receiptContent);
    if (!printSuccess)
    {
        // Fallback: dosyaya kaydet
        await _receiptService.SaveReceiptToFileAsync(receiptContent, fileName);
        _logger.LogWarning("Printing failed, saved to file instead");
    }
}
catch (Exception ex)
{
    _logger.LogError(ex, "Error during receipt printing");
    // Hata durumunda dosyaya kaydet
    await _receiptService.SaveReceiptToFileAsync(receiptContent, fileName);
}
```

### Yazıcı Bulunamadı
```csharp
var availablePrinters = _receiptService.GetAvailablePrinters();
if (!availablePrinters.Any())
{
    _logger.LogWarning("No printers available, saving to file");
    await _receiptService.SaveReceiptToFileAsync(receiptContent, fileName);
    return;
}
```

## Test ve Debug

### 1. Yazıcı Entegrasyon Testi
```bash
curl -X GET "https://localhost:7001/api/test/printer" \
  -H "Authorization: Bearer {token}"
```

### 2. Yazıcı Durumu Kontrolü
```bash
curl -X GET "https://localhost:7001/api/printer/status" \
  -H "Authorization: Bearer {token}"
```

### 3. Test Sayfası Yazdırma
```bash
curl -X POST "https://localhost:7001/api/printer/print-test" \
  -H "Authorization: Bearer {token}" \
  -H "Content-Type: application/json" \
  -d '{"printerName": "EPSON TM-T88VI"}'
```

## Performans ve Optimizasyon

### 1. Asenkron İşlemler
- Tüm yazdırma işlemleri asenkron
- UI bloklanması yok
- Paralel işlem desteği

### 2. Caching
- Yazıcı listesi cache'leme
- Durum bilgisi cache'leme
- Bağlantı test sonuçları cache'leme

### 3. Batch Processing
- Çoklu fiş yazdırma desteği
- Kuyruk sistemi (gelecek özellik)
- Öncelik bazlı yazdırma

## Güvenlik

### 1. Yetkilendirme
```csharp
[Authorize(Roles = "Administrator,Manager,Cashier")]
public class PrinterController : ControllerBase
```

### 2. Audit Logging
- Tüm yazdırma işlemleri loglanır
- Kullanıcı bilgileri kaydedilir
- Zaman damgası eklenir

### 3. Input Validation
```csharp
public class PrinterTestRequest
{
    public string? PrinterName { get; set; }
}
```

## Gelecek Özellikler

### 1. Gelişmiş Yazıcı Yönetimi
- CUPS entegrasyonu (Linux)
- Windows Print Spooler entegrasyonu
- Yazıcı kuyruğu yönetimi

### 2. Çoklu Format Desteği
- PDF çıktı
- HTML çıktı
- XML çıktı
- E-posta gönderimi

### 3. Yazıcı Monitoring
- Real-time durum takibi
- Performans metrikleri
- Hata raporlama

## Sorun Giderme

### Yaygın Sorunlar

#### 1. Yazıcı Bulunamadı
```bash
# Windows
wmic printer list brief

# Linux
lpstat -p
```

#### 2. Bağlantı Hatası
```bash
# Test bağlantısı
curl -X POST "https://localhost:7001/api/printer/test"
```

#### 3. Font Sorunu
- OCRA-B font yüklü olmalı
- Yazıcı font desteği kontrol edilmeli

### Debug Logları
```csharp
_logger.LogInformation("Attempting to print receipt to printer: {PrinterName}", printerName);
_logger.LogWarning("Printer {PrinterName} is not ready. Status: {Status}", printerName, printerStatus);
_logger.LogError(ex, "Error printing receipt");
```

## Destek ve İletişim

### Teknik Destek
- Backend geliştirici: [Developer Name]
- E-posta: [email@example.com]
- Dokümantasyon: [Wiki Link]

### Kaynak Kod
- Repository: [GitHub Link]
- Branch: `main`
- Son güncelleme: 15.01.2025

---

**Not:** Bu dokümantasyon sürekli güncellenmektedir. En güncel bilgiler için repository'yi kontrol ediniz.

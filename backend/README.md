# Registrierkasse Backend

## 🖥️ Backend API
.NET 8 Web API tabanlı, RKSV standartlarına uygun kasa yazılımı backend servisi. PostgreSQL veritabanı kullanır ve TSE entegrasyonu içerir.

## 🛠️ Teknik Detaylar
- **Framework:** .NET 8
- **Veritabanı:** PostgreSQL 15+
- **ORM:** Entity Framework Core
- **API Dokümantasyonu:** Swagger/OpenAPI
- **Loglama:** Serilog
- **Cache:** Redis
- **Message Queue:** RabbitMQ
- **TSE Entegrasyonu:** fiskaly API

## 📦 Kurulum

### Gereksinimler
- .NET 8 SDK
- PostgreSQL 15+
- Redis (opsiyonel, cache için)
- RabbitMQ (opsiyonel, message queue için)
- TSE Cihazı (Epson-TSE veya fiskaly)

### Kurulum Adımları
1. Bağımlılıkları yükleyin:
```bash
dotnet restore
```

2. Veritabanını oluşturun:
```bash
dotnet ef database update
```

3. Uygulamayı başlatın:
```bash
dotnet run
```

## 🏗️ Proje Yapısı
```
backend/
├── Registrierkasse.API/           # API projesi
├── Registrierkasse.Core/          # Domain modelleri ve interfaces
├── Registrierkasse.Infrastructure/# Veritabanı ve harici servisler
├── Registrierkasse.Application/   # İş mantığı ve servisler
└── Registrierkasse.Tests/         # Unit ve integration testler
```

## 🔧 Geliştirme

### Önemli Komutlar
```bash
dotnet build        # Projeyi derle
dotnet test        # Testleri çalıştır
dotnet run         # Uygulamayı başlat
dotnet ef migrate  # Yeni migration oluştur
```

### API Endpointleri
- `POST /api/invoices` - Yeni fiş oluştur
- `GET /api/invoices/{id}` - Fiş detayları
- `POST /api/tse/sign` - TSE imzası al
- `POST /api/tse/daily-report` - Günlük rapor
- `GET /api/reports` - Raporlar

### TSE Entegrasyonu
- TSE cihazı bağlantı yönetimi
- Fiş imzalama işlemleri
- Günlük raporlama (Tagesabschluss)
- Zaman senkronizasyonu

## 📚 Detaylı Dokümantasyon
Daha detaylı bilgi için [DEVELOPMENT.md](DEVELOPMENT.md) dosyasını inceleyin.

## ⚠️ Önemli Notlar
- TSE cihazı bağlantısı zorunludur
- FinanzOnline API bağlantısı gereklidir
- Gün sonu raporu (Tagesabschluss) atlanmamalıdır
- Müşteri verileri 7 yıl saklanmalıdır
- Kredi kartı bilgileri loglanmamalıdır
- Tüm fişlerde TSE imzası olmalıdır
- Fiş numarası formatı: AT-{TSE_ID}-{YYYYMMDD}-{SEQ}

## 🔐 Yapılandırma ve Güvenlik

### Hassas Bilgiler
Hassas bilgiler içeren yapılandırma dosyaları Git'e gönderilmez:
- `appsettings.json`
- `appsettings.Development.json`
- `appsettings.Production.json`
- `secrets.json`

### Yapılandırma Kurulumu
1. `appsettings.example.json` dosyasını `appsettings.json` olarak kopyalayın:
```bash
cp appsettings.example.json appsettings.json
```

2. `appsettings.json` dosyasındaki değerleri kendi ortamınıza göre güncelleyin:
   - Veritabanı bağlantı bilgileri
   - TSE cihaz bilgileri
   - FinanzOnline API bilgileri
   - JWT secret key

### Güvenli Geliştirme
- Hassas bilgileri asla Git'e commit etmeyin
- Production ortamında Azure Key Vault veya benzeri bir servis kullanın
- Tüm API anahtarlarını ve şifreleri güvenli bir şekilde saklayın
- Development ortamında User Secrets kullanabilirsiniz:
```bash
dotnet user-secrets init
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "your_connection_string"
``` 
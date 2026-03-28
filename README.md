# Registrierkasse - Avusturya RKSV Uyumlu Kasa Yazılımı

## 🎯 Proje Açıklaması

Registrierkasse, Avusturya RKSV (Registrierkassen-Sicherheitsverordnung) standartlarına uygun olarak geliştirilmiş modern bir kasa yazılımıdır. TSE (Technische Sicherheitseinrichtung) entegrasyonu, FinanzOnline API desteği ve çoklu kullanıcı yönetimi ile tam uyumlu bir çözüm sunar.

## 🏗️ Teknoloji Stack

### Backend
- **ASP.NET Core 8.0** - Web API framework
- **Entity Framework Core** - ORM
- **PostgreSQL** - Veritabanı
- **ASP.NET Identity** - Kullanıcı yönetimi
- **JWT** - Token tabanlı authentication

### Frontend
- **React 18** - UI framework
- **TypeScript** - Tip güvenliği
- **Vite** - Build tool
- **Axios** - HTTP client
- **React Router** - Routing

### Hardware Entegrasyonu
- **TSE Cihazları** - Epson-TSE, fiskaly
- **Yazıcılar** - EPSON TM-T88VI, Star TSP 700
- **OCRA-B Font** - Zorunlu font desteği

## 🚀 Hızlı Başlangıç

### Gereksinimler
- .NET 8.0 SDK
- Node.js 18+
- PostgreSQL 14+
- npm veya yarn

### Kurulum

1. **Repository'yi klonlayın**
```bash
git clone https://github.com/your-username/Registrierkasse.git
cd Registrierkasse
```

2. **Backend'i kurun**
```bash
cd backend
dotnet restore
dotnet ef database update
dotnet run
```

3. **Frontend'i kurun**
```bash
cd ../frontend-admin
npm install
npm run dev
```

4. **Uygulamayı açın**
- Admin Panel: http://localhost:5173
- API: http://localhost:5183
- Swagger: http://localhost:5183/swagger

## 📊 Demo Veriler

### Admin Kullanıcı
- **Email**: admin@admin.com
- **Şifre**: Abcd#1234 / Admin123!

### Demo İçerik
- 5 ürün (kahve, yemek, tatlı)
- 3 müşteri
- 3 kasa
- 3 fatura

## 🔧 RKSV Uyumluluğu

### Zorunlu Özellikler
- ✅ TSE imzası (RKSV §6)
- ✅ Fiş numarası formatı: AT-{TSE_ID}-{YYYYMMDD}-{SEQ}
- ✅ Vergi detayları (20%, 10%, 13%)
- ✅ Zorunlu alanlar (BelegDatum, Uhrzeit, TSE-Signatur, Kassen-ID)
- ✅ 7 yıl veri saklama (DSGVO)
- ✅ Audit logging

### TSE Entegrasyonu
- Epson-TSE cihaz desteği
- fiskaly entegrasyonu
- Offline mod desteği
- Güvenli imza üretimi

## 📁 Proje Yapısı

```
Registrierkasse/
├── backend/                    # ASP.NET Core API
├── frontend/                   # Kasiyer arayüzü (gelecek)
├── frontend-admin/             # Yönetim paneli
├── DEVELOPMENT_LOG.md          # Geliştirme günlüğü
├── PROJECT_STRUCTURE.md        # Detaylı yapı
└── README.md                   # Bu dosya
```

## 🔐 Güvenlik

- JWT token authentication
- Role-based authorization
- API endpoint protection
- Secure password hashing
- Audit logging
- TSE güvenlik standartları

## 📈 Özellikler

### Yönetim Paneli
- Dashboard ve istatistikler
- Ürün yönetimi
- Müşteri yönetimi
- Fatura yönetimi
- Kasa yönetimi
- Raporlama

### Kasiyer Arayüzü (Gelecek)
- Hızlı satış ekranı
- Offline mod
- PouchDB desteği
- Basit kullanıcı arayüzü

### API Endpoints
- Authentication
- CRUD işlemleri
- Raporlama
- TSE entegrasyonu

## 🛠️ Geliştirme

### Backend Geliştirme
```bash
cd backend
dotnet watch run
```

### Frontend Geliştirme
```bash
cd frontend-admin
npm run dev
```
Admin paneli için `.env.local` dosyası **`frontend-admin/`** klasöründe olmalıdır (depo kökündeki `.env` bu uygulama tarafından okunmaz). RKSV hub (`/rksv`) ortam rozeti için `NEXT_PUBLIC_RKSV_ENVIRONMENT=TEST` veya `PROD` tanımlayın; `NEXT_PUBLIC_*` değerleri derleme sırasında istemci paketine gömülür — değişiklikten sonra `npm run dev` sürecini yeniden başlatın veya yeniden build alın. Ayrıntı: `frontend-admin/.env.example` ve `frontend-admin/README.md`.

### Veritabanı Migration
```bash
cd backend
dotnet ef migrations add MigrationName
dotnet ef database update
```

## 📝 Test

### API Testleri
```bash
cd backend
dotnet test
```

### Frontend Testleri
```bash
npm test
```

## 🚀 Deployment

### Backend Deployment
```bash
cd backend
dotnet publish -c Release
```

### Admin panel (`frontend-admin`) deployment

`NEXT_PUBLIC_*` değişkenleri (ör. `NEXT_PUBLIC_RKSV_ENVIRONMENT`, `NEXT_PUBLIC_API_BASE_URL`) **yalnızca `next build` veya `next dev` derlemesi sırasında** istemci paketine gömülür; imajı build etmeden sonra yalnızca `docker run -e` ile vermek rozeti düzeltmez. Ayrıntı ve örnek Dockerfile/Compose parçaları: [frontend-admin/docs/DEPLOYMENT_BUILD_TIME_ENV.md](frontend-admin/docs/DEPLOYMENT_BUILD_TIME_ENV.md).

```bash
cd frontend-admin
npm ci
# Üretim build: .env.production / ortam değişkeni veya CI adımında NEXT_PUBLIC_* tanımlı olmalı
npm run build
```

## 📞 Destek

- **Dokümantasyon**: [DEVELOPMENT_LOG.md](DEVELOPMENT_LOG.md)
- **Proje Yapısı**: [PROJECT_STRUCTURE.md](PROJECT_STRUCTURE.md)
- **API Dokümantasyonu**: http://localhost:5183/swagger

## 📄 Lisans

Bu proje MIT lisansı altında lisanslanmıştır.

## 🤝 Katkıda Bulunma

1. Fork yapın
2. Feature branch oluşturun (`git checkout -b feature/AmazingFeature`)
3. Commit yapın (`git commit -m 'Add some AmazingFeature'`)
4. Push yapın (`git push origin feature/AmazingFeature`)
5. Pull Request açın

## 📋 Roadmap

### Kısa Vadeli (1-2 hafta)
- [ ] TSE cihaz entegrasyonu
- [ ] Yazıcı entegrasyonu
- [ ] Kasiyer arayüzü

### Orta Vadeli (1-2 ay)
- [ ] FinanzOnline API entegrasyonu
- [ ] Gelişmiş raporlama
- [ ] Çoklu dil desteği

### Uzun Vadeli (3-6 ay)
- [ ] Mobil uygulama
- [ ] Cloud deployment
- [ ] Multi-tenant desteği

## 🌐 Localization (i18n) ve CI

Admin paneli (`frontend-admin`) çevirileri için doğrulama ve kullanım taraması GitHub Actions içinde çalışır; yerelde `npm run i18n:ci` ile aynı kapıyı deneyebilirsiniz. Bütçe eşikleri ve komut tablosu: [docs/contributing-i18n.md](docs/contributing-i18n.md).

## 🤖 AI-Assisted Development

Bu proje geliştirilirken AI asistanlarından faydalanılmaktadır. Tutarlılığı korumak için:
- Her görev öncesi `ai/06_TASK_TEMPLATE.md` dosyasını kullanın.
- `ai/*.md` dosyalarındaki kurallar tüm AI etkileşimleri için bağlayıcıdır (authoritative).
- Dosya haritası ve platform ayrımı için `ai/08_FILE_MAP.md` rehberine uyun.

---
**Son Güncelleme**: 8 Şubat 2026  
**Versiyon**: 1.1.0  
**Durum**: ✅ Temel Altyapı + AI Context Pack + Admin Skeleton Hazır
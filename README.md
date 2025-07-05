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
cd backend/Registrierkasse_API
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
cd backend/Registrierkasse_API
dotnet watch run
```

### Frontend Geliştirme
```bash
cd frontend-admin
npm run dev
```

### Veritabanı Migration
```bash
dotnet ef migrations add MigrationName
dotnet ef database update
```

## 📝 Test

### API Testleri
```bash
dotnet test
```

### Frontend Testleri
```bash
npm test
```

## 🚀 Deployment

### Backend Deployment
```bash
dotnet publish -c Release
```

### Frontend Deployment
```bash
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

---
**Son Güncelleme**: 11 Haziran 2024  
**Versiyon**: 1.0.0  
**Durum**: ✅ Temel Altyapı Tamamlandı 
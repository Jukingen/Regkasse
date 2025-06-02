# Registrierkasse - Avusturya Kasa Yazılımı

## 📋 Proje Hakkında
Bu proje, Avusturya'da kullanılmak üzere RKSV (Registrierkassensicherheitsverordnung) standartlarına uygun, modern bir kasa yazılımıdır. TSE (Technische Sicherheitseinrichtung) entegrasyonu ve FinanzOnline API desteği ile tam uyumlu çalışmaktadır.

## 🏗️ Teknoloji Yığını
- **Frontend:** React Native/Expo (TypeScript)
- **Backend:** .NET 8 Web API
- **Veritabanı:** PostgreSQL
- **TSE Cihazı:** Epson-TSE veya fiskaly
- **Yazıcı:** EPSON TM-T88VI veya Star TSP 700

## 📦 Kurulum

### Gereksinimler
- Node.js 18+
- .NET 8 SDK
- PostgreSQL 15+
- TSE Cihazı (Epson-TSE veya fiskaly)
- Desteklenen Yazıcı

### Hızlı Başlangıç
1. Repoyu klonlayın:
```bash
git clone https://github.com/[kullanici-adi]/registrierkasse.git
cd registrierkasse
```

2. Frontend kurulumu:
```bash
cd frontend
npm install
```

3. Backend kurulumu:
```bash
cd backend
dotnet restore
```

4. Detaylı kurulum adımları için:
- Frontend: [Frontend README.md](frontend/README.md)
- Backend: [Backend README.md](backend/README.md)

## 🔑 Önemli Özellikler
- RKSV §6 uyumlu TSE imzalı fişler
- DSGVO uyumlu veri saklama (7 yıl)
- Çoklu dil desteği (de-DE, en)
- Çevrimdışı mod desteği (PouchDB)
- FinanzOnline API entegrasyonu
- Günlük raporlama (Tagesabschluss)

## 📝 Lisans
Bu proje [MIT lisansı](LICENSE) altında lisanslanmıştır.

## 🤝 Katkıda Bulunma
Katkıda bulunmak için lütfen [CONTRIBUTING.md](CONTRIBUTING.md) dosyasını inceleyin.

## 📚 Dokümantasyon
- [Geliştirici Dokümantasyonu](DEVELOPMENT.md)
- [Frontend Geliştirme Kılavuzu](frontend/DEVELOPMENT.md)
- [Backend Geliştirme Kılavuzu](backend/DEVELOPMENT.md)

## ⚠️ Önemli Notlar
- TSE cihazı bağlı değilse sistem çalışmaz
- FinanzOnline API bağlantısı olmadan fiş kesilemez
- Gün sonu raporu (Tagesabschluss) atlanmamalıdır
- Tüm fişlerde TSE imzası zorunludur 
# Registrierkasse Frontend

## 📱 Frontend Uygulaması
React Native/Expo tabanlı mobil kasa uygulaması. TypeScript ile geliştirilmiş, modern ve kullanıcı dostu bir arayüz sunar.

## 🛠️ Teknik Detaylar
- **Framework:** React Native/Expo
- **Dil:** TypeScript
- **State Yönetimi:** React Context + Custom Hooks
- **UI Kütüphanesi:** React Native Paper
- **Çevrimdışı Depolama:** PouchDB
- **Çoklu Dil:** i18next
- **Form Yönetimi:** React Hook Form
- **Validasyon:** Zod

## 📦 Kurulum

### Gereksinimler
- Node.js 18+
- npm veya yarn
- Expo CLI
- Android Studio (Android geliştirme için)
- Xcode (iOS geliştirme için, sadece macOS)

### Kurulum Adımları
1. Bağımlılıkları yükleyin:
```bash
npm install
```

2. Geliştirme sunucusunu başlatın:
```bash
npm start
```

3. Expo Go uygulamasını kullanarak test edin:
```bash
npm run android  # Android için
npm run ios     # iOS için (sadece macOS)
```

## 🏗️ Proje Yapısı
```
frontend/
├── app/              # Ana uygulama sayfaları
├── components/       # Yeniden kullanılabilir bileşenler
├── hooks/           # Özel React hooks'ları
├── services/        # API servisleri
├── contexts/        # React context'leri
├── i18n/            # Çoklu dil dosyaları
├── constants/       # Sabit değerler
└── assets/          # Resimler, fontlar vb.
```

## 🔧 Geliştirme

### Önemli Komutlar
```bash
npm start           # Geliştirme sunucusunu başlat
npm run android    # Android uygulamasını başlat
npm run ios        # iOS uygulamasını başlat
npm run test       # Testleri çalıştır
npm run lint       # Lint kontrolü
npm run build      # Production build
```

### TSE Entegrasyonu
- TSE cihazı bağlantı kontrolü
- Fiş imzalama işlemleri
- Günlük raporlama (Tagesabschluss)

### Yazıcı Entegrasyonu
- EPSON TM-T88VI desteği
- Star TSP 700 desteği
- OCRA-B font zorunluluğu

## 📚 Detaylı Dokümantasyon
Daha detaylı bilgi için [DEVELOPMENT.md](DEVELOPMENT.md) dosyasını inceleyin.

## ⚠️ Önemli Notlar
- TSE cihazı bağlantısı zorunludur
- Çevrimdışı modda PouchDB kullanılır
- Tüm fişlerde TSE imzası olmalıdır
- Steuernummer formatı: ATU12345678
- Kunden-ID 8 haneli olmalıdır

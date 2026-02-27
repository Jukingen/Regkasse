# Regkasse Projesi Geliştirici Onboarding Özeti

Bu doküman, **Regkasse** (Avusturya POS / Yazar Kasa) projesine yeni katılan geliştiriciler için sistemin genel yapısını ve teknik detaylarını özetler.

## 🚀 Proje Vizyonu ve Uyumluluk
Regkasse, Avusturya yasal mevzuatlarına (RKSV, DSGVO) tam uyumlu bir satış noktası (POS) çözümüdür.
- **RKSV**: Tüm fişlerin TSE (Teknik Güvenlik Cihazı) imzası taşıması ve FinanzOnline entegrasyonu zorunludur.
- **Compliance**: `DailyClosing` (Gün sonu), `TSE` imzalama ve veri güvenliği kritik önem taşır.

---

## 🏗️ Backend Mimarisi (.NET 8 Core)
Backend, yüksek güvenlikli ve mevzuata uygun bir yapı sunan ASP.NET Core üzerinde kuruludur.

- **Teknoloji**: .NET 8, EF Core, PostgreSQL.
- **Mimari**: Controller-based API + Service Layer (İş mantığı).
- **Kritik Servisler**:
    - `ReceiptService`: Satış ve fiş oluşturma işlemlerini yönetir.
    - `TseService`: RKSV uyumlu dijital imzalama işlemlerinden sorumludur.
    - `FinanzOnlineService`: Avusturya vergi dairesi ile iletişim kurar.
- **Para Birimi**: `Money` mantığında asla yuvarlama varsayımı yapılmaz, hassas hesaplama kritik seviyededir.

---

## 🎨 Frontend-Admin Mimarisi (Next.js)
Yönetim paneli, işletme sahiplerinin ürünlerini, müşterilerini ve raporlarını yönettiği modern bir web uygulamasıdır.

- **Teknoloji**: Next.js 14 (App Router), TypeScript, Ant Design (AntD).
- **Veri Yönetimi**: React Query (Server state), Zustand (Global client state).
- **API İletişimi**: Axios ve otomatik üretilen `orval` API hook'ları kullanılır.
- **Kural**: Frontend-admin içerisinde Expo/React Native desenleri **asla** kullanılmaz.

---

## 📝 Geliştirme Standartları
- **Onboarding Dosyaları**: Kök dizindeki `DEVELOPER_ONBOARDING.md` ve `PROJECT_STRUCTURE.md` dosyalarını mutlaka inceleyin.
- **Dil Politikası**: UI dilleri Almanca, Türkçe ve İngilizce'dir; ancak kod içi teknik terimler daima İngilizce'dir.
- **Dokümantasyon**: `ai/` klasörü altındaki kontratlar (`01_BACKEND_CONTRACT.md` vb.) AI yardımıyla geliştirme yaparken referans alınmalıdır.

---

## 🛠️ Yerel Kurulum (Hızlı Başlangıç)
1. **Backend**: `backend` dizininde `dotnet run` ile API'yi başlatın (Varsayılan: Port 5183).
2. **Frontend-Admin**: `frontend-admin` dizininde `npm run dev` ile admin panelini açın.
3. **Veritabanı**: PostgreSQL bağlantı dizesini `appsettings.json` üzerinden kontrol edin.

🚀 **Hoş geldiniz ve başarılar!**

# Repository File Map

Bu dosya, AI'nin projeyi karıştırmaması için hızlı bir navigasyon rehberidir.

## 📂 Backend (ASP.NET Core)
- **Root**: `backend/`
- **Controllers**: `backend/Controllers/`
- **Models/Entities**: `backend/Models/`
- **Data/Context**: `backend/Data/AppDbContext.cs`
- **Services**: `backend/Services/`

## 📱 Frontend Mobile (React Native + Expo)
- **Root**: `frontend/`
- **Navigation**: `frontend/app/` (Expo Router setup)
- **Tabs**: `frontend/app/(tabs)/`
- **Auth**: `frontend/app/(auth)/`
- **Components**: `frontend/components/`
- **Check**: `@react-navigation/*` ve `expo-*` paketleri sadece buradadır.

## 💻 Frontend Admin (React Web)
- **Root**: `frontend-admin/`
- **Entry**: `frontend-admin/src/main.tsx`
- **Routes**: `frontend-admin/src/routes.tsx` (React Router)
- **Pages**: `frontend-admin/src/pages/`
- **API**: `frontend-admin/src/api/`
- **Check**: Vite tabanlıdır. **Expo Router kullanılmaz.**

## ⚠️ CRITICAL WARNING: DO NOT MIX
- **React Native (frontend/)** dosyalarında `react-router-dom` kullanma; Expo Router kullan.
- **Admin Web (frontend-admin/)** dosyalarında `react-native` paketlerini import etme.
- Paylaşılan bir `common` klasörü yoksa, UI elementlerini kopyalarken platform spesifik API'leri (örn: `View` vs `div`) mutlaka dönüştür.

## 🤖 AI Interaction Template
- Planlama yaparken hangi klasörde (Mobile vs Admin) olduğunu her zaman teyit et.

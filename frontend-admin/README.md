# Registrierkasse Admin Panel

Bu proje, Registrierkasse uygulamasının yönetim panelidir.

## Gereksinimler

- Node.js 18 veya üzeri
- npm 9 veya üzeri

## Kurulum

1. Projeyi klonlayın:
   ```bash
   git clone https://github.com/yourusername/registrierkasse.git
   cd registrierkasse/frontend-admin
   ```

2. Bağımlılıkları yükleyin:
   ```bash
   npm install
   ```

3. `.env` dosyasını oluşturun:
   ```env
   VITE_API_URL=http://localhost:5000
   VITE_APP_TITLE=Registrierkasse Admin
   VITE_APP_VERSION=1.0.0
   VITE_APP_ENV=development
   ```

## Geliştirme

Geliştirme sunucusunu başlatmak için:

```bash
npm run dev
```

Uygulama http://localhost:3000 adresinde çalışacaktır.

## Derleme

Üretim için derlemek için:

```bash
npm run build
```

Derlenen dosyalar `dist` dizininde oluşturulacaktır.

## Lint

Kodu kontrol etmek için:

```bash
npm run lint
```

## Teknolojiler

- React 18
- TypeScript
- Material-UI
- React Router
- React Query
- i18next
- Vite
- ESLint

## Lisans

Bu proje MIT lisansı altında lisanslanmıştır.

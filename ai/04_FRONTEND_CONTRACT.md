# Frontend Contract (React Native + Expo Router)

## Navigation
- Expo Router yapısı:
  - app/(auth) : login vb
  - app/(tabs) : POS ana akış
  - _layout.tsx mevcut

## Screen Pattern
- cash-register.tsx: POS orchestrator ekran örneğidir.
- Ekran; header, table selector, product list, cart display, summary gibi modüler component'leri birleştirir.

## API Access
- API çağrıları services/api/* üzerinden yapılır:
  - services/api/productService
  - services/api/config
- Yeni endpoint entegrasyonu eklerken aynı service layer yaklaşımını takip et (ekranda doğrudan fetch/axios yazma).

## Component Structure
- components altı modüler yapı:
  - ui / soft / debug
- Yeni UI eklerken mevcut component stilini ve naming’ini koru.

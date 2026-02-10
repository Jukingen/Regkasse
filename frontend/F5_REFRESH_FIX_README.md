# F5 Refresh Auth API Fix

## Problem
F5 (refresh) tuşuna basıldığında `auth/me` API'si ard arda birden fazla kez çağrılıyordu. Bu durum:
- Gereksiz API trafiği
- Backend'de yoğunluk 
- Console'da spam log'lar
- Potansiyel rate limiting sorunları

## Çözüm
Aşağıdaki optimizasyonlar uygulandı:

### 1. AuthContext.tsx Optimizasyonları
- ✅ **Eski checkAuthStatus fonksiyonu kaldırıldı**
- ✅ **stableCheckAuthStatus sadeleştirildi**
- ✅ **Session storage flag eklendi** - F5 refresh'te tekrar auth check yapılmasını önler
- ✅ **Debouncing kontrolü** - 2 saniye içinde tekrar auth check yapmaz
- ✅ **Loading state optimizasyonu**

### 2. authService.ts Optimizasyonları  
- ✅ **validateToken debouncing** - 1 saniye içinde tekrar çağrılmaz
- ✅ **Static flag kontrolü** - Zaten çalışıyorsa tekrar çağrılmaz

### 3. API Config Optimizasyonları
- ✅ **Request debouncing** - 500ms içinde aynı endpoint tekrar çağrılmaz
- ✅ **Request tracking** - Hangi API'lerin ne zaman çağrıldığı izlenir
- ✅ **Debounced request handling** - İptal edilen request'ler graceful handle edilir

## Test Nasıl Yapılır

### 1. Developer Console'u Açın
```
F12 > Console sekmesi
```

### 2. Uygulamaya Login Olun
- Demo kullanıcı: `cashier@demo.com` / `Cashier123!`

### 3. F5 Refresh Test
1. Console'u temizleyin
2. F5 tuşuna basın
3. Console log'larını inceleyin

### Beklenen Sonuç (FIX SONRASI)
```
🔄 AUTH PROVIDER: Mount detected, starting auth check...
🔍 AUTH INIT: Checking storage for existing auth...
🔍 AUTH INIT: Storage check result: {hasToken: true, hasUser: true, tokenLength: 613, userLength: 142}
🔍 AUTH INIT: Checking token expiration...
🔍 TOKEN CHECK: {tokenExp: "2025-01-XX...", currentTime: "2025-01-XX...", bufferMinutes: 5, timeLeft: "25 minutes", isExpired: false, willExpireSoon: false}
✅ AUTH INIT: Restoring user state from storage: user@email.com
✅ AUTH INIT: User state successfully restored
TabLayout: Authenticated, showing cashier tabs for user: user@email.com role: cashier
```

### Önceki Problem (FIX ÖNCESI)
```
🔍 Auth status check başlatıldı...
🔄 Backend auth check yapılıyor...
🔐 Backend token validation başlatılıyor...
🚀 Making API request: GET /auth/me
🔍 Auth status check başlatıldı...  // ← TEKRAR!
🔄 Backend auth check yapılıyor...  // ← TEKRAR!
🔐 Backend token validation başlatılıyor...  // ← TEKRAR!
🚀 Making API request: GET /auth/me  // ← TEKRAR!
```

## Teknik Detaylar

### Session Storage Flag
```typescript
sessionStorage.setItem('hasInitialAuthCheck', 'true');
```
Bu flag F5 refresh'te korunur ve tekrar auth check yapılmasını önler.

### Debouncing Mekanizması
- **AuthContext**: 2000ms debounce
- **validateToken**: 1000ms debounce  
- **API requests**: 500ms debounce

### Priority Sırası
1. **Session flag kontrolü** (en hızlı çıkış)
2. **User state kontrolü** 
3. **Token geçerlilik kontrolü**
4. **Storage'dan user restore**
5. **Backend auth check** (en son çare)

## Sonuç
✅ F5 refresh'te `auth/me` API'si sadece 1 kez çağrılır
✅ Console spam'i elimine edildi
✅ Backend yükü azaldı
✅ User experience iyileşti

## Loglar
F5 refresh sonrasında console'da `[F5 FIX]` tag'li log'ları takip edin.
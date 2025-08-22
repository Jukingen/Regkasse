# 🚨 API Çağrım Sorunları - Acil Düzeltme Planı

## 🎯 **Tespit Edilen Kritik Sorunlar**

### **1. Çoklu HTTP Client Karmaşası**
```typescript
// ❌ SORUNLU: 4 farklı HTTP client kullanımı
import axios from 'axios';
import { fetch } from 'react-native';
import { apiClient } from './config';
import { useFetch } from './useFetch';

// ✅ ÇÖZÜM: Sadece apiClient kullanın
import { apiClient } from '../services/api/config';
```

### **2. Token Management Tutarsızlığı**
```typescript
// ❌ SORUNLU: Farklı Bearer prefix uygulamaları
headers: { 'Authorization': token }  // Bearer eksik
headers: { 'Authorization': `Bearer ${token}` }  // Bearer var

// ✅ ÇÖZÜM: TokenManager'dan tek standart
import { TokenManager, apiClient } from '../services/api/config';
// TokenManager otomatik Bearer prefix ekler
```

### **3. Infinite Loop ve Performance**
```typescript
// ❌ SORUNLU: useEffect dependency array'de state variables
useEffect(() => {
  checkAuthStatus();
}, [user, isAuthenticated, checkAuthStatus]); // Sürekli re-render

// ✅ ÇÖZÜM: Minimal dependency ve conditional logic
useEffect(() => {
  if (!justLoggedIn && isAuthenticated && user) {
    checkAuthStatus();
  }
}, [justLoggedIn]); // Sadece justLoggedIn
```

## 🔧 **Acil Düzeltme Adımları**

### **Adım 1: API Client Standardizasyonu**

```typescript
// 1. Tüm HTTP çağrılarını apiClient'a geçirin
// 2. useFetch hook'unu deprecate edin
// 3. Direct fetch() çağrılarını kaldırın

// Örnek: useCashRegister.ts'de
// ❌ ESKİ:
const initiateResponse = await fetch(`${API_BASE_URL}/Payment/initiate`, {
  method: 'POST',
  headers: {
    'Authorization': `Bearer ${user.token}`,
    'Content-Type': 'application/json'
  },
  body: JSON.stringify(data)
});

// ✅ YENİ:
const initiateResponse = await apiClient.post('/Payment/initiate', data);
```

### **Adım 2: Hook Optimizasyonları**

```typescript
// 1. useProductOperations → useProductOperationsOptimized
// 2. useCart dependency array'lerini optimize edin
// 3. useApiManager'ı entegre edin

// Örnek: ProductList.tsx'de
// ❌ ESKİ:
import { useProductOperations } from '../hooks/useProductOperations';

// ✅ YENİ:
import { useProductOperationsOptimized } from '../hooks/useProductOperationsOptimized';
import { useApiManager } from '../hooks/useApiManager';
```

### **Adım 3: Cache ve Error Handling**

```typescript
// 1. useApiManager cache sistemini kullanın
// 2. Consistent error handling
// 3. Network status kontrolü

const { apiCall, getCachedData } = useApiManager();

// Cache'li API çağrısı
const result = await apiCall(
  `products-list`,
  async () => await apiClient.get('/products'),
  {
    cacheKey: 'products',
    cacheTTL: 5, // 5 dakika
    skipDuplicate: true,
  }
);
```

## 📋 **Düzeltme Checklist**

### **Phase 1: Kritik Sorunlar (1-2 saat)**
- [ ] useCashRegister.ts → fetch'i apiClient'a geçir
- [ ] useCart.ts → dependency array'leri optimize et
- [ ] useProductOperations.ts → Optimized versiyonu kullan
- [ ] AuthContext.tsx → checkAuthStatus loop'unu düzelt

### **Phase 2: Service Layer (2-3 saat)**
- [ ] Tüm services/api/*.ts → apiClient standardı
- [ ] Error handling'i ErrorService ile standardize et
- [ ] Token management'i TokenManager ile unify et
- [ ] Network timeout'ları optimize et (10s → 30s)

### **Phase 3: Hook ve Component (3-4 saat)**
- [ ] useApiManager'ı tüm data-fetching hook'larda kullan
- [ ] Component seviyesinde useEffect optimize et
- [ ] Infinite loop detection'ı aktif et
- [ ] Cache stratejilerini implement et

### **Phase 4: Testing ve Monitoring (1-2 saat)**
- [ ] API call pattern'lerini test et
- [ ] Performance metrics ekle
- [ ] Error tracking implement et
- [ ] Debug logging standardize et

## 🚨 **Acil Müdahale Gereken Dosyalar**

### **1. hooks/useCashRegister.ts**
```typescript
// SORUN: fetch() kullanımı, token handling tutarsızlığı
// ÇÖZÜM: apiClient'a geçiş, error handling iyileştirmesi
```

### **2. hooks/useCart.ts**  
```typescript
// SORUN: Dependency array'de state variables, sürekli re-render
// ÇÖZÜM: Minimal dependency, conditional logic
```

### **3. contexts/AuthContext.tsx**
```typescript
// SORUN: checkAuthStatus infinite loop
// ÇÖZÜM: Dependency array optimization
```

### **4. services/api/config.ts**
```typescript
// SORUN: Token handling edge cases
// ÇÖZÜM: TokenManager iyileştirmesi
```

## 🔍 **Debug ve Monitoring**

### **Performance Tracking**
```typescript
// InfiniteLoopDetector'ı aktif edin
import { InfiniteLoopDetector } from '../components/InfiniteLoopDetector';

// Settings sayfasında gösterin (development modunda)
{__DEV__ && <InfiniteLoopDetector />}
```

### **API Call Monitoring**
```typescript
// useApiManager'dan status bilgisi alın
const { hasActiveCalls, getActiveCallStatus } = useApiManager();

// Console'da izleyin
console.log('Active API calls:', hasActiveCalls);
```

## 📊 **Beklenen İyileştirmeler**

| Metrik | Öncesi | Sonrası | İyileştirme |
|--------|--------|---------|-------------|
| API Call Sayısı | Sürekli | Cache'li | %70+ azalma |
| Re-render | Her state | Minimal | %60+ azalma |
| Memory Usage | Artıyor | Stabil | %40+ azalma |
| Infinite Loop | Var | Yok | %100 çözüm |
| Loading Speed | Yavaş | Hızlı | %50+ artış |

## 🛠️ **Hızlı Başlangıç**

### **1. Bu komutu çalıştırın:**
```bash
# Infinite loop detection'ı aktif edin
npm run dev
# Settings sayfasına gidin
# InfiniteLoopDetector'ı açın
```

### **2. Bu dosyaları öncelikle düzeltin:**
1. `hooks/useCashRegister.ts` → fetch'leri apiClient'a
2. `hooks/useCart.ts` → dependency array optimize
3. `contexts/AuthContext.tsx` → checkAuthStatus loop fix
4. `hooks/useProductOperations.ts` → Optimized version'a geç

### **3. Bu pattern'i takip edin:**
```typescript
// ✅ DOĞRU API ÇAĞRISI PATTERN:
const { apiCall } = useApiManager();

const result = await apiCall(
  'unique-operation-key',
  async () => await apiClient.post('/endpoint', data),
  {
    cacheKey: 'cache-key',
    cacheTTL: 5,
    skipDuplicate: true,
  }
);
```

## 🚀 **Sonuç**

Bu düzeltmeler yapıldıktan sonra:
- ✅ API çağrıları %70+ daha hızlı olacak
- ✅ Infinite loop sorunları tamamen çözülecek  
- ✅ Memory kullanımı optimize olacak
- ✅ User experience iyileşecek
- ✅ RKSV uyumluluğu korunacak

**Tahmini süre: 6-8 saat toplam implementasyon**
**Kritik sorunlar: 1-2 saatte çözülebilir**

## 📞 **Yardım ve Destek**

Bu README'deki adımları takip ederken sorun yaşarsanız:
1. InfiniteLoopDetector'ı kontrol edin
2. Console logları inceleyin  
3. Performance metrics'leri izleyin
4. Cache durumunu kontrol edin

**Not: Mevcut optimize edilmiş hook'lar zaten projede var, sadece kullanıma geçirilmesi gerek!**

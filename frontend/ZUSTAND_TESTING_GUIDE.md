# 🧪 Zustand Cart Store - Test ve Doğrulama

## ✅ Kurulum Doğrulaması

### 1. Paket Kontrolü

```bash
# package.json'da zustand olmalı
npm list zustand
```

Beklenen çıktı:
```
zustand@5.x.x
```

---

### 2. Store Test Kodu

Aşağıdaki kod parçasını herhangi bir component'te deneyin:

```typescript
import { useCartStore } from '../stores/useCartStore';

function TestComponent() {
  const { activeTableId, cartsByTable, setActiveTable } = useCartStore();
  
  console.log('Current Table:', activeTableId);
  console.log('Carts:', cartsByTable);
  
  return <Text>Test Component</Text>;
}
```

Eğer import hatası almazsanız ✅ kurulum başarılı.

---

### 3. AsyncStorage Kontrolü

```typescript
import AsyncStorage from '@react-native-async-storage/async-storage';

// Cart storage'ı kontrol et
const checkStorage = async () => {
  const cartData = await AsyncStorage.getItem('cart-storage');
  console.log('Stored cart data:', cartData);
};

checkStorage();
```

---

### 4. Backend Bağlantı Testi

```typescript
import { useCartStore } from '../stores/useCartStore';

function BackendTest() {
  const { addItem } = useCartStore();
  
  const testBackend = async () => {
    try {
      await addItem('test-product-id', 1);
      console.log('✅ Backend connection successful');
    } catch (error) {
      console.error('❌ Backend connection failed:', error);
    }
  };
  
  return <Button title="Test Backend" onPress={testBackend} />;
}
```

**Beklenen Yanıt:**
- Backend çalışıyorsa: `✅ Backend connection successful`
- Backend kapalıysa: `❌ Backend connection failed: Network Error`

---

## 🔧 Common Issues ve Çözümleri

### Issue 1: "Cannot find module 'zustand'"

**Çözüm:**
```bash
cd frontend
npm install zustand
```

---

### Issue 2: "Cannot find module '../stores/useCartStore'"

**Çözüm:**
Store dosyasının doğru yerde olduğundan emin olun:
```
frontend/
└── stores/
    └── useCartStore.ts
```

---

### Issue 3: Backend API hatası (Network Error)

**Çözüm:**
1. Backend'in çalıştığından emin olun:
   ```bash
   cd backend/KasseAPI_Final
   dotnet run
   ```

2. Backend URL'i kontrol edin:
   ```typescript
   // config.ts
   export const API_BASE_URL = 'http://localhost:5183/api';
   ```

3. CORS ayarlarını kontrol edin (backend'de).

---

### Issue 4: "401 Unauthorized"

**Çözüm:**
Token'ın geçerli olduğundan emin olun:

```typescript
import AsyncStorage from '@react-native-async-storage/async-storage';

const checkToken = async () => {
  const token = await AsyncStorage.getItem('token');
  console.log('Token:', token);
};
```

Eğer token yoksa veya geçersizse, önce login yapın.

---

### Issue 5: AsyncStorage persist çalışmıyor

**Çözüm:**
1. AsyncStorage paketinin kurulu olduğundan emin olun:
   ```bash
   npm list @react-native-async-storage/async-storage
   ```

2. Persist middleware'in doğru import edildiğinden emin olun:
   ```typescript
   import { persist, createJSONStorage } from 'zustand/middleware';
   import AsyncStorage from '@react-native-async-storage/async-storage';
   ```

---

## 🧪 Manuel Test Senaryoları

### Senaryo 1: Masa Değiştirme

1. App'i açın
2. Store'dan `setActiveTable(2)` çağırın
3. `activeTableId`'nin 2 olduğunu doğrulayın
4. App'i kapatıp tekrar açın
5. `activeTableId` hala 2 olmalı (persistence)

✅ Başarılı: Masa ID persist edildi

---

### Senaryo 2: Ürün Ekleme

1. Backend'in çalıştığından emin olun
2. `addItem('product-uuid', 1)` çağırın
3. Console'da backend response'u görmelisiniz:
   ```
   ✅ [CartStore] Backend response: {...}
   ```
4. `cartsByTable[activeTableId].items` listesinde ürün olmalı

✅ Başarılı: Ürün backend'e eklendi ve local state güncellendi

---

### Senaryo 3: Sepet Temizleme

1. Sepete ürün ekleyin
2. `clearCart()` çağırın
3. Backend'e POST request gitmeli
4. `cartsByTable[activeTableId]` undefined olmalı

✅ Başarılı: Sepet backend ve local state'den temizlendi

---

### Senaryo 4: Çoklu Masa Yönetimi

1. Masa 1'e ürün ekleyin
2. `setActiveTable(2)` ile Masa 2'ye geçin
3. Masa 2'ye farklı ürün ekleyin
4. `setActiveTable(1)` ile Masa 1'e geri dönün
5. Masa 1'in sepeti bozulmadan durmalı

✅ Başarılı: Her masa bağımsız sepet tutuyor

---

## 📊 Performance Test

### Memory Leak Kontrolü

```typescript
// 100 kere masa değiştirme testi
const testMassSwitching = async () => {
  const { setActiveTable } = useCartStore.getState();
  
  for (let i = 1; i <= 100; i++) {
    setActiveTable(i % 10 + 1);
    await new Promise(resolve => setTimeout(resolve, 10));
  }
  
  console.log('✅ Mass switching test completed');
};
```

Beklenen sonuç: No memory leaks, app hızlı çalışmalı.

---

### AsyncStorage Size Test

```typescript
import AsyncStorage from '@react-native-async-storage/async-storage';

const checkStorageSize = async () => {
  const cartData = await AsyncStorage.getItem('cart-storage');
  const sizeInBytes = new Blob([cartData || '']).size;
  const sizeInKB = (sizeInBytes / 1024).toFixed(2);
  
  console.log(`Cart storage size: ${sizeInKB} KB`);
  
  if (sizeInBytes > 1024 * 100) { // 100KB
    console.warn('⚠️ Cart storage is large, consider cleanup');
  }
};
```

---

## 🎯 Pre-Production Checklist

- [ ] Zustand kurulu (`npm list zustand`)
- [ ] Store dosyası mevcut (`stores/useCartStore.ts`)
- [ ] AsyncStorage persist çalışıyor
- [ ] Backend API bağlantısı başarılı
- [ ] Token yönetimi çalışıyor
- [ ] Masa değiştirme çalışıyor
- [ ] Ürün ekleme backend'e gidiyor
- [ ] Sepet gösterimi doğru
- [ ] Miktar artırma/azaltma çalışıyor
- [ ] Sepet temizleme çalışıyor
- [ ] Checkout sonrası sepet temizleniyor
- [ ] Çoklu masa yönetimi çalışıyor
- [ ] Console'da hata yok
- [ ] Memory leak yok
- [ ] AsyncStorage boyutu makul (\<100KB)

---

## 🚀 Production Ready kontrolü

### Stage 1: Development Test ✅
- [ ] Local'de tüm testler geçti
- [ ] Console logları temiz
- [ ] Backend entegrasyonu çalışıyor

### Stage 2: Integration Test
- [ ] Mevcut `cash-register.tsx` ile entegre edildi
- [ ] Mevcut component'ler uyumlu
- [ ] UI testleri geçti

### Stage 3: E2E Test
- [ ] Tam bir satış akışı test edildi (ürün ekle → checkout)
- [ ] Çoklu masa senaryosu test edildi
- [ ] Hata senaryoları test edildi (backend kapalı, token yok, vs.)

---

## 📝 Test Sonuçları Şablonu

Testlerinizi dokümante edin:

```markdown
## Test Sonuçları - [Tarih]

### Environment
- Platform: [iOS / Android / Web]
- Node: [version]
- Expo: [version]
- Backend: [çalışıyor mu?]

### Testler
- [x] Zustand kurulumu ✅
- [x] AsyncStorage persist ✅
- [x] Backend bağlantısı ✅
- [x] Masa değiştirme ✅
- [x] Ürün ekleme ✅
- [x] Sepet gösterimi ✅
- [ ] Checkout ⏳

### Hatalar
- Hata 1: [Açıklama] - Çözüldü ✅
- Hata 2: [Açıklama] - Devam ediyor ⏳

### Notlar
- [Test sırasında dikkat edilecek noktalar]
```

---

## 🎉 Başarılı Test Sonucu

Eğer tüm testler geçtiyse:

```
✅ Zustand Cart Store kurulumu başarıyla tamamlandı!
✅ Backend entegrasyonu çalışıyor
✅ Persistence aktif
✅ Production'a hazır

Sonraki adım: cash-register.tsx'e entegre et
```

---

## 🆘 Yardım

Sorun yaşarsanız:

1. **Console loglarını inceleyin**
   - Store aksiyonları `[CartStore]` prefix'i ile loglanır
   - Backend çağrıları `🚀 Request:` ve `✅ API response:` ile loglanır

2. **AsyncStorage'ı temizleyin**
   ```typescript
   await AsyncStorage.removeItem('cart-storage');
   ```

3. **Store'u reset edin**
   ```typescript
   const { setActiveTable, clearCart } = useCartStore.getState();
   setActiveTable(1);
   await clearCart(1);
   ```

4. **Dokümantasyonu okuyun**
   - `ZUSTAND_CART_USAGE.md`
   - `ZUSTAND_SETUP_SUMMARY.md`
   - `ZUSTAND_INTEGRATION_EXAMPLE.tsx`

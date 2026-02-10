# 🐛 BUG FIX: Cart UI Update + Session Expired Sorunları

## 🔍 Sorun Analizi

### BUG 1: UI Güncellenmiyor ❌

**Problem:**
- `cash-register.tsx` **Zustand store kullanmıyor**!
- CartDisplay component'e eski cart data (muhtemelen local state) geçiliyor
- `useCartStore` import bile yok

**Kanıt:**
```bash
grep_search "useCartStore" in cash-register.tsx
Result: No results found ❌
```

**Sonuç:**
- Backend'e item ekleniyor ✅
- Zustand store güncelleniyor ✅
- CartDisplay Zustand'dan okum

uyor ❌ → **UI güncellenmiyor!**

---

### BUG 2: Session Expired (Masa Değiştirme) ❌

**Problem:**
- Table switch sırasında `fetchCartForTable` API call yapıyor
- Backend 401 Unauthorized dönüyor
- Auth interceptor otomatik logout yapıyor

**Muhtemel Sebep:**
```typescript
setActiveTable: (tableNumber) => {
  set({ activeTableId: tableNumber });
  
  // ❌ PROBLEM: Her table switch'te API call!
  const cart = get().cartsByTable[tableNumber];
  if (!cart) {
    get().fetchCartForTable(tableNumber); // 401 Error!
  }
}
```

**Neden 401?**
1. Token expired (ama diğer API'lar çalışıyor)
2. Cookie-based auth (RN ortamında cookie persist olmuyor)
3. Backend table endpoint extra auth check yapıyor
4. CORS issue

---

## ✅ ÇÖZÜM 1: cash-register.tsx → Zustand Integration

### Adım 1: Zustand Import ve Subscribe

```typescript
// app/(tabs)/cash-register.tsx

import { useCartStore } from '../../stores/useCartStore';

export default function CashRegisterScreen() {
  // ✅ Zustand store'dan subscribe
  const {
    activeTableId,
    cartsByTable,
    loading: cartLoading,
    error: cartError,
    setActiveTable,
    addItem,
    clearCart
  } = useCartStore();

  // ✅ Aktif table'ın cart'ını al
  const currentCart = cartsByTable[activeTableId];

  // ... diğer state'ler
  const [selectedTable, setSelectedTable] = useState(1);

  // ❌ ESKİ: Local cart state (Sil!)
  // const [cart, setCart] = useState({ items: [] });
```

---

### Adım 2: Table Selection Handler

```typescript
const handleTableSelect = async (tableNumber: number) => {
  console.log(`🏷️ Table selected: ${tableNumber}`);
  
  // Update local UI state
  setSelectedTable(tableNumber);
  
  // ✅ Zustand store'u güncelle
  setActiveTable(tableNumber);
  
  // ❌ REMOVE: Eski API fetch
  // await loadCartForTable(tableNumber);
};
```

---

### Adım 3: Product Selection Handler

```typescript
const handleProductSelect = async (product: any) => {
  try {
    console.log(`➕ Adding product ${product.name} to table ${activeTableId}`);
    
    // ✅ Zustand store action çağır
    await addItem(product.id, 1);
    
    // Success toast
    addToast({
      type: 'success',
      message: `${product.name} added to Table ${activeTableId}`,
      duration: 2000
    });
  } catch (error: any) {
    console.error('Failed to add product:', error);
    addToast({
      type: 'error',
      message: error.message || 'Failed to add item',
      duration: 3000
    });
  }
};
```

---

### Adım 4: CartDisplay Props Update

```typescript
<CartDisplay
  cart={currentCart}  // ✅ Zustand'dan gelen cart
  selectedTable={activeTableId}  // ✅ Zustand activeTableId
  loading={cartLoading}  // ✅ Zustand loading
  error={cartError}  // ✅ Zustand error
  onQuantityUpdate={handleQuantityUpdate}
  onItemRemove={handleItemRemove}
  onClearCart={() => clearCart(activeTableId)}
/>
```

---

### Adım 5: Quantity/Remove Handlers

```typescript
const handleQuantityUpdate = async (itemId: string, newQuantity: number) => {
  // itemId yerine productId kullanmalıyız
  // CartDisplay item.id yerine item.productId geçmeli
  
  if (newQuantity <= 0) {
    await handleItemRemove(itemId);
    return;
  }
  
  // Zustand increment/decrement kullan
  // (Bu kısım CartDisplay'in item.productId yerine item.id geçmesi problemi var)
};

const handleItemRemove = async (productId: string) => {
  try {
    const { remove } = useCartStore.getState();
    await remove(productId);
  } catch (error: any) {
    console.error('Remove failed:', error);
  }
};
```

---

## ✅ ÇÖZÜM 2: Session Expired Fix

### Seçenek A: fetchCartForTable'ı Kaldır (Önerilen)

**Problem:** Her table switch'te API call → 401 error

**Çözüm:** Persist + On-demand fetch

```typescript
// stores/useCartStore.ts

setActiveTable: (tableNumber: number) => {
  console.log(`🏷️ Switching to table ${tableNumber}`);
  set({ activeTableId: tableNumber });
  
  // ❌ REMOVE: Auto-fetch
  // const cart = get().cartsByTable[tableNumber];
  // if (!cart) {
  //   get().fetchCartForTable(tableNumber);
  // }
  
  // ✅ FIX: Sadece manuel refresh gerekirse fetch et
  // UI'da "Refresh" button olabilir
},
```

**Avantaj:**
- Token expired hatası yok
- Offline-friendly
- AsyncStorage'dan cart yüklenir

**Dezavantaj:**
- Başka cihazdan eklenen item'lar görünmez (refresh yapana kadar)

---

### Seçenek B: Auth Token Check + Retry

```typescript
fetchCartForTable: async (tableNumber: number) => {
  try {
    // ✅ Token check
    const token = await AsyncStorage.getItem('token');
    if (!token) {
      console.warn('No token, skipping cart fetch');
      return;
    }
    
    const response = await apiClient.get(
      `/api/cart/current?tableNumber=${tableNumber}`
    );
    
    const localCart = mapBackendCartToLocal(response);
    
    set({
      cartsByTable: {
        ...get().cartsByTable,
        [tableNumber]: localCart
      }
    });
  } catch (error: any) {
    // ✅ 401 hatası → Silent fail (logout yapma!)
    if (error.response?.status === 401) {
      console.warn('Cart fetch 401, using cached cart');
      // AsyncStorage'daki cart devam eder
    } else {
      console.error('Cart fetch failed:', error);
    }
  }
}
```

---

### Seçenek C: Cookie-Based Auth → Token Migration

Eğer backend cookie-based auth kullanıyorsa:

**Problem:** RN'de cookie persist olmuyor

**Çözüm:**

1. **Backend'de token-based auth aktif et:**
```csharp
// Backend: JWT token support ekle
[Authorize(AuthenticationSchemes = "Bearer")]
```

2. **Frontend'de token persist et:**
```typescript
// Login sonrası:
const token = response.data.token;
await AsyncStorage.setItem('token', token);

// Axios interceptor:
axiosInstance.interceptors.request.use(config => {
  const token = await AsyncStorage.getItem('token');
  if (token) {
    config.headers.Authorization = `Bearer ${token}`;
  }
  return config;
});
```

---

## 🧪 Test Senaryoları

### Test 1: UI Update (BUG 1)

```
1. Table 1 seç
2. Ürün ekle ("Bier 0.5L")
3. Console logs:
   ➕ [CartStore] Adding item to table 1
   ✅ [CartStore] Backend response received
   📦 [CartStore] Mapped items: [...]
   ✅ [CartStore] Backend state replaced
4. UI'da "Cart Items - Table 1" altında ANINDA görün ✅
```

---

### Test 2: Session Expired (BUG 2)

**Seçenek A Uygulandıysa:**
```
1. Table 1 → Table 2 → Table 3
2. Console: "Switching to table X" (3 kere)
3. No API calls, no 401, no logout ✅
```

**Seçenek B Uygulandıysa:**
```
1. Table switch
2. API çağrısı 401 dönerse:
   Console: "Cart fetch 401, using cached cart"
3. Logout yapılmıyor ✅
```

---

## 📋 Quick Fix Checklist

### BUG 1 Fix:
- [ ] `cash-register.tsx`'de `useCartStore` import et
- [ ] `cartsByTable[activeTableId]` ile currentCart al
- [ ] `handleProductSelect` içinde `addItem(productId, 1)` çağır
- [ ] CartDisplay'e Zustand state geç (`currentCart`, `activeTableId`, `cartLoading`)
- [ ] Eski local cart state'i sil
- [ ] Test: Ürün ekle, UI'da hemen görün

### BUG 2 Fix:
- [ ] `setActiveTable` içinden `fetchCartForTable` çağrısını kaldır (Seçenek A)
- [ ] VEYA `fetchCartForTable` içinde 401 silent fail yap (Seçenek B)
- [ ] Test: Masa değiştir, logout olma

---

## 🚀 Implementation

### 1. cash-register.tsx Minimal Changes

```typescript
// Sadece bu değişiklikleri yap:

// 1️⃣ Import
import { useCartStore } from '../../stores/useCartStore';

// 2️⃣ Subscribe
const {
  activeTableId,
  cartsByTable,
  setActiveTable,
  addItem,
} = useCartStore();

const currentCart = cartsByTable[activeTableId];

// 3️⃣ Table select
const handleTableSelect = (tableNumber: number) => {
  setSelectedTable(tableNumber);
  setActiveTable(tableNumber);  // ✅ Zustand
};

// 4️⃣ Product select
const handleProductSelect = async (product: any) => {
  await addItem(product.id, 1);  // ✅ Zustand
};

// 5️⃣ Cart display
<CartDisplay
  cart={currentCart}  // ✅ Zustand
  selectedTable={activeTableId}  // ✅ Zustand
  // ...
/>
```

---

### 2. useCartStore.ts Fix

```typescript
// stores/useCartStore.ts

setActiveTable: (tableNumber: number) => {
  console.log(`🏷️ Switching to table ${tableNumber}`);
  set({ activeTableId: tableNumber });
  
  // ❌ KALDIRIN: Auto-fetch
  // const cart = get().cartsByTable[tableNumber];
  // if (!cart) {
  //   get().fetchCartForTable(tableNumber);
  // }
},
```

---

## ✅ Final Result

**BUG 1 Çözüldü:** ✅
- Ürün eklenir eklenmez UI'da görünür
- Backend response store'a yazılır
- CartDisplay Zustand'dan okur
- Instant update!

**BUG 2 Çözüldü:** ✅
- Table switch API call yapmaz
- 401 hatası olmaz
- Logout redirect olmaz
- AsyncStorage'daki cart kullanılır

---

## 📞 Eğer Problem Devam Ederse

**BUG 1 hala varsa:**
```typescript
// cash-register.tsx'de debug:
useEffect(() => {
  console.log('=== CART DEBUG ===');
  console.log('Active Table:', activeTableId);
  console.log('Current Cart:', currentCart);
  console.log('Items:', currentCart?.items);
}, [activeTableId, currentCart]);
```

**BUG 2 hala varsa:**
```typescript
// Axios interceptor'da:
axiosInstance.interceptors.response.use(
  response => response,
  error => {
    if (error.response?.status === 401) {
      console.log('401 URL:', error.config?.url);
      // Sadece login endpoint'lerde logout yap
      if (!error.config?.url?.includes('/cart/')) {
        logout();
      }
    }
    throw error;
  }
);
```

Console log output'u gönderin, daha spesifik debug yaparım! 🚀

# 🎯 Backend Entegrasyon Stratejileri - Cart State Yönetimi

> **Backend Endpoint:**
> ```
> POST http://localhost:5183/api/cart/add-item
> Payload: { productId, quantity, tableNumber }
> ```

---

## 📋 İki Senaryo Karşılaştırması

| Özellik | Senaryo 1: Success Only | Senaryo 2: Full Cart Response |
|---------|------------------------|-------------------------------|
| **Backend Response** | `{ success: true }` | `{ cart: { items: [...], totals: {...} } }` |
| **State Update** | Optimistic/Local | Replace from backend |
| **Source of Truth** | Frontend | Backend |
| **Complexity** | Yüksek (rollback gerekli) | Düşük (replace yap) |
| **Network Overhead** | Düşük | Yüksek |
| **Consistency** | Risk var | Garantili |
| **UI Speed** | Çok hızlı | Orta hızlı |

---

## 🔄 Senaryo 1: Backend Sadece Success Döndürüyor

### Response Format
```json
{
  "success": true,
  "message": "Item added to cart successfully"
}
```

---

### ✅ Strateji A: Optimistic Update (Recommended)

**Akış:**
```
1. UI'dan addItem çağrılır
2. Immediately local state'i güncelle (optimistic)
3. Backend'e POST gönder (async)
4. Success → No action (zaten güncelledik)
5. Error → Rollback (önceki state'e dön)
```

**Implementation:**

```typescript
addItem: async (productId: string, quantity: number = 1) => {
  const { activeTableId, cartsByTable } = get();
  const currentCart = cartsByTable[activeTableId] || { items: [] };
  
  // 🎯 OPTIMISTIC UPDATE: İlk önce local state'i güncelle
  const existingItemIndex = currentCart.items.findIndex(
    item => item.productId === productId
  );
  
  let previousCart = { ...currentCart }; // Rollback için sakla
  let optimisticCart;
  
  if (existingItemIndex !== -1) {
    // Mevcut ürünü artır
    optimisticCart = {
      ...currentCart,
      items: currentCart.items.map((item, index) =>
        index === existingItemIndex
          ? { ...item, qty: item.qty + quantity }
          : item
      ),
      updatedAt: Date.now()
    };
  } else {
    // Yeni ürün ekle (ürün detaylarını cache'den veya context'ten al)
    const product = get().productCache?.[productId]; // Product cache varsayımı
    
    optimisticCart = {
      ...currentCart,
      items: [
        ...currentCart.items,
        {
          productId,
          name: product?.name || 'Loading...',
          price: product?.price || 0,
          qty: quantity,
          unitPrice: product?.price || 0,
          totalPrice: (product?.price || 0) * quantity
        }
      ],
      updatedAt: Date.now()
    };
  }
  
  // Immediately UI'da göster
  set({
    cartsByTable: {
      ...cartsByTable,
      [activeTableId]: optimisticCart
    },
    loading: false // UI responsive kalsın
  });
  
  // 🌐 Backend'e gönder (background)
  try {
    const response = await apiClient.post('/api/cart/add-item', {
      productId,
      quantity,
      tableNumber: activeTableId
    });
    
    if (response.success) {
      // ✅ Backend başarılı - optimistic update doğrulandı
      console.log('✅ [Optimistic] Backend confirmed add item');
      // State zaten güncel, hiçbir şey yapma
    } else {
      throw new Error(response.message || 'Failed to add item');
    }
  } catch (error: any) {
    // ❌ Backend hatası - ROLLBACK
    console.error('❌ [Optimistic] Backend failed, rolling back...', error);
    
    set({
      cartsByTable: {
        ...cartsByTable,
        [activeTableId]: previousCart // Önceki state'e dön
      },
      error: error?.message || 'Failed to add item'
    });
    
    throw error; // UI'a hata bildirimi
  }
}
```

**Pros:**
- ✅ **Çok hızlı UI**: Kullanıcı anında response görür
- ✅ **Düşük network overhead**: Sadece add request
- ✅ **Offline-first yaklaşıma uygun**: Network yoksa bile UI responsive

**Cons:**
- ❌ **Rollback complexity**: Hata durumunda state'i geri almak gerekir
- ❌ **Inconsistency riski**: Backend ve frontend farklı state'lerde olabilir
- ❌ **Concurrent request sorunları**: Aynı anda 2 istek olursa race condition

---

### ⚖️ Strateji B: Pessimistic Update (Safer)

**Akış:**
```
1. UI'dan addItem çağrılır
2. loading = true (spinner göster)
3. Backend'e POST gönder
4. Success → Local state'i manuel güncelle
5. Error → Hata göster
6. loading = false
```

**Implementation:**

```typescript
addItem: async (productId: string, quantity: number = 1) => {
  const { activeTableId, cartsByTable } = get();
  
  set({ loading: true, error: null });
  
  try {
    // 🌐 Backend'e gönder (önce)
    const response = await apiClient.post('/api/cart/add-item', {
      productId,
      quantity,
      tableNumber: activeTableId
    });
    
    if (!response.success) {
      throw new Error(response.message || 'Failed to add item');
    }
    
    // ✅ Backend başarılı - şimdi local state'i güncelle
    const currentCart = cartsByTable[activeTableId] || { items: [] };
    const existingItemIndex = currentCart.items.findIndex(
      item => item.productId === productId
    );
    
    let updatedCart;
    
    if (existingItemIndex !== -1) {
      updatedCart = {
        ...currentCart,
        items: currentCart.items.map((item, index) =>
          index === existingItemIndex
            ? { ...item, qty: item.qty + quantity }
            : item
        ),
        updatedAt: Date.now()
      };
    } else {
      const product = get().productCache?.[productId];
      updatedCart = {
        ...currentCart,
        items: [
          ...currentCart.items,
          {
            productId,
            name: product?.name || 'Unknown',
            price: product?.price || 0,
            qty: quantity,
            unitPrice: product?.price || 0,
            totalPrice: (product?.price || 0) * quantity
          }
        ],
        updatedAt: Date.now()
      };
    }
    
    set({
      cartsByTable: {
        ...cartsByTable,
        [activeTableId]: updatedCart
      },
      loading: false
    });
    
    console.log('✅ [Pessimistic] Item added successfully');
    
  } catch (error: any) {
    console.error('❌ [Pessimistic] Add item failed:', error);
    set({ 
      error: error?.message || 'Failed to add item',
      loading: false 
    });
    throw error;
  }
}
```

**Pros:**
- ✅ **Consistency garantisi**: Backend başarılı olmadan state değişmez
- ✅ **Basit mantık**: Rollback gerekmez
- ✅ **Daha az bug riski**: State her zaman backend ile sync

**Cons:**
- ❌ **Yavaş UI**: Network latency kadar bekleme
- ❌ **Loading state görünür**: Kullanıcı spinner görür
- ❌ **Kötü UX**: Özellikle yavaş networklerde

---

### 🛡️ Rollback Stratejisi (Optimistic için)

**Rollback Mekanizması:**

```typescript
interface OptimisticOperation {
  id: string;
  type: 'add' | 'update' | 'remove';
  tableNumber: number;
  previousState: Cart;
  timestamp: number;
}

// Store'a ekle
optimisticQueue: OptimisticOperation[] = [];

addItem: async (productId: string, quantity: number = 1) => {
  const operationId = `add-${Date.now()}-${Math.random()}`;
  const { activeTableId, cartsByTable, optimisticQueue } = get();
  const previousCart = cartsByTable[activeTableId] || { items: [] };
  
  // Operation'ı queue'ya ekle
  const operation: OptimisticOperation = {
    id: operationId,
    type: 'add',
    tableNumber: activeTableId,
    previousState: JSON.parse(JSON.stringify(previousCart)), // Deep copy
    timestamp: Date.now()
  };
  
  set({ 
    optimisticQueue: [...optimisticQueue, operation] 
  });
  
  // ... optimistic update ...
  
  try {
    await apiClient.post('/api/cart/add-item', { /* ... */ });
    
    // Başarılı - operation'ı queue'dan çıkar
    set({
      optimisticQueue: optimisticQueue.filter(op => op.id !== operationId)
    });
    
  } catch (error) {
    // ROLLBACK: Operation'ı bul ve state'i geri al
    const failedOp = optimisticQueue.find(op => op.id === operationId);
    
    if (failedOp) {
      set({
        cartsByTable: {
          ...cartsByTable,
          [failedOp.tableNumber]: failedOp.previousState
        },
        optimisticQueue: optimisticQueue.filter(op => op.id !== operationId),
        error: 'Failed to add item'
      });
    }
    
    throw error;
  }
}
```

**Network Hatasında Davranış:**

```typescript
// Timeout handling
const addItemWithTimeout = async (productId: string, quantity: number) => {
  const timeoutMs = 5000; // 5 saniye
  
  const timeoutPromise = new Promise((_, reject) => {
    setTimeout(() => reject(new Error('Request timeout')), timeoutMs);
  });
  
  const requestPromise = apiClient.post('/api/cart/add-item', {
    productId,
    quantity,
    tableNumber: get().activeTableId
  });
  
  try {
    await Promise.race([requestPromise, timeoutPromise]);
  } catch (error) {
    // Timeout veya network hatası
    console.error('Network error, rolling back optimistic update');
    // Rollback logic...
    throw error;
  }
};
```

---

## 🎯 Senaryo 2: Backend Tüm Cart'ı Döndürüyor (RECOMMENDED)

### Response Format
```json
{
  "success": true,
  "message": "Item added to cart successfully",
  "cart": {
    "cartId": "uuid",
    "tableNumber": 1,
    "items": [
      {
        "id": "item-uuid",
        "productId": "product-uuid",
        "productName": "Pizza Margherita",
        "quantity": 2,
        "unitPrice": 8.50,
        "totalPrice": 17.00,
        "notes": null
      }
    ],
    "totalItems": 2,
    "subtotal": 17.00,
    "totalTax": 3.40,
    "grandTotal": 20.40
  }
}
```

---

### ✅ Strateji: Replace Local State with Backend Response

**Akış:**
```
1. UI'dan addItem çağrılır
2. loading = true (isteğe bağlı - UX için kısa spinner)
3. Backend'e POST gönder
4. Response → Local state'i TAMAMEN replace et
5. loading = false
```

**Implementation:**

```typescript
addItem: async (productId: string, quantity: number = 1) => {
  const { activeTableId, cartsByTable } = get();
  
  set({ loading: true, error: null });
  
  try {
    // 🌐 Backend'e gönder
    const response = await apiClient.post<{
      success: boolean;
      message: string;
      cart: BackendCart;
    }>('/api/cart/add-item', {
      productId,
      quantity,
      tableNumber: activeTableId
    });
    
    if (!response.success || !response.cart) {
      throw new Error(response.message || 'Failed to add item');
    }
    
    // ✅ Backend'den gelen cart'ı SOURCE OF TRUTH kabul et
    const backendCart = response.cart;
    
    // Backend formatını local format'a çevir
    const localCart: Cart = {
      cartId: backendCart.cartId,
      items: backendCart.items.map(item => ({
        productId: item.productId,
        name: item.productName,
        price: item.unitPrice,
        qty: item.quantity,
        unitPrice: item.unitPrice,
        totalPrice: item.totalPrice,
        notes: item.notes || undefined
      })),
      updatedAt: Date.now()
    };
    
    // 🔄 REPLACE: Bu masa için cart'ı tamamen değiştir
    set({
      cartsByTable: {
        ...cartsByTable,
        [backendCart.tableNumber]: localCart
      },
      loading: false
    });
    
    console.log(`✅ [Replace] Cart updated for table ${backendCart.tableNumber}:`, {
      itemCount: backendCart.items.length,
      totalItems: backendCart.totalItems
    });
    
  } catch (error: any) {
    console.error('❌ [Replace] Add item failed:', error);
    set({ 
      error: error?.message || 'Failed to add item',
      loading: false 
    });
    throw error;
  }
}
```

---

### 🎨 Hybrid Approach: Optimistic + Replace (BEST OF BOTH)

**En iyi kullanıcı deneyimi için:**

```typescript
addItem: async (productId: string, quantity: number = 1) => {
  const { activeTableId, cartsByTable } = get();
  const currentCart = cartsByTable[activeTableId] || { items: [] };
  
  // 🚀 PHASE 1: Optimistic Update (Instant UI)
  const existingItemIndex = currentCart.items.findIndex(
    item => item.productId === productId
  );
  
  let optimisticCart: Cart;
  
  if (existingItemIndex !== -1) {
    optimisticCart = {
      ...currentCart,
      items: currentCart.items.map((item, index) =>
        index === existingItemIndex
          ? { ...item, qty: item.qty + quantity }
          : item
      ),
      updatedAt: Date.now()
    };
  } else {
    const product = get().productCache?.[productId];
    optimisticCart = {
      ...currentCart,
      items: [
        ...currentCart.items,
        {
          productId,
          name: product?.name || 'Loading...',
          price: product?.price || 0,
          qty: quantity
        }
      ],
      updatedAt: Date.now()
    };
  }
  
  // Immediately update UI
  set({
    cartsByTable: {
      ...cartsByTable,
      [activeTableId]: optimisticCart
    },
    loading: false // UI responsive
  });
  
  // 🌐 PHASE 2: Backend Call (Background)
  try {
    const response = await apiClient.post<{ cart: BackendCart }>('/api/cart/add-item', {
      productId,
      quantity,
      tableNumber: activeTableId
    });
    
    // 🔄 PHASE 3: Replace with Backend Truth
    const localCart: Cart = {
      cartId: response.cart.cartId,
      items: response.cart.items.map(item => ({
        productId: item.productId,
        name: item.productName,
        price: item.unitPrice,
        qty: item.quantity,
        unitPrice: item.unitPrice,
        totalPrice: item.totalPrice
      })),
      updatedAt: Date.now()
    };
    
    // Backend response ile replace (optimistic state'in üzerine yaz)
    set({
      cartsByTable: {
        ...cartsByTable,
        [activeTableId]: localCart
      }
    });
    
    console.log('✅ [Hybrid] Optimistic update confirmed and replaced with backend state');
    
  } catch (error: any) {
    // Rollback to previous state
    set({
      cartsByTable: {
        ...cartsByTable,
        [activeTableId]: currentCart // Önceki state (backend call öncesi)
      },
      error: error?.message || 'Failed to add item'
    });
    
    throw error;
  }
}
```

**Bu yaklaşımın avantajları:**
- ✅ Instant UI response (optimistic)
- ✅ Backend consistency (replace)
- ✅ Rollback on error
- ✅ En iyi UX

---

## 📊 Karşılaştırma Tablosu

### Senaryo 1 (Success Only)

| Strateji | UI Speed | Consistency | Complexity | UX | Recommendation |
|----------|----------|-------------|------------|----|----------------|
| Optimistic | ⭐⭐⭐⭐⭐ | ⭐⭐ | ⭐⭐⭐⭐⭐ | ⭐⭐⭐⭐⭐ | 🎯 Hızlı UI için |
| Pessimistic | ⭐⭐ | ⭐⭐⭐⭐⭐ | ⭐⭐ | ⭐⭐⭐ | ✅ Güvenlik için |

### Senaryo 2 (Full Cart Response)

| Strateji | UI Speed | Consistency | Complexity | UX | Recommendation |
|----------|----------|-------------|------------|----|----------------|
| Replace Only | ⭐⭐⭐ | ⭐⭐⭐⭐⭐ | ⭐⭐ | ⭐⭐⭐⭐ | ✅ Basit ve güvenilir |
| Hybrid (Opt + Replace) | ⭐⭐⭐⭐⭐ | ⭐⭐⭐⭐⭐ | ⭐⭐⭐ | ⭐⭐⭐⭐⭐ | 🎯 **EN İYİ** |

---

## 🎯 Gereksinimler ve Çözümler

### 1. Aynı Üründen Tekrar Eklenince Qty Artmalı

**Senaryo 1 (Success Only):**
```typescript
const existingItem = currentCart.items.find(item => item.productId === productId);

if (existingItem) {
  // Qty artır
  updatedCart.items = currentCart.items.map(item =>
    item.productId === productId
      ? { ...item, qty: item.qty + quantity }
      : item
  );
} else {
  // Yeni item ekle
  updatedCart.items = [...currentCart.items, newItem];
}
```

**Senaryo 2 (Full Cart Response):**
Backend zaten bunu halleder! Sadece response'u replace et.

```typescript
// Backend'den gelen items zaten merged
set({
  cartsByTable: {
    ...cartsByTable,
    [tableNumber]: convertBackendCart(response.cart)
  }
});
```

✅ Senaryo 2 daha basit!

---

### 2. TableNumber'a Göre Cart Isolation

**Her İki Senaryoda da:**

```typescript
// ✅ DOĞRU: TableNumber ile isolation
set({
  cartsByTable: {
    ...cartsByTable,
    [tableNumber]: updatedCart  // Sadece bu masa değişir
  }
});

// ❌ YANLIŞ: Tüm masaları etkilemek
set({
  cartsByTable: { [tableNumber]: updatedCart }  // Diğer masalar kaybolur!
});
```

**Isolation Garantisi:**

```typescript
const addItem = async (productId: string, quantity: number) => {
  const { activeTableId, cartsByTable } = get();
  
  // 🔒 Sadece aktif masanın cart'ını al
  const targetCart = cartsByTable[activeTableId] || { items: [] };
  
  // ... update logic ...
  
  // 🔒 Sadece aktif masayı güncelle (diğerleri olduğu gibi)
  set({
    cartsByTable: {
      ...cartsByTable,  // Spread ile diğer masalar korunur
      [activeTableId]: updatedCart
    }
  });
};
```

✅ Her iki senaryo da isolation sağlar, ama dikkatli implementation gerekir.

---

### 3. UI Hızlı Olmalı (Grid'de Çok Item)

**Performance Optimizasyonları:**

#### a) Memoization
```typescript
import { useMemo } from 'react';

const currentCart = useMemo(
  () => cartsByTable[activeTableId],
  [cartsByTable, activeTableId]
);
```

#### b) Debounced Updates (Çok hızlı tıklamalarda)
```typescript
import { debounce } from 'lodash';

const debouncedAddItem = useMemo(
  () => debounce(addItem, 300, { leading: true, trailing: false }),
  [addItem]
);
```

#### c) Virtualized List
```typescript
import { FlashList } from '@shopify/flash-list'; // veya FlatList

<FlashList
  data={products}
  renderItem={({ item }) => <ProductCard product={item} />}
  estimatedItemSize={120}
  // Optimize render
  removeClippedSubviews={true}
  maxToRenderPerBatch={10}
  windowSize={5}
/>
```

#### d) Loading State Strategy

**Optimistic Approach (Senaryo 1):**
```typescript
// ✅ Loading false hemen (UI responsive)
set({ loading: false });

// Backend call background'da
apiClient.post(...).then(...).catch(...);
```

**Hybrid Approach (Senaryo 2):**
```typescript
// ✅ Optimistic update + loading false
set({
  cartsByTable: { ...optimisticCart },
  loading: false  // UI freeze olmasın
});

// Backend call yine background'da
apiClient.post(...).then(response => {
  // Replace cart silently
  set({ cartsByTable: { ...backendCart } });
});
```

---

## 🏆 Final Recommendation

### 🥇 En İyi Seçim: **Senaryo 2 Hybrid Approach**

```typescript
// ✅ RECOMMENDED IMPLEMENTATION
addItem: async (productId: string, quantity: number = 1) => {
  const { activeTableId, cartsByTable } = get();
  const previousCart = cartsByTable[activeTableId] || { items: [] };
  
  // 1️⃣ Optimistic Update (Instant UI)
  const optimisticCart = calculateOptimisticCart(previousCart, productId, quantity);
  set({
    cartsByTable: {
      ...cartsByTable,
      [activeTableId]: optimisticCart
    }
  });
  
  // 2️⃣ Backend Call
  try {
    const response = await apiClient.post('/api/cart/add-item', {
      productId,
      quantity,
      tableNumber: activeTableId
    });
    
    // 3️⃣ Replace with Backend Truth
    const backendCart = convertBackendCart(response.cart);
    set({
      cartsByTable: {
        ...cartsByTable,
        [activeTableId]: backendCart
      }
    });
    
  } catch (error) {
    // 4️⃣ Rollback on Error
    set({
      cartsByTable: {
        ...cartsByTable,
        [activeTableId]: previousCart
      },
      error: error.message
    });
    throw error;
  }
};
```

**Neden Bu Yaklaşım?**
- ✅ Instant UI (optimistic)
- ✅ Backend consistency (replace)
- ✅ Basit rollback
- ✅ Aynı ürün qty otomatik merge (backend)
- ✅ Table isolation garantili
- ✅ UI performanslı

---

## 📝 Implementation Checklist

- [x] Backend full cart response döndürüyor ✅
- [ ] Optimistic update implementasyonu
- [ ] Rollback mekanizması
- [ ] Table isolation testi
- [ ] Aynı ürün qty merge testi
- [ ] UI performance optimizasyonu (memoization, virtualization)
- [ ] Error handling
- [ ] Loading states
- [ ] Network timeout handling
- [ ] Race condition handling (concurrent requests)

---

## 🔍 Test Scenarios

### Test 1: Aynı Ürün Ekleme
```
1. Ürün A'yı ekle (qty: 1)
2. Tekrar Ürün A'yı ekle
3. ✓ Cart'ta qty: 2 olmalı (iki ayrı item değil)
```

### Test 2: Farklı Masalar
```
1. Masa 1'de Ürün A ekle
2. Masa 2'ye geç
3. Ürün B ekle
4. Masa 1'e geri dön
5. ✓ Ürün A hala orada olmalı
```

### Test 3: Network Hatası
```
1. Backend'i kapat
2. Ürün ekle
3. ✓ Optimistic update görünür
4. ✓ 5 saniye sonra rollback
5. ✓ Error toast görünür
```

### Test 4: Concurrent Requests
```
1. Ürün A'ya çok hızlı 5 kere tıkla
2. ✓ Backend'e 5 request gitmeli
3. ✓ Final qty: 5 olmalı
4. ✓ Race condition olmamalı
```

---

## 🎉 Özet

| Kriter | Senaryo 1 | Senaryo 2 | Kazanan |
|--------|-----------|-----------|---------|
| **UI Speed** | ⭐⭐⭐⭐⭐ | ⭐⭐⭐⭐⭐ | 🤝 Berabere |
| **Consistency** | ⭐⭐⭐ | ⭐⭐⭐⭐⭐ | 🏆 Senaryo 2 |
| **Basitlik** | ⭐⭐ | ⭐⭐⭐⭐ | 🏆 Senaryo 2 |
| **Rollback** | Karmaşık | Basit | 🏆 Senaryo 2 |
| **Network Overhead** | ⭐⭐⭐⭐⭐ | ⭐⭐⭐ | 🏆 Senaryo 1 |

**🎯 Final Verdict: Senaryo 2 (Hybrid Approach) kullanın!**

Backend full cart response döndürüyorsa, bu hem basit hem güvenilir hem de hızlı bir yaklaşımdır.

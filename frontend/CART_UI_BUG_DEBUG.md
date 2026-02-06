# 🐛 Cart UI Update Bug - Debug Checklist

## Sorun: Backend'e ürün ekleniyor ama UI'da görünmüyor

### 📋 Muhtemel Sebepler (Öncelik Sırasına Göre)

#### 1. ❌ Store State Güncellenmiyor (En Olası)
**Belirti:** Backend başarılı ama `cartsByTable[activeTableId]` güncellenmemiş

**Sebep:**
- `addItem` fonksiyonu `set()` çağrısı yapmıyor
- Backend response cart döndürüyor ama `set()` içinde kullanılmıyor
- Async setState timing issue (response geldi ama `set()` çağrılmadan önce component render oldu)

**Kontrol:**
```typescript
// Store'da addItem içinde:
console.log('Before set:', get().cartsByTable);
set({ cartsByTable: { ...updatedCart } });
console.log('After set:', get().cartsByTable);
```

---

#### 2. ❌ Wrong Selector / Stale State (UI Okuma Hatası)
**Belirti:** Store güncel ama component eski veriyi gösteriyor

**Sebep:**
- Component wrong table ID kullanıyor: `cartsByTable[1]` yerine `cartsByTable[activeTableId]`
- Selector cache'lenmiş ve re-render olmuyor
- `useMemo` dependency array yanlış

**Kontrol:**
```typescript
// Component'te:
const { activeTableId, cartsByTable } = useCartStore();
console.log('Active Table:', activeTableId);
console.log('Cart for this table:', cartsByTable[activeTableId]);
```

---

#### 3. ❌ Response Mapping Hatası (Backend → Store)
**Belirti:** Backend `Items` (uppercase) döndürüyor, store `items` (lowercase) bekliyor

**Sebep:**
```typescript
// Backend response:
{ Items: [...], ProductName: "Bier", Quantity: 1 }

// Store beklenen format:
{ items: [...], productName: "Bier", qty: 1 }
```

Case sensitivity veya field name mismatch!

**Kontrol:**
```typescript
console.log('Backend response:', response);
console.log('Mapped items:', localItems);
```

---

#### 4. ❌ activeTable ID Mismatch
**Belirti:** Table 1'e ürün ekliyorsun ama store Table 2'yi güncelliyor

**Sebep:**
- `addItem` içinde `activeTableId` yerine hardcoded `tableNumber: 1`
- Backend response `tableNumber` farklı table döndürüyor

**Kontrol:**
```typescript
console.log('Adding to table:', activeTableId);
console.log('Backend response table:', response.cart.tableNumber);
```

---

#### 5. ❌ Async setState Race Condition
**Belirti:** Hızlı tıklamada bazen çalışıyor bazen çalışmıyor

**Sebep:**
- Backend response gelmeden component unmount oldu
- İki concurrent `addItem` çağrısı birbirinin state'ini ezdi

**Kontrol:**
```typescript
let isMounted = true;
// ...
if (isMounted) {
  set({ cartsByTable: ... });
}
return () => { isMounted = false; };
```

---

#### 6. ❌ Items Alanı Map Edilmemiş
**Belirti:** `cart.Items` (capital I) var ama UI `cart.items` (lowercase) bekliyor

**Sebep:**
Backend C# backend (PascalCase) → Frontend JavaScript (camelCase) mapping eksik

**Fix:**
```typescript
const localItems = response.cart.Items.map(item => ({
  productId: item.ProductId,
  name: item.ProductName,
  qty: item.Quantity,
  price: item.UnitPrice
}));
```

---

#### 7. ❌ Memoization Issue (useMemo/useCallback)
**Belirti:** State değişiyor ama UI re-render olmuyor

**Sebep:**
- `useMemo` dependency array'e `cartsByTable` eklenmemiş
- `useCartStore()` hook'u re-subscribe olmuyor

**Fix:**
```typescript
const currentCart = useMemo(
  () => cartsByTable[activeTableId],
  [cartsByTable, activeTableId] // Dependencies!
);
```

---

#### 8. ❌ Key Issue (React List Rendering)
**Belirti:** Items array güncel ama FlatList/map render etmiyor

**Sebep:**
```typescript
// ❌ BAD: Index as key
{items.map((item, index) => <CartItem key={index} />)}

// ✅ GOOD: Unique ID as key
{items.map(item => <CartItem key={item.productId} />)}
```

---

#### 9. ❌ Backend Response Cart Yok
**Belirti:** `response.cart === undefined` ama success true

**Sebep:**
Backend `{ success: true, message: "..." }` döndürüyor ama cart nesnesi yok

**Fix:**
```typescript
if (response.cart) {
  // Update store
} else {
  // Fallback: Fetch cart manually
  await fetchCartForTable(activeTableId);
}
```

---

#### 10. ❌ Store Persistence Conflict
**Belirti:** AsyncStorage'dan eski data yükleniyor, yeni state'i override ediyor

**Sebep:**
- Persist middleware hydration timing
- AsyncStorage'da stale data

**Fix:**
```typescript
// AsyncStorage temizle (debug için)
await AsyncStorage.removeItem('cart-storage');
```

---

## 🎯 En Olası Senaryo (İlk Kontrol Et)

### Senaryo: Backend Response Mapping Hatası

```typescript
// ❌ PROBLEM: Backend PascalCase, Store camelCase
response.cart.Items // Backend
cartsByTable[1].items // Store
```

**Backend Response:**
```json
{
  "Items": [ // Capital I
    {
      "ProductId": "...", // Capital P
      "ProductName": "Bier", // Capital P
      "Quantity": 1 // Capital Q
    }
  ]
}
```

**Store Beklenen Format:**
```typescript
{
  items: [ // lowercase i
    {
      productId: "...", // lowercase p
      name: "Bier", // name (not productName)
      qty: 1 // qty (not quantity)
    }
  ]
}
```

**Fix:**
```typescript
const localItems = response.cart.Items.map(item => ({
  productId: item.ProductId,
  name: item.ProductName,
  qty: item.Quantity,
  price: item.UnitPrice,
  totalPrice: item.TotalPrice
}));

set({
  cartsByTable: {
    ...cartsByTable,
    [activeTableId]: {
      items: localItems, // lowercase!
      cartId: response.cart.CartId,
      updatedAt: Date.now()
    }
  }
});
```

---

## 🔧 Debug Komutları

### 1. Console'da Store State Kontrol
```javascript
// Console'a yaz:
window.__CART_DEBUG__ = () => {
  const store = useCartStore.getState();
  console.log('Active Table:', store.activeTableId);
  console.log('All Carts:', store.cartsByTable);
  console.log('Current Cart:', store.cartsByTable[store.activeTableId]);
};

// Çağır:
window.__CART_DEBUG__();
```

### 2. Component Render Tracking
```typescript
useEffect(() => {
  console.log('🔄 Cart Panel Rendered:', {
    activeTableId,
    cartItems: currentCart?.items?.length || 0
  });
}, [activeTableId, currentCart]);
```

### 3. Backend Response Logging
```typescript
addItem: async (productId, quantity) => {
  const response = await apiClient.post('/api/cart/add-item', ...);
  
  console.log('🌐 Backend Response:', response);
  console.log('  - cart exists?', !!response.cart);
  console.log('  - Items?', response.cart?.Items);
  console.log('  - items?', response.cart?.items);
  console.log('  - Table:', response.cart?.TableNumber || response.cart?.tableNumber);
}
```

---

## ✅ Hızlı Fix Checklist

- [ ] Backend response console'da görünüyor mu?
- [ ] `response.cart` undefined değil mi?
- [ ] `response.cart.Items` (capital I) var mı?
- [ ] Store `set()` çağrılıyor mu?
- [ ] `cartsByTable[activeTableId]` güncelleniyor mu?
- [ ] Component `useCartStore()` hook'u kullanıyor mu?
- [ ] `currentCart.items` (lowercase) render ediliyor mu?
- [ ] FlatList/map'te unique `key` var mı?
- [ ] activeTableId doğru mu? (1-10 arası)
- [ ] AsyncStorage stale data yok mu?

---

## 🚨 Acil Test Kodu

Component'te şunu ekle:

```typescript
useEffect(() => {
  console.log('===== CART DEBUG =====');
  console.log('Active Table ID:', activeTableId);
  console.log('Carts By Table:', cartsByTable);
  console.log('Current Cart:', cartsByTable[activeTableId]);
  console.log('Items:', cartsByTable[activeTableId]?.items);
  console.log('======================');
}, [activeTableId, cartsByTable]);
```

Ürün ekle ve console'u izle. Hangi log eksikse o sebep!

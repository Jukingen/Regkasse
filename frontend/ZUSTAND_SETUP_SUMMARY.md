# 🎯 Zustand Masa Bazlı Sepet Yönetimi - Kurulum Özeti

## ✅ Tamamlanan Adımlar

### A) Kurulum
```bash
npm install zustand
```
✅ Kurulum başarıyla tamamlandı.

---

## 📁 Oluşturulan Dosyalar

### 1. **Store Dosyası**
📍 `stores/useCartStore.ts`

**Özellikler:**
- ✅ Masa bazlı bağımsız sepet yönetimi
- ✅ AsyncStorage ile otomatik persist
- ✅ Backend API entegrasyonu (axios)
- ✅ TypeScript tam tip desteği
- ✅ Error handling ve loading states

**State Yapısı:**
```typescript
{
  activeTableId: number;              // Aktif masa (1-10)
  cartsByTable: {                     // Masa → Sepet mapping
    1: { items: [...], cartId: "...", updatedAt: ... },
    2: { items: [...], cartId: "...", updatedAt: ... },
    // ...
  },
  loading: boolean,
  error: string | null
}
```

**Aksiyonlar:**
- `setActiveTable(tableNumber)` - Masa değiştir
- `addItem(productId, quantity)` - Ürün ekle (backend çağrısı yapar)
- `increment(productId)` - Miktar artır
- `decrement(productId)` - Miktar azalt (0 olursa kaldırır)
- `remove(productId)` - Ürünü kaldır
- `clearCart(tableNumber?)` - Sepeti temizle
- `checkout(tableNumber?)` - Ödeme sonrası temizle

---

### 2. **Kullanım Kılavuzu**
📍 `ZUSTAND_CART_USAGE.md`

Detaylı kullanım örnekleri, API entegrasyonu, debugging ipuçları içerir.

---

### 3. **Entegrasyon Örneği**
📍 `ZUSTAND_INTEGRATION_EXAMPLE.tsx`

Mevcut `cash-register.tsx` ekranınıza nasıl entegre edeceğinizi gösteren tam örnek kod.

---

## 🔌 Backend API Entegrasyonu

Store aşağıdaki endpoint'leri kullanır:

| Aksiyon | Method | Endpoint | Payload |
|---------|--------|----------|---------|
| Ürün Ekle | POST | `/api/cart/add-item` | `{ productId, quantity, tableNumber }` |
| Sepet Al | GET | `/api/cart/current?tableNumber={n}` | - |
| Miktar Güncelle | PUT | `/api/cart/items/{itemId}` | `{ quantity, notes }` |
| Ürün Sil | DELETE | `/api/cart/{cartId}/items/{itemId}` | - |
| Sepet Temizle | POST | `/api/cart/clear?tableNumber={n}` | - |

**Axios Instance:**
Projenizde zaten mevcut olan `services/api/config.ts` kullanılır. Token yönetimi otomatik.

---

## 🚀 Cash Register Ekranına Entegrasyon

### Adım 1: Store'u Import Edin

```typescript
import { useCartStore } from '../../stores/useCartStore';

export default function CashRegisterScreen() {
  const {
    activeTableId,
    cartsByTable,
    setActiveTable,
    addItem,
    increment,
    decrement,
    remove,
    clearCart,
    checkout
  } = useCartStore();

  const currentCart = cartsByTable[activeTableId];
  
  // ...
}
```

### Adım 2: Masa Değiştirme

```typescript
const handleTableSelect = (tableNumber: number) => {
  setActiveTable(tableNumber);
};

<TableSelector
  selectedTable={activeTableId}
  onTableSelect={handleTableSelect}
/>
```

### Adım 3: Ürün Ekleme

```typescript
const handleProductSelect = async (product: Product) => {
  try {
    await addItem(product.id, 1);
    console.log('Product added!');
  } catch (error) {
    console.error('Failed to add product:', error);
  }
};

<ProductCard
  product={product}
  onPress={() => handleProductSelect(product)}
/>
```

### Adım 4: Sepet Gösterimi

```typescript
{currentCart && currentCart.items.length > 0 ? (
  <FlatList
    data={currentCart.items}
    renderItem={({ item }) => (
      <View>
        <Text>{item.name || item.productId}</Text>
        <Text>Qty: {item.qty}</Text>
        <Text>€{((item.price ?? 0) * item.qty).toFixed(2)}</Text>
        
        {/* Miktar Kontrolleri */}
        <Button title="−" onPress={() => decrement(item.productId)} />
        <Button title="+" onPress={() => increment(item.productId)} />
        <Button title="🗑️" onPress={() => remove(item.productId)} />
      </View>
    )}
  />
) : (
  <Text>Cart is empty</Text>
)}
```

### Adım 5: Checkout

```typescript
const handlePaymentSuccess = async (paymentId: string) => {
  await checkout(activeTableId);  // Sepeti temizle
  setActiveTable(1);              // İlk masaya dön
};

<PaymentModal
  visible={paymentModalVisible}
  onSuccess={handlePaymentSuccess}
  cartItems={currentCart?.items || []}
  tableNumber={activeTableId}
/>
```

---

## 💾 Persistence (AsyncStorage)

Store otomatik olarak AsyncStorage'a kaydedilir:

- **Key:** `cart-storage`
- **Kaydedilen:** `activeTableId`, `cartsByTable`
- **Kaydedilmeyen:** `loading`, `error`

App kapatılıp açılınca sepetler otomatik yüklenir.

---

## 🔍 Debug

### Console Logları

Store tüm aksiyonları loglar:

```
🏷️ [CartStore] Switching to table: 2
➕ [CartStore] Adding item to table 2: { productId: '123', quantity: 1 }
✅ [CartStore] Backend response: {...}
✅ [CartStore] Cart updated for table 2: { itemCount: 3 }
```

### Persist Temizleme (Development)

```typescript
import AsyncStorage from '@react-native-async-storage/async-storage';

// Tüm cart state'ini temizle
await AsyncStorage.removeItem('cart-storage');
```

---

## 📊 Data Flow

```
UI Event (ürün tıklama)
    ↓
Store Action (addItem)
    ↓
Backend API Call (POST /api/cart/add-item)
    ↓
Backend Response (cart with items)
    ↓
Store Update (cartsByTable[tableId] = {...})
    ↓
AsyncStorage Persist (otomatik)
    ↓
UI Re-render (updated cart)
```

---

## 🎨 Kullanım Özeti

### Temel Kullanım

```typescript
// 1. Store'u kullan
const { activeTableId, cartsByTable, addItem } = useCartStore();
const cart = cartsByTable[activeTableId];

// 2. Masa değiştir
setActiveTable(3);

// 3. Ürün ekle
await addItem('product-uuid', 1);

// 4. Sepeti göster
{cart?.items.map(item => <CartItem {...item} />)}

// 5. Checkout
await checkout();
```

---

## ⚠️ Önemli Notlar

1. **Backend Bağımlılığı**: Tüm işlemler backend ile senkronize. Backend kapalıysa hata alırsınız.

2. **Error Handling**: Her async aksiyon `try-catch` ile sarılmalı:
   ```typescript
   try {
     await addItem(productId, 1);
   } catch (error) {
     // UI'da hata göster
   }
   ```

3. **Source of Truth**: Backend response her zaman local state'e önceliklidir.

4. **Automatic Table ID**: `activeTableId` otomatik tüm aksiyonlarda kullanılır.

5. **Cart Format**: Backend'den gelen cart response otomatik local format'a dönüştürülür.

---

## 📚 Daha Fazla Bilgi

- **Store Implementation**: `stores/useCartStore.ts`
- **Usage Guide**: `ZUSTAND_CART_USAGE.md`
- **Integration Example**: `ZUSTAND_INTEGRATION_EXAMPLE.tsx`
- **Backend API**: `backend/KasseAPI_Final/Controllers/CartController.cs`

---

## ✨ Sonraki Adımlar

1. ✅ Zustand store oluşturuldu
2. ✅ Persist konfigürasyonu yapıldı
3. ✅ Backend API entegrasyonu tamamlandı
4. ⏳ **SİZİN YAPMANIZ GEREKEN**: `cash-register.tsx` dosyanıza entegre edin

### Entegrasyon Adımları:

1. `ZUSTAND_INTEGRATION_EXAMPLE.tsx` dosyasını inceleyin
2. Mevcut `cash-register.tsx` dosyanızda:
   - `useCartOptimized` yerine `useCartStore` kullanın
   - `selectedTable` yerine `activeTableId` kullanın
   - `addToCart` yerine `addItem` kullanın
   - Handler fonksiyonlarını güncelleyin

3. Test edin:
   ```bash
   npm run start
   ```

---

## 🎉 Tebrikler!

Zustand ile masa bazlı sepet yönetimi kurulumu tamamlandı! 🚀

Herhangi bir sorun yaşarsanız:
- Console loglarını kontrol edin
- `ZUSTAND_CART_USAGE.md` dosyasına bakın
- Backend API'sinin çalıştığından emin olun

# Zustand Masa Bazlı Sepet Yönetimi - Kullanım Kılavuzu

## 📦 Kurulum

Zustand paketi zaten yüklendi:
```bash
npm install zustand
```

## 🏗️ Yapı

### Dosya Organizasyonu
```
frontend/
├── stores/
│   └── useCartStore.ts       # Zustand store (masa bazlı sepet)
├── services/
│   └── api/
│       └── config.ts          # Axios instance (zaten mevcut)
└── app/
    └── (tabs)/
        └── cash-register.tsx  # Ana kassa ekranı
```

## 🎯 Store Özellikleri

### State Modeli
```typescript
{
  activeTableId: number;              // Seçili masa numarası
  cartsByTable: {                     // Masa bazlı sepetler
    [tableNumber: number]: {
      items: CartItem[];              // Sepet ürünleri
      updatedAt: number;              // Son güncelleme
      cartId: string;                 // Backend cart ID
    }
  };
  loading: boolean;                   // Yükleme durumu
  error: string | null;               // Hata mesajı
}
```

### CartItem Modeli
```typescript
{
  productId: string;     // Ürün ID (backend ID)
  name?: string;         // Ürün adı
  price?: number;        // Birim fiyat
  qty: number;           // Miktar
  unitPrice?: number;    // Birim fiyat (alternatif)
  totalPrice?: number;   // Toplam fiyat
  notes?: string;        // Notlar
}
```

## 🚀 Kullanım Örnekleri

### 1. Store'u Component'e Import Et

```typescript
import { useCartStore } from '../../stores/useCartStore';

export default function CashRegisterScreen() {
  // Store'dan ihtiyacınız olan state ve aksiyonları çekin
  const {
    activeTableId,
    cartsByTable,
    loading,
    error,
    setActiveTable,
    addItem,
    increment,
    decrement,
    remove,
    clearCart,
    checkout
  } = useCartStore();

  // Aktif masanın sepetini al
  const currentCart = cartsByTable[activeTableId];

  return (
    // ... UI
  );
}
```

### 2. Masa Değiştirme

```typescript
const handleTableSelect = (tableNumber: number) => {
  setActiveTable(tableNumber);
  console.log(`Switched to table ${tableNumber}`);
};

// UI'da kullanım
<TableSelector
  selectedTable={activeTableId}
  onTableSelect={handleTableSelect}
/>
```

### 3. Ürün Ekleme (Backend Entegrasyonu)

```typescript
const handleProductSelect = async (product: Product) => {
  try {
    // Backend'e POST gönderir: /api/cart/add-item
    await addItem(product.id, 1);
    
    console.log(`Added ${product.name} to table ${activeTableId}`);
  } catch (error) {
    console.error('Add item failed:', error);
    // Error handling (UI'da toast göster vs.)
  }
};

// UI'da kullanım
<ProductCard
  product={product}
  onPress={() => handleProductSelect(product)}
/>
```

### 4. Sepet Gösterimi

```typescript
const currentCart = cartsByTable[activeTableId];

return (
  <View>
    <Text>Table {activeTableId} Cart</Text>
    
    {currentCart && currentCart.items.length > 0 ? (
      <FlatList
        data={currentCart.items}
        keyExtractor={(item) => item.productId}
        renderItem={({ item }) => (
          <View>
            <Text>{item.name || item.productId}</Text>
            <Text>Qty: {item.qty}</Text>
            <Text>Price: €{item.price?.toFixed(2)}</Text>
            
            {/* Miktar Kontrolleri */}
            <TouchableOpacity onPress={() => decrement(item.productId)}>
              <Text>−</Text>
            </TouchableOpacity>
            
            <Text>{item.qty}</Text>
            
            <TouchableOpacity onPress={() => increment(item.productId)}>
              <Text>+</Text>
            </TouchableOpacity>
            
            {/* Kaldır */}
            <TouchableOpacity onPress={() => remove(item.productId)}>
              <Text>🗑️ Remove</Text>
            </TouchableOpacity>
          </View>
        )}
      />
    ) : (
      <Text>Cart is empty</Text>
    )}
  </View>
);
```

### 5. Miktar İşlemleri

```typescript
// Ürün miktarını artır
const handleIncrement = async (productId: string) => {
  try {
    await increment(productId);
  } catch (error) {
    console.error('Increment failed:', error);
  }
};

// Ürün miktarını azalt (0 olursa kaldırır)
const handleDecrement = async (productId: string) => {
  try {
    await decrement(productId);
  } catch (error) {
    console.error('Decrement failed:', error);
  }
};

// Ürünü tamamen kaldır
const handleRemove = async (productId: string) => {
  try {
    await remove(productId);
  } catch (error) {
    console.error('Remove failed:', error);
  }
};
```

### 6. Sepeti Temizle

```typescript
// Aktif masanın sepetini temizle
const handleClearCart = async () => {
  try {
    await clearCart(); // activeTableId otomatik kullanılır
    console.log('Cart cleared');
  } catch (error) {
    console.error('Clear cart failed:', error);
  }
};

// Belirli bir masanın sepetini temizle
const handleClearSpecificTable = async (tableNumber: number) => {
  try {
    await clearCart(tableNumber);
    console.log(`Table ${tableNumber} cart cleared`);
  } catch (error) {
    console.error('Clear cart failed:', error);
  }
};
```

### 7. Checkout (Ödeme)

```typescript
const handleCheckout = async () => {
  try {
    // Ödeme mantığı burada...
    
    // Başarılı ödeme sonrası sepeti temizle
    await checkout(); // activeTableId için
    
    console.log('Checkout successful');
  } catch (error) {
    console.error('Checkout failed:', error);
  }
};

// Belirli masa için checkout
const handleCheckoutForTable = async (tableNumber: number) => {
  try {
    await checkout(tableNumber);
    console.log(`Checkout successful for table ${tableNumber}`);
  } catch (error) {
    console.error('Checkout failed:', error);
  }
};
```

### 8. Loading ve Error Handling

```typescript
const {
  loading,
  error,
  // ... diğer state ve actions
} = useCartStore();

return (
  <View>
    {/* Loading Indicator */}
    {loading && <ActivityIndicator />}
    
    {/* Error Display */}
    {error && (
      <View style={styles.errorContainer}>
        <Text style={styles.errorText}>{error}</Text>
      </View>
    )}
    
    {/* Cart Content */}
    {/* ... */}
  </View>
);
```

### 9. Toplam Hesaplama

```typescript
const currentCart = cartsByTable[activeTableId];

// Toplam ürün sayısı
const totalItems = currentCart?.items.reduce(
  (sum, item) => sum + item.qty,
  0
) ?? 0;

// Toplam fiyat
const totalPrice = currentCart?.items.reduce(
  (sum, item) => sum + (item.totalPrice ?? (item.price ?? 0) * item.qty),
  0
) ?? 0;

return (
  <View>
    <Text>Total Items: {totalItems}</Text>
    <Text>Total Price: €{totalPrice.toFixed(2)}</Text>
  </View>
);
```

## 🔄 Backend API Entegrasyonu

### Endpoint'ler

Store aşağıdaki backend endpoint'lerini kullanır:

1. **Ürün Ekle**
   - `POST /api/cart/add-item`
   - Payload: `{ productId, quantity, tableNumber }`

2. **Sepet Al**
   - `GET /api/cart/current?tableNumber={tableNumber}`

3. **Ürün Güncelle**
   - `PUT /api/cart/items/{itemId}`
   - Payload: `{ quantity, notes }`

4. **Ürün Sil**
   - `DELETE /api/cart/{cartId}/items/{itemId}`

5. **Sepet Temizle**
   - `POST /api/cart/clear?tableNumber={tableNumber}`

### Response Format (Backend)

```typescript
{
  message: "Item added to cart successfully",
  cart: {
    cartId: "uuid",
    tableNumber: 1,
    items: [
      {
        id: "item-uuid",
        productId: "product-uuid",
        productName: "Pizza Margherita",
        quantity: 2,
        unitPrice: 8.50,
        totalPrice: 17.00,
        notes: "Extra cheese"
      }
    ],
    totalItems: 2,
    subtotal: 17.00,
    totalTax: 3.40,
    grandTotal: 20.40
  }
}
```

## 💾 Persistence (AsyncStorage)

Store otomatik olarak `AsyncStorage` kullanarak persist edilir:

- **Key**: `cart-storage`
- **Persisted Data**: `activeTableId`, `cartsByTable`
- **Not Persisted**: `loading`, `error` (geçici state)

App yeniden açıldığında sepetler otomatik yüklenir.

### Manuel Persist Temizleme (Debug için)

```typescript
import AsyncStorage from '@react-native-async-storage/async-storage';

// Tüm cart state'ini temizle
await AsyncStorage.removeItem('cart-storage');
```

## 🐛 Debug

### Console Logları

Store tüm işlemleri console'a loglar:

```
🏷️ [CartStore] Switching to table: 2
➕ [CartStore] Adding item to table 2: { productId: 'abc', quantity: 1 }
✅ [CartStore] Backend response: { ... }
✅ [CartStore] Cart updated for table 2: { itemCount: 3, totalItems: 5 }
```

### State İnceleme (React DevTools)

Zustand DevTools kullanarak state'i inceleyebilirsiniz:

```typescript
// stores/useCartStore.ts içinde
import { devtools } from 'zustand/middleware';

export const useCartStore = create<CartState>()(
  devtools(
    persist(
      // ... store implementation
    )
  )
);
```

## ⚠️ Önemli Notlar

1. **Backend Zorunlu**: Tüm cart işlemleri backend üzerinden yapılır. Backend kapalıysa işlemler başarısız olur.

2. **Error Handling**: Her async action hata fırlatabilir. `try-catch` kullanın.

3. **Source of Truth**: Backend response'u her zaman local state'e önceliklidir.

4. **Table ID**: `activeTableId` her zaman `addItem`, `increment`, `decrement` vs. için otomatik kullanılır.

5. **AsyncStorage Limits**: Çok fazla masa/ürün varsa AsyncStorage sınırlarına dikkat edin.

## 🎨 UI Entegrasyon Özeti

```typescript
import { useCartStore } from '../../stores/useCartStore';

export default function CashRegisterScreen() {
  const {
    activeTableId,
    cartsByTable,
    setActiveTable,
    addItem
  } = useCartStore();

  const currentCart = cartsByTable[activeTableId];

  return (
    <View>
      {/* 1. Masa Seçici */}
      <TableSelector
        selectedTable={activeTableId}
        onTableSelect={setActiveTable}
      />

      {/* 2. Ürün Listesi */}
      <ProductList
        onProductSelect={(product) => addItem(product.id, 1)}
      />

      {/* 3. Sepet Görünümü */}
      <CartDisplay
        cart={currentCart}
        tableId={activeTableId}
      />
    </View>
  );
}
```

## 📚 Daha Fazla Bilgi

- [Zustand Docs](https://docs.pmnd.rs/zustand)
- [AsyncStorage Docs](https://react-native-async-storage.github.io/async-storage/)
- Backend API Docs: `Regkasse/backend/KasseAPI_Final/Controllers/CartController.cs`

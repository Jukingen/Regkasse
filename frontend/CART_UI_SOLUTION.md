# 🎯 Cart UI Update - Tam Çözüm Kılavuzu

## B) Doğru Çözüm Akışı

### 🔄 Akış Diagramı

```
USER CLICKS "ADD TO CART"
    ↓
[1] OPTIMISTIC UPDATE (Instant UI)
    set({ cartsByTable: { [activeTableId]: optimisticCart } })
    ↓
[2] BACKEND CALL
    POST /api/cart/add-item
    { productId, quantity, tableNumber: activeTableId }
    ↓
[3] BACKEND SUCCESS ✅
    ↓
[4] RESPONSE HAS CART? 
    ├─→ YES: REPLACE WITH BACKEND CART
    │   const localItems = response.cart.Items.map(...)
    │   set({ cartsByTable: { [activeTableId]: { items: localItems } } })
    │   
    └─→ NO: FETCH CART MANUALLY
        const cart = await fetchCartByTable(activeTableId)
        set({ cartsByTable: { [activeTableId]: cart } })
    ↓
[5] UI RE-RENDERS
    CartPanel receives cartsByTable[activeTableId].items
    FlatList renders updated items
    ✅ User sees new item!
```

---

## C) Backend Response Senaryoları ve Çözümleri

### Senaryo 1: Backend Cart Döndürüyor (Sizin Durum) ✅

**Backend Response:**
```json
{
  "message": "Item added to cart successfully",
  "cart": {
    "CartId": "uuid",
    "TableNumber": 1,
    "Items": [
      {
        "Id": "item-uuid",
        "ProductId": "product-uuid",
        "ProductName": "Bier 0.5L",
        "Quantity": 1,
        "UnitPrice": 4.8,
        "TotalPrice": 4.8
      }
    ],
    "TotalItems": 1,
    "Subtotal": 4.8,
    "GrandTotal": 5.76
  }
}
```

**✅ ÇÖZÜM: Replace Strategy**

```typescript
addItem: async (productId: string, quantity: number = 1) => {
  const { activeTableId, cartsByTable } = get();
  const currentCart = cartsByTable[activeTableId] || { items: [] };
  
  // 🎯 PHASE 1: Optimistic Update
  const optimisticCart = {
    ...currentCart,
    items: [
      ...currentCart.items,
      { productId, name: 'Loading...', qty: quantity, price: 0 }
    ],
    updatedAt: Date.now()
  };
  
  set({
    cartsByTable: { ...cartsByTable, [activeTableId]: optimisticCart }
  });
  
  // 🌐 PHASE 2: Backend Call
  try {
    const response = await apiClient.post('/api/cart/add-item', {
      productId,
      quantity,
      tableNumber: activeTableId
    });
    
    // 🔄 PHASE 3: Replace with Backend Cart
    if (response.cart) {
      const backendCart = mapBackendCartToLocal(response.cart);
      
      set({
        cartsByTable: {
          ...cartsByTable,
          [activeTableId]: backendCart // Replace!
        }
      });
    }
  } catch (error) {
    // ❌ Rollback
    set({
      cartsByTable: { ...cartsByTable, [activeTableId]: currentCart }
    });
    throw error;
  }
}
```

**Mapping Function:**
```typescript
function mapBackendCartToLocal(backendCart: any): Cart {
  return {
    cartId: backendCart.CartId,
    items: backendCart.Items.map((item: any) => ({
      productId: item.ProductId,
      name: item.ProductName,
      qty: item.Quantity,
      price: item.UnitPrice,
      unitPrice: item.UnitPrice,
      totalPrice: item.TotalPrice,
      notes: item.Notes
    })),
    updatedAt: Date.now()
  };
}
```

---

### Senaryo 2: Backend Cart Döndürmüyor (Success Only)

**Backend Response:**
```json
{
  "success": true,
  "message": "Item added successfully"
}
```

#### Çözüm 1: Optimistic Update + Rollback on Error

```typescript
addItem: async (productId: string, quantity: number = 1) => {
  const { activeTableId, cartsByTable } = get();
  const currentCart = cartsByTable[activeTableId] || { items: [] };
  
  // Find existing item
  const existingItemIndex = currentCart.items.findIndex(
    item => item.productId === productId
  );
  
  let optimisticCart: Cart;
  
  if (existingItemIndex !== -1) {
    // Increment existing item
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
    // Add new item (need product info from cache/context)
    const product = get().productCache?.[productId];
    
    optimisticCart = {
      ...currentCart,
      items: [
        ...currentCart.items,
        {
          productId,
          name: product?.name || 'Unknown Product',
          qty: quantity,
          price: product?.price || 0,
          unitPrice: product?.price || 0,
          totalPrice: (product?.price || 0) * quantity
        }
      ],
      updatedAt: Date.now()
    };
  }
  
  // Immediately update UI
  set({
    cartsByTable: { ...cartsByTable, [activeTableId]: optimisticCart }
  });
  
  // Backend call
  try {
    const response = await apiClient.post('/api/cart/add-item', {
      productId,
      quantity,
      tableNumber: activeTableId
    });
    
    if (!response.success) {
      throw new Error(response.message);
    }
    
    // Success - optimistic update was correct
    console.log('✅ Backend confirmed optimistic update');
    
  } catch (error) {
    // Rollback to previous state
    set({
      cartsByTable: { ...cartsByTable, [activeTableId]: currentCart }
    });
    throw error;
  }
}
```

**Artılar:**
- ✅ Instant UI
- ✅ Düşük network overhead

**Eksiler:**
- ❌ Product cache gerekli (price, name için)
- ❌ Rollback complexity

---

#### Çözüm 2: Success Sonrası Fetch Cart

```typescript
addItem: async (productId: string, quantity: number = 1) => {
  const { activeTableId, cartsByTable } = get();
  
  set({ loading: true });
  
  try {
    // Backend call
    const response = await apiClient.post('/api/cart/add-item', {
      productId,
      quantity,
      tableNumber: activeTableId
    });
    
    if (!response.success) {
      throw new Error(response.message);
    }
    
    // Success - now fetch cart
    const cartResponse = await apiClient.get(
      `/api/cart/current?tableNumber=${activeTableId}`
    );
    
    const backendCart = mapBackendCartToLocal(cartResponse);
    
    set({
      cartsByTable: {
        ...cartsByTable,
        [activeTableId]: backendCart
      },
      loading: false
    });
    
  } catch (error) {
    set({ loading: false });
    throw error;
  }
}
```

**Artılar:**
- ✅ Backend always source of truth
- ✅ No product cache needed
- ✅ Simple logic

**Eksiler:**
- ❌ 2 API calls (slower)
- ❌ Loading spinner görünür

---

## D) Backend Response → UI Model Mapping

### Backend Model (C# PascalCase)

```csharp
// Backend Response
{
  "CartId": "uuid",
  "TableNumber": 1,
  "Items": [
    {
      "Id": "item-uuid",
      "ProductId": "product-uuid",
      "ProductName": "Bier 0.5L",
      "Quantity": 1,
      "UnitPrice": 4.8,
      "TotalPrice": 4.8,
      "Notes": null,
      "TaxType": "Standard",
      "TaxRate": 0.2
    }
  ],
  "TotalItems": 1,
  "Subtotal": 4.8,
  "TotalTax": 0.96,
  "GrandTotal": 5.76,
  "Status": 1,
  "CreatedAt": "2026-02-06T05:57:15Z",
  "ExpiresAt": "2026-02-07T05:57:15Z"
}
```

### Frontend Model (JavaScript camelCase)

```typescript
interface Cart {
  cartId: string;
  items: CartItem[];
  updatedAt: number;
  
  // Optionals (for display)
  tableNumber?: number;
  totalItems?: number;
  subtotal?: number;
  totalTax?: number;
  grandTotal?: number;
}

interface CartItem {
  productId: string;
  name: string;
  qty: number;
  price: number;
  unitPrice: number;
  totalPrice: number;
  notes?: string;
  
  // Optionals
  taxType?: string;
  taxRate?: number;
  itemId?: string; // Backend's "Id" for delete/update
}
```

### Mapping Function (Complete)

```typescript
/**
 * Backend Cart Response → Frontend Cart Model
 */
function mapBackendCartToLocal(backendCart: any): Cart {
  return {
    // Core fields
    cartId: backendCart.CartId || backendCart.cartId,
    
    // Items mapping (PascalCase → camelCase)
    items: (backendCart.Items || backendCart.items || []).map((item: any) => ({
      // Required fields
      productId: item.ProductId || item.productId,
      name: item.ProductName || item.productName || 'Unknown Product',
      qty: item.Quantity || item.quantity || 0,
      price: item.UnitPrice || item.unitPrice || 0,
      unitPrice: item.UnitPrice || item.unitPrice || 0,
      totalPrice: item.TotalPrice || item.totalPrice || 0,
      
      // Optional fields
      notes: item.Notes || item.notes,
      taxType: item.TaxType || item.taxType,
      taxRate: item.TaxRate || item.taxRate,
      itemId: item.Id || item.id, // For backend CRUD operations
    })),
    
    // Metadata
    updatedAt: Date.now(),
    
    // Optional summary fields (for display)
    tableNumber: backendCart.TableNumber || backendCart.tableNumber,
    totalItems: backendCart.TotalItems || backendCart.totalItems,
    subtotal: backendCart.Subtotal || backendCart.subtotal,
    totalTax: backendCart.TotalTax || backendCart.totalTax,
    grandTotal: backendCart.GrandTotal || backendCart.grandTotal,
  };
}
```

**Usage:**
```typescript
const localCart = mapBackendCartToLocal(response.cart);
console.log(localCart);
// {
//   cartId: "uuid",
//   items: [
//     {
//       productId: "...",
//       name: "Bier 0.5L",
//       qty: 1,
//       price: 4.8,
//       totalPrice: 4.8
//     }
//   ],
//   updatedAt: 1675692345000,
//   grandTotal: 5.76
// }
```

---

## E) Zustand Store + CartPanel Tam Örnek

### 1. Zustand Store (Düzeltilmiş)

```typescript
// stores/useCartStore.ts
import { create } from 'zustand';
import { persist, createJSONStorage } from 'zustand/middleware';
import AsyncStorage from '@react-native-async-storage/async-storage';
import { apiClient } from '../services/api/config';

// Types
export interface CartItem {
  productId: string;
  name: string;
  qty: number;
  price: number;
  unitPrice: number;
  totalPrice: number;
  notes?: string;
  itemId?: string; // Backend ID for CRUD
}

export interface Cart {
  cartId?: string;
  items: CartItem[];
  updatedAt: number;
  tableNumber?: number;
  totalItems?: number;
  subtotal?: number;
  totalTax?: number;
  grandTotal?: number;
}

interface CartState {
  activeTableId: number;
  cartsByTable: Record<number, Cart>;
  loading: boolean;
  error: string | null;
  
  setActiveTable: (tableNumber: number) => void;
  addItem: (productId: string, quantity?: number) => Promise<void>;
  fetchCartForTable: (tableNumber: number) => Promise<void>;
  clearCart: (tableNumber?: number) => Promise<void>;
}

// Mapping function
function mapBackendCartToLocal(backendCart: any): Cart {
  return {
    cartId: backendCart.CartId || backendCart.cartId,
    items: (backendCart.Items || backendCart.items || []).map((item: any) => ({
      productId: item.ProductId || item.productId,
      name: item.ProductName || item.productName || 'Unknown',
      qty: item.Quantity || item.quantity || 0,
      price: item.UnitPrice || item.unitPrice || 0,
      unitPrice: item.UnitPrice || item.unitPrice || 0,
      totalPrice: item.TotalPrice || item.totalPrice || 0,
      notes: item.Notes || item.notes,
      itemId: item.Id || item.id,
    })),
    updatedAt: Date.now(),
    tableNumber: backendCart.TableNumber || backendCart.tableNumber,
    totalItems: backendCart.TotalItems || backendCart.totalItems,
    subtotal: backendCart.Subtotal || backendCart.subtotal,
    totalTax: backendCart.TotalTax || backendCart.totalTax,
    grandTotal: backendCart.GrandTotal || backendCart.grandTotal,
  };
}

// Store
export const useCartStore = create<CartState>()(
  persist(
    (set, get) => ({
      activeTableId: 1,
      cartsByTable: {},
      loading: false,
      error: null,

      setActiveTable: (tableNumber: number) => {
        console.log(`🏷️ Switching to table ${tableNumber}`);
        set({ activeTableId: tableNumber });
        
        // Fetch cart for this table if not loaded
        const cart = get().cartsByTable[tableNumber];
        if (!cart) {
          get().fetchCartForTable(tableNumber);
        }
      },

      fetchCartForTable: async (tableNumber: number) => {
        try {
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
          
          console.log(`✅ Fetched cart for table ${tableNumber}:`, localCart);
        } catch (error: any) {
          console.error(`❌ Failed to fetch cart for table ${tableNumber}:`, error);
        }
      },

      addItem: async (productId: string, quantity: number = 1) => {
        const { activeTableId, cartsByTable } = get();
        const currentCart = cartsByTable[activeTableId] || { items: [], updatedAt: Date.now() };
        
        console.log(`➕ Adding item to table ${activeTableId}`);
        
        // Optimistic Update
        const optimisticCart: Cart = {
          ...currentCart,
          items: [
            ...currentCart.items,
            {
              productId,
              name: 'Loading...',
              qty: quantity,
              price: 0,
              unitPrice: 0,
              totalPrice: 0
            }
          ],
          updatedAt: Date.now()
        };
        
        set({
          cartsByTable: { ...cartsByTable, [activeTableId]: optimisticCart },
          loading: false // UI stays responsive
        });
        
        // Backend Call
        try {
          const response = await apiClient.post('/api/cart/add-item', {
            productId,
            quantity,
            tableNumber: activeTableId
          });
          
          console.log('🌐 Backend response:', response);
          
          // Replace with backend cart (source of truth)
          if (response.cart) {
            const backendCart = mapBackendCartToLocal(response.cart);
            
            set({
              cartsByTable: {
                ...cartsByTable,
                [activeTableId]: backendCart
              }
            });
            
            console.log(`✅ Cart updated for table ${activeTableId}:`, backendCart);
          } else {
            // Fallback: Fetch cart manually
            await get().fetchCartForTable(activeTableId);
          }
        } catch (error: any) {
          // Rollback
          console.error('❌ Add item failed, rolling back:', error);
          set({
            cartsByTable: { ...cartsByTable, [activeTableId]: currentCart },
            error: error.message
          });
          throw error;
        }
      },

      clearCart: async (tableNumber?: number) => {
        const targetTable = tableNumber ?? get().activeTableId;
        
        try {
          await apiClient.post(`/api/cart/clear?tableNumber=${targetTable}`);
          
          const { cartsByTable } = get();
          const updatedCarts = { ...cartsByTable };
          delete updatedCarts[targetTable];
          
          set({ cartsByTable: updatedCarts });
          
          console.log(`✅ Cleared cart for table ${targetTable}`);
        } catch (error: any) {
          console.error('❌ Clear cart failed:', error);
          throw error;
        }
      }
    }),
    {
      name: 'cart-storage',
      storage: createJSONStorage(() => AsyncStorage),
      partialize: (state) => ({
        activeTableId: state.activeTableId,
        cartsByTable: state.cartsByTable
      })
    }
  )
);
```

---

### 2. CartPanel Component

```typescript
// components/CartPanel.tsx
import React, { useMemo, useEffect } from 'react';
import { View, Text, FlatList, TouchableOpacity, StyleSheet } from 'react-native';
import { useCartStore } from '../stores/useCartStore';

export function CartPanel() {
  const { activeTableId, cartsByTable, loading } = useCartStore();
  
  // Get cart for active table
  const currentCart = useMemo(
    () => cartsByTable[activeTableId],
    [cartsByTable, activeTableId]
  );
  
  // Calculate totals
  const { totalItems, totalPrice } = useMemo(() => {
    if (!currentCart || !currentCart.items.length) {
      return { totalItems: 0, totalPrice: 0 };
    }
    
    return {
      totalItems: currentCart.items.reduce((sum, item) => sum + item.qty, 0),
      totalPrice: currentCart.items.reduce((sum, item) => sum + item.totalPrice, 0)
    };
  }, [currentCart]);
  
  // Debug log
  useEffect(() => {
    console.log('🔄 CartPanel Re-rendered:', {
      activeTableId,
      itemsCount: currentCart?.items?.length || 0,
      totalItems
    });
  }, [activeTableId, currentCart, totalItems]);
  
  return (
    <View style={styles.container}>
      <Text style={styles.title}>
        Cart Items - Table {activeTableId}
      </Text>
      
      {loading && <Text style={styles.loading}>Loading...</Text>}
      
      {currentCart && currentCart.items.length > 0 ? (
        <>
          <FlatList
            data={currentCart.items}
            keyExtractor={(item) => item.productId} // Unique key!
            renderItem={({ item }) => (
              <View style={styles.cartItem}>
                <View style={styles.itemInfo}>
                  <Text style={styles.itemName}>{item.name}</Text>
                  <Text style={styles.itemDetails}>
                    {item.qty} x €{item.price.toFixed(2)}
                  </Text>
                </View>
                <Text style={styles.itemTotal}>
                  €{item.totalPrice.toFixed(2)}
                </Text>
              </View>
            )}
          />
          
          <View style={styles.summary}>
            <Text style={styles.summaryText}>
              Total Items: {totalItems}
            </Text>
            <Text style={styles.summaryTotal}>
              Total: €{(currentCart.grandTotal || totalPrice).toFixed(2)}
            </Text>
          </View>
        </>
      ) : (
        <Text style={styles.emptyText}>
          No items in cart for Table {activeTableId}
        </Text>
      )}
    </View>
  );
}

const styles = StyleSheet.create({
  container: {
    backgroundColor: '#fff',
    padding: 16,
    borderRadius: 8,
    marginVertical: 8,
  },
  title: {
    fontSize: 18,
    fontWeight: 'bold',
    marginBottom: 12,
  },
  loading: {
    textAlign: 'center',
    color: '#666',
    padding: 20,
  },
  cartItem: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    paddingVertical: 12,
    borderBottomWidth: 1,
    borderBottomColor: '#eee',
  },
  itemInfo: {
    flex: 1,
  },
  itemName: {
    fontSize: 16,
    fontWeight: '600',
  },
  itemDetails: {
    fontSize: 14,
    color: '#666',
    marginTop: 4,
  },
  itemTotal: {
    fontSize: 16,
    fontWeight: 'bold',
    color: '#007AFF',
  },
  summary: {
    marginTop: 16,
    paddingTop: 16,
    borderTopWidth: 2,
    borderTopColor: '#ddd',
  },
  summaryText: {
    fontSize: 14,
    color: '#666',
  },
  summaryTotal: {
    fontSize: 20,
    fontWeight: 'bold',
    color: '#007AFF',
    marginTop: 8,
  },
  emptyText: {
    textAlign: 'center',
    color: '#999',
    padding: 40,
    fontSize: 16,
  },
});
```

---

### 3. Cash Register Integration

```typescript
// app/(tabs)/cash-register.tsx
import React from 'react';
import { SafeAreaView, ScrollView, StyleSheet } from 'react-native';
import { useCartStore } from '../../stores/useCartStore';
import { TableSelector } from '../../components/TableSelector';
import { ProductList } from '../../components/ProductList';
import { CartPanel } from '../../components/CartPanel';

export default function CashRegisterScreen() {
  const { activeTableId, setActiveTable, addItem } = useCartStore();
  
  const handleTableSelect = (tableNumber: number) => {
    setActiveTable(tableNumber);
  };
  
  const handleProductSelect = async (product: any) => {
    try {
      await addItem(product.id, 1);
      console.log(`✅ Added ${product.name} to table ${activeTableId}`);
    } catch (error) {
      console.error('Failed to add product:', error);
    }
  };
  
  return (
    <SafeAreaView style={styles.container}>
      <ScrollView>
        {/* Table Selector */}
        <TableSelector
          selectedTable={activeTableId}
          onTableSelect={handleTableSelect}
        />
        
        {/* Product Grid */}
        <ProductList
          onProductSelect={handleProductSelect}
        />
        
        {/* Cart Panel - Shows items for active table */}
        <CartPanel />
      </ScrollView>
    </SafeAreaView>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: '#f5f5f5',
  },
});
```

---

## ✅ Final Checklist

Sorununuz şu adımlarla çözülecek:

- [ ] Store'da `mapBackendCartToLocal` mapping function ekle
- [ ] `addItem` içinde optimistic update + backend replace yap
- [ ] Backend response'da `response.cart.Items` (capital I) map et
- [ ] CartPanel `cartsByTable[activeTableId].items` render etsin
- [ ] FlatList unique `keyExtractor` kullan (`item.productId`)
- [ ] Debug log ekle ve console'u izle
- [ ] AsyncStorage temizle (teste başlarken)

**Test:**
1. Ürüne tıkla
2. Console'da `🚀 Optimistic update` log'unu gör
3. Console'da `🌐 Backend response` log'unu gör
4. Console'da `✅ Cart updated` log'unu gör
5. UI'da CartPanel'de ürün görün!

Sorun devam ederse console log'ları gönderin, debug edelim! 🚀

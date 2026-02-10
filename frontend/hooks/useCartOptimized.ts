// Türkçe Açıklama: Optimize edilmiş sepet yönetimi hook'u - sonsuz döngü sorunlarını çözer
// useApiManager kullanarak duplicate API çağrılarını önler ve akıllı cache yönetimi sağlar

import { useState, useCallback, useRef, useEffect } from 'react';
import { cartService } from '../services/api/cartService';
import { useApiManager } from './useApiManager';

// Cart item interface
interface CartItem {
  id: string;
  productId: string;
  productName: string;
  productImage?: string | null;
  quantity: number;
  unitPrice: number;
  totalPrice: number;
  notes?: string | null;
  taxType?: string;
  taxRate?: number;
}

// Cart interface
interface Cart {
  cartId: string;
  items: CartItem[];
  totalItems: number;
  subtotal: number;
  totalTax: number;
  grandTotal: number;
  status: string;
  tableNumber?: number;
  createdAt: string;
  expiresAt?: string;
  waiterName?: string;
  customerId?: string;
  notes?: string;
}

export const useCartOptimized = () => {
  const { apiCall, getCachedData, setCachedData } = useApiManager();
  
  // Masa bazlı sepet yönetimi - Map kullanarak performans artırımı
  const [tableCarts, setTableCarts] = useState<Map<number, Cart>>(new Map());
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  
  // Ref'ler ile sürekli re-render'ı önle
  const tableCartsRef = useRef(tableCarts);
  const loadingRef = useRef(loading);
  const errorRef = useRef(error);

  // State güncelleme fonksiyonları - batch update
  const updateTableCarts = useCallback((updater: (prev: Map<number, Cart>) => Map<number, Cart>) => {
    setTableCarts(prev => {
      const newCarts = updater(prev);
      tableCartsRef.current = newCarts;
      return newCarts;
    });
  }, []);

  const setLoadingState = useCallback((isLoading: boolean) => {
    setLoading(isLoading);
    loadingRef.current = isLoading;
  }, []);

  const setErrorState = useCallback((errorMsg: string | null) => {
    setError(errorMsg);
    errorRef.current = errorMsg;
  }, []);

  // Sepet getter fonksiyonu - tableNumber parametresi ile
  const getCartForTable = useCallback((tableNumber: number): Cart | null => {
    return tableCartsRef.current.get(tableNumber) || null;
  }, []);

  // Add item to cart with backend API integration
  const addToCart = useCallback(async (
    item: Omit<CartItem, 'id' | 'totalPrice'>, 
    tableNumber: number
  ): Promise<{ success: boolean; message: string; cart?: Cart }> => {
    if (!tableNumber) {
      const errorMsg = 'Table number is required';
      setErrorState(errorMsg);
      return { success: false, message: errorMsg };
    }

    try {
      setLoadingState(true);
      setErrorState(null);

      console.log('🛒 Ürün sepete ekleniyor:', { item, tableNumber });

      // API çağrısı - duplicate call'ları önler
      const result = await apiCall(
        `add-to-cart-${tableNumber}-${item.productId}`,
        async () => {
          // Backend'e ekle
          const response = await cartService.addItemToCart({
            productId: item.productId,
            quantity: item.quantity,
            notes: item.notes || undefined,
            tableNumber: tableNumber
          });

          if (response && response.cart) {
            return response.cart;
          }

          // Backend başarısız olursa local fallback
          throw new Error('Backend failed, using local fallback');
        },
        {
          cacheKey: `cart-${tableNumber}`,
          cacheTTL: 1, // 1 dakika cache
          skipDuplicate: true,
        }
      );

      if (result) {
        // Backend success - update local state
        updateTableCarts(prev => {
          const newCarts = new Map(prev);
          newCarts.set(tableNumber, result);
          return newCarts;
        });

        // Cache'i güncelle
        setCachedData(`cart-${tableNumber}`, result, 1);
        
        console.log('✅ Ürün başarıyla eklendi');
        return { success: true, message: 'Item added successfully', cart: result };
      }

      // Local fallback
      const newItem: CartItem = {
        ...item,
        id: `local_${Date.now()}_${Math.random().toString(36).substr(2, 9)}`,
        totalPrice: item.quantity * item.unitPrice
      };

      updateTableCarts(prev => {
        const prevCart = prev.get(tableNumber);
        
        if (!prevCart) {
          // Create new cart for this table
          const newCart: Cart = {
            cartId: `local_cart_${tableNumber}_${Date.now()}`,
            items: [newItem],
            totalItems: newItem.quantity,
            subtotal: newItem.totalPrice,
            totalTax: newItem.totalPrice * 0.20,
            grandTotal: newItem.totalPrice * 1.20,
            status: 'active',
            tableNumber: tableNumber,
            createdAt: new Date().toISOString(),
            expiresAt: new Date(Date.now() + 24 * 60 * 60 * 1000).toISOString(),
            waiterName: undefined,
            customerId: undefined,
            notes: undefined
          };
          
          const newCarts = new Map(prev);
          newCarts.set(tableNumber, newCart);
          return newCarts;
        }

        // Check if product already exists in cart
        const existingItemIndex = prevCart.items.findIndex(
          (cartItem: CartItem) => cartItem.productId === item.productId
        );

        if (existingItemIndex >= 0) {
          // Update existing item quantity
          const updatedItems = [...prevCart.items];
          const existingItem = updatedItems[existingItemIndex];
          const newQuantity = existingItem.quantity + item.quantity;
          const newTotalPrice = newQuantity * existingItem.unitPrice;
          
          updatedItems[existingItemIndex] = {
            ...existingItem,
            quantity: newQuantity,
            totalPrice: newTotalPrice
          };

          const updatedCart: Cart = {
            ...prevCart,
            items: updatedItems,
            totalItems: updatedItems.reduce((sum: number, item: CartItem) => sum + item.quantity, 0),
            subtotal: updatedItems.reduce((sum: number, item: CartItem) => sum + item.totalPrice, 0),
            totalTax: updatedItems.reduce((sum: number, item: CartItem) => sum + item.totalPrice, 0) * 0.20,
            grandTotal: updatedItems.reduce((sum: number, item: CartItem) => sum + item.totalPrice, 0) * 1.20
          };
          
          const newCarts = new Map(prev);
          newCarts.set(tableNumber, updatedCart);
          return newCarts;
        } else {
          // Add new item
          const newItems = [...prevCart.items, newItem];
          
          const updatedCart: Cart = {
            ...prevCart,
            items: newItems,
            totalItems: newItems.reduce((sum: number, item: CartItem) => sum + item.quantity, 0),
            subtotal: newItems.reduce((sum: number, item: CartItem) => sum + item.totalPrice, 0),
            totalTax: newItems.reduce((sum: number, item: CartItem) => sum + item.totalPrice, 0) * 0.20,
            grandTotal: newItems.reduce((sum: number, item: CartItem) => sum + item.totalPrice, 0) * 1.20
          };
          
          const newCarts = new Map(prev);
          newCarts.set(tableNumber, updatedCart);
          return newCarts;
        }
      });

      console.log('✅ Ürün local state\'e başarıyla eklendi');
      return { success: true, message: 'Item added successfully' };

    } catch (error: any) {
      const errorMessage = 'Failed to add item to cart';
      setErrorState(errorMessage);
      console.error('❌ Error adding to cart:', error);
      return { success: false, message: errorMessage };
    } finally {
      setLoadingState(false);
    }
  }, [apiCall, setCachedData, updateTableCarts, setLoadingState, setErrorState]);

  // Load cart from backend for specific table
  const loadCartForTable = useCallback(async (tableNumber: number): Promise<{ success: boolean; error?: string; cart: Cart | null }> => {
    if (!tableNumber) {
      const errorMsg = 'Table number is required for loading cart';
      setErrorState(errorMsg);
      return { success: false, error: errorMsg, cart: null };
    }

    try {
      setLoadingState(true);
      setErrorState(null);
      
      console.log('🛒 Masa', tableNumber, 'sepeti yükleniyor...');
      
      // Cache kontrolü
      const cachedCart = getCachedData<Cart>(`cart-${tableNumber}`);
      if (cachedCart) {
        console.log('✅ Cache hit for table', tableNumber);
        updateTableCarts(prev => {
          const newCarts = new Map(prev);
          newCarts.set(tableNumber, cachedCart);
          return newCarts;
        });
        return { success: true, cart: cachedCart };
      }

      // API çağrısı
      const result = await apiCall(
        `load-cart-${tableNumber}`,
        async () => {
          const backendCart = await cartService.getCurrentCart(tableNumber);
          if (backendCart) {
            return backendCart;
          }
          throw new Error('No cart found');
        },
        {
          cacheKey: `cart-${tableNumber}`,
          cacheTTL: 2, // 2 dakika cache
          skipDuplicate: true,
        }
      );

      if (result) {
        // Backend'den gelen sepeti masa bazlı olarak sakla
        updateTableCarts(prev => {
          const newCarts = new Map(prev);
          newCarts.set(tableNumber, result);
          return newCarts;
        });
        
        console.log('✅ Masa', tableNumber, 'sepeti backend\'den başarıyla yüklendi');
        return { success: true, cart: result };
      }
      
      // Hiç sepet yoksa boş sepet oluştur
      console.log('✅ Masa', tableNumber, 'için yeni sepet oluşturuldu');
      return { success: true, cart: null };
      
    } catch (error: any) {
      console.error('❌ Masa', tableNumber, 'sepeti yükleme hatası:', error);
      const errorMessage = error?.message || 'Failed to load cart';
      setErrorState(errorMessage);
      return { success: false, error: errorMessage, cart: null };
    } finally {
      setLoadingState(false);
    }
  }, [apiCall, getCachedData, setCachedData, updateTableCarts, setLoadingState, setErrorState]);

  // Update item quantity in cart
  const updateItemQuantity = useCallback(async (tableNumber: number, itemId: string, newQuantity: number) => {
    if (!tableNumber) {
      setErrorState('Table number is required for updating item');
      return;
    }

    try {
      setLoadingState(true);
      setErrorState(null);

      console.log('🔄 Ürün miktarı güncelleniyor:', { tableNumber, itemId, newQuantity });

      // API çağrısı
      await apiCall(
        `update-quantity-${tableNumber}-${itemId}`,
        async () => {
          const response = await cartService.updateCartItem(itemId, { quantity: newQuantity });
          if (response && response.success) {
            return response;
          }
          throw new Error('Backend failed, using local fallback');
        },
        {
          skipDuplicate: true,
        }
      );

      // Local fallback
      updateTableCarts(prev => {
        const prevCart = prev.get(tableNumber);
        if (!prevCart) return prev;

        const updatedItems = prevCart.items.map(item => 
          item.id === itemId 
            ? { ...item, quantity: newQuantity, totalPrice: newQuantity * item.unitPrice }
            : item
        );

        const updatedCart: Cart = {
          ...prevCart,
          items: updatedItems,
          totalItems: updatedItems.reduce((sum: number, item: CartItem) => sum + item.quantity, 0),
          subtotal: updatedItems.reduce((sum: number, item: CartItem) => sum + item.totalPrice, 0),
          totalTax: updatedItems.reduce((sum: number, item: CartItem) => sum + item.totalPrice, 0) * 0.20,
          grandTotal: updatedItems.reduce((sum: number, item: CartItem) => sum + item.totalPrice, 0) * 1.20
        };

        const newCarts = new Map(prev);
        newCarts.set(tableNumber, updatedCart);
        return newCarts;
      });

      // Cache'i güncelle
      const updatedCart = getCartForTable(tableNumber);
      if (updatedCart) {
        setCachedData(`cart-${tableNumber}`, updatedCart, 2);
      }

      console.log('✅ Ürün miktarı güncellendi');

    } catch (error) {
      const errorMessage = 'Failed to update item quantity';
      setErrorState(errorMessage);
      console.error('❌ Error updating item quantity:', error);
    } finally {
      setLoadingState(false);
    }
  }, [apiCall, getCartForTable, setCachedData, updateTableCarts, setLoadingState, setErrorState]);

  // Remove item from cart
  const removeFromCart = useCallback(async (tableNumber: number, itemId: string) => {
    if (!tableNumber) {
      setErrorState('Table number is required for removing item');
      return;
    }

    try {
      setLoadingState(true);
      setErrorState(null);

      console.log('🗑️ Ürün sepetten kaldırılıyor:', { tableNumber, itemId });

      // API çağrısı
      await apiCall(
        `remove-item-${tableNumber}-${itemId}`,
        async () => {
          const response = await cartService.removeCartItem(itemId);
          if (response && response.success) {
            return response;
          }
          throw new Error('Backend failed, using local fallback');
        },
        {
          skipDuplicate: true,
        }
      );

      // Local fallback
      updateTableCarts(prev => {
        const prevCart = prev.get(tableNumber);
        if (!prevCart) return prev;

        const updatedItems = prevCart.items.filter(item => item.id !== itemId);

        if (updatedItems.length === 0) {
          // Cart is empty, remove it
          const newCarts = new Map(prev);
          newCarts.delete(tableNumber);
          return newCarts;
        }

        const updatedCart: Cart = {
          ...prevCart,
          items: updatedItems,
          totalItems: updatedItems.reduce((sum: number, item: CartItem) => sum + item.quantity, 0),
          subtotal: updatedItems.reduce((sum: number, item: CartItem) => sum + item.totalPrice, 0),
          totalTax: updatedItems.reduce((sum: number, item: CartItem) => sum + item.totalPrice, 0) * 0.20,
          grandTotal: updatedItems.reduce((sum: number, item: CartItem) => sum + item.totalPrice, 0) * 1.20
        };

        const newCarts = new Map(prev);
        newCarts.set(tableNumber, updatedCart);
        return newCarts;
      });

      // Cache'i güncelle
      const updatedCart = getCartForTable(tableNumber);
      if (updatedCart) {
        setCachedData(`cart-${tableNumber}`, updatedCart, 2);
      }

      console.log('✅ Ürün sepetten kaldırıldı');

    } catch (error) {
      const errorMessage = 'Failed to remove item from cart';
      setErrorState(errorMessage);
      console.error('❌ Error removing item from cart:', error);
    } finally {
      setLoadingState(false);
    }
  }, [apiCall, getCartForTable, setCachedData, updateTableCarts, setLoadingState, setErrorState]);

  // Clear cart for specific table
  const clearCartForTable = useCallback(async (tableNumber: number): Promise<{ success: boolean; message: string }> => {
    console.log('🧹 clearCartForTable called with tableNumber:', tableNumber);
    
    if (!tableNumber) {
      const errorMsg = 'Table number is required for clearing cart';
      setErrorState(errorMsg);
      return { success: false, message: errorMsg };
    }

    try {
      setLoadingState(true);
      setErrorState(null);

      console.log('🧹 Masa', tableNumber, 'sepeti temizleniyor...');

      // API çağrısı
      await apiCall(
        `clear-cart-${tableNumber}`,
        async () => {
          const response = await cartService.clearCart(tableNumber);
          if (response && response.success) {
            return response;
          }
          throw new Error('Backend failed, using local fallback');
        },
        {
          skipDuplicate: true,
        }
      );

      // Local fallback
      updateTableCarts(prev => {
        const newCarts = new Map(prev);
        newCarts.delete(tableNumber);
        return newCarts;
      });

      // Cache'i temizle
      setCachedData(`cart-${tableNumber}`, null, 0);

      console.log('✅ Masa', tableNumber, 'sepeti temizlendi');
      return { success: true, message: 'Cart cleared successfully' };

    } catch (error) {
      const errorMessage = 'Failed to clear cart';
      setErrorState(errorMessage);
      console.error('❌ Error clearing cart:', error);
      return { success: false, message: errorMessage };
    } finally {
      setLoadingState(false);
    }
  }, [apiCall, setCachedData, updateTableCarts, setLoadingState, setErrorState]);

  // Clear all carts for all tables
  const clearAllTables = useCallback(async () => {
    console.log('🧹 clearAllTables called');
    
    try {
      setLoadingState(true);
      setErrorState(null);

      console.log('🧹 TÜM MASALAR temizleniyor...');

      // API çağrısı
      const result = await apiCall(
        'clear-all-tables',
        async () => {
          const response = await cartService.clearAllCarts();
          if (response && response.success) {
            return response;
          }
          throw new Error('Backend failed, using local fallback');
        },
        {
          skipDuplicate: true,
        }
      );

      if (result && result.success) {
        // Local state'i tamamen temizle
        updateTableCarts(() => new Map());
        
        // Tüm cache'i temizle
        for (let i = 1; i <= 10; i++) {
          setCachedData(`cart-${i}`, null, 0);
        }
        
        console.log('✅ TÜM MASALAR temizlendi');
        return result;
      } else {
        return { success: false, message: 'Clear all failed', clearedCarts: 0, clearedItems: 0, affectedTables: [] };
      }
    } catch (error: any) {
      console.error('❌ TÜM MASALAR temizleme hatası:', error);
      const errorMessage = error?.message || 'Failed to clear all carts';
      setErrorState(errorMessage);
      return { success: false, message: errorMessage, clearedCarts: 0, clearedItems: 0, affectedTables: [] };
    } finally {
      setLoadingState(false);
    }
  }, [apiCall, setCachedData, updateTableCarts, setLoadingState, setErrorState]);

  // Set cart from external source (e.g., backend) - masa bazlı
  const setCartFromBackend = useCallback((backendCart: Cart, tableNumber: number) => {
    if (!tableNumber) {
      console.error('❌ Table number is required for setting cart from backend');
      return;
    }
    
    updateTableCarts(prev => {
      const newCarts = new Map(prev);
      newCarts.set(tableNumber, backendCart);
      return newCarts;
    });

    // Cache'i güncelle
    setCachedData(`cart-${tableNumber}`, backendCart, 2);
  }, [setCachedData, updateTableCarts]);

  // Tüm sepetleri temizle
  const clearAllCarts = useCallback(() => {
    console.log('🧹 All carts cleared');
    updateTableCarts(() => new Map());
    setErrorState(null);
  }, [updateTableCarts, setErrorState]);

  return {
    loading: loadingRef.current,
    error: errorRef.current,
    getCartForTable,
    addToCart,
    updateItemQuantity,
    removeFromCart,
    clearCartForTable,
    clearCart: clearCartForTable,
    clearAllTables,
    setCartFromBackend,
    loadCartForTable,
    clearAllCarts
  };
};

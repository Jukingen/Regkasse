import { useState, useCallback, useEffect } from 'react';
import { cartService } from '../services/api/cartService';
import { useAuth } from '../contexts/AuthContext';
import AsyncStorage from '@react-native-async-storage/async-storage';

// Cart item interface - API response formatına uygun
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

// Cart interface - CartService ile uyumlu
interface Cart {
  cartId: string;
  items: CartItem[];
  totalItems: number;
  subtotal: number;
  totalTax: number; // taxAmount yerine totalTax
  grandTotal: number;
  status: string;
  tableNumber?: number;
  createdAt: string;
  expiresAt?: string;
  waiterName?: string;
  customerId?: string;
  notes?: string;
}

export const useCart = () => {
  // Masa bazlı sepet yönetimi - her masa için ayrı sepet
  const [tableCarts, setTableCarts] = useState<Map<number, Cart>>(new Map());
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  
  // Auth context'ten kullanıcı bilgilerini al
  const { user, isAuthenticated } = useAuth();

  // 🧹 Token expire kontrolü
  const isTokenExpired = useCallback(async (): Promise<boolean> => {
    try {
      const token = await AsyncStorage.getItem('token');
      if (!token) return true;

      // JWT token'ı decode et ve expire kontrolü yap
      const payload = JSON.parse(atob(token.split('.')[1]));
      const currentTime = Math.floor(Date.now() / 1000);
      
      if (payload.exp && payload.exp < currentTime) {
        console.log('⚠️ Token expired, clearing carts...');
        return true;
      }
      
      return false;
    } catch (error) {
      console.error('Token expire check error:', error);
      return true;
    }
  }, []);

  // 🧹 Tüm sepetleri temizle
  const clearAllCarts = useCallback(() => {
    console.log('🧹 All carts cleared');
    setTableCarts(new Map());
    setError(null);
  }, []);

  // LOGOUT EVENT DİNLEYİCİSİ - Cache temizleme için (Platform-aware)
  useEffect(() => {
    const handleLogout = () => {
      console.log('🧹 Logout event received, clearing all carts...');
      clearAllCarts();
    };

    // Web platform kontrolü - React Native'de window objesi yok
    if (typeof window !== 'undefined' && window.addEventListener) {
      try {
        window.addEventListener('logout-clear-cache', handleLogout);
        console.log('✅ Web platform: logout event listener added');
        
        // Cleanup
        return () => {
          if (typeof window !== 'undefined' && window.removeEventListener) {
            window.removeEventListener('logout-clear-cache', handleLogout);
            console.log('✅ Web platform: logout event listener removed');
          }
        };
      } catch (error) {
        console.warn('⚠️ Failed to add window event listener:', error);
      }
    } else {
      console.log('📱 Mobile platform: window events not available, using direct method');
      // Mobile platformda direkt çağrı kullanılabilir (gerekirse)
    }
  }, [clearAllCarts]);

  // 🧹 TOKEN EXPIRE KONTROLÜ: 5 dakikada bir
  useEffect(() => {
    if (!isAuthenticated || !user) return;

    const checkTokenAndCleanup = async () => {
      if (!user?.token) {
        console.log('⚠️ No token found, clearing all carts...');
        clearAllCarts();
        return;
      }

      const expired = await isTokenExpired();
      if (expired) {
        console.log('⚠️ Token expired, clearing all carts...');
        clearAllCarts();
      }
    };

    checkTokenAndCleanup();
    
    // OPTIMIZATION: Her 5 dakika yerine 15 dakikada bir token kontrolü yap
    const interval = setInterval(checkTokenAndCleanup, 15 * 60 * 1000); // 15 dakika
    
    return () => clearInterval(interval);
  }, [isAuthenticated, user]); // ✅ YENİ: Minimal dependency - isTokenExpired ve clearAllCarts kaldırıldı

  // ⏰ OTOMATİK SEPET SIFIRLAMA: 15 dakika + gece 00:00
  useEffect(() => {
    if (!isAuthenticated || !user) return;

    const checkCartExpiration = () => {
      const now = new Date();
      const currentHour = now.getHours();
      const currentMinute = now.getMinutes();
      
      // Gece 00:00 kontrolü
      if (currentHour === 0 && currentMinute === 0) {
        console.log('🌙 Gece 00:00 - Tüm sepetler otomatik sıfırlanıyor...');
        clearAllCarts();
        return;
      }
      
      // 15 dakika kontrolü - her sepet için
      setTableCarts(prevTableCarts => {
        const newTableCarts = new Map(prevTableCarts);
        let hasExpiredCarts = false;
        
        for (const [tableNumber, cart] of prevTableCarts) {
          const cartCreatedAt = new Date(cart.createdAt);
          const timeDiff = now.getTime() - cartCreatedAt.getTime();
          const minutesDiff = Math.floor(timeDiff / (1000 * 60));
          
          if (minutesDiff >= 15) {
            console.log(`⏰ Masa ${tableNumber} sepeti 15 dakika geçti, sıfırlanıyor...`);
            newTableCarts.delete(tableNumber);
            hasExpiredCarts = true;
          }
        }
        
        if (hasExpiredCarts) {
          console.log('✅ Süresi dolan sepetler temizlendi');
        }
        
        return newTableCarts;
      });
    };

    // OPTIMIZATION: Her dakika yerine 5 dakikada bir kontrol et
    const interval = setInterval(checkCartExpiration, 5 * 60 * 1000); // 5 dakika
    
    // İlk kontrolü hemen yap
    checkCartExpiration();
    
    return () => clearInterval(interval);
  }, [isAuthenticated, user]); // ✅ YENİ: Minimal dependency - clearAllCarts kaldırıldı

  // Sepet getter fonksiyonu - tableNumber parametresi ile
  const getCartForTable = useCallback((tableNumber: number) => {
    return tableCarts.get(tableNumber) || null;
  }, []); // Dependency array boş - fonksiyon sürekli yeniden oluşturulmasın

  // Add item to cart with backend API integration
  const addToCart = useCallback(async (item: Omit<CartItem, 'id' | 'totalPrice'>, tableNumber: number): Promise<{ success: boolean; message: string; cart?: Cart }> => {
            if (!tableNumber) {
          console.error('❌ Table number is required for cart operations');
          setError('Table number is required');
          return { success: false, message: 'Table number is required' };
        }

        // 🧹 Token expire kontrolü
        const expired = await isTokenExpired();
        if (expired) {
          console.error('❌ Token expired, cannot add to cart');
          setError('Session expired, please login again');
          clearAllCarts();
          return { success: false, message: 'Session expired, please login again' };
        }

    try {
      setLoading(true);
      setError(null);

      console.log('🛒 Ürün sepete ekleniyor:', { item, tableNumber });

      // Try to add to backend first
      try {
        const response = await cartService.addItemToCart({
          productId: item.productId,
          quantity: item.quantity,
          notes: item.notes || undefined,
          tableNumber: tableNumber
        });

        if (response && response.cart) {
          // Backend success - update local state with backend data
          setTableCarts(prevTableCarts => {
            const newTableCarts = new Map(prevTableCarts);
            newTableCarts.set(tableNumber, response.cart);
            return newTableCarts;
          });
          console.log('✅ Ürün backend\'e başarıyla eklendi');
          return { success: true, message: 'Item added successfully', cart: response.cart };
        }
      } catch (apiError) {
        console.warn('⚠️ Backend API failed, using local fallback:', apiError);
        // Continue with local fallback
      }

      // Fallback to local state management
      const newItem: CartItem = {
        ...item,
        id: `local_${Date.now()}_${Math.random().toString(36).substr(2, 9)}`, // Unique local ID
        totalPrice: item.quantity * item.unitPrice
      };

      let updatedCart: Cart | undefined;

      setTableCarts(prevTableCarts => {
        const prevCart = prevTableCarts.get(tableNumber);
        
        if (!prevCart) {
          // Create new cart for this table
          const newCart: Cart = {
            cartId: `local_cart_${tableNumber}_${Date.now()}`,
            items: [newItem],
            totalItems: newItem.quantity,
            subtotal: newItem.totalPrice,
            totalTax: newItem.totalPrice * 0.20, // 20% tax
            grandTotal: newItem.totalPrice * 1.20,
            status: 'active',
            tableNumber: tableNumber,
            createdAt: new Date().toISOString(),
            expiresAt: new Date(Date.now() + 24 * 60 * 60 * 1000).toISOString(), // 24 hours
            waiterName: undefined,
            customerId: undefined,
            notes: undefined
          };
          
          const newTableCarts = new Map(prevTableCarts);
          newTableCarts.set(tableNumber, newCart);
          updatedCart = newCart;
          return newTableCarts;
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

          const updatedCartLocal: Cart = {
            ...prevCart,
            items: updatedItems, // Keep original order
            totalItems: updatedItems.reduce((sum: number, item: CartItem) => sum + item.quantity, 0),
            subtotal: updatedItems.reduce((sum: number, item: CartItem) => sum + item.totalPrice, 0),
            totalTax: updatedItems.reduce((sum: number, item: CartItem) => sum + item.totalPrice, 0) * 0.20,
            grandTotal: updatedItems.reduce((sum: number, item: CartItem) => sum + item.totalPrice, 0) * 1.20
          };
          
          const newTableCarts = new Map(prevTableCarts);
          newTableCarts.set(tableNumber, updatedCartLocal);
          updatedCart = updatedCartLocal;
          return newTableCarts;
        } else {
          // Add new item to the end of the list (maintain order)
          const newItems = [...prevCart.items, newItem];
          
          const updatedCart: Cart = {
            ...prevCart,
            items: newItems, // New item added to the end
            totalItems: newItems.reduce((sum: number, item: CartItem) => sum + item.quantity, 0),
            subtotal: newItems.reduce((sum: number, item: CartItem) => sum + item.totalPrice, 0),
            totalTax: newItems.reduce((sum: number, item: CartItem) => sum + item.totalPrice, 0) * 0.20,
            grandTotal: newItems.reduce((sum: number, item: CartItem) => sum + item.totalPrice, 0) * 1.20
          };
          
          const newTableCarts = new Map(prevTableCarts);
          newTableCarts.set(tableNumber, updatedCart);
          return newTableCarts;
        }
      });

      console.log('✅ Ürün local state\'e başarıyla eklendi');
      return { success: true, message: 'Item added successfully', cart: updatedCart };

    } catch (error) {
      const errorMessage = 'Failed to add item to cart';
      setError(errorMessage);
      console.error('❌ Error adding to cart:', error);
      return { success: false, message: errorMessage };
    } finally {
      setLoading(false);
    }
  }, [isTokenExpired, clearAllCarts]);

  // Load cart from backend for specific table
  const loadCartForTable = useCallback(async (tableNumber: number): Promise<{ success: boolean; error?: string; cart: Cart | null }> => {
    if (!tableNumber) {
      console.error('❌ Table number is required for loading cart');
      setError('Table number is required');
      return { success: false, error: 'Table number is required', cart: null };
    }

    // 🧹 Token expire kontrolü
    const expired = await isTokenExpired();
    if (expired) {
      console.error('❌ Token expired, cannot load cart');
      setError('Session expired, please login again');
      clearAllCarts();
      return { success: false, error: 'Session expired', cart: null };
    }

    setLoading(true);
    setError(null);
    
    try {
      console.log('🛒 Masa', tableNumber, 'sepeti yükleniyor...');
      
      // Backend'den sepet yüklemeyi dene
      try {
        const backendCart = await cartService.getCurrentCart(tableNumber);
        if (backendCart) {
          console.log('📦 Backend\'den gelen sepet verileri:', {
            cartId: backendCart.cartId,
            tableNumber: backendCart.tableNumber,
            itemsCount: backendCart.items?.length || 0,
            items: backendCart.items?.map(item => ({
              id: item.id,
              productId: item.productId,
              productName: item.productName,
              quantity: item.quantity,
              unitPrice: item.unitPrice,
              totalPrice: item.totalPrice
            })) || []
          });
          
          // Backend'den gelen sepeti masa bazlı olarak sakla (boş olsa bile)
          setTableCarts(prevTableCarts => {
            const newTableCarts = new Map(prevTableCarts);
            newTableCarts.set(tableNumber, backendCart);
            return newTableCarts;
          });
          
          if (backendCart.items && backendCart.items.length > 0) {
            console.log('✅ Masa', tableNumber, 'sepeti backend\'den başarıyla yüklendi (items var)');
          } else {
            console.log('✅ Masa', tableNumber, 'sepeti backend\'den başarıyla yüklendi (boş sepet)');
          }
          return { success: true, cart: backendCart };
        }
      } catch (backendError) {
        console.warn('⚠️ Backend sepet yükleme hatası, local sepet kontrol ediliyor:', backendError);
      }
      
      // Backend başarısız olursa local sepeti kontrol et
      const localCart = tableCarts.get(tableNumber);
      if (localCart) {
        console.log('✅ Masa', tableNumber, 'local sepeti yüklendi');
        return { success: true, cart: localCart };
      }
      
      // Hiç sepet yoksa boş sepet oluştur
      console.log('✅ Masa', tableNumber, 'için yeni sepet oluşturuldu');
      return { success: true, cart: null };
      
    } catch (error: any) {
      console.error('❌ Masa', tableNumber, 'sepeti yükleme hatası:', error);
      const errorMessage = error?.message || 'Failed to load cart';
      setError(errorMessage);
      return { success: false, error: errorMessage, cart: null };
    } finally {
      setLoading(false);
    }
  }, [isTokenExpired, clearAllCarts]);

  // Update item quantity in cart
  const updateItemQuantity = useCallback(async (tableNumber: number, itemId: string, newQuantity: number) => {
    if (!tableNumber) {
      console.error('❌ Table number is required for updating item');
      setError('Table number is required');
      return;
    }

    // 🧹 Token expire kontrolü
    const expired = await isTokenExpired();
    if (expired) {
      console.error('❌ Token expired, cannot update cart');
      setError('Session expired, please login again');
      clearAllCarts();
      return;
    }

    try {
      setLoading(true);
      setError(null);

      console.log('🔄 Ürün miktarı güncelleniyor:', { tableNumber, itemId, newQuantity });

      // Try to update backend first
      try {
        const response = await cartService.updateCartItem(itemId, { quantity: newQuantity });
        if (response && response.success) {
          console.log('✅ Ürün miktarı backend\'de güncellendi');
          // Backend success - reload cart to get updated data
          await loadCartForTable(tableNumber);
          return;
        }
      } catch (apiError) {
        console.warn('⚠️ Backend API failed, using local fallback:', apiError);
        // Continue with local fallback
      }

      // Local fallback
      setTableCarts(prevTableCarts => {
        const prevCart = prevTableCarts.get(tableNumber);
        if (!prevCart) return prevTableCarts;

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

        const newTableCarts = new Map(prevTableCarts);
        newTableCarts.set(tableNumber, updatedCart);
        return newTableCarts;
      });

      console.log('✅ Ürün miktarı local state\'de güncellendi');

    } catch (error) {
      const errorMessage = 'Failed to update item quantity';
      setError(errorMessage);
      console.error('❌ Error updating item quantity:', error);
    } finally {
      setLoading(false);
    }
  }, [isTokenExpired, clearAllCarts, loadCartForTable]);

  // Remove item from cart
  const removeFromCart = useCallback(async (tableNumber: number, itemId: string) => {
    if (!tableNumber) {
      console.error('❌ Table number is required for removing item');
      setError('Table number is required');
      return;
    }

    // 🧹 Token expire kontrolü
    const expired = await isTokenExpired();
    if (expired) {
      console.error('❌ Token expired, cannot remove from cart');
      setError('Session expired, please login again');
      clearAllCarts();
      return;
    }

    try {
      setLoading(true);
      setError(null);

      console.log('🗑️ Ürün sepetten kaldırılıyor:', { tableNumber, itemId });

      // Try to remove from backend first
      try {
        const response = await cartService.removeCartItem(itemId);
        if (response && response.success) {
          console.log('✅ Ürün backend\'den kaldırıldı');
          // Backend success - reload cart to get updated data
          await loadCartForTable(tableNumber);
          return;
        }
      } catch (apiError) {
        console.warn('⚠️ Backend API failed, using local fallback:', apiError);
        // Continue with local fallback
      }

      // Local fallback
      setTableCarts(prevTableCarts => {
        const prevCart = prevTableCarts.get(tableNumber);
        if (!prevCart) return prevTableCarts;

        const updatedItems = prevCart.items.filter(item => item.id !== itemId);

        if (updatedItems.length === 0) {
          // Cart is empty, remove it
          const newTableCarts = new Map(prevTableCarts);
          newTableCarts.delete(tableNumber);
          return newTableCarts;
        }

        const updatedCart: Cart = {
          ...prevCart,
          items: updatedItems,
          totalItems: updatedItems.reduce((sum: number, item: CartItem) => sum + item.quantity, 0),
          subtotal: updatedItems.reduce((sum: number, item: CartItem) => sum + item.totalPrice, 0),
          totalTax: updatedItems.reduce((sum: number, item: CartItem) => sum + item.totalPrice, 0) * 0.20,
          grandTotal: updatedItems.reduce((sum: number, item: CartItem) => sum + item.totalPrice, 0) * 1.20
        };

        const newTableCarts = new Map(prevTableCarts);
        newTableCarts.set(tableNumber, updatedCart);
        return newTableCarts;
      });

      console.log('✅ Ürün local state\'den kaldırıldı');

    } catch (error) {
      const errorMessage = 'Failed to remove item from cart';
      setError(errorMessage);
      console.error('❌ Error removing item from cart:', error);
    } finally {
      setLoading(false);
    }
  }, [isTokenExpired, clearAllCarts, loadCartForTable]);

  // Clear all carts for all tables
  const clearAllTables = useCallback(async () => {
    console.log('🧹 clearAllTables called');
    
    // 🧹 Token expire kontrolü
    const expired = await isTokenExpired();
    if (expired) {
      console.error('❌ Token expired, cannot clear all carts');
      setError('Session expired, please login again');
      clearAllCarts();
      return { success: false, message: 'Session expired' };
    }

    try {
      setLoading(true);
      setError(null);

      console.log('🧹 TÜM MASALAR temizleniyor (DANGEROUS OPERATION)...');
      console.log('🔍 About to call cartService.clearAllCarts()...');

      // Backend'den tüm sepetleri temizle
      console.log('🚀 Calling cartService.clearAllCarts() now...');
      const response = await cartService.clearAllCarts();
      console.log('📦 cartService.clearAllCarts() response received:', response);
      
      if (response && response.success) {
        console.log('✅ TÜM MASALAR backend\'de temizlendi');
        
        // Local state'i tamamen temizle
        setTableCarts(new Map());
        
        console.log('✅ TÜM MASALAR local state\'de temizlendi');
        return response;
      } else {
        console.error('❌ TÜM MASALAR temizleme başarısız');
        return response || { success: false, message: 'Clear all failed', clearedCarts: 0, clearedItems: 0, affectedTables: [] };
      }
    } catch (error: any) {
      console.error('❌ TÜM MASALAR temizleme hatası:', error);
      const errorMessage = error?.message || 'Failed to clear all carts';
      setError(errorMessage);
      return { success: false, message: errorMessage, clearedCarts: 0, clearedItems: 0, affectedTables: [] };
    } finally {
      setLoading(false);
    }
  }, [isTokenExpired, clearAllCarts]);

  // Clear cart for specific table
  const clearCartForTable = useCallback(async (tableNumber: number): Promise<{ success: boolean; message: string }> => {
    console.log('🧹 clearCartForTable called with tableNumber:', tableNumber);
    
    if (!tableNumber) {
      console.error('❌ Table number is required for clearing cart');
      setError('Table number is required');
      return { success: false, message: 'Table number is required' };
    }

    // 🧹 Token expire kontrolü
    const expired = await isTokenExpired();
    if (expired) {
      console.error('❌ Token expired, cannot clear cart');
      setError('Session expired, please login again');
      clearAllCarts();
      return { success: false, message: 'Session expired, please login again' };
    }

    try {
      setLoading(true);
      setError(null);

      console.log('🧹 SADECE Masa', tableNumber, 'sepeti temizleniyor (diğer masalar korunuyor)...');
      console.log('🔍 About to call cartService.clearCart()...');

      // Try to clear backend first
      try {
        console.log('🚀 Calling cartService.clearCart() for table:', tableNumber);
        const response = await cartService.clearCart(tableNumber);
        console.log('📦 cartService.clearCart() response:', response);
        
        if (response && response.success) {
          console.log('✅ Masa', tableNumber, 'sepeti backend\'de temizlendi');
        } else {
          console.warn('⚠️ Backend clear cart response not successful:', response);
        }
      } catch (apiError) {
        console.warn('⚠️ Backend API failed, using local fallback:', apiError);
        // Continue with local fallback
      }

      // Local fallback
      setTableCarts(prevTableCarts => {
        const newTableCarts = new Map(prevTableCarts);
        newTableCarts.delete(tableNumber);
        return newTableCarts;
      });

      console.log('✅ Masa', tableNumber, 'sepeti local state\'de temizlendi');
      return { success: true, message: 'Cart cleared successfully' };

    } catch (error) {
      const errorMessage = 'Failed to clear cart';
      setError(errorMessage);
      console.error('❌ Error clearing cart:', error);
      return { success: false, message: errorMessage };
    } finally {
      setLoading(false);
    }
  }, [isTokenExpired, clearAllCarts]);

  // Set cart from external source (e.g., backend) - masa bazlı
  const setCartFromBackend = useCallback((backendCart: Cart, tableNumber: number) => {
    if (!tableNumber) {
      console.error('❌ Table number is required for setting cart from backend');
      return;
    }
    
    setTableCarts(prevTableCarts => {
      const newTableCarts = new Map(prevTableCarts);
      newTableCarts.set(tableNumber, backendCart);
      return newTableCarts;
    });
  }, []);

  // KAPSAMLI CACHE TEMİZLEME - Logout sırasında çağrılmalı
  // const clearAllCarts = useCallback(() => {
  //   console.log('🧹 Tüm masa sepetleri temizleniyor...');
  //   setTableCarts(new Map());
  //   setLoading(false);
  //   setError(null);
  // }, []);

  return {
    loading,
    error,
    getCartForTable,
    addToCart,
    updateItemQuantity,
    removeFromCart,
    clearCartForTable,
    clearCart: clearCartForTable, // clearCart alias eklendi
    clearAllTables, // Tüm masaları temizle
    setCartFromBackend,
    loadCartForTable,
    clearAllCarts
  };
};

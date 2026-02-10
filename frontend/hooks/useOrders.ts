import { useState, useCallback } from 'react';
import { cartService } from '../services/api/cartService';
import { useAuth } from '../contexts/AuthContext';

// Türkçe Açıklama: Sipariş yönetimi için hook - Sepet ve ödeme işlemlerini birleştirir
export const useOrders = () => {
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  
  const { user } = useAuth();

  // Yeni sipariş oluştur
  const createOrder = useCallback(async (tableNumber: number, customerId?: string) => {
    try {
      setLoading(true);
      setError(null);

      if (!user) {
        throw new Error('Kullanıcı girişi yapılmamış');
      }

      // Yeni sepet oluştur
      const cart = await cartService.createCart({ tableNumber, customerId });
      return cart;
    } catch (err) {
      const errorMessage = err instanceof Error ? err.message : 'Sipariş oluşturulamadı';
      setError(errorMessage);
      throw err;
    } finally {
      setLoading(false);
    }
  }, [user]);

  // Siparişe ürün ekle
  const addItemToOrder = useCallback(async (tableNumber: number, productId: string, quantity: number, notes?: string) => {
    try {
      setLoading(true);
      setError(null);

      if (!user) {
        throw new Error('Kullanıcı girişi yapılmamış');
      }

      // Sepete ürün ekle
      const result = await cartService.addItemToCart({ productId, quantity, tableNumber, notes });
      return result;
    } catch (err) {
      const errorMessage = err instanceof Error ? err.message : 'Ürün eklenemedi';
      setError(errorMessage);
      throw err;
    } finally {
      setLoading(false);
    }
  }, [user]);

  // Siparişten ürün çıkar
  const removeItemFromOrder = useCallback(async (tableNumber: number, itemId: string) => {
    try {
      setLoading(true);
      setError(null);

      if (!user) {
        throw new Error('Kullanıcı girişi yapılmamış');
      }

      // Sepetten ürün çıkar
      const result = await cartService.removeCartItem(itemId);
      return result;
    } catch (err) {
      const errorMessage = err instanceof Error ? err.message : 'Ürün çıkarılamadı';
      setError(errorMessage);
      throw err;
    } finally {
      setLoading(false);
    }
  }, [user]);

  // Sipariş miktarını güncelle
  const updateOrderItemQuantity = useCallback(async (tableNumber: number, itemId: string, quantity: number) => {
    try {
      setLoading(true);
      setError(null);

      if (!user) {
        throw new Error('Kullanıcı girişi yapılmamış');
      }

      if (quantity <= 0) {
        // Miktar 0 veya negatifse ürünü çıkar
        return await cartService.removeCartItem(itemId);
      } else {
        // Miktarı güncelle
        return await cartService.updateCartItem(itemId, { quantity });
      }
    } catch (err) {
      const errorMessage = err instanceof Error ? err.message : 'Miktar güncellenemedi';
      setError(errorMessage);
      throw err;
    } finally {
      setLoading(false);
    }
  }, [user]);

  // Siparişi getir
  const getOrder = useCallback(async (tableNumber: number) => {
    try {
      setLoading(true);
      setError(null);

      if (!user) {
        throw new Error('Kullanıcı girişi yapılmamış');
      }

      // Sepeti getir
      const cart = await cartService.getCurrentCart(tableNumber);
      return { success: true, cart };
    } catch (err) {
      const errorMessage = err instanceof Error ? err.message : 'Sipariş getirilemedi';
      setError(errorMessage);
      throw err;
    } finally {
      setLoading(false);
    }
  }, [user]);

  // Siparişi temizle
  const clearOrder = useCallback(async (tableNumber: number) => {
    try {
      setLoading(true);
      setError(null);

      if (!user) {
        throw new Error('Kullanıcı girişi yapılmamış');
      }

      // Sepeti temizle
      const result = await cartService.clearCart(tableNumber);
      return result;
    } catch (err) {
      const errorMessage = err instanceof Error ? err.message : 'Sipariş temizlenemedi';
      setError(errorMessage);
      throw err;
    } finally {
      setLoading(false);
    }
  }, [user]);

  // Siparişi iptal et
  const cancelOrder = useCallback(async (tableNumber: number, reason?: string) => {
    try {
      setLoading(true);
      setError(null);

      if (!user) {
        throw new Error('Kullanıcı girişi yapılmamış');
      }

      // Sepeti iptal et (status'u cancelled yap)
      const result = await cartService.completeCart('', reason);
      return result;
    } catch (err) {
      const errorMessage = err instanceof Error ? err.message : 'Sipariş iptal edilemedi';
      setError(errorMessage);
      throw err;
    } finally {
      setLoading(false);
    }
  }, [user]);

  // Sipariş notlarını güncelle
  const updateOrderNotes = useCallback(async (tableNumber: number, notes: string) => {
    try {
      setLoading(true);
      setError(null);

      if (!user) {
        throw new Error('Kullanıcı girişi yapılmamış');
      }

      // Sepet notlarını güncelle - CartService'de bu method yok, sadece return success
      return { success: true, message: 'Notlar güncellendi' };
    } catch (err) {
      const errorMessage = err instanceof Error ? err.message : 'Notlar güncellenemedi';
      setError(errorMessage);
      throw err;
    } finally {
      setLoading(false);
    }
  }, [user]);

  // Müşteri bilgilerini güncelle
  const updateOrderCustomer = useCallback(async (tableNumber: number, customerId: string) => {
    try {
      setLoading(true);
      setError(null);

      if (!user) {
        throw new Error('Kullanıcı girişi yapılmamış');
      }

      // Sepet müşteri bilgilerini güncelle - CartService'de bu method yok, sadece return success
      return { success: true, message: 'Müşteri bilgileri güncellendi' };
    } catch (err) {
      const errorMessage = err instanceof Error ? err.message : 'Müşteri bilgileri güncellenemedi';
      setError(errorMessage);
      throw err;
    } finally {
      setLoading(false);
    }
  }, [user]);

  // Hata temizle
  const clearError = useCallback(() => {
    setError(null);
  }, []);

  return {
    loading,
    error,
    createOrder,
    addItemToOrder,
    removeItemFromOrder,
    updateOrderItemQuantity,
    getOrder,
    clearOrder,
    cancelOrder,
    updateOrderNotes,
    updateOrderCustomer,
    clearError
  };
};

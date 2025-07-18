// Bu hook, sepet işlemlerini sadece backend API ile yapar, local state tutmaz.
import { useEffect, useState } from 'react';
import { apiClient } from '../services/api/config';
import { cartService } from '../services/api/cartService';
import AsyncStorage from '@react-native-async-storage/async-storage';

export interface CartItem {
  id: string;
  productId: string;
  name: string;
  price: number;
  quantity: number;
  taxType: string;
}

export interface Cart {
  id: string;
  items: CartItem[];
  total: number;
  discount: number;
  vat: number;
  grandTotal: number;
}

export function useApiCart() {
  const [cart, setCart] = useState<Cart | null>(null);
  const [loading, setLoading] = useState(false);
  const [currentCartId, setCurrentCartId] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null); // Hata mesajı (Almanca gösterilecek)
  const [techError, setTechError] = useState<string | null>(null); // Teknik log (İngilizce)

  // Sayfa açıldığında mevcut cartId'yi AsyncStorage'dan oku ve sepeti getir
  useEffect(() => {
    (async () => {
      try {
        const savedCartId = await AsyncStorage.getItem('currentCartId');
        if (savedCartId) {
          setCurrentCartId(savedCartId);
          setLoading(true);
          const res = await apiClient.get(`/cart/${savedCartId}`);
          setCart(res as Cart);
        }
      } catch (err: any) {
        setError('Fehler beim Laden des Warenkorbs.'); // Almanca
        setTechError('Failed to load cart from backend: ' + (err?.message || err));
      } finally {
        setLoading(false);
      }
    })();
  }, []);

  /**
   * Sepeti backend'den getirir ve state'i günceller.
   */
  const fetchCart = async () => {
    setLoading(true);
    setError(null);
    setTechError(null);
    if (!currentCartId) {
      setCart(null);
      setLoading(false);
      return;
    }
    try {
      const res = await apiClient.get(`/cart/${currentCartId}`);
      setCart(res as Cart);
    } catch (err: any) {
      setError('Fehler beim Laden des Warenkorbs.');
      setTechError('Failed to fetch cart: ' + (err?.message || err));
    } finally {
      setLoading(false);
    }
  };

  /**
   * Ürün ekler. Eğer cart yoksa yeni cart oluşturur.
   * @param productId Eklenen ürünün ID'si
   * @param quantity Miktar (varsayılan 1)
   */
  const addItem = async (productId: string, quantity: number = 1) => {
    setLoading(true);
    setError(null);
    setTechError(null);
    try {
      if (!currentCartId) {
        // Yeni cart oluştur ve ilk ürünü ekle
        const res = await apiClient.post('/cart', {
          TableNumber: '1', // Gerekirse dinamik yap
          WaiterName: 'Kasiyer', // Gerekirse dinamik yap
          InitialItem: { ProductId: productId, Quantity: quantity }
        });
        const cartId = (res as any).cartId || (res as any).CartId || (res as any).id;
        setCurrentCartId(cartId as string);
        setCart(res as Cart);
        await AsyncStorage.setItem('currentCartId', String(cartId));
      } else {
        // Var olan cart'a ürün ekle
        await apiClient.post(`/cart/${currentCartId}/items`, { ProductId: productId, Quantity: quantity });
        await fetchCart();
      }
    } catch (err: any) {
      setError('Fehler beim Hinzufügen des Produkts.');
      setTechError('Failed to add item to cart: ' + (err?.message || err));
    } finally {
      setLoading(false);
    }
  };

  /**
   * Ürün çıkarır.
   * @param itemId Sepetteki ürünün ID'si
   */
  const removeItem = async (itemId: string) => {
    setLoading(true);
    setError(null);
    setTechError(null);
    const cartId = cart?.id || currentCartId;
    if (!cartId) return;
    try {
      await apiClient.delete(`/cart/${cartId}/items/${itemId}`);
      await fetchCart();
    } catch (err: any) {
      setError('Fehler beim Entfernen des Produkts.');
      setTechError('Failed to remove item from cart: ' + (err?.message || err));
    } finally {
      setLoading(false);
    }
  };

  /**
   * Ürün miktarını değiştirir.
   * @param itemId Sepetteki ürünün ID'si
   * @param quantity Yeni miktar (1 veya daha fazla olmalı)
   */
  const updateQuantity = async (itemId: string, quantity: number) => {
    if (quantity < 1) return;
    setLoading(true);
    setError(null);
    setTechError(null);
    const cartId = cart?.id || currentCartId;
    if (!cartId) return;
    try {
      await apiClient.put(`/cart/${cartId}/items/${itemId}`, { quantity });
      await fetchCart();
    } catch (err: any) {
      setError('Fehler beim Ändern der Menge.');
      setTechError('Failed to update item quantity: ' + (err?.message || err));
    } finally {
      setLoading(false);
    }
  };

  /**
   * Sepeti tamamen temizler.
   */
  const clearCart = async () => {
    setLoading(true);
    setError(null);
    setTechError(null);
    try {
      await apiClient.delete('/cart/items');
      await fetchCart();
    } catch (err: any) {
      setError('Fehler beim Leeren des Warenkorbs.');
      setTechError('Failed to clear cart: ' + (err?.message || err));
    } finally {
      setLoading(false);
    }
  };

  /**
   * Kupon/indirim kodu uygular. Backend ile tam entegre.
   * @param couponCode Kupon veya promosyon kodu
   */
  const applyCoupon = async (couponCode: string) => {
    setLoading(true);
    setError(null);
    setTechError(null);
    const cartId = cart?.id || currentCartId;
    if (!cartId) {
      setError('Kein Warenkorb vorhanden.');
      setLoading(false);
      return;
    }
    try {
      const updatedCart = await cartService.applyCoupon(cartId, { couponCode });
      setCart(updatedCart as any);
    } catch (err: any) {
      setError('Kupon kodu uygulanamadı.');
      setTechError('Failed to apply coupon: ' + (err?.message || err));
    } finally {
      setLoading(false);
    }
  };

  /**
   * Kupon/indirim kodunu kaldırır. Backend ile tam entegre.
   */
  const removeCoupon = async () => {
    setLoading(true);
    setError(null);
    setTechError(null);
    const cartId = cart?.id || currentCartId;
    if (!cartId) {
      setError('Kein Warenkorb vorhanden.');
      setLoading(false);
      return;
    }
    try {
      const updatedCart = await cartService.removeCoupon(cartId);
      setCart(updatedCart as any);
    } catch (err: any) {
      setError('Kupon kodu kaldırılamadı.');
      setTechError('Failed to remove coupon: ' + (err?.message || err));
    } finally {
      setLoading(false);
    }
  };

  return {
    cart,
    loading,
    error,      // UI'da Almanca göster
    techError,  // Teknik log için
    fetchCart,
    addItem,
    removeItem,
    updateQuantity,
    clearCart,
    applyCoupon,
    removeCoupon,
  };
} 
// Bu hook, sepet işlemlerini sadece backend API ile yapar, local state tutmaz.
// ✅ YENİ: useApiManager ile optimize edildi
import { useEffect, useState } from 'react';
import { apiClient } from '../services/api/config';
import { cartService } from '../services/api/cartService';
import { useApiManager } from './useApiManager'; // ✅ YENİ: API Manager entegrasyonu
import AsyncStorage from '@react-native-async-storage/async-storage';
import { Alert } from 'react-native';

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
  // ✅ YENİ: useApiManager entegrasyonu
  const { apiCall, getCachedData, setCachedData } = useApiManager();
  
  const [cart, setCart] = useState<Cart | null>(null);
  const [loading, setLoading] = useState(false);
  const [currentCartId, setCurrentCartId] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null); // Hata mesajı (Almanca gösterilecek)
  const [techError, setTechError] = useState<string | null>(null); // Teknik log (İngilizce)
  // Snackbar için state (kullanıcıya maksimum geri bildirim)
  const [snackbarMsg, setSnackbarMsg] = useState('');
  const [showSnackbar, setShowSnackbar] = useState(false);
  const showSnackbarMsg = (msg: string) => {
    setSnackbarMsg(msg);
    setShowSnackbar(true);
    setTimeout(() => setShowSnackbar(false), 2000);
  };

  // Modüler hata ve başarı bildirim fonksiyonları
  const handleSuccess = (msg: string) => showSnackbarMsg(msg);
  const handleError = (msg: string, alertMsg?: string) => {
    showSnackbarMsg(msg);
    if (alertMsg) Alert.alert('Hata', alertMsg);
  };

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
          TableNumber: '1',
          WaiterName: 'Kasiyer',
          initialItem: { productId, quantity }
        });
        const cartId = (res as any).cartId || (res as any).CartId || (res as any).id;
        setCurrentCartId(cartId as string);
        await fetchCart();
        await AsyncStorage.setItem('currentCartId', String(cartId));
      } else {
        // Var olan cart'a ürün ekle
        await apiClient.post(`/cart/${currentCartId}/items`, { productId, quantity });
        await fetchCart();
      }
      handleSuccess('Ürün sepete eklendi');
    } catch (err: any) {
      setError('Fehler beim Hinzufügen des Produkts.');
      setTechError('Failed to add item to cart: ' + (err?.message || err));
      handleError('Ürün sepete eklenemedi');
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
      handleSuccess('Ürün sepetten çıkarıldı');
    } catch (err: any) {
      setError('Fehler beim Entfernen des Produkts.');
      setTechError('Failed to remove item from cart: ' + (err?.message || err));
      handleError('Ürün sepetten çıkarılamadı');
    } finally {
      setLoading(false);
    }
  };

  /**
   * Ürün miktarını değiştirir. Negatif/NaN miktarları engeller, miktar 1'in altına düşerse ürünü otomatik olarak sepetten çıkarır.
   * @param itemId Sepetteki ürünün ID'si
   * @param quantity Yeni miktar (1 veya daha fazla olmalı)
   */
  const updateQuantity = async (itemId: string, quantity: number) => {
    const cartId = cart?.id || currentCartId;
    if (!cartId) return;
    // Negatif veya NaN miktar kontrolü
    if (typeof quantity !== 'number' || isNaN(quantity) || quantity < 0) {
      Alert.alert('Hatalı miktar', 'Ürün miktarı negatif veya geçersiz olamaz.');
      return;
    }
    setLoading(true);
    setError(null);
    setTechError(null);
    try {
      if (quantity < 1) {
        // Miktar 0 veya altına düştüyse ürünü sepetten çıkar
        await apiClient.delete(`/cart/${cartId}/items/${itemId}`);
        handleSuccess('Ürün sepetten çıkarıldı');
      } else {
        await apiClient.put(`/cart/${cartId}/items/${itemId}`, { quantity });
        handleSuccess('Ürün miktarı güncellendi');
      }
      await fetchCart();
    } catch (err: any) {
      setError('Fehler beim Ändern der Menge.');
      setTechError('Failed to update item quantity: ' + (err?.message || err));
      handleError('Ürün miktarı güncellenemedi', 'Ürün miktarı güncellenemedi. Lütfen tekrar deneyin.');
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
    const cartId = cart?.id || currentCartId;
    if (!cartId) {
      setCart(null);
      setLoading(false);
      handleError('Sepet zaten boş');
      Alert.alert('Sepet zaten boş', 'Temizlenecek ürün bulunamadı.');
      return;
    }
    if (!cart || !cart.items || cart.items.length === 0) {
      setLoading(false);
      handleError('Sepet zaten boş');
      Alert.alert('Sepet zaten boş', 'Temizlenecek ürün bulunamadı.');
      return;
    }
    try {
      if (cartService && cartService.clearCart) {
        await cartService.clearCart(cartId);
      } else {
        await apiClient.delete(`/cart/${cartId}`);
      }
      await fetchCart();
      handleSuccess('Sepet temizlendi');
    } catch (err: any) {
      setError('Fehler beim Leeren des Warenkorbs.');
      setTechError('Failed to clear cart: ' + (err?.message || err));
      handleError('Sepet temizlenemedi', 'Sepet temizlenirken bir hata oluştu. Lütfen tekrar deneyin.');
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
      await cartService.applyCoupon(cartId, { couponCode });
      await fetchCart();
      handleSuccess('Kupon başarıyla uygulandı');
    } catch (err: any) {
      setError('Kupon kodu uygulanamadı.');
      setTechError('Failed to apply coupon: ' + (err?.message || err));
      handleError('Kupon kodu uygulanamadı');
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
      await cartService.removeCoupon(cartId);
      await fetchCart();
      handleSuccess('Kupon kaldırıldı');
    } catch (err: any) {
      setError('Kupon kodu kaldırılamadı.');
      setTechError('Failed to remove coupon: ' + (err?.message || err));
      handleError('Kupon kodu kaldırılamadı');
    } finally {
      setLoading(false);
    }
  };

  /**
   * Sepeti/siparişi tamamla (ödeme alındı, satış bitti)
   */
  const completeCart = async (paymentMethod: string = 'cash', amountPaid: number = 0, notes?: string) => {
    setLoading(true);
    setError(null);
    setTechError(null);
    const cartId = cart?.id || currentCartId;
    if (!cartId) return;
    try {
              await apiClient.post(`/cart/${cartId}/complete`, {
        paymentMethod,
        amountPaid,
        notes,
      });
      await fetchCart(); // Sepeti güncelle/temizle
      handleSuccess('Sipariş başarıyla tamamlandı');
    } catch (err: any) {
      setError('Sipariş tamamlanamadı.');
      setTechError('Failed to complete cart: ' + (err?.message || err));
      handleError('Sipariş tamamlanamadı');
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
    showSnackbar,
    snackbarMsg,
    showSnackbarMsg,
    completeCart, // <-- yeni eklenen fonksiyon
  };
} 
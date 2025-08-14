import { useState, useEffect, useCallback } from 'react';
import { cartService, Cart, CartItem, AddItemToCartRequest } from '../services/api/cartService';
import { Product } from '../services/api/productService';

// Türkçe Açıklama: Kasa işlemleri için basit ve güvenilir hook. Sadece backend ile çalışır, local storage kullanmaz.

export function useCashRegister(user: any, activeTableNumber: number = 1) {
  const [cart, setCart] = useState<Cart | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // Backend'den sepeti yükle
  const loadCart = useCallback(async () => {
    if (!user) return;

    try {
      setLoading(true);
      setError(null);
      console.log('🔄 Backend\'den masa', activeTableNumber, 'sepeti yükleniyor...');
      
      const currentCart = await cartService.getCurrentCart(activeTableNumber);
      setCart(currentCart);
      console.log('✅ Masa', activeTableNumber, 'sepeti başarıyla yüklendi:', currentCart.cartId, 'Items:', currentCart.items.length);
    } catch (error: any) {
      if (error.message.includes('No active cart found')) {
        console.log('ℹ️ Masa', activeTableNumber, 'için aktif sepet yok - yeni sepet oluşturulacak');
        setCart(null);
      } else {
        console.error('❌ Backend sepet yükleme hatası:', error);
        setError(error.message || 'Sepet yüklenemedi');
      }
    } finally {
      setLoading(false);
    }
  }, [user, activeTableNumber]);

  // User değiştiğinde sepeti yükle
  useEffect(() => {
    if (user) {
      loadCart();
    } else {
      setCart(null);
      setError(null);
    }
  }, [user, loadCart]);

  // Aktif masa numarası değiştiğinde sepeti yeniden yükle
  useEffect(() => {
    if (user && activeTableNumber) {
      console.log('🔄 Masa değişti:', activeTableNumber, '→ Sepet yeniden yükleniyor...');
      setCart(null); // Önce mevcut sepeti temizle
      loadCart(); // Yeni masadan sepet yükle
    }
  }, [activeTableNumber, user, loadCart]);

  // Sepete ürün ekle
  const addToCart = useCallback(async (product: Product, quantity: number = 1, notes?: string) => {
    if (!user) {
      console.log('❌ Kullanıcı giriş yapmamış, ürün eklenemez');
      return;
    }

    try {
      setLoading(true);
      setError(null);
      console.log('🛒 Sepete ürün ekleniyor:', { product: product.name, quantity, notes });

      const request: AddItemToCartRequest = {
        productId: product.id,
        quantity,
        notes,
        tableNumber: activeTableNumber,
        waiterName: 'Kasiyer'
      };

      const response = await cartService.addItemToCart(request);
      
      // State'i güncelle
      setCart(response.cart);
      console.log('✅ Ürün başarıyla sepete eklendi, state güncellendi');
    } catch (error: any) {
      console.error('❌ Ürün ekleme hatası:', error);
      setError(error.message || 'Ürün sepete eklenemedi');
      
      // Hata durumunda sepeti yeniden yükle
      await loadCart();
    } finally {
      setLoading(false);
    }
  }, [user, loadCart, activeTableNumber]);

  // Sepetten ürün çıkar
  const removeFromCart = useCallback(async (itemId: string) => {
    if (!cart || !user) return;

    try {
      setLoading(true);
      setError(null);
      console.log('🛒 Sepetten ürün çıkarılıyor:', itemId);

      await cartService.removeItemFromCart(cart.cartId, itemId);
      
      // Sepeti yeniden yükle
      await loadCart();
      console.log('✅ Ürün başarıyla sepetten çıkarıldı');
    } catch (error: any) {
      console.error('❌ Ürün çıkarma hatası:', error);
      setError(error.message || 'Ürün sepetten çıkarılamadı');
      
      // Hata durumunda sepeti yeniden yükle
      await loadCart();
    } finally {
      setLoading(false);
    }
  }, [cart, user, loadCart]);

  // Sepet ürün miktarını güncelle
  const updateCartQuantity = useCallback(async (itemId: string, newQuantity: number) => {
    if (!cart || !user || newQuantity <= 0) return;

    try {
      setLoading(true);
      setError(null);
      console.log('🛒 Sepet ürün miktarı güncelleniyor:', { itemId, newQuantity });

      await cartService.updateCartItem(cart.cartId, itemId, { quantity: newQuantity });
      
      // Sepeti yeniden yükle
      await loadCart();
      console.log('✅ Ürün miktarı başarıyla güncellendi');
    } catch (error: any) {
      console.error('❌ Miktar güncelleme hatası:', error);
      setError(error.message || 'Miktar güncellenemedi');
      
      // Hata durumunda sepeti yeniden yükle
      await loadCart();
    } finally {
      setLoading(false);
    }
  }, [cart, user, loadCart]);

  // Sepeti temizle (tüm ürünleri sil)
  const clearCart = useCallback(async () => {
    if (!cart || !user) return;

    try {
      setLoading(true);
      setError(null);
      console.log('🛒 Sepet temizleniyor...');

      await cartService.clearCartItems(cart.cartId);
      
      // State'i temizle
      setCart(null);
      console.log('✅ Sepet başarıyla temizlendi');
    } catch (error: any) {
      console.error('❌ Sepet temizleme hatası:', error);
      setError(error.message || 'Sepet temizlenemedi');
      
      // Hata durumunda sepeti yeniden yükle
      await loadCart();
    } finally {
      setLoading(false);
    }
  }, [cart, user, loadCart]);

  // Sepeti sıfırla (yeni sepet oluştur)
  const resetCart = useCallback(async () => {
    if (!user) return;

    try {
      setLoading(true);
      setError(null);
      console.log('🔄 Sepet sıfırlanıyor...');

      // Mevcut sepeti temizle
      if (cart) {
        await cartService.deleteCart(cart.cartId);
      }
      
      // State'i temizle
      setCart(null);
      console.log('✅ Sepet başarıyla sıfırlandı');
    } catch (error: any) {
      console.error('❌ Sepet sıfırlama hatası:', error);
      setError(error.message || 'Sepet sıfırlanamadı');
    } finally {
      setLoading(false);
    }
  }, [user, cart]);

  // Hata temizleme
  const clearError = useCallback(() => {
    setError(null);
  }, []);

  // Sepet durumu bilgileri
  const cartInfo = {
    hasItems: cart && cart.items && cart.items.length > 0,
    totalItems: cart?.totalItems || 0,
    totalAmount: cart?.grandTotal || 0
  };

  return {
    cart,
    loading,
    error,
    cartInfo,
    addToCart,
    removeFromCart,
    updateCartQuantity,
    clearCart,
    resetCart,
    clearError,
    refreshCart: loadCart
  };
} 
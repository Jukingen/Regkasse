import { apiClient } from './config';

// Türkçe Açıklama: Sepet işlemleri için kapsamlı API servisi. Ürün ekleme, çıkarma, güncelleme, sepet görüntüleme ve yönetim işlevleri sağlar.

// Interface'ler
export interface CartItem {
  id: string;
  productId: string;
  productName: string;
  productImage?: string;
  quantity: number;
  unitPrice: number;
  totalPrice: number;
  notes?: string;
  taxType: string;
  taxRate: number;
}

export interface Cart {
  cartId: string;
  tableNumber?: number;
  waiterName?: string;
  customerId?: string;
  notes?: string;
  status: string;
  createdAt: string;
  expiresAt: string;
  items: CartItem[];
  totalItems: number;
  subtotal: number;
  totalTax: number;
  grandTotal: number;
}

export interface CreateCartRequest {
  tableNumber?: number;
  waiterName?: string;
  customerId?: string;
  notes?: string;
}

export interface AddItemToCartRequest {
  productId: string;
  quantity: number;
  tableNumber?: number;
  waiterName?: string;
  notes?: string;
}

export interface UpdateCartItemRequest {
  quantity: number;
  notes?: string;
}

export interface CartHistoryItem {
  cartId: string;
  tableNumber?: number;
  status: string;
  createdAt: string;
  completedAt?: string;
  totalItems: number;
  totalAmount: number;
}

export class CartService {
  private static instance: CartService;
  private currentCartId: string | null = null;

  static getInstance(): CartService {
    if (!CartService.instance) {
      CartService.instance = new CartService();
    }
    return CartService.instance;
  }

  // Mevcut kullanıcının sepetini getir
  async getCurrentCart(tableNumber: number = 1): Promise<Cart> {
    try {
      console.log('🛒 Masa', tableNumber, 'sepeti getiriliyor...');
      const response = await apiClient.get<Cart>(`/cart/current?tableNumber=${tableNumber}`);
      this.currentCartId = response.cartId;
      console.log('✅ Masa', tableNumber, 'sepeti başarıyla getirildi:', response.cartId);
      return response;
    } catch (error) {
      console.error('❌ Masa', tableNumber, 'sepeti getirme hatası:', error);
      throw new Error('Sepet getirilemedi');
    }
  }

  // Belirli bir sepeti getir
  async getCart(cartId: string): Promise<Cart> {
    try {
      console.log('🛒 Sepet getiriliyor:', cartId);
      const response = await apiClient.get<Cart>(`/cart/${cartId}`);
      this.currentCartId = cartId;
      console.log('✅ Sepet başarıyla getirildi');
      return response;
    } catch (error) {
      console.error('❌ Sepet getirme hatası:', error);
      throw new Error('Sepet getirilemedi');
    }
  }

  // Yeni sepet oluştur
  async createCart(request: CreateCartRequest): Promise<{ cartId: string; expiresAt: string }> {
    try {
      console.log('🛒 Yeni sepet oluşturuluyor:', request);
      const response = await apiClient.post<{ cartId: string; expiresAt: string }>('/cart', request);
      this.currentCartId = response.cartId;
      console.log('✅ Sepet başarıyla oluşturuldu:', response.cartId);
      return response;
    } catch (error: any) {
      if (error.status === 400 && error.data?.message?.includes('already has an active cart')) {
        // Kullanıcının zaten aktif sepeti var
        const existingCartId = error.data.cartId;
        this.currentCartId = existingCartId;
        console.log('ℹ️ Kullanıcının zaten aktif sepeti var:', existingCartId);
        return { cartId: existingCartId, expiresAt: new Date().toISOString() };
      }
      console.error('❌ Sepet oluşturma hatası:', error);
      throw new Error('Sepet oluşturulamadı');
    }
  }

  // Sepete ürün ekle (otomatik sepet oluşturma ile)
  async addItemToCart(request: AddItemToCartRequest): Promise<{ message: string; cart: Cart }> {
    try {
      console.log('🛒 Sepete ürün ekleniyor:', request);
      const response = await apiClient.post<{ message: string; cart: Cart }>('/cart/add-item', request);
      this.currentCartId = response.cart.cartId;
      console.log('✅ Ürün başarıyla sepete eklendi');
      return response;
    } catch (error) {
      console.error('❌ Ürün ekleme hatası:', error);
      throw new Error('Ürün sepete eklenemedi');
    }
  }

  // Belirli bir sepete ürün ekle
  async addItemToSpecificCart(cartId: string, request: AddItemToCartRequest): Promise<{ message: string }> {
    try {
      console.log('🛒 Belirli sepete ürün ekleniyor:', { cartId, request });
      const response = await apiClient.post<{ message: string }>(`/cart/${cartId}/items`, request);
      console.log('✅ Ürün başarıyla sepete eklendi');
      return response;
    } catch (error) {
      console.error('❌ Ürün ekleme hatası:', error);
      throw new Error('Ürün sepete eklenemedi');
    }
  }

  // Sepet ürününü güncelle
  async updateCartItem(cartId: string, itemId: string, request: UpdateCartItemRequest): Promise<{ message: string }> {
    try {
      console.log('🛒 Sepet ürünü güncelleniyor:', { cartId, itemId, request });
      const response = await apiClient.put<{ message: string }>(`/cart/${cartId}/items/${itemId}`, request);
      console.log('✅ Sepet ürünü başarıyla güncellendi');
      return response;
    } catch (error) {
      console.error('❌ Sepet ürünü güncelleme hatası:', error);
      throw new Error('Sepet ürünü güncellenemedi');
    }
  }

  // Sepetten ürün çıkar
  async removeItemFromCart(cartId: string, itemId: string): Promise<{ message: string }> {
    try {
      console.log('🛒 Sepetten ürün çıkarılıyor:', { cartId, itemId });
      const response = await apiClient.delete<{ message: string }>(`/cart/${cartId}/items/${itemId}`);
      console.log('✅ Ürün başarıyla sepetten çıkarıldı');
      return response;
    } catch (error) {
      console.error('❌ Ürün çıkarma hatası:', error);
      throw new Error('Ürün sepetten çıkarılamadı');
    }
  }

  // Sepeti temizle (tüm ürünleri sil)
  async clearCartItems(cartId: string): Promise<{ message: string }> {
    try {
      console.log('🛒 Sepet ürünleri temizleniyor:', cartId);
      const response = await apiClient.post<{ message: string }>(`/cart/${cartId}/clear-items`);
      console.log('✅ Sepet ürünleri başarıyla temizlendi');
      return response;
    } catch (error) {
      console.error('❌ Sepet temizleme hatası:', error);
      throw new Error('Sepet temizlenemedi');
    }
  }

  // Sepeti tamamen sil
  async deleteCart(cartId: string): Promise<{ message: string }> {
    try {
      console.log('🛒 Sepet siliniyor:', cartId);
      const response = await apiClient.delete<{ message: string }>(`/cart/${cartId}`);
      if (this.currentCartId === cartId) {
        this.currentCartId = null;
      }
      console.log('✅ Sepet başarıyla silindi');
      return response;
    } catch (error) {
      console.error('❌ Sepet silme hatası:', error);
      throw new Error('Sepet silinemedi');
    }
  }

  // Sepeti tamamla (siparişe dönüştür)
  async completeCart(cartId: string, notes?: string): Promise<{ message: string; cartId: string; totalItems: number; totalAmount: number }> {
    try {
      console.log('🛒 Sepet tamamlanıyor:', cartId);
      const response = await apiClient.post<{ message: string; cartId: string; totalItems: number; totalAmount: number }>(`/cart/${cartId}/complete`, { notes });
      if (this.currentCartId === cartId) {
        this.currentCartId = null;
      }
      console.log('✅ Sepet başarıyla tamamlandı');
      return response;
    } catch (error) {
      console.error('❌ Sepet tamamlama hatası:', error);
      throw new Error('Sepet tamamlanamadı');
    }
  }

  // Sepet geçmişini getir
  async getCartHistory(): Promise<CartHistoryItem[]> {
    try {
      console.log('🛒 Sepet geçmişi getiriliyor...');
      const response = await apiClient.get<CartHistoryItem[]>('/cart/history');
      console.log('✅ Sepet geçmişi başarıyla getirildi');
      return response;
    } catch (error) {
      console.error('❌ Sepet geçmişi getirme hatası:', error);
      throw new Error('Sepet geçmişi getirilemedi');
    }
  }

  // Mevcut sepet ID'sini al
  getCurrentCartId(): string | null {
    return this.currentCartId;
  }

  // Sepet ID'sini ayarla
  setCurrentCartId(cartId: string): void {
    this.currentCartId = cartId;
  }

  // Sepet ID'sini temizle
  clearCurrentCartId(): void {
    this.currentCartId = null;
  }
}

// Singleton instance'ı export et
export const cartService = CartService.getInstance(); 
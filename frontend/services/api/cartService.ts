import { apiClient } from './config';
import AsyncStorage from '@react-native-async-storage/async-storage';

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
  private tableCarts: Map<number, string> = new Map(); // Masa bazlı sepet ID'leri

  static getInstance(): CartService {
    if (!CartService.instance) {
      CartService.instance = new CartService();
    }
    return CartService.instance;
  }

  // 🧹 Token expire kontrolü
  private async isTokenExpired(): Promise<boolean> {
    try {
      const token = await AsyncStorage.getItem('token');
      if (!token) return true;

      // JWT token'ı decode et ve expire kontrolü yap
      const payload = JSON.parse(atob(token.split('.')[1]));
      const currentTime = Math.floor(Date.now() / 1000);
      
      if (payload.exp && payload.exp < currentTime) {
        console.log('⚠️ Token expired in CartService');
        return true;
      }
      
      return false;
    } catch (error) {
      console.error('Token expire check error in CartService:', error);
      return true;
    }
  }

  // 🔒 Güvenlik kontrolü
  private async checkSecurity(): Promise<void> {
    const expired = await this.isTokenExpired();
    if (expired) {
      throw new Error('Session expired, please login again');
    }
  }

  // Mevcut kullanıcının sepetini getir
  async getCurrentCart(tableNumber: number): Promise<Cart> {
    if (!tableNumber) {
      throw new Error('Table number is required');
    }

    // 🔒 Güvenlik kontrolü
    await this.checkSecurity();

    try {
      console.log('🛒 Masa', tableNumber, 'sepeti getiriliyor...');
      console.log('🔍 API endpoint: /cart/current?tableNumber=' + tableNumber);
      
      const response = await apiClient.get<Cart>(`/cart/current?tableNumber=${tableNumber}`);
      
      console.log('📦 API Response:', {
        cartId: response.cartId,
        tableNumber: response.tableNumber,
        status: response.status,
        itemsCount: response.items?.length ?? 0,
        items: response.items?.map(item => ({
          id: item.id,
          productId: item.productId,
          productName: item.productName,
          quantity: item.quantity,
          unitPrice: item.unitPrice,
          totalPrice: item.totalPrice
        })) ?? [],
        subtotal: response.subtotal,
        totalTax: response.totalTax,
        grandTotal: response.grandTotal
      });
      
      // Masa bazlı sepet ID'sini sakla
      this.tableCarts.set(tableNumber, response.cartId);
      
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
      console.log('✅ Sepet başarıyla getirildi');
      return response;
    } catch (error) {
      console.error('❌ Sepet getirme hatası:', error);
      throw new Error('Sepet getirilemedi');
    }
  }

  // Yeni sepet oluştur
  async createCart(request: CreateCartRequest): Promise<{ cartId: string; expiresAt: string }> {
    if (!request.tableNumber) {
      throw new Error('Table number is required for creating cart');
    }

    // 🔒 Güvenlik kontrolü
    await this.checkSecurity();

    try {
      console.log('🛒 Yeni sepet oluşturuluyor:', request);
      const response = await apiClient.post<{ cartId: string; expiresAt: string }>('/cart', request);
      
      // Masa bazlı sepet ID'sini sakla
      this.tableCarts.set(request.tableNumber, response.cartId);
      
      console.log('✅ Sepet başarıyla oluşturuldu:', response.cartId);
      return response;
    } catch (error: any) {
      if (error.status === 400 && error.data?.message?.includes('already has an active cart')) {
        // Kullanıcının zaten aktif sepeti var
        const existingCartId = error.data.cartId;
        this.tableCarts.set(request.tableNumber, existingCartId);
        console.log('ℹ️ Kullanıcının zaten aktif sepeti var:', existingCartId);
        return { cartId: existingCartId, expiresAt: new Date().toISOString() };
      }
      console.error('❌ Sepet oluşturma hatası:', error);
      throw new Error('Sepet oluşturulamadı');
    }
  }

  // Sepete ürün ekle (otomatik sepet oluşturma ile)
  async addItemToCart(request: AddItemToCartRequest): Promise<{ message: string; cart: Cart }> {
    if (!request.tableNumber) {
      throw new Error('Table number is required for adding item to cart');
    }

    // 🔒 Güvenlik kontrolü
    await this.checkSecurity();

    try {
      console.log('🛒 Sepete ürün ekleniyor:', request);
      const response = await apiClient.post<{ message: string; cart: Cart }>('/cart/add-item', request);
      
      // Masa bazlı sepet ID'sini güncelle
      this.tableCarts.set(request.tableNumber, response.cart.cartId);
      
      console.log('✅ Ürün başarıyla sepete eklendi');
      return response;
    } catch (error) {
      console.error('❌ Ürün ekleme hatası:', error);
      throw new Error('Ürün sepete eklenemedi');
    }
  }

  // Belirli bir sepete ürün ekle
  async addItemToSpecificCart(cartId: string, request: AddItemToCartRequest): Promise<{ message: string }> {
    // 🔒 Güvenlik kontrolü
    await this.checkSecurity();

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
  async updateCartItem(itemId: string, request: UpdateCartItemRequest): Promise<{ success: boolean; message: string }> {
    // 🔒 Güvenlik kontrolü
    await this.checkSecurity();

    try {
      console.log('🛒 Sepet ürünü güncelleniyor:', { itemId, request });
      const response = await apiClient.put<{ message: string }>(`/cart/items/${itemId}`, request);
      console.log('✅ Sepet ürünü başarıyla güncellendi');
      return { success: true, message: response.message };
    } catch (error) {
      console.error('❌ Sepet ürünü güncelleme hatası:', error);
      return { success: false, message: 'Sepet ürünü güncellenemedi' };
    }
  }

  // Sepetten ürün çıkar
  async removeCartItem(itemId: string): Promise<{ success: boolean; message: string }> {
    // 🔒 Güvenlik kontrolü
    await this.checkSecurity();

    try {
      console.log('🛒 Sepetten ürün çıkarılıyor:', { itemId });
      const response = await apiClient.delete<{ message: string }>(`/cart/items/${itemId}`);
      console.log('✅ Ürün başarıyla sepetten çıkarıldı');
      return { success: true, message: response.message };
    } catch (error) {
      console.error('❌ Ürün çıkarma hatası:', error);
      return { success: false, message: 'Ürün sepetten çıkarılamadı' };
    }
  }

  // Sepeti temizle (tüm ürünleri sil)
  async clearCartItems(cartId: string): Promise<{ message: string }> {
    // 🔒 Güvenlik kontrolü
    await this.checkSecurity();

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

  // Ödeme sonrası sepeti sıfırla ve yeni sipariş durumunu güncelle
  async resetCartAfterPayment(cartId: string, notes?: string): Promise<{ 
    message: string; 
    oldCartId: string; 
    newCartId: string; 
    tableNumber: number; 
    status: string 
  }> {
    // 🔒 Güvenlik kontrolü
    await this.checkSecurity();

    try {
      console.log('🛒 Ödeme sonrası sepet sıfırlanıyor:', cartId);
      const response = await apiClient.post<{ 
        message: string; 
        oldCartId: string; 
        newCartId: string; 
        tableNumber: number; 
        status: string 
      }>(`/cart/${cartId}/reset-after-payment`, { notes });
      
      // Yeni sepet ID'sini güncelle
      this.tableCarts.set(response.tableNumber, response.newCartId);
      
      console.log('✅ Ödeme sonrası sepet başarıyla sıfırlandı, yeni sepet ID:', response.newCartId);
      return response;
    } catch (error) {
      console.error('❌ Ödeme sonrası sepet sıfırlama hatası:', error);
      throw new Error('Ödeme sonrası sepet sıfırlanamadı');
    }
  }

  // Sepeti tamamen sil
  async deleteCart(cartId: string): Promise<{ message: string }> {
    // 🔒 Güvenlik kontrolü
    await this.checkSecurity();

    try {
      console.log('🛒 Sepet siliniyor:', cartId);
      const response = await apiClient.delete<{ message: string }>(`/cart/${cartId}`);
      
      // Masa bazlı sepet ID'sini temizle
      for (const [tableNumber, cartIdValue] of this.tableCarts.entries()) {
        if (cartIdValue === cartId) {
          this.tableCarts.delete(tableNumber);
          break;
        }
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
    // 🔒 Güvenlik kontrolü
    await this.checkSecurity();

    try {
      console.log('🛒 Sepet tamamlanıyor:', cartId);
      const response = await apiClient.post<{ message: string; cartId: string; totalItems: number; totalAmount: number }>(`/cart/${cartId}/complete`, { notes });
      
      // Masa bazlı sepet ID'sini temizle
      for (const [tableNumber, cartIdValue] of this.tableCarts.entries()) {
        if (cartIdValue === cartId) {
          this.tableCarts.delete(tableNumber);
          break;
        }
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
    // 🔒 Güvenlik kontrolü
    await this.checkSecurity();

    try {
      console.log('🛒 Sepet geçmişi getiriliyor...');
      const response = await apiClient.get<CartHistoryItem[]>('/cart/history');
      console.log('✅ Sepet geçmişi başarıyla getirildi, {Count} kayıt bulundu', response.length);
      return response;
    } catch (error) {
      console.error('❌ Sepet geçmişi getirme hatası:', error);
      throw new Error('Sepet geçmişi getirilemedi');
    }
  }

  // Masa bazlı sepeti temizle
  async clearCart(tableNumber: number): Promise<{ success: boolean; message: string }> {
    // 🔒 Güvenlik kontrolü
    await this.checkSecurity();

    try {
      console.log('🧹 Masa', tableNumber, 'sepeti temizleniyor...');
      const response = await apiClient.post<{ 
        message: string; 
        clearedCarts: number; 
        clearedItems: number; 
        tableNumber: number 
      }>('/cart/clear', null, { params: { tableNumber } });
      
      // Masa bazlı sepet ID'sini temizle
      this.tableCarts.delete(tableNumber);
      
      console.log('✅ Masa', tableNumber, 'sepeti başarıyla temizlendi');
      return { success: true, message: response.message };
    } catch (error) {
      console.error('❌ Masa', tableNumber, 'sepeti temizleme hatası:', error);
      return { success: false, message: 'Sepet temizlenemedi' };
    }
  }

  // Belirli masa için sepet ID'sini al
  getCartIdForTable(tableNumber: number): string | null {
    return this.tableCarts.get(tableNumber) || null;
  }

  // Masa için sepet ID'sini ayarla
  setCartIdForTable(tableNumber: number, cartId: string): void {
    this.tableCarts.set(tableNumber, cartId);
  }

  // Masa için sepet ID'sini temizle
  clearCartIdForTable(tableNumber: number): void {
    this.tableCarts.delete(tableNumber);
  }

  // Tüm masa sepet ID'lerini temizle
  clearAllTableCarts(): void {
    this.tableCarts.clear();
  }

  // Masa bazlı sepet durumunu kontrol et
  hasActiveCartForTable(tableNumber: number): boolean {
    return this.tableCarts.has(tableNumber);
  }

  // Tüm aktif masa sepetlerini listele
  getActiveTableCarts(): Map<number, string> {
    return new Map(this.tableCarts);
  }
}

// Singleton instance'ı export et
export const cartService = CartService.getInstance(); 
import { apiClient } from './config';

// Backend API Response Types
export interface BackendCartItem {
  id: string;
  cartId: string;
  productId: string;
  productName: string;
  quantity: number;
  unitPrice: number;
  taxRate: number;
  discountAmount: number;
  taxAmount: number;
  totalAmount: number;
  notes?: string;
  isModified: boolean;
  modifiedAt?: string;
  originalUnitPrice: number;
  originalQuantity: number;
  product: {
    id: string;
    name: string;
    description: string;
    price: number;
    stockQuantity: number;
    unit: string;
    category: string;
    taxType: 'Standard' | 'Reduced' | 'Special';
    isActive: boolean;
  };
}

export interface BackendCart {
  cartId: string;
  tableNumber?: string;
  waiterName?: string;
  customerId?: string;
  customer?: {
    id: string;
    name: string;
    email: string;
    phone: string;
    category: 'Regular' | 'VIP' | 'Wholesale' | 'Corporate';
  };
  userId?: string;
  cashRegisterId?: string;
  subtotal: number;
  taxAmount: number;
  discountAmount: number;
  totalAmount: number;
  appliedCouponId?: string;
  appliedCoupon?: {
    id: string;
    code: string;
    name: string;
    discountType: 'Percentage' | 'FixedAmount';
    discountValue: number;
    minimumAmount: number;
  };
  notes?: string;
  status: 'Active' | 'Completed' | 'Cancelled' | 'Expired';
  expiresAt?: string;
  createdAt: string;
  updatedAt: string;
  items: BackendCartItem[];
}

// Request Types
export interface CreateCartRequest {
  tableNumber?: string;
  waiterName?: string;
  customerId?: string;
  cashRegisterId?: string;
  notes?: string;
  initialItem?: AddCartItemRequest;
}

export interface AddCartItemRequest {
  productId: string;
  quantity: number;
  notes?: string;
}

export interface UpdateCartItemRequest {
  quantity: number;
  unitPrice: number;
  notes?: string;
}

export interface ApplyCouponRequest {
  couponCode: string;
}

export interface CompleteCartRequest {
  paymentMethod: string;
  amountPaid: number;
  notes?: string;
}

// Frontend Cart Types (Backendden dönüştürülmüş)
export interface CartItem {
  id: string;
  productName: string;
  product: {
    id: string;
    name: string;
    description: string;
    price: number;
    stockQuantity: number;
    unit: string;
    category: string;
    taxType: 'Standard' | 'Reduced' | 'Special';
  };
  quantity: number;
  unitPrice: number;
  taxRate: number;
  discountAmount: number;
  taxAmount: number;
  totalAmount: number;
  notes?: string;
  isModified: boolean;
}

export interface Cart {
  cartId: string;
  tableNumber?: string;
  waiterName?: string;
  customer?: {
    id: string;
    name: string;
    email: string;
    phone: string;
    category: 'Regular' | 'VIP' | 'Wholesale' | 'Corporate';
  };
  subtotal: number;
  taxAmount: number;
  discountAmount: number;
  totalAmount: number;
  appliedCoupon?: {
    id: string;
    code: string;
    name: string;
    discountType: 'Percentage' | 'FixedAmount';
    discountValue: number;
  };
  notes?: string;
  status: 'Active' | 'Completed' | 'Cancelled' | 'Expired';
  expiresAt?: string;
  items: CartItem[];
}

// Backend'den Frontend'e dönüştürme
const transformBackendCart = (backendCart: BackendCart): Cart => {
  return {
    cartId: backendCart.cartId,
    tableNumber: backendCart.tableNumber,
    waiterName: backendCart.waiterName,
    customer: backendCart.customer,
    subtotal: backendCart.subtotal,
    taxAmount: backendCart.taxAmount,
    discountAmount: backendCart.discountAmount,
    totalAmount: backendCart.totalAmount,
    appliedCoupon: backendCart.appliedCoupon,
    notes: backendCart.notes,
    status: backendCart.status,
    expiresAt: backendCart.expiresAt,
    items: backendCart.items.map(item => ({
      id: item.id,
      productName: item.productName,
      product: item.product,
      quantity: item.quantity,
      unitPrice: item.unitPrice,
      taxRate: item.taxRate,
      discountAmount: item.discountAmount,
      taxAmount: item.taxAmount,
      totalAmount: item.totalAmount,
      notes: item.notes,
      isModified: item.isModified,
    })),
  };
};

// Sepet API Servisi
export class CartService {
  private static instance: CartService;
  private currentCartId: string | null = null;

  static getInstance(): CartService {
    if (!CartService.instance) {
      CartService.instance = new CartService();
    }
    return CartService.instance;
  }

  // Yeni sepet oluştur
  async createCart(request: CreateCartRequest): Promise<Cart> {
    try {
      const response = await apiClient.post<BackendCart>('/cart', request);
      const cart = transformBackendCart(response);
      this.currentCartId = cart.cartId;
      return cart;
    } catch (error) {
      console.error('Error creating cart:', error);
      throw new Error('Sepet oluşturulamadı');
    }
  }

  // Sepeti getir
  async getCart(cartId: string): Promise<Cart> {
    try {
      const response = await apiClient.get<BackendCart>(`/cart/${cartId}`);
      const cart = transformBackendCart(response);
      this.currentCartId = cart.cartId;
      return cart;
    } catch (error) {
      console.error('Error getting cart:', error);
      throw new Error('Sepet getirilemedi');
    }
  }

  // Sepete ürün ekle
  async addCartItem(cartId: string, request: AddCartItemRequest): Promise<Cart> {
    try {
      const response = await apiClient.post<BackendCart>(`/cart/${cartId}/items`, request);
      return transformBackendCart(response);
    } catch (error) {
      console.error('Error adding cart item:', error);
      throw new Error('Ürün sepete eklenemedi');
    }
  }

  // Sepet ürününü güncelle
  async updateCartItem(cartId: string, itemId: string, request: UpdateCartItemRequest): Promise<Cart> {
    try {
      const response = await apiClient.put<BackendCart>(`/cart/${cartId}/items/${itemId}`, request);
      return transformBackendCart(response);
    } catch (error) {
      console.error('Error updating cart item:', error);
      throw new Error('Ürün güncellenemedi');
    }
  }

  // Sepet ürününü sil
  async removeCartItem(cartId: string, itemId: string): Promise<Cart> {
    try {
      const response = await apiClient.delete<BackendCart>(`/cart/${cartId}/items/${itemId}`);
      return transformBackendCart(response);
    } catch (error) {
      console.error('Error removing cart item:', error);
      throw new Error('Ürün sepetten silinemedi');
    }
  }

  // Kupon uygula
  async applyCoupon(cartId: string, request: ApplyCouponRequest): Promise<Cart> {
    try {
      const response = await apiClient.post<BackendCart>(`/cart/${cartId}/apply-coupon`, request);
      return transformBackendCart(response);
    } catch (error) {
      console.error('Error applying coupon:', error);
      throw new Error('Kupon uygulanamadı');
    }
  }

  // Sepeti tamamla
  async completeCart(cartId: string, request: CompleteCartRequest): Promise<Cart> {
    try {
      const response = await apiClient.post<BackendCart>(`/cart/${cartId}/complete`, request);
      return transformBackendCart(response);
    } catch (error) {
      console.error('Error completing cart:', error);
      throw new Error('Sepet tamamlanamadı');
    }
  }

  // Sepeti iptal et
  async cancelCart(cartId: string): Promise<void> {
    try {
      await apiClient.post(`/cart/${cartId}/cancel`);
    } catch (error) {
      console.error('Error cancelling cart:', error);
      throw new Error('Sepet iptal edilemedi');
    }
  }

  // Sepeti temizle (tüm ürünleri kaldır)
  async clearCart(cartId: string): Promise<void> {
    try {
      await apiClient.delete(`/cart/${cartId}`);
    } catch (error) {
      console.error('Error clearing cart:', error);
      throw new Error('Sepet temizlenemedi');
    }
  }

  // Kuponu kaldır
  async removeCoupon(cartId: string): Promise<Cart> {
    try {
      const response = await apiClient.delete<BackendCart>(`/cart/${cartId}/remove-coupon`);
      return transformBackendCart(response);
    } catch (error) {
      console.error('Error removing coupon:', error);
      throw new Error('Kupon kaldırılamadı');
    }
  }

  // Mevcut sepet ID'sini al
  getCurrentCartId(): string | null {
    return this.currentCartId;
  }

  // Mevcut sepet ID'sini temizle
  clearCurrentCartId(): void {
    this.currentCartId = null;
  }
}

// Singleton instance
export const cartService = CartService.getInstance(); 
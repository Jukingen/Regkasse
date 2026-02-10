// Order servisi - Backend Orders API'leri ile iletişim
import { secureApiService } from './secureApiService';
import { 
  CreateOrderRequest, 
  OrderResponse, 
  Order, 
  OrderItem 
} from '../../types/order';

const API_BASE_URL = '/api/orders';

export const orderService = {
  // Yeni sipariş oluştur
  async createOrder(request: CreateOrderRequest): Promise<OrderResponse> {
    try {
      const response = await secureApiService.post<OrderResponse>(API_BASE_URL, request);
      console.log('Order created successfully:', response.data);
      return response.data;
    } catch (error) {
      console.error('Error creating order:', error);
      throw error;
    }
  },

  // Tüm siparişleri getir
  async getOrders(): Promise<Order[]> {
    try {
      const response = await secureApiService.get<Order[]>(API_BASE_URL);
      return response.data;
    } catch (error) {
      console.error('Error fetching orders:', error);
      throw error;
    }
  },

  // Belirli siparişi getir
  async getOrder(orderId: string): Promise<Order> {
    try {
      const response = await secureApiService.get<Order>(`${API_BASE_URL}/${orderId}`);
      return response.data;
    } catch (error) {
      console.error('Error fetching order:', error);
      throw error;
    }
  },

  // Sipariş durumunu güncelle
  async updateOrderStatus(orderId: string, status: string): Promise<Order> {
    try {
      const response = await secureApiService.put<Order>(`${API_BASE_URL}/${orderId}/status`, { status });
      return response.data;
    } catch (error) {
      console.error('Error updating order status:', error);
      throw error;
    }
  },

  // Siparişi sil
  async deleteOrder(orderId: string): Promise<void> {
    try {
      await secureApiService.delete(`${API_BASE_URL}/${orderId}`);
      console.log('Order deleted successfully:', orderId);
    } catch (error) {
      console.error('Error deleting order:', error);
      throw error;
    }
  },

  // Sepetten sipariş oluştur (yardımcı fonksiyon)
  async createOrderFromCart(
    tableNumber: string,
    waiterName: string,
    cartItems: any[],
    customerName?: string,
    customerPhone?: string,
    notes?: string,
    cartId?: string
  ): Promise<OrderResponse> {
    const orderRequest: CreateOrderRequest = {
      tableNumber,
      waiterName,
      customerName,
      customerPhone,
      notes,
      cartId,
      items: cartItems.map(item => ({
        productId: item.productId,
        quantity: item.quantity,
        specialNotes: item.notes || undefined
      }))
    };

    return this.createOrder(orderRequest);
  }
};

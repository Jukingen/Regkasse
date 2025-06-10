import { apiClient } from './config';

export interface PaymentMethod {
  id: string;
  name: string;
  type: 'cash' | 'card' | 'voucher' | 'transfer';
  icon: string;
}

export interface PaymentItem {
  productId: string;
  quantity: number;
  price: number;
  taxType: 'standard' | 'reduced' | 'special';
}

export interface PaymentRequest {
  items: PaymentItem[];
  payment: {
    method: 'cash' | 'card' | 'voucher';
    amount: number;
    tseRequired: boolean;
  };
}

export interface PaymentResponse {
  success: boolean;
  paymentId: string;
  error?: string;
  message?: string;
}

export interface Receipt {
  id: string;
  receiptNumber: string;
  items: Array<{
    productName: string;
    quantity: number;
    price: number;
    taxType: string;
    totalPrice: number;
  }>;
  subtotal: number;
  taxStandard: number;
  taxReduced: number;
  taxSpecial: number;
  total: number;
  paymentMethod: string;
  timestamp: string;
  cashierId: string;
}

class PaymentService {
  private baseUrl = '/payments';

  // Ödeme yöntemlerini getir
  async getPaymentMethods(): Promise<PaymentMethod[]> {
    const response = await apiClient.get<PaymentMethod[]>(`${this.baseUrl}/methods`);
    return response.data;
  }

  // Ödeme işlemi (mod kontrolü ile)
  async processPayment(paymentRequest: PaymentRequest): Promise<PaymentResponse> {
    try {
      const response = await apiClient.post<PaymentResponse>(`${this.baseUrl}/process`, paymentRequest);
      return response.data;
    } catch (error) {
      console.error('Online payment failed:', error);
      
      // Çevrimdışı modda çalışıyorsa offline kaydet
      const { offlineManager } = await import('../offline/OfflineManager');
      const offlinePaymentId = await offlineManager.saveOfflinePayment(paymentRequest);
      
      console.log('Payment saved offline:', offlinePaymentId);
      return {
        success: true,
        paymentId: offlinePaymentId,
        message: 'Payment saved offline'
      };
    }
  }

  // Fiş oluştur
  async createReceipt(paymentId: string): Promise<Receipt> {
    const response = await apiClient.post<Receipt>(`${this.baseUrl}/${paymentId}/receipt`);
    return response.data;
  }

  // Ödeme geçmişi
  async getPaymentHistory(limit: number = 50, offset: number = 0): Promise<PaymentResponse[]> {
    try {
      const response = await apiClient.get<PaymentResponse[]>(
        `${this.baseUrl}/history?limit=${limit}&offset=${offset}`
      );
      return response.data;
    } catch (error) {
      console.error('Payment history fetch failed:', error);
      
      // Çevrimdışı modda çalışıyorsa offline verilerden getir
      const { offlineManager } = await import('../offline/OfflineManager');
      const offlinePayments = await offlineManager.getOfflinePayments();
      
      return offlinePayments.map(payment => ({
        success: true,
        paymentId: payment.id,
        message: 'Offline payment'
      }));
    }
  }

  // Belirli bir ödeme
  async getPaymentById(id: string): Promise<PaymentResponse> {
    try {
      const response = await apiClient.get<PaymentResponse>(`${this.baseUrl}/${id}`);
      return response.data;
    } catch (error) {
      console.error('Payment fetch failed:', error);
      
      // Çevrimdışı modda çalışıyorsa offline verilerden getir
      const { offlineManager } = await import('../offline/OfflineManager');
      const offlinePayments = await offlineManager.getOfflinePayments();
      const payment = offlinePayments.find(p => p.id === id);
      
      if (payment) {
        return {
          success: true,
          paymentId: payment.id,
          message: 'Offline payment'
        };
      }
      
      return {
        success: false,
        paymentId: '',
        error: 'Payment not found'
      };
    }
  }

  // Ödeme iptal
  async cancelPayment(id: string): Promise<boolean> {
    try {
      const response = await apiClient.post(`${this.baseUrl}/${id}/cancel`);
      return response.status === 200;
    } catch (error) {
      console.error('Payment cancellation failed:', error);
      
      // Çevrimdışı modda çalışıyorsa offline iptal et
      const { offlineManager } = await import('../offline/OfflineManager');
      await offlineManager.cancelOfflinePayment(id);
      
      return true;
    }
  }

  // Ödeme iade
  async refundPayment(id: string, amount: number, reason: string): Promise<PaymentResponse> {
    try {
      const response = await apiClient.post<PaymentResponse>(`${this.baseUrl}/${id}/refund`, {
        amount,
        reason
      });
      return response.data;
    } catch (error) {
      console.error('Payment refund failed:', error);
      
      // Çevrimdışı modda çalışıyorsa offline iade et
      const { offlineManager } = await import('../offline/OfflineManager');
      const refundId = await offlineManager.refundOfflinePayment(id, amount, reason);
      
      return {
        success: true,
        paymentId: refundId,
        message: 'Refund processed offline'
      };
    }
  }

  // Günlük ödeme raporu
  async getDailyPaymentReport(date: string): Promise<{
    totalPayments: number;
    totalAmount: number;
    paymentMethodBreakdown: Record<string, number>;
    payments: PaymentResponse[];
  }> {
    try {
      const response = await apiClient.get(`${this.baseUrl}/daily-report/${date}`);
      return response.data as {
        totalPayments: number;
        totalAmount: number;
        paymentMethodBreakdown: Record<string, number>;
        payments: PaymentResponse[];
      };
    } catch (error) {
      console.error('Daily payment report failed:', error);
      
      // Çevrimdışı modda çalışıyorsa offline verilerden rapor oluştur
      const { offlineManager } = await import('../offline/OfflineManager');
      const offlinePayments = await offlineManager.getOfflinePayments();
      
      const dayPayments = offlinePayments
        .filter(p => p.timestamp.startsWith(date))
        .map(p => ({
          success: true,
          paymentId: p.id,
          message: 'Offline payment'
        }));
      
      const totalAmount = dayPayments.reduce((sum, p) => sum + (p.amount || 0), 0);
      
      return {
        totalPayments: dayPayments.length,
        totalAmount,
        paymentMethodBreakdown: { cash: totalAmount }, // Çevrimdışı modda sadece nakit
        payments: dayPayments
      };
    }
  }

  // Ödeme istatistikleri
  async getPaymentStatistics(period: 'day' | 'week' | 'month' | 'year'): Promise<{
    totalPayments: number;
    totalAmount: number;
    averageAmount: number;
    topPaymentMethods: Array<{ method: string; count: number; amount: number }>;
  }> {
    try {
      const response = await apiClient.get(`${this.baseUrl}/statistics?period=${period}`);
      return response.data as {
        totalPayments: number;
        totalAmount: number;
        averageAmount: number;
        topPaymentMethods: Array<{ method: string; count: number; amount: number }>;
      };
    } catch (error) {
      console.error('Payment statistics failed:', error);
      
      // Çevrimdışı modda çalışıyorsa basit istatistikler döndür
      const { offlineManager } = await import('../offline/OfflineManager');
      const offlinePayments = await offlineManager.getOfflinePayments();
      
      const totalPayments = offlinePayments.length;
      const totalAmount = offlinePayments.reduce((sum, p) => sum + (p.payment.amount || 0), 0);
      const averageAmount = totalPayments > 0 ? totalAmount / totalPayments : 0;
      
      return {
        totalPayments,
        totalAmount,
        averageAmount,
        topPaymentMethods: [{ method: 'cash', count: totalPayments, amount: totalAmount }]
      };
    }
  }

  // Çevrimdışı ödemeleri senkronize et
  async syncOfflinePayments(): Promise<number> {
    try {
      const { offlineManager } = await import('../offline/OfflineManager');
      const offlinePayments = await offlineManager.getOfflinePayments();
      
      let syncedCount = 0;
      
      for (const offlinePayment of offlinePayments) {
        if (offlinePayment.status === 'pending') {
          try {
            // Online ödeme işlemi
            const response = await this.processPayment(offlinePayment.payment);
            
            if (response.success) {
              // Offline ödemeyi güncelle
              await offlineManager.syncOfflinePayment(offlinePayment);
              syncedCount++;
            }
          } catch (error) {
            console.error('Payment sync failed for:', offlinePayment.id, error);
          }
        }
      }
      
      console.log('Payments synced:', syncedCount);
      return syncedCount;
    } catch (error) {
      console.error('Payment sync failed:', error);
      return 0;
    }
  }
}

export const paymentService = new PaymentService();
export default paymentService; 
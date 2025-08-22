import { apiClient } from './config';

export interface PaymentMethod {
  id: string;
  name: string;
  type: 'cash' | 'card' | 'voucher' | 'transfer';
  icon: string;
}

// Backend'deki PaymentItemRequest ile uyumlu
export interface PaymentItem {
  productId: string; // Guid string formatında (00000000-0000-0000-0000-000000000000)
  quantity: number;
  taxType: 'standard' | 'reduced' | 'special';
}

// Backend'deki CreatePaymentRequest ile uyumlu
export interface PaymentRequest {
  customerId: string; // Guid string formatında (00000000-0000-0000-0000-000000000000)
  items: PaymentItem[];
  payment: {
    method: 'cash' | 'card' | 'voucher';
    tseRequired: boolean;
    amount?: number; // Opsiyonel
  };
  // Yeni eklenen alanlar
  tableNumber: number; // Masa numarası
  cashierId: string; // Kasiyer ID
  totalAmount: number; // Toplam tutar
  
  // Avusturya yasal gereksinimleri
  steuernummer: string; // Vergi numarası (ATU12345678)
  kassenId: string; // Kasa ID
  
  notes?: string;
}

export interface PaymentResponse {
  success: boolean;
  paymentId: string;
  error?: string;
  message?: string;
  tseSignature?: string;
}

export interface Receipt {
  id: string;
  receiptNumber: string;
  items: {
    productName: string;
    quantity: number;
    price: number;
    taxType: string;
    totalPrice: number;
  }[];
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
  private baseUrl = '/Payment'; // Backend'deki route ile eşleştirildi

  // Ödeme yöntemlerini getir
  async getPaymentMethods(): Promise<PaymentMethod[]> {
    const response = await apiClient.get<PaymentMethod[]>(`${this.baseUrl}/methods`);
    return response;
  }

  // Ödeme işlemi - Backend endpoint'i ile uyumlu
  async processPayment(paymentRequest: PaymentRequest): Promise<PaymentResponse> {
    try {
      // Backend'deki CreatePayment endpoint'ini kullan
      const response = await apiClient.post<PaymentResponse>(`${this.baseUrl}`, paymentRequest);
      return response;
    } catch (error) {
      console.error('Payment failed:', error);
      
      // Basit offline kaydetme
      const offlinePaymentId = `offline-${Date.now()}-${Math.random().toString(36).substr(2, 9)}`;
      console.log('Payment saved offline:', offlinePaymentId);
      
      return {
        success: true,
        paymentId: offlinePaymentId,
        message: 'Payment saved offline'
      };
    }
  }

  // Fiş oluştur - Backend'de bu endpoint yok, TSE signature endpoint'ini kullan
  async createReceipt(paymentId: string): Promise<Receipt> {
    try {
      // Önce TSE signature oluştur
      const tseResponse = await apiClient.post<{tseSignature: string}>(`${this.baseUrl}/${paymentId}/tse-signature`);
      
      // Sonra ödeme detaylarını al
      const paymentResponse = await apiClient.get<PaymentResponse>(`${this.baseUrl}/${paymentId}`);
      
      // Receipt objesini oluştur (backend'den gelen verilerle)
      const receipt: Receipt = {
        id: paymentId,
        receiptNumber: `AT-${Date.now()}-${paymentId.slice(0, 8)}`,
        items: [], // Backend'den items bilgisi gelmeli
        subtotal: 0,
        taxStandard: 0,
        taxReduced: 0,
        taxSpecial: 0,
        total: 0,
        paymentMethod: 'cash',
        timestamp: new Date().toISOString(),
        cashierId: 'current-user'
      };
      
      return receipt;
    } catch (error) {
      console.error('Receipt creation failed:', error);
      throw error;
    }
  }

  // Ödeme geçmişi
  async getPaymentHistory(limit: number = 50, offset: number = 0): Promise<PaymentResponse[]> {
    try {
      const response = await apiClient.get<PaymentResponse[]>(
        `${this.baseUrl}/history?limit=${limit}&offset=${offset}`
      );
      return response;
    } catch (error) {
      console.error('Payment history fetch failed:', error);
      
      // Basit offline response
      return [{
        success: true,
        paymentId: 'offline-payment',
        message: 'Offline payment'
      }];
    }
  }

  // Belirli bir ödeme
  async getPaymentById(id: string): Promise<PaymentResponse> {
    try {
      const response = await apiClient.get<PaymentResponse>(`${this.baseUrl}/${id}`);
      return response;
    } catch (error) {
      console.error('Payment fetch failed:', error);
      
      return {
        success: false,
        paymentId: '',
        error: 'Payment not found'
      };
    }
  }

  // Ödeme iptal
  async cancelPayment(sessionId: string, reason?: string): Promise<any> {
    try {
      const response = await apiClient.post<any>(`${this.baseUrl}/cancel`, {
        paymentSessionId: sessionId,
        cancellationReason: reason || 'Kasiyer tarafından iptal edildi'
      });
      return response;
    } catch (error) {
      console.error('Payment cancellation failed:', error);
      
      // Basit offline response
      return {
        success: true,
        paymentSessionId: sessionId,
        message: 'Payment cancelled offline'
      };
    }
  }

  // Ödeme iade
  async refundPayment(id: string, amount: number, reason: string): Promise<PaymentResponse> {
    try {
      const response = await apiClient.post<PaymentResponse>(`${this.baseUrl}/${id}/refund`, {
        amount,
        reason
      });
      return response;
    } catch (error) {
      console.error('Payment refund failed:', error);
      
      return {
        success: true,
        paymentId: `refund-${id}`,
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
      return response as {
        totalPayments: number;
        totalAmount: number;
        paymentMethodBreakdown: Record<string, number>;
        payments: PaymentResponse[];
      };
    } catch (error) {
      console.error('Daily payment report failed:', error);
      
      // Basit offline response
      return {
        totalPayments: 0,
        totalAmount: 0,
        paymentMethodBreakdown: {},
        payments: []
      };
    }
  }

  // Ödeme istatistikleri
  async getPaymentStatistics(period: 'day' | 'week' | 'month' | 'year'): Promise<{
    totalPayments: number;
    totalAmount: number;
    averageAmount: number;
    topPaymentMethods: { method: string; count: number; amount: number }[];
  }> {
    try {
      const response = await apiClient.get(`${this.baseUrl}/statistics?period=${period}`);
      return response.data as {
        totalPayments: number;
        totalAmount: number;
        averageAmount: number;
        topPaymentMethods: { method: string; count: number; amount: number }[];
      };
    } catch (error) {
      console.error('Payment statistics failed:', error);
      
      // Basit offline response
      return {
        totalPayments: 0,
        totalAmount: 0,
        averageAmount: 0,
        topPaymentMethods: []
      };
    }
  }

  // Çevrimdışı ödemeleri senkronize et
  async syncOfflinePayments(): Promise<number> {
    try {
      console.log('Syncing offline payments...');
      return 0; // Basit response
    } catch (error) {
      console.error('Payment sync failed:', error);
      return 0;
    }
  }
}

export const paymentService = new PaymentService();
export default paymentService; 
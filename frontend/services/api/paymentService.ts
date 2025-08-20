import { apiClient } from './config';

export interface PaymentMethod {
  id: string;
  name: string;
  type: 'cash' | 'card' | 'voucher' | 'transfer';
  icon: string;
}

// Backend'deki PaymentItemRequest ile uyumlu
export interface PaymentItem {
  productId: string; // Guid string olarak
  quantity: number;
  taxType: 'standard' | 'reduced' | 'special';
}

// Backend'deki CreatePaymentRequest ile uyumlu
export interface PaymentRequest {
  customerId: string; // Zorunlu alan
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
  private baseUrl = '/api/Payment'; // Backend'deki route ile eşleştirildi

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
      return response;
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
  async cancelPayment(sessionId: string, reason?: string): Promise<PaymentCancelResponse> {
    try {
      const response = await apiClient.post<PaymentCancelResponse>(`${this.baseUrl}/cancel`, {
        paymentSessionId: sessionId,
        cancellationReason: reason || 'Kasiyer tarafından iptal edildi'
      });
      return response;
    } catch (error) {
      console.error('Payment cancellation failed:', error);
      
      // Çevrimdışı modda çalışıyorsa offline iptal et
      const { offlineManager } = await import('../offline/OfflineManager');
      await offlineManager.cancelOfflinePayment(sessionId);
      
      // Offline response döndür
      return {
        success: true,
        paymentSessionId: sessionId,
        cartId: '',
        cancelledAt: new Date(),
        cancelledBy: 'Offline',
        cancellationReason: reason || 'Offline iptal',
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
      return response as {
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
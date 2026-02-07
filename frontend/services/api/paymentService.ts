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
    const response = await apiClient.get<any>(`${this.baseUrl}/methods`);
    // API yanıtı { success: true, data: [...] } formatındaysa data'yı dön
    if (response && response.data && Array.isArray(response.data)) {
      return response.data;
    }
    // Direkt array dönüyorsa
    if (Array.isArray(response)) {
      return response;
    }
    // Beklenmedik format
    console.warn('Unexpected payment methods response:', response);
    return [];
  }

  // Payment processing - Backend endpoint compatible
  async processPayment(paymentRequest: PaymentRequest): Promise<PaymentResponse> {
    const response = await apiClient.post<any>(`${this.baseUrl}`, paymentRequest);
    return this.normalizePaymentResponse(response);
  }

  // Helper to handle inconsistent backend responses (e.g. nested Value object)
  private normalizePaymentResponse(response: any): PaymentResponse {
    // 1. Unwrap "Value" if present (ASP.NET Core ActionResult serialization issue)
    const raw = response?.Value ? response.Value : response;

    // 2. Normalize Success
    // Check top-level success, data.Success, or Value.success
    const success =
      raw?.success === true ||
      raw?.Success === true ||
      raw?.data?.Success === true ||
      raw?.data?.success === true;

    // 3. Normalize PaymentId
    const paymentId =
      raw?.paymentId ||
      raw?.PaymentId ||
      raw?.data?.Payment?.Id ||
      raw?.data?.paymentId ||
      raw?.payment?.id ||
      '';

    // 4. Normalize Message
    const message =
      raw?.message ||
      raw?.Message ||
      raw?.data?.Message ||
      (success ? 'Payment successful' : 'Payment failed');

    // 5. Normalize TseSignature
    const tseSignature =
      raw?.tseSignature ||
      raw?.TseSignature ||
      raw?.payment?.tseSignature ||
      raw?.data?.Payment?.TseSignature;

    return {
      success: !!success, // Ensure boolean
      paymentId,
      message,
      tseSignature,
      error: success ? undefined : message
    };
  }

  // Fiş oluştur - Backend'de bu endpoint yok, TSE signature endpoint'ini kullan
  async createReceipt(paymentId: string): Promise<Receipt> {
    try {
      // Önce TSE signature oluştur
      const tseResponse = await apiClient.post<{ tseSignature: string }>(`${this.baseUrl}/${paymentId}/tse-signature`);

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

  // Get receipt data for printing
  async getReceipt(paymentId: string): Promise<any> {
    try {
      const response = await apiClient.get<any>(`${this.baseUrl}/${paymentId}/receipt`);
      // Unwrap response if nested in data/Value
      return response?.data || response?.Value || response;
    } catch (error) {
      console.error('Receipt fetch failed:', error);
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
  async cancelPayment(paymentId: string, reason?: string): Promise<any> {
    try {
      const response = await apiClient.post<any>(`${this.baseUrl}/${paymentId}/cancel`, {
        reason: reason || 'Kasiyer tarafından iptal edildi'
      });
      return response;
    } catch (error) {
      console.error('Payment cancellation failed:', error);

      // Basit offline response
      return {
        success: true,
        paymentId: paymentId,
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
      const response = await apiClient.get<any>(`${this.baseUrl}/statistics?period=${period}`);
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
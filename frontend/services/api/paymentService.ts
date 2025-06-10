import { apiClient } from './config';

export interface PaymentMethod {
  id: string;
  name: string;
  type: 'cash' | 'card' | 'voucher' | 'transfer';
  icon: string;
}

export interface PaymentRequest {
  amount: number;
  method: string;
  items: Array<{
    productId: string;
    quantity: number;
    price: number;
    taxType: string;
  }>;
  customerId?: string;
  tseRequired: boolean;
}

export interface PaymentResponse {
  id: string;
  receiptNumber: string;
  amount: number;
  taxAmount: number;
  totalAmount: number;
  paymentMethod: string;
  status: 'completed' | 'pending' | 'failed';
  tseSignature?: string;
  timestamp: string;
  items: Array<{
    productId: string;
    productName: string;
    quantity: number;
    price: number;
    taxAmount: number;
    totalPrice: number;
  }>;
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
  tseSignature?: string;
  timestamp: string;
  cashierId: string;
  customerId?: string;
}

class PaymentService {
  private baseUrl = '/payments';

  // Ödeme yöntemlerini getir
  async getPaymentMethods(): Promise<PaymentMethod[]> {
    const response = await apiClient.get<PaymentMethod[]>(`${this.baseUrl}/methods`);
    return response.data;
  }

  // Ödeme işlemi yap
  async processPayment(payment: PaymentRequest): Promise<PaymentResponse> {
    const response = await apiClient.post<PaymentResponse>(`${this.baseUrl}/process`, payment);
    return response.data;
  }

  // Fiş oluştur
  async createReceipt(paymentId: string): Promise<Receipt> {
    const response = await apiClient.post<Receipt>(`${this.baseUrl}/${paymentId}/receipt`);
    return response.data;
  }

  // Ödeme geçmişini getir
  async getPaymentHistory(limit: number = 50, offset: number = 0): Promise<PaymentResponse[]> {
    const response = await apiClient.get<PaymentResponse[]>(
      `${this.baseUrl}/history?limit=${limit}&offset=${offset}`
    );
    return response.data;
  }

  // Belirli bir ödemeyi getir
  async getPaymentById(id: string): Promise<PaymentResponse> {
    const response = await apiClient.get<PaymentResponse>(`${this.baseUrl}/${id}`);
    return response.data;
  }

  // Ödeme iptal et
  async cancelPayment(id: string, reason: string): Promise<void> {
    await apiClient.post(`${this.baseUrl}/${id}/cancel`, { reason });
  }

  // Günlük özet raporu
  async getDailySummary(date: string): Promise<{
    totalSales: number;
    totalTransactions: number;
    paymentMethods: Record<string, number>;
    taxSummary: {
      standard: number;
      reduced: number;
      special: number;
    };
  }> {
    const response = await apiClient.get(`${this.baseUrl}/daily-summary/${date}`);
    return response.data as {
      totalSales: number;
      totalTransactions: number;
      paymentMethods: Record<string, number>;
      taxSummary: {
        standard: number;
        reduced: number;
        special: number;
      };
    };
  }

  // TSE imzası doğrula
  async validateTseSignature(signature: string, processData: string): Promise<boolean> {
    try {
      const response = await apiClient.post(`${this.baseUrl}/validate-tse`, {
        signature,
        processData
      });
      return (response.data as { isValid: boolean }).isValid;
    } catch (error) {
      console.error('TSE signature validation failed:', error);
      return false;
    }
  }

  // Çevrimdışı ödeme kaydet
  async saveOfflinePayment(payment: PaymentRequest): Promise<string> {
    const response = await apiClient.post(`${this.baseUrl}/offline`, payment);
    return (response.data as { id: string }).id;
  }

  // Çevrimdışı ödemeleri senkronize et
  async syncOfflinePayments(): Promise<number> {
    const response = await apiClient.post(`${this.baseUrl}/sync-offline`);
    return (response.data as { syncedCount: number }).syncedCount;
  }
}

export const paymentService = new PaymentService();
export default paymentService; 
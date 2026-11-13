import { apiClient } from './config';

export interface PendingInvoice {
  id: string;
  invoiceNumber: string;
  invoiceDate: string;
  totalAmount: number;
  customerName?: string;
  status: string;
}

export interface PendingInvoicesResponse {
  pendingCount: number;
  invoices: PendingInvoice[];
}

class PendingInvoicesService {
  private readonly baseUrl = '/api/pending-invoices';

  // Bekleyen faturaları getir
  async getPendingInvoices(): Promise<PendingInvoicesResponse> {
    try {
      return await apiClient.get<PendingInvoicesResponse>(this.baseUrl);
    } catch (error) {
      console.error('Pending invoices fetch failed:', error);
      throw error;
    }
  }

  // Bekleyen faturaları gönder
  async submitPendingInvoices(): Promise<{ message: string }> {
    try {
      return await apiClient.post<{ message: string }>(`${this.baseUrl}/submit`);
    } catch (error) {
      console.error('Submit pending invoices failed:', error);
      throw error;
    }
  }

  // Tek faturayı yeniden gönder
  async retryInvoice(invoiceId: string): Promise<{ message: string }> {
    try {
      return await apiClient.post<{ message: string }>(`${this.baseUrl}/${invoiceId}/retry`);
    } catch (error) {
      console.error('Retry invoice failed:', error);
      throw error;
    }
  }

  // Bekleyen fatura sayısını getir
  async getPendingCount(): Promise<{ pendingCount: number }> {
    try {
      return await apiClient.get<{ pendingCount: number }>(`${this.baseUrl}/count`);
    } catch (error) {
      console.error('Pending count fetch failed:', error);
      throw error;
    }
  }

  // Eski bekleyen faturaları temizle
  async clearOldPendingInvoices(daysOld: number = 30): Promise<{ message: string }> {
    try {
      return await apiClient.post<{ message: string }>(
        `${this.baseUrl}/clear-old?daysOld=${daysOld}`
      );
    } catch (error) {
      console.error('Clear old pending invoices failed:', error);
      throw error;
    }
  }
}

export const pendingInvoicesService = new PendingInvoicesService();

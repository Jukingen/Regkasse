import { apiClient } from './config';
import { Receipt } from './paymentService';

export interface PrintOptions {
  printerName?: string;
  paperSize?: '80mm' | '58mm';
  fontSize?: 'small' | 'medium' | 'large';
  includeLogo?: boolean;
  includeQRCode?: boolean;
  copies?: number;
}

export interface PrinterStatus {
  isConnected: boolean;
  printerName: string;
  paperStatus: 'ready' | 'low' | 'empty' | 'error';
  errorMessage?: string;
}

export interface ReceiptTemplate {
  id: string;
  name: string;
  template: string;
  isDefault: boolean;
}

class ReceiptService {
  private baseUrl = '/receipts';

  // Fiş yazdır
  async printReceipt(receipt: Receipt, options?: PrintOptions): Promise<boolean> {
    try {
      const response = await apiClient.post(`${this.baseUrl}/print`, {
        receipt,
        options: {
          printerName: options?.printerName || 'EPSON TM-T88VI',
          paperSize: options?.paperSize || '80mm',
          fontSize: options?.fontSize || 'medium',
          includeLogo: options?.includeLogo ?? true,
          includeQRCode: options?.includeQRCode ?? true,
          copies: options?.copies || 1
        }
      });
      return response.status === 200;
    } catch (error) {
      console.error('Receipt printing failed:', error);
      return false;
    }
  }

  // Yazıcı durumunu kontrol et
  async getPrinterStatus(printerName?: string): Promise<PrinterStatus> {
    const response = await apiClient.get<PrinterStatus>(
      `${this.baseUrl}/printer-status${printerName ? `?printer=${printerName}` : ''}`
    );
    return response.data;
  }

  // Mevcut yazıcıları listele
  async getAvailablePrinters(): Promise<string[]> {
    const response = await apiClient.get<string[]>(`${this.baseUrl}/printers`);
    return response.data;
  }

  // Fiş şablonlarını getir
  async getReceiptTemplates(): Promise<ReceiptTemplate[]> {
    const response = await apiClient.get<ReceiptTemplate[]>(`${this.baseUrl}/templates`);
    return response.data;
  }

  // Fiş şablonu oluştur/güncelle
  async saveReceiptTemplate(template: Omit<ReceiptTemplate, 'id'>): Promise<ReceiptTemplate> {
    const response = await apiClient.post<ReceiptTemplate>(`${this.baseUrl}/templates`, template);
    return response.data;
  }

  // Fiş şablonu sil
  async deleteReceiptTemplate(id: string): Promise<void> {
    await apiClient.delete(`${this.baseUrl}/templates/${id}`);
  }

  // Fişi PDF olarak oluştur
  async generatePDFReceipt(receipt: Receipt): Promise<ArrayBuffer> {
    const response = await apiClient.post(`${this.baseUrl}/pdf`, receipt, {
      responseType: 'arraybuffer'
    });
    return response.data as ArrayBuffer;
  }

  // Fişi email ile gönder
  async emailReceipt(receiptId: string, email: string): Promise<boolean> {
    try {
      const response = await apiClient.post(`${this.baseUrl}/${receiptId}/email`, {
        email
      });
      return response.status === 200;
    } catch (error) {
      console.error('Email receipt failed:', error);
      return false;
    }
  }

  // Fiş geçmişini getir
  async getReceiptHistory(limit: number = 50, offset: number = 0): Promise<Receipt[]> {
    const response = await apiClient.get<Receipt[]>(
      `${this.baseUrl}/history?limit=${limit}&offset=${offset}`
    );
    return response.data;
  }

  // Belirli bir fişi getir
  async getReceiptById(id: string): Promise<Receipt> {
    const response = await apiClient.get<Receipt>(`${this.baseUrl}/${id}`);
    return response.data;
  }

  // Fiş yeniden yazdır
  async reprintReceipt(id: string, options?: PrintOptions): Promise<boolean> {
    try {
      const receipt = await this.getReceiptById(id);
      return await this.printReceipt(receipt, options);
    } catch (error) {
      console.error('Receipt reprint failed:', error);
      return false;
    }
  }

  // Günlük fiş raporu
  async getDailyReceiptReport(date: string): Promise<{
    totalReceipts: number;
    totalAmount: number;
    receipts: Receipt[];
  }> {
    const response = await apiClient.get(`${this.baseUrl}/daily-report/${date}`);
    return response.data as {
      totalReceipts: number;
      totalAmount: number;
      receipts: Receipt[];
    };
  }

  // Fiş formatını önizle
  async previewReceipt(receipt: Receipt, templateId?: string): Promise<string> {
    const response = await apiClient.post(`${this.baseUrl}/preview`, {
      receipt,
      templateId
    });
    return (response.data as { preview: string }).preview;
  }

  // Çevrimdışı fiş yazdırma kuyruğu
  async queueOfflinePrint(receipt: Receipt): Promise<string> {
    const response = await apiClient.post(`${this.baseUrl}/offline-queue`, receipt);
    return (response.data as { queueId: string }).queueId;
  }

  // Çevrimdışı yazdırma kuyruğunu işle
  async processOfflineQueue(): Promise<number> {
    const response = await apiClient.post(`${this.baseUrl}/process-offline-queue`);
    return (response.data as { processedCount: number }).processedCount;
  }

  // Fiş oluştur
  async createReceipt(paymentId: string): Promise<Receipt> {
    const response = await apiClient.post<Receipt>(`${this.baseUrl}/create`, { paymentId });
    return response.data;
  }
}

export const receiptService = new ReceiptService();
export default receiptService; 
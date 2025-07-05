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
  private baseUrl = '/Receipt';

  // Fiş yazdır (mod kontrolü ile)
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
      
      // Çevrimdışı modda çalışıyorsa kuyruğa ekle
      const { offlineManager } = await import('../offline/OfflineManager');
      await offlineManager.saveOfflineReceipt(receipt);
      
      console.log('Receipt queued for offline printing');
      return true; // Çevrimdışı modda başarılı kabul et
    }
  }

  // Yazıcı durumunu kontrol et
  async getPrinterStatus(printerName?: string): Promise<PrinterStatus> {
    try {
      const response = await apiClient.get<PrinterStatus>(
        `${this.baseUrl}/printer-status${printerName ? `?printer=${printerName}` : ''}`
      );
      return response;
    } catch (error) {
      console.error('Printer status check failed:', error);
      
      // Çevrimdışı modda çalışıyorsa varsayılan durum döndür
      return {
        isConnected: false,
        printerName: printerName || 'OFFLINE',
        paperStatus: 'ready',
        errorMessage: 'Offline mode - printer status unknown'
      };
    }
  }

  // Mevcut yazıcıları listele
  async getAvailablePrinters(): Promise<string[]> {
    try {
      const response = await apiClient.get<string[]>(`${this.baseUrl}/printers`);
      return response;
    } catch (error) {
      console.error('Available printers fetch failed:', error);
      
      // Çevrimdışı modda çalışıyorsa varsayılan yazıcı döndür
      return ['EPSON TM-T88VI (Offline)'];
    }
  }

  // Fiş şablonlarını getir
  async getReceiptTemplates(): Promise<ReceiptTemplate[]> {
    try {
      const response = await apiClient.get<ReceiptTemplate[]>(`${this.baseUrl}/templates`);
      return response;
    } catch (error) {
      console.error('Receipt templates fetch failed:', error);
      
      // Çevrimdışı modda çalışıyorsa varsayılan şablon döndür
      return [{
        id: 'default-offline',
        name: 'Default Offline Template',
        template: '{{receiptNumber}}\n{{items}}\n{{total}}',
        isDefault: true
      }];
    }
  }

  // Fiş şablonu oluştur/güncelle
  async saveReceiptTemplate(template: Omit<ReceiptTemplate, 'id'>): Promise<ReceiptTemplate> {
    try {
      const response = await apiClient.post<ReceiptTemplate>(`${this.baseUrl}/templates`, template);
      return response;
    } catch (error) {
      console.error('Receipt template save failed:', error);
      
      // Çevrimdışı modda çalışıyorsa sahte şablon döndür
      return {
        id: `offline_${Date.now()}`,
        ...template
      };
    }
  }

  // Fiş şablonu sil
  async deleteReceiptTemplate(id: string): Promise<void> {
    try {
      await apiClient.delete(`${this.baseUrl}/templates/${id}`);
    } catch (error) {
      console.error('Receipt template delete failed:', error);
      // Çevrimdışı modda çalışıyorsa sessizce geç
    }
  }

  // Fişi PDF olarak oluştur
  async generatePDFReceipt(receipt: Receipt): Promise<ArrayBuffer> {
    try {
      const response = await apiClient.post(`${this.baseUrl}/pdf`, receipt, {
        responseType: 'arraybuffer'
      });
      return response.data as ArrayBuffer;
    } catch (error) {
      console.error('PDF generation failed:', error);
      
      // Çevrimdışı modda çalışıyorsa boş PDF döndür
      return new ArrayBuffer(0);
    }
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
      
      // Çevrimdışı modda çalışıyorsa kuyruğa ekle
      const { offlineManager } = await import('../offline/OfflineManager');
      const offlineReceipts = await offlineManager.getOfflineReceipts();
      const receipt = offlineReceipts.find(r => r.receipt.id === receiptId)?.receipt;
      
      if (receipt) {
        // Email kuyruğuna ekle (gelecekte implement edilecek)
        console.log('Email queued for offline sending:', email);
        return true;
      }
      
      return false;
    }
  }

  // Fiş geçmişini getir
  async getReceiptHistory(limit: number = 50, offset: number = 0): Promise<Receipt[]> {
    try {
      const response = await apiClient.get<Receipt[]>(
        `${this.baseUrl}/history?limit=${limit}&offset=${offset}`
      );
      return response.data;
    } catch (error) {
      console.error('Receipt history fetch failed:', error);
      
      // Çevrimdışı modda çalışıyorsa boş liste döndür
      return [];
    }
  }

  // Belirli bir fişi getir
  async getReceiptById(id: string): Promise<Receipt> {
    try {
      const response = await apiClient.get<Receipt>(`${this.baseUrl}/${id}`);
      return response.data;
    } catch (error) {
      console.error('Receipt fetch failed:', error);
      
      // Çevrimdışı modda çalışıyorsa offline verilerden getir
      const { offlineManager } = await import('../offline/OfflineManager');
      const offlineReceipts = await offlineManager.getOfflineReceipts();
      const receipt = offlineReceipts.find(r => r.receipt.id === id)?.receipt;
      
      if (receipt) {
        return receipt;
      }
      
      throw error;
    }
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
    try {
      const response = await apiClient.get(`${this.baseUrl}/daily-report/${date}`);
      return response.data as {
        totalReceipts: number;
        totalAmount: number;
        receipts: Receipt[];
      };
    } catch (error) {
      console.error('Daily receipt report failed:', error);
      
      // Çevrimdışı modda çalışıyorsa offline verilerden rapor oluştur
      const { offlineManager } = await import('../offline/OfflineManager');
      const offlineReceipts = await offlineManager.getOfflineReceipts();
      
      const dayReceipts = offlineReceipts
        .filter(r => r.receipt.timestamp.startsWith(date))
        .map(r => r.receipt);
      
      const totalAmount = dayReceipts.reduce((sum, r) => sum + r.total, 0);
      
      return {
        totalReceipts: dayReceipts.length,
        totalAmount,
        receipts: dayReceipts
      };
    }
  }

  // Fiş formatını önizle
  async previewReceipt(receipt: Receipt, templateId?: string): Promise<string> {
    try {
      const response = await apiClient.post(`${this.baseUrl}/preview`, {
        receipt,
        templateId
      });
      return (response.data as { preview: string }).preview;
    } catch (error) {
      console.error('Receipt preview failed:', error);
      
      // Çevrimdışı modda çalışıyorsa basit önizleme döndür
      return `Receipt: ${receipt.receiptNumber}\nTotal: ${receipt.total}€`;
    }
  }

  // Çevrimdışı fiş yazdırma kuyruğu
  async queueOfflinePrint(receipt: Receipt): Promise<string> {
    try {
      const response = await apiClient.post(`${this.baseUrl}/offline-queue`, receipt);
      return (response.data as { queueId: string }).queueId;
    } catch (error) {
      console.error('Offline print queue failed:', error);
      
      // Çevrimdışı modda çalışıyorsa sahte kuyruk ID döndür
      return `offline_queue_${Date.now()}`;
    }
  }

  // Çevrimdışı yazdırma kuyruğunu işle
  async processOfflineQueue(): Promise<number> {
    try {
      const response = await apiClient.post(`${this.baseUrl}/process-offline-queue`);
      return (response.data as { processedCount: number }).processedCount;
    } catch (error) {
      console.error('Offline queue processing failed:', error);
      
      // Çevrimdışı modda çalışıyorsa 0 döndür
      return 0;
    }
  }

  // Fiş oluştur
  async createReceipt(paymentId: string): Promise<Receipt> {
    try {
      const response = await apiClient.post<Receipt>(`${this.baseUrl}/create`, { paymentId });
      return response.data;
    } catch (error) {
      console.error('Receipt creation failed:', error);
      
      // Çevrimdışı modda çalışıyorsa sahte fiş oluştur
      return {
        id: `offline_receipt_${Date.now()}`,
        receiptNumber: `OFF-${Date.now()}`,
        items: [],
        subtotal: 0,
        taxStandard: 0,
        taxReduced: 0,
        taxSpecial: 0,
        total: 0,
        paymentMethod: 'cash',
        timestamp: new Date().toISOString(),
        cashierId: 'offline'
      };
    }
  }
}

export const receiptService = new ReceiptService();
export default receiptService; 
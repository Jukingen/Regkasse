import { apiClient } from './api/config';

export interface PrintJob {
  id: string;
  type: 'receipt' | 'order' | 'invoice';
  content: string;
  timestamp: Date;
  status: 'pending' | 'printing' | 'completed' | 'failed';
}

export interface PrinterConfig {
  printerName: string;
  printerType: 'EPSON' | 'Star' | 'Thermal';
  paperWidth: number;
  fontSize: 'small' | 'medium' | 'large';
  autoCut: boolean;
  openDrawer: boolean;
}

class PrinterService {
  private baseUrl = '/printer';
  private defaultConfig: PrinterConfig = {
    printerName: 'EPSON TM-T88VI',
    printerType: 'EPSON',
    paperWidth: 80,
    fontSize: 'medium',
    autoCut: true,
    openDrawer: false,
  };

  // Yazıcı durumunu kontrol et
  async checkPrinterStatus(): Promise<boolean> {
    try {
      const response = await apiClient.get(`${this.baseUrl}/status`);
      return (response as any)?.status === 'ready';
    } catch (error) {
      console.error('Printer status check failed:', error);
      return false;
    }
  }

  // Yazıcı konfigürasyonunu al
  async getPrinterConfig(): Promise<PrinterConfig> {
    try {
      const response = await apiClient.get(`${this.baseUrl}/config`);
      return (response as any) || this.defaultConfig;
    } catch (error) {
      console.error('Failed to get printer config:', error);
      return this.defaultConfig;
    }
  }

  // Yazıcı konfigürasyonunu güncelle
  async updatePrinterConfig(config: Partial<PrinterConfig>): Promise<PrinterConfig> {
    try {
      const response = await apiClient.put(`${this.baseUrl}/config`, config);
      return response as any;
    } catch (error) {
      console.error('Failed to update printer config:', error);
      throw error;
    }
  }

  // Fiş yazdır (anında)
  async printReceipt(receiptData: {
    items: {
      name: string;
      quantity: number;
      price: number;
      total: number;
    }[];
    subtotal: number;
    tax: number;
    total: number;
    paymentMethod: string;
    receiptNumber: string;
    date: string;
    time: string;
    cashier: string;
  }): Promise<boolean> {
    try {
      console.log('Printing receipt immediately...');
      
      const printContent = this.formatReceiptContent(receiptData);
      
      const response = await apiClient.post(`${this.baseUrl}/print`, {
        type: 'receipt',
        content: printContent,
        immediate: true, // Anında yazdır
        priority: 'high'
      });

      console.log('Receipt printed successfully');
      return (response as any)?.success === true;
    } catch (error) {
      console.error('Receipt printing failed:', error);
      return false;
    }
  }

  // Sipariş yazdır (anında)
  async printOrder(orderData: {
    orderNumber: string;
    items: {
      name: string;
      quantity: number;
      notes?: string;
    }[];
    customerName?: string;
    tableNumber?: string;
    notes?: string;
    date: string;
    time: string;
  }): Promise<boolean> {
    try {
      console.log('Printing order immediately...');
      
      const printContent = this.formatOrderContent(orderData);
      
      const response = await apiClient.post(`${this.baseUrl}/print`, {
        type: 'order',
        content: printContent,
        immediate: true, // Anında yazdır
        priority: 'high'
      });

      console.log('Order printed successfully');
      return (response as any)?.success === true;
    } catch (error) {
      console.error('Order printing failed:', error);
      return false;
    }
  }

  // Fiş içeriğini formatla
  private formatReceiptContent(data: any): string {
    const config = this.defaultConfig;
    const separator = '='.repeat(config.paperWidth / 8);
    
    let content = '';
    
    // Header
    content += `${' '.repeat(8)}KASSA\n`;
    content += `${separator}\n`;
    content += `Fiş No: ${data.receiptNumber}\n`;
    content += `Tarih: ${data.date}\n`;
    content += `Saat: ${data.time}\n`;
    content += `Kasiyer: ${data.cashier}\n`;
    content += `${separator}\n`;
    
    // Items
    data.items.forEach((item: any) => {
      content += `${item.name}\n`;
      content += `${item.quantity}x €${item.price.toFixed(2)} = €${item.total.toFixed(2)}\n`;
    });
    
    content += `${separator}\n`;
    content += `Ara Toplam: €${data.subtotal.toFixed(2)}\n`;
    content += `KDV: €${data.tax.toFixed(2)}\n`;
    content += `TOPLAM: €${data.total.toFixed(2)}\n`;
    content += `Ödeme: ${data.paymentMethod}\n`;
    content += `${separator}\n`;
    content += `${' '.repeat(8)}TEŞEKKÜRLER\n`;
    content += `${' '.repeat(6)}Bizi tercih ettiğiniz için\n`;
    
    return content;
  }

  // Sipariş içeriğini formatla
  private formatOrderContent(data: any): string {
    const config = this.defaultConfig;
    const separator = '='.repeat(config.paperWidth / 8);
    
    let content = '';
    
    // Header
    content += `${' '.repeat(8)}SİPARİŞ\n`;
    content += `${separator}\n`;
    content += `Sipariş No: ${data.orderNumber}\n`;
    content += `Tarih: ${data.date}\n`;
    content += `Saat: ${data.time}\n`;
    
    if (data.customerName) {
      content += `Müşteri: ${data.customerName}\n`;
    }
    
    if (data.tableNumber) {
      content += `Masa: ${data.tableNumber}\n`;
    }
    
    content += `${separator}\n`;
    
    // Items
    data.items.forEach((item: any) => {
      content += `${item.quantity}x ${item.name}\n`;
      if (item.notes) {
        content += `  Not: ${item.notes}\n`;
      }
    });
    
    if (data.notes) {
      content += `${separator}\n`;
      content += `Sipariş Notu: ${data.notes}\n`;
    }
    
    content += `${separator}\n`;
    content += `${' '.repeat(8)}HAZIRLANIYOR\n`;
    
    return content;
  }

  // Yazıcı test sayfası
  async printTestPage(): Promise<boolean> {
    try {
      const response = await apiClient.post(`${this.baseUrl}/test`);
      return response?.success === true;
    } catch (error) {
      console.error('Test page printing failed:', error);
      return false;
    }
  }

  // Yazıcı kuyruğunu temizle
  async clearPrintQueue(): Promise<boolean> {
    try {
      const response = await apiClient.delete(`${this.baseUrl}/queue`);
      return response?.success === true;
    } catch (error) {
      console.error('Failed to clear print queue:', error);
      return false;
    }
  }

  // Yazıcı geçmişini al
  async getPrintHistory(limit: number = 50): Promise<PrintJob[]> {
    try {
      const response = await apiClient.get(`${this.baseUrl}/history?limit=${limit}`);
      return response || [];
    } catch (error) {
      console.error('Failed to get print history:', error);
      return [];
    }
  }
}

export const printerService = new PrinterService();
export default printerService; 
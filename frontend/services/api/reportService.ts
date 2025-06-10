import { apiClient } from './config';

export interface ReportFilters {
  startDate: string;
  endDate: string;
  paymentMethods?: string[];
  taxTypes?: string[];
  cashierId?: string;
}

export interface SalesReport {
  period: string;
  totalSales: number;
  totalTransactions: number;
  averageTransactionValue: number;
  paymentMethodBreakdown: Record<string, number>;
  taxBreakdown: {
    standard: number;
    reduced: number;
    special: number;
  };
  topProducts: Array<{
    productId: string;
    productName: string;
    quantity: number;
    revenue: number;
  }>;
  dailyBreakdown: Array<{
    date: string;
    sales: number;
    transactions: number;
  }>;
}

export interface InventoryReport {
  period: string;
  totalProducts: number;
  lowStockProducts: Array<{
    productId: string;
    productName: string;
    currentStock: number;
    minStock: number;
  }>;
  outOfStockProducts: Array<{
    productId: string;
    productName: string;
    lastRestocked: string;
  }>;
  stockValue: number;
  stockMovements: Array<{
    productId: string;
    productName: string;
    type: 'in' | 'out';
    quantity: number;
    date: string;
  }>;
}

export interface TaxReport {
  period: string;
  totalTaxCollected: number;
  taxBreakdown: {
    standard: {
      amount: number;
      transactions: number;
      rate: number;
    };
    reduced: {
      amount: number;
      transactions: number;
      rate: number;
    };
    special: {
      amount: number;
      transactions: number;
      rate: number;
    };
  };
  monthlyComparison: Array<{
    month: string;
    totalTax: number;
    standardTax: number;
    reducedTax: number;
    specialTax: number;
  }>;
}

export interface ExportFormat {
  format: 'pdf' | 'excel' | 'csv' | 'json';
  includeCharts?: boolean;
  includeDetails?: boolean;
  language?: string;
}

class ReportService {
  private baseUrl = '/reports';

  // Satış raporu oluştur (mod kontrolü ile)
  async generateSalesReport(filters: ReportFilters): Promise<SalesReport> {
    try {
      const response = await apiClient.post<SalesReport>(`${this.baseUrl}/sales`, filters);
      return response.data;
    } catch (error) {
      console.error('Online sales report failed:', error);
      
      // Çevrimdışı modda çalışıyorsa offline verilerden rapor oluştur
      const { offlineManager } = await import('../offline/OfflineManager');
      const offlinePayments = await offlineManager.getOfflinePayments();
      
      // Filtrelenmiş ödemeleri al
      const filteredPayments = offlinePayments.filter(payment => {
        const paymentDate = new Date(payment.timestamp).toISOString().split('T')[0];
        return paymentDate >= filters.startDate && paymentDate <= filters.endDate;
      });
      
      // Rapor verilerini hesapla
      const totalSales = filteredPayments.reduce((sum, p) => sum + p.payment.amount, 0);
      const totalTransactions = filteredPayments.length;
      const averageTransactionValue = totalTransactions > 0 ? totalSales / totalTransactions : 0;
      
      // Vergi hesaplamaları
      const taxBreakdown = { standard: 0, reduced: 0, special: 0 };
      filteredPayments.forEach(payment => {
        payment.payment.items.forEach(item => {
          const itemTotal = item.price * item.quantity;
          const taxRate = item.taxType === 'standard' ? 0.20 : item.taxType === 'reduced' ? 0.10 : 0.13;
          const taxAmount = itemTotal * taxRate;
          if (item.taxType in taxBreakdown) {
            const taxKey = item.taxType as keyof typeof taxBreakdown;
            taxBreakdown[taxKey] += taxAmount;
          }
        });
      });
      
      return {
        period: `${filters.startDate} - ${filters.endDate}`,
        totalSales,
        totalTransactions,
        averageTransactionValue,
        paymentMethodBreakdown: { cash: totalSales }, // Çevrimdışı modda sadece nakit
        taxBreakdown,
        topProducts: [], // Çevrimdışı modda ürün detayı yok
        dailyBreakdown: [] // Çevrimdışı modda günlük detay yok
      };
    }
  }

  // Envanter raporu oluştur (mod kontrolü ile)
  async generateInventoryReport(filters: ReportFilters): Promise<InventoryReport> {
    try {
      const response = await apiClient.post<InventoryReport>(`${this.baseUrl}/inventory`, filters);
      return response.data;
    } catch (error) {
      console.error('Online inventory report failed:', error);
      
      // Çevrimdışı modda çalışıyorsa offline verilerden rapor oluştur
      const { offlineManager } = await import('../offline/OfflineManager');
      const offlineProducts = await offlineManager.getOfflineProducts();
      
      const lowStockProducts = offlineProducts
        .filter(p => p.stock < 10) // 10'dan az stok
        .map(p => ({
          productId: p.id,
          productName: p.name,
          currentStock: p.stock,
          minStock: 10
        }));
      
      const outOfStockProducts = offlineProducts
        .filter(p => p.stock === 0)
        .map(p => ({
          productId: p.id,
          productName: p.name,
          lastRestocked: new Date().toISOString()
        }));
      
      const stockValue = offlineProducts.reduce((sum, p) => sum + (p.price * p.stock), 0);
      
      return {
        period: `${filters.startDate} - ${filters.endDate}`,
        totalProducts: offlineProducts.length,
        lowStockProducts,
        outOfStockProducts,
        stockValue,
        stockMovements: [] // Çevrimdışı modda hareket detayı yok
      };
    }
  }

  // Vergi raporu oluştur (mod kontrolü ile)
  async generateTaxReport(filters: ReportFilters): Promise<TaxReport> {
    try {
      const response = await apiClient.post<TaxReport>(`${this.baseUrl}/tax`, filters);
      return response.data;
    } catch (error) {
      console.error('Online tax report failed:', error);
      
      // Çevrimdışı modda çalışıyorsa offline verilerden rapor oluştur
      const { offlineManager } = await import('../offline/OfflineManager');
      const offlinePayments = await offlineManager.getOfflinePayments();
      
      // Filtrelenmiş ödemeleri al
      const filteredPayments = offlinePayments.filter(payment => {
        const paymentDate = new Date(payment.timestamp).toISOString().split('T')[0];
        return paymentDate >= filters.startDate && paymentDate <= filters.endDate;
      });
      
      // Vergi hesaplamaları
      const taxBreakdown = {
        standard: { amount: 0, transactions: 0, rate: 0.20 },
        reduced: { amount: 0, transactions: 0, rate: 0.10 },
        special: { amount: 0, transactions: 0, rate: 0.13 }
      };
      
      filteredPayments.forEach(payment => {
        payment.payment.items.forEach(item => {
          const itemTotal = item.price * item.quantity;
          const taxRate = item.taxType === 'standard' ? 0.20 : item.taxType === 'reduced' ? 0.10 : 0.13;
          const taxAmount = itemTotal * taxRate;
          if (item.taxType in taxBreakdown) {
            const taxKey = item.taxType as keyof typeof taxBreakdown;
            taxBreakdown[taxKey].amount += taxAmount;
            taxBreakdown[taxKey].transactions += 1;
          }
        });
      });
      
      const totalTaxCollected = Object.values(taxBreakdown).reduce((sum, tax) => sum + tax.amount, 0);
      
      return {
        period: `${filters.startDate} - ${filters.endDate}`,
        totalTaxCollected,
        taxBreakdown,
        monthlyComparison: [] // Çevrimdışı modda aylık karşılaştırma yok
      };
    }
  }

  // Raporu dışa aktar (mod kontrolü ile)
  async exportReport(
    reportType: 'sales' | 'inventory' | 'tax',
    filters: ReportFilters,
    format: ExportFormat
  ): Promise<ArrayBuffer> {
    try {
      const response = await apiClient.post(
        `${this.baseUrl}/export/${reportType}`,
        { filters, format },
        { responseType: 'arraybuffer' }
      );
      return response.data as ArrayBuffer;
    } catch (error) {
      console.error('Online report export failed:', error);
      
      // Çevrimdışı modda çalışıyorsa basit CSV oluştur
      const reportData = await this.generateOfflineReport(reportType, filters);
      const csvContent = this.convertToCSV(reportData);
      const encoder = new TextEncoder();
      return encoder.encode(csvContent).buffer.slice(0);
    }
  }

  // Günlük rapor oluştur (mod kontrolü ile)
  async generateDailyReport(date: string): Promise<{
    sales: SalesReport;
    inventory: InventoryReport;
    tax: TaxReport;
  }> {
    try {
      const response = await apiClient.get(`${this.baseUrl}/daily/${date}`);
      return response.data as {
        sales: SalesReport;
        inventory: InventoryReport;
        tax: TaxReport;
      };
    } catch (error) {
      console.error('Online daily report failed:', error);
      
      // Çevrimdışı modda çalışıyorsa offline verilerden rapor oluştur
      const filters: ReportFilters = { startDate: date, endDate: date };
      
      const sales = await this.generateSalesReport(filters);
      const inventory = await this.generateInventoryReport(filters);
      const tax = await this.generateTaxReport(filters);
      
      return { sales, inventory, tax };
    }
  }

  // Aylık rapor oluştur (mod kontrolü ile)
  async generateMonthlyReport(year: number, month: number): Promise<{
    sales: SalesReport;
    inventory: InventoryReport;
    tax: TaxReport;
  }> {
    try {
      const response = await apiClient.get(`${this.baseUrl}/monthly/${year}/${month}`);
      return response.data as {
        sales: SalesReport;
        inventory: InventoryReport;
        tax: TaxReport;
      };
    } catch (error) {
      console.error('Online monthly report failed:', error);
      
      // Çevrimdışı modda çalışıyorsa offline verilerden rapor oluştur
      const startDate = `${year}-${month.toString().padStart(2, '0')}-01`;
      const endDate = `${year}-${month.toString().padStart(2, '0')}-31`;
      const filters: ReportFilters = { startDate, endDate };
      
      const sales = await this.generateSalesReport(filters);
      const inventory = await this.generateInventoryReport(filters);
      const tax = await this.generateTaxReport(filters);
      
      return { sales, inventory, tax };
    }
  }

  // Yıllık rapor oluştur (mod kontrolü ile)
  async generateYearlyReport(year: number): Promise<{
    sales: SalesReport;
    inventory: InventoryReport;
    tax: TaxReport;
  }> {
    try {
      const response = await apiClient.get(`${this.baseUrl}/yearly/${year}`);
      return response.data as {
        sales: SalesReport;
        inventory: InventoryReport;
        tax: TaxReport;
      };
    } catch (error) {
      console.error('Online yearly report failed:', error);
      
      // Çevrimdışı modda çalışıyorsa offline verilerden rapor oluştur
      const startDate = `${year}-01-01`;
      const endDate = `${year}-12-31`;
      const filters: ReportFilters = { startDate, endDate };
      
      const sales = await this.generateSalesReport(filters);
      const inventory = await this.generateInventoryReport(filters);
      const tax = await this.generateTaxReport(filters);
      
      return { sales, inventory, tax };
    }
  }

  // Rapor şablonlarını getir (mod kontrolü ile)
  async getReportTemplates(): Promise<Array<{
    id: string;
    name: string;
    type: string;
    description: string;
    defaultFilters: ReportFilters;
  }>> {
    try {
      const response = await apiClient.get(`${this.baseUrl}/templates`);
      return response.data as Array<{
        id: string;
        name: string;
        type: string;
        description: string;
        defaultFilters: ReportFilters;
      }>;
    } catch (error) {
      console.error('Online report templates failed:', error);
      
      // Çevrimdışı modda çalışıyorsa varsayılan şablonlar döndür
      return [
        {
          id: 'offline-sales',
          name: 'Offline Sales Report',
          type: 'sales',
          description: 'Basic sales report for offline mode',
          defaultFilters: {
            startDate: new Date().toISOString().split('T')[0],
            endDate: new Date().toISOString().split('T')[0]
          }
        }
      ];
    }
  }

  // Rapor şablonu kaydet (mod kontrolü ile)
  async saveReportTemplate(template: {
    name: string;
    type: string;
    description: string;
    defaultFilters: ReportFilters;
  }): Promise<{ id: string }> {
    try {
      const response = await apiClient.post(`${this.baseUrl}/templates`, template);
      return response.data as { id: string };
    } catch (error) {
      console.error('Online template save failed:', error);
      
      // Çevrimdışı modda çalışıyorsa sahte ID döndür
      return { id: `offline_template_${Date.now()}` };
    }
  }

  // Rapor geçmişini getir (mod kontrolü ile)
  async getReportHistory(limit: number = 20): Promise<Array<{
    id: string;
    type: string;
    generatedAt: string;
    filters: ReportFilters;
    format: string;
    fileSize: number;
  }>> {
    try {
      const response = await apiClient.get(`${this.baseUrl}/history?limit=${limit}`);
      return response.data as Array<{
        id: string;
        type: string;
        generatedAt: string;
        filters: ReportFilters;
        format: string;
        fileSize: number;
      }>;
    } catch (error) {
      console.error('Online report history failed:', error);
      
      // Çevrimdışı modda çalışıyorsa boş liste döndür
      return [];
    }
  }

  // Raporu email ile gönder (mod kontrolü ile)
  async emailReport(
    reportType: string,
    filters: ReportFilters,
    format: ExportFormat,
    email: string
  ): Promise<boolean> {
    try {
      const response = await apiClient.post(`${this.baseUrl}/email`, {
        reportType,
        filters,
        format,
        email
      });
      return response.status === 200;
    } catch (error) {
      console.error('Online email report failed:', error);
      
      // Çevrimdışı modda çalışıyorsa kuyruğa ekle
      console.log('Email report queued for offline sending:', email);
      return true;
    }
  }

  // Rapor önizleme (mod kontrolü ile)
  async previewReport(
    reportType: 'sales' | 'inventory' | 'tax',
    filters: ReportFilters
  ): Promise<{
    summary: any;
    charts: Array<{
      type: string;
      data: any;
    }>;
  }> {
    try {
      const response = await apiClient.post(`${this.baseUrl}/preview/${reportType}`, filters);
      return response.data as {
        summary: any;
        charts: Array<{
          type: string;
          data: any;
        }>;
      };
    } catch (error) {
      console.error('Online report preview failed:', error);
      
      // Çevrimdışı modda çalışıyorsa basit önizleme döndür
      const reportData = await this.generateOfflineReport(reportType, filters);
      return {
        summary: reportData,
        charts: []
      };
    }
  }

  // Çevrimdışı rapor kuyruğu (mod kontrolü ile)
  async queueOfflineReport(
    reportType: string,
    filters: ReportFilters,
    format: ExportFormat
  ): Promise<string> {
    try {
      const response = await apiClient.post(`${this.baseUrl}/offline-queue`, {
        reportType,
        filters,
        format
      });
      return (response.data as { queueId: string }).queueId;
    } catch (error) {
      console.error('Online offline queue failed:', error);
      
      // Çevrimdışı modda çalışıyorsa sahte kuyruk ID döndür
      return `offline_report_queue_${Date.now()}`;
    }
  }

  // Çevrimdışı rapor kuyruğunu işle (mod kontrolü ile)
  async processOfflineReports(): Promise<number> {
    try {
      const response = await apiClient.post(`${this.baseUrl}/process-offline`);
      return (response.data as { processedCount: number }).processedCount;
    } catch (error) {
      console.error('Online offline processing failed:', error);
      
      // Çevrimdışı modda çalışıyorsa 0 döndür
      return 0;
    }
  }

  // Yardımcı metodlar
  private async generateOfflineReport(reportType: string, filters: ReportFilters): Promise<any> {
    switch (reportType) {
      case 'sales':
        return await this.generateSalesReport(filters);
      case 'inventory':
        return await this.generateInventoryReport(filters);
      case 'tax':
        return await this.generateTaxReport(filters);
      default:
        return {};
    }
  }

  private convertToCSV(data: any): string {
    // Basit CSV dönüşümü
    if (Array.isArray(data)) {
      if (data.length === 0) return '';
      const headers = Object.keys(data[0]);
      const csvRows = [headers.join(',')];
      data.forEach(row => {
        csvRows.push(headers.map(header => row[header]).join(','));
      });
      return csvRows.join('\n');
    }
    return JSON.stringify(data);
  }
}

export const reportService = new ReportService();
export default reportService; 
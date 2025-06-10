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

  // Satış raporu oluştur
  async generateSalesReport(filters: ReportFilters): Promise<SalesReport> {
    const response = await apiClient.post<SalesReport>(`${this.baseUrl}/sales`, filters);
    return response.data;
  }

  // Envanter raporu oluştur
  async generateInventoryReport(filters: ReportFilters): Promise<InventoryReport> {
    const response = await apiClient.post<InventoryReport>(`${this.baseUrl}/inventory`, filters);
    return response.data;
  }

  // Vergi raporu oluştur
  async generateTaxReport(filters: ReportFilters): Promise<TaxReport> {
    const response = await apiClient.post<TaxReport>(`${this.baseUrl}/tax`, filters);
    return response.data;
  }

  // Raporu dışa aktar
  async exportReport(
    reportType: 'sales' | 'inventory' | 'tax',
    filters: ReportFilters,
    format: ExportFormat
  ): Promise<ArrayBuffer> {
    const response = await apiClient.post(
      `${this.baseUrl}/export/${reportType}`,
      { filters, format },
      { responseType: 'arraybuffer' }
    );
    return response.data as ArrayBuffer;
  }

  // Günlük rapor oluştur
  async generateDailyReport(date: string): Promise<{
    sales: SalesReport;
    inventory: InventoryReport;
    tax: TaxReport;
  }> {
    const response = await apiClient.get(`${this.baseUrl}/daily/${date}`);
    return response.data as {
      sales: SalesReport;
      inventory: InventoryReport;
      tax: TaxReport;
    };
  }

  // Aylık rapor oluştur
  async generateMonthlyReport(year: number, month: number): Promise<{
    sales: SalesReport;
    inventory: InventoryReport;
    tax: TaxReport;
  }> {
    const response = await apiClient.get(`${this.baseUrl}/monthly/${year}/${month}`);
    return response.data as {
      sales: SalesReport;
      inventory: InventoryReport;
      tax: TaxReport;
    };
  }

  // Yıllık rapor oluştur
  async generateYearlyReport(year: number): Promise<{
    sales: SalesReport;
    inventory: InventoryReport;
    tax: TaxReport;
  }> {
    const response = await apiClient.get(`${this.baseUrl}/yearly/${year}`);
    return response.data as {
      sales: SalesReport;
      inventory: InventoryReport;
      tax: TaxReport;
    };
  }

  // Rapor şablonlarını getir
  async getReportTemplates(): Promise<Array<{
    id: string;
    name: string;
    type: string;
    description: string;
    defaultFilters: ReportFilters;
  }>> {
    const response = await apiClient.get(`${this.baseUrl}/templates`);
    return response.data as Array<{
      id: string;
      name: string;
      type: string;
      description: string;
      defaultFilters: ReportFilters;
    }>;
  }

  // Rapor şablonu kaydet
  async saveReportTemplate(template: {
    name: string;
    type: string;
    description: string;
    defaultFilters: ReportFilters;
  }): Promise<{ id: string }> {
    const response = await apiClient.post(`${this.baseUrl}/templates`, template);
    return response.data as { id: string };
  }

  // Rapor geçmişini getir
  async getReportHistory(limit: number = 20): Promise<Array<{
    id: string;
    type: string;
    generatedAt: string;
    filters: ReportFilters;
    format: string;
    fileSize: number;
  }>> {
    const response = await apiClient.get(`${this.baseUrl}/history?limit=${limit}`);
    return response.data as Array<{
      id: string;
      type: string;
      generatedAt: string;
      filters: ReportFilters;
      format: string;
      fileSize: number;
    }>;
  }

  // Raporu email ile gönder
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
      console.error('Email report failed:', error);
      return false;
    }
  }

  // Rapor önizleme
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
    const response = await apiClient.post(`${this.baseUrl}/preview/${reportType}`, filters);
    return response.data as {
      summary: any;
      charts: Array<{
        type: string;
        data: any;
      }>;
    };
  }

  // Çevrimdışı rapor kuyruğu
  async queueOfflineReport(
    reportType: string,
    filters: ReportFilters,
    format: ExportFormat
  ): Promise<string> {
    const response = await apiClient.post(`${this.baseUrl}/offline-queue`, {
      reportType,
      filters,
      format
    });
    return (response.data as { queueId: string }).queueId;
  }

  // Çevrimdışı rapor kuyruğunu işle
  async processOfflineReports(): Promise<number> {
    const response = await apiClient.post(`${this.baseUrl}/process-offline`);
    return (response.data as { processedCount: number }).processedCount;
  }
}

export const reportService = new ReportService();
export default reportService; 
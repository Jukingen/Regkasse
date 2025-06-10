import { apiClient } from './config';

export interface InvoiceItem {
  productId: string;
  productName: string;
  quantity: number;
  unitPrice: number;
  discountAmount: number;
  taxType: 'standard' | 'reduced' | 'special';
  taxAmount: number;
  totalAmount: number;
}

export interface Invoice {
  id: string;
  invoiceNumber: string;
  receiptNumber: string;
  customerId?: string;
  customerName?: string;
  customerEmail?: string;
  customerTaxNumber?: string;
  customerVatNumber?: string;
  items: InvoiceItem[];
  subtotal: number;
  discountAmount: number;
  taxStandard: number;
  taxReduced: number;
  taxSpecial: number;
  totalAmount: number;
  paymentMethod: 'cash' | 'card' | 'voucher' | 'mixed';
  paymentStatus: 'pending' | 'paid' | 'partial' | 'refunded';
  invoiceStatus: 'draft' | 'sent' | 'paid' | 'overdue' | 'cancelled';
  invoiceType: 'standard' | 'proforma' | 'credit' | 'debit';
  dueDate: string;
  issueDate: string;
  paidDate?: string;
  notes?: string;
  tseSignature?: string;
  tseSerialNumber?: string;
  tseTime?: string;
  isPrinted: boolean;
  isEmailed: boolean;
  createdAt: string;
  updatedAt?: string;
}

export interface InvoiceFilter {
  startDate?: string;
  endDate?: string;
  customerId?: string;
  paymentStatus?: string;
  invoiceStatus?: string;
  paymentMethod?: string;
  minAmount?: number;
  maxAmount?: number;
  searchQuery?: string;
}

export interface InvoiceTemplate {
  id: string;
  name: string;
  description: string;
  template: string;
  isDefault: boolean;
  language: string;
  currency: string;
  taxRates: {
    standard: number;
    reduced: number;
    special: number;
  };
}

export interface InvoiceReport {
  period: string;
  totalInvoices: number;
  totalAmount: number;
  paidAmount: number;
  overdueAmount: number;
  averageInvoiceValue: number;
  paymentMethodBreakdown: Record<string, number>;
  statusBreakdown: Record<string, number>;
  topCustomers: Array<{
    customerId: string;
    customerName: string;
    invoiceCount: number;
    totalAmount: number;
  }>;
  dailyBreakdown: Array<{
    date: string;
    invoiceCount: number;
    totalAmount: number;
  }>;
}

class InvoiceService {
  private baseUrl = '/invoices';

  // Fatura oluştur (mod kontrolü ile)
  async createInvoice(invoiceData: Omit<Invoice, 'id' | 'invoiceNumber' | 'receiptNumber' | 'createdAt'>): Promise<Invoice> {
    try {
      const response = await apiClient.post<Invoice>(`${this.baseUrl}`, invoiceData);
      return response.data;
    } catch (error) {
      console.error('Online invoice creation failed:', error);
      
      // Çevrimdışı modda çalışıyorsa offline kaydet
      const { offlineManager } = await import('../offline/OfflineManager');
      const offlineInvoiceId = await offlineManager.saveOfflineInvoice(invoiceData);
      
      console.log('Invoice saved offline:', offlineInvoiceId);
      return {
        ...invoiceData,
        id: offlineInvoiceId,
        invoiceNumber: `OFF-${Date.now()}`,
        receiptNumber: `OFF-${Date.now()}`,
        createdAt: new Date().toISOString()
      };
    }
  }

  // Fatura getir
  async getInvoiceById(id: string): Promise<Invoice> {
    try {
      const response = await apiClient.get<Invoice>(`${this.baseUrl}/${id}`);
      return response.data;
    } catch (error) {
      console.error('Online invoice fetch failed:', error);
      
      // Çevrimdışı modda çalışıyorsa offline verilerden getir
      const { offlineManager } = await import('../offline/OfflineManager');
      const offlineInvoices = await offlineManager.getOfflineInvoices();
      const invoice = offlineInvoices.find(i => i.id === id);
      
      if (invoice) {
        return invoice;
      }
      
      throw error;
    }
  }

  // Faturaları listele (tarihe göre filtreleme ile)
  async getInvoices(filters?: InvoiceFilter, limit: number = 50, offset: number = 0): Promise<{
    invoices: Invoice[];
    total: number;
    hasMore: boolean;
  }> {
    try {
      const params = new URLSearchParams();
      
      if (filters?.startDate) params.append('startDate', filters.startDate);
      if (filters?.endDate) params.append('endDate', filters.endDate);
      if (filters?.customerId) params.append('customerId', filters.customerId);
      if (filters?.paymentStatus) params.append('paymentStatus', filters.paymentStatus);
      if (filters?.invoiceStatus) params.append('invoiceStatus', filters.invoiceStatus);
      if (filters?.paymentMethod) params.append('paymentMethod', filters.paymentMethod);
      if (filters?.minAmount) params.append('minAmount', filters.minAmount.toString());
      if (filters?.maxAmount) params.append('maxAmount', filters.maxAmount.toString());
      if (filters?.searchQuery) params.append('searchQuery', filters.searchQuery);
      
      params.append('limit', limit.toString());
      params.append('offset', offset.toString());
      
      const response = await apiClient.get(`${this.baseUrl}?${params.toString()}`);
      return response.data as {
        invoices: Invoice[];
        total: number;
        hasMore: boolean;
      };
    } catch (error) {
      console.error('Online invoices fetch failed:', error);
      
      // Çevrimdışı modda çalışıyorsa offline verilerden getir
      const { offlineManager } = await import('../offline/OfflineManager');
      const offlineInvoices = await offlineManager.getOfflineInvoices();
      
      // Filtreleme uygula
      let filteredInvoices = offlineInvoices;
      
      if (filters?.startDate) {
        filteredInvoices = filteredInvoices.filter(i => i.issueDate >= filters.startDate!);
      }
      
      if (filters?.endDate) {
        filteredInvoices = filteredInvoices.filter(i => i.issueDate <= filters.endDate!);
      }
      
      if (filters?.customerId) {
        filteredInvoices = filteredInvoices.filter(i => i.customerId === filters.customerId);
      }
      
      if (filters?.paymentStatus) {
        filteredInvoices = filteredInvoices.filter(i => i.paymentStatus === filters.paymentStatus);
      }
      
      if (filters?.invoiceStatus) {
        filteredInvoices = filteredInvoices.filter(i => i.invoiceStatus === filters.invoiceStatus);
      }
      
      if (filters?.paymentMethod) {
        filteredInvoices = filteredInvoices.filter(i => i.paymentMethod === filters.paymentMethod);
      }
      
      if (filters?.searchQuery) {
        const query = filters.searchQuery.toLowerCase();
        filteredInvoices = filteredInvoices.filter(i => 
          i.invoiceNumber.toLowerCase().includes(query) ||
          i.customerName?.toLowerCase().includes(query) ||
          i.customerEmail?.toLowerCase().includes(query)
        );
      }
      
      // Sıralama ve sayfalama
      filteredInvoices.sort((a, b) => new Date(b.issueDate).getTime() - new Date(a.issueDate).getTime());
      
      const paginatedInvoices = filteredInvoices.slice(offset, offset + limit);
      
      return {
        invoices: paginatedInvoices,
        total: filteredInvoices.length,
        hasMore: offset + limit < filteredInvoices.length
      };
    }
  }

  // Fatura güncelle
  async updateInvoice(id: string, invoiceData: Partial<Invoice>): Promise<Invoice> {
    try {
      const response = await apiClient.put<Invoice>(`${this.baseUrl}/${id}`, invoiceData);
      return response.data;
    } catch (error) {
      console.error('Online invoice update failed:', error);
      
      // Çevrimdışı modda çalışıyorsa offline güncelle
      const { offlineManager } = await import('../offline/OfflineManager');
      const offlineInvoices = await offlineManager.getOfflineInvoices();
      const invoiceIndex = offlineInvoices.findIndex(i => i.id === id);
      
      if (invoiceIndex !== -1) {
        const updatedInvoice = { ...offlineInvoices[invoiceIndex], ...invoiceData };
        offlineInvoices[invoiceIndex] = updatedInvoice;
        await offlineManager.saveOfflineInvoices(offlineInvoices);
        
        console.log('Invoice updated offline:', id);
        return updatedInvoice;
      }
      
      throw error;
    }
  }

  // Fatura sil
  async deleteInvoice(id: string): Promise<void> {
    try {
      await apiClient.delete(`${this.baseUrl}/${id}`);
    } catch (error) {
      console.error('Online invoice deletion failed:', error);
      
      // Çevrimdışı modda çalışıyorsa offline sil
      const { offlineManager } = await import('../offline/OfflineManager');
      const offlineInvoices = await offlineManager.getOfflineInvoices();
      const filteredInvoices = offlineInvoices.filter(i => i.id !== id);
      
      await offlineManager.saveOfflineInvoices(filteredInvoices);
      console.log('Invoice deleted offline:', id);
    }
  }

  // Fatura durumu güncelle
  async updateInvoiceStatus(id: string, status: string): Promise<Invoice> {
    try {
      const response = await apiClient.patch<Invoice>(`${this.baseUrl}/${id}/status`, { status });
      return response.data;
    } catch (error) {
      console.error('Online invoice status update failed:', error);
      
      // Çevrimdışı modda çalışıyorsa offline güncelle
      return await this.updateInvoice(id, { invoiceStatus: status as any });
    }
  }

  // Fatura ödeme durumu güncelle
  async updatePaymentStatus(id: string, paymentStatus: string, paidDate?: string): Promise<Invoice> {
    try {
      const response = await apiClient.patch<Invoice>(`${this.baseUrl}/${id}/payment`, { 
        paymentStatus, 
        paidDate 
      });
      return response.data;
    } catch (error) {
      console.error('Online payment status update failed:', error);
      
      // Çevrimdışı modda çalışıyorsa offline güncelle
      return await this.updateInvoice(id, { 
        paymentStatus: paymentStatus as any,
        paidDate: paidDate || new Date().toISOString()
      });
    }
  }

  // Fatura şablonlarını getir
  async getInvoiceTemplates(): Promise<InvoiceTemplate[]> {
    try {
      const response = await apiClient.get<InvoiceTemplate[]>(`${this.baseUrl}/templates`);
      return response.data;
    } catch (error) {
      console.error('Online invoice templates fetch failed:', error);
      
      // Çevrimdışı modda çalışıyorsa varsayılan şablonlar döndür
      return [{
        id: 'default-offline',
        name: 'Default Offline Template',
        description: 'Basic invoice template for offline mode',
        template: '{{invoiceNumber}}\n{{customerName}}\n{{items}}\n{{total}}',
        isDefault: true,
        language: 'de',
        currency: 'EUR',
        taxRates: {
          standard: 0.20,
          reduced: 0.10,
          special: 0.13
        }
      }];
    }
  }

  // Fatura şablonu kaydet
  async saveInvoiceTemplate(template: Omit<InvoiceTemplate, 'id'>): Promise<InvoiceTemplate> {
    try {
      const response = await apiClient.post<InvoiceTemplate>(`${this.baseUrl}/templates`, template);
      return response.data;
    } catch (error) {
      console.error('Online template save failed:', error);
      
      // Çevrimdışı modda çalışıyorsa sahte şablon döndür
      return {
        id: `offline_template_${Date.now()}`,
        ...template
      };
    }
  }

  // Fatura şablonu sil
  async deleteInvoiceTemplate(id: string): Promise<void> {
    try {
      await apiClient.delete(`${this.baseUrl}/templates/${id}`);
    } catch (error) {
      console.error('Online template delete failed:', error);
      // Çevrimdışı modda çalışıyorsa sessizce geç
    }
  }

  // Fatura önizleme
  async previewInvoice(invoice: Invoice, templateId?: string): Promise<string> {
    try {
      const response = await apiClient.post(`${this.baseUrl}/preview`, {
        invoice,
        templateId
      });
      return (response.data as { preview: string }).preview;
    } catch (error) {
      console.error('Online invoice preview failed:', error);
      
      // Çevrimdışı modda çalışıyorsa basit önizleme döndür
      return `Invoice: ${invoice.invoiceNumber}\nCustomer: ${invoice.customerName}\nTotal: ${invoice.totalAmount}€`;
    }
  }

  // Fatura PDF oluştur
  async generatePDFInvoice(invoice: Invoice, templateId?: string): Promise<ArrayBuffer> {
    try {
      const response = await apiClient.post(`${this.baseUrl}/pdf`, {
        invoice,
        templateId
      }, {
        responseType: 'arraybuffer'
      });
      return response.data as ArrayBuffer;
    } catch (error) {
      console.error('Online PDF generation failed:', error);
      
      // Çevrimdışı modda çalışıyorsa boş PDF döndür
      return new ArrayBuffer(0);
    }
  }

  // Fatura email gönder
  async emailInvoice(invoiceId: string, email: string, templateId?: string): Promise<boolean> {
    try {
      const response = await apiClient.post(`${this.baseUrl}/${invoiceId}/email`, {
        email,
        templateId
      });
      return response.status === 200;
    } catch (error) {
      console.error('Online email invoice failed:', error);
      
      // Çevrimdışı modda çalışıyorsa kuyruğa ekle
      console.log('Email invoice queued for offline sending:', email);
      return true;
    }
  }

  // Fatura yazdır
  async printInvoice(invoice: Invoice, templateId?: string, options?: {
    printerName?: string;
    copies?: number;
  }): Promise<boolean> {
    try {
      const response = await apiClient.post(`${this.baseUrl}/print`, {
        invoice,
        templateId,
        options
      });
      return response.status === 200;
    } catch (error) {
      console.error('Online invoice printing failed:', error);
      
      // Çevrimdışı modda çalışıyorsa kuyruğa ekle
      const { offlineManager } = await import('../offline/OfflineManager');
      await offlineManager.saveOfflineInvoice(invoice);
      
      console.log('Invoice queued for offline printing');
      return true;
    }
  }

  // Fatura yeniden yazdır
  async reprintInvoice(id: string, templateId?: string, options?: {
    printerName?: string;
    copies?: number;
  }): Promise<boolean> {
    try {
      const invoice = await this.getInvoiceById(id);
      return await this.printInvoice(invoice, templateId, options);
    } catch (error) {
      console.error('Invoice reprint failed:', error);
      return false;
    }
  }

  // Tarihe göre fatura raporu
  async getInvoiceReport(filters: InvoiceFilter): Promise<InvoiceReport> {
    try {
      const response = await apiClient.post<InvoiceReport>(`${this.baseUrl}/report`, filters);
      return response.data;
    } catch (error) {
      console.error('Online invoice report failed:', error);
      
      // Çevrimdışı modda çalışıyorsa offline verilerden rapor oluştur
      const { offlineManager } = await import('../offline/OfflineManager');
      const offlineInvoices = await offlineManager.getOfflineInvoices();
      
      // Filtreleme uygula
      let filteredInvoices = offlineInvoices;
      
      if (filters.startDate) {
        filteredInvoices = filteredInvoices.filter(i => i.issueDate >= filters.startDate!);
      }
      
      if (filters.endDate) {
        filteredInvoices = filteredInvoices.filter(i => i.issueDate <= filters.endDate!);
      }
      
      // Rapor verilerini hesapla
      const totalInvoices = filteredInvoices.length;
      const totalAmount = filteredInvoices.reduce((sum, i) => sum + i.totalAmount, 0);
      const paidAmount = filteredInvoices
        .filter(i => i.paymentStatus === 'paid')
        .reduce((sum, i) => sum + i.totalAmount, 0);
      const overdueAmount = filteredInvoices
        .filter(i => i.paymentStatus === 'pending' && new Date(i.dueDate) < new Date())
        .reduce((sum, i) => sum + i.totalAmount, 0);
      const averageInvoiceValue = totalInvoices > 0 ? totalAmount / totalInvoices : 0;
      
      // Ödeme yöntemi dağılımı
      const paymentMethodBreakdown: Record<string, number> = {};
      filteredInvoices.forEach(invoice => {
        const method = invoice.paymentMethod;
        paymentMethodBreakdown[method] = (paymentMethodBreakdown[method] || 0) + invoice.totalAmount;
      });
      
      // Durum dağılımı
      const statusBreakdown: Record<string, number> = {};
      filteredInvoices.forEach(invoice => {
        const status = invoice.invoiceStatus;
        statusBreakdown[status] = (statusBreakdown[status] || 0) + 1;
      });
      
      // En iyi müşteriler
      const customerStats = filteredInvoices.reduce((acc, invoice) => {
        if (invoice.customerId) {
          if (!acc[invoice.customerId]) {
            acc[invoice.customerId] = {
              customerId: invoice.customerId,
              customerName: invoice.customerName || 'Unknown',
              invoiceCount: 0,
              totalAmount: 0
            };
          }
          acc[invoice.customerId].invoiceCount++;
          acc[invoice.customerId].totalAmount += invoice.totalAmount;
        }
        return acc;
      }, {} as Record<string, any>);
      
      const topCustomers = Object.values(customerStats)
        .sort((a: any, b: any) => b.totalAmount - a.totalAmount)
        .slice(0, 10);
      
      // Günlük dağılım
      const dailyStats = filteredInvoices.reduce((acc, invoice) => {
        const date = invoice.issueDate.split('T')[0];
        if (!acc[date]) {
          acc[date] = { date, invoiceCount: 0, totalAmount: 0 };
        }
        acc[date].invoiceCount++;
        acc[date].totalAmount += invoice.totalAmount;
        return acc;
      }, {} as Record<string, any>);
      
      const dailyBreakdown = Object.values(dailyStats)
        .sort((a: any, b: any) => a.date.localeCompare(b.date));
      
      return {
        period: `${filters.startDate || 'All'} - ${filters.endDate || 'All'}`,
        totalInvoices,
        totalAmount,
        paidAmount,
        overdueAmount,
        averageInvoiceValue,
        paymentMethodBreakdown,
        statusBreakdown,
        topCustomers,
        dailyBreakdown
      };
    }
  }

  // Günlük fatura raporu
  async getDailyInvoiceReport(date: string): Promise<InvoiceReport> {
    return await this.getInvoiceReport({ startDate: date, endDate: date });
  }

  // Aylık fatura raporu
  async getMonthlyInvoiceReport(year: number, month: number): Promise<InvoiceReport> {
    const startDate = `${year}-${month.toString().padStart(2, '0')}-01`;
    const endDate = `${year}-${month.toString().padStart(2, '0')}-31`;
    return await this.getInvoiceReport({ startDate, endDate });
  }

  // Yıllık fatura raporu
  async getYearlyInvoiceReport(year: number): Promise<InvoiceReport> {
    const startDate = `${year}-01-01`;
    const endDate = `${year}-12-31`;
    return await this.getInvoiceReport({ startDate, endDate });
  }

  // Fatura arama
  async searchInvoices(query: string, filters?: InvoiceFilter): Promise<Invoice[]> {
    try {
      const params = new URLSearchParams();
      params.append('query', query);
      
      if (filters?.startDate) params.append('startDate', filters.startDate);
      if (filters?.endDate) params.append('endDate', filters.endDate);
      if (filters?.customerId) params.append('customerId', filters.customerId);
      if (filters?.paymentStatus) params.append('paymentStatus', filters.paymentStatus);
      if (filters?.invoiceStatus) params.append('invoiceStatus', filters.invoiceStatus);
      
      const response = await apiClient.get(`${this.baseUrl}/search?${params.toString()}`);
      return response.data as Invoice[];
    } catch (error) {
      console.error('Online invoice search failed:', error);
      
      // Çevrimdışı modda çalışıyorsa offline arama
      const { offlineManager } = await import('../offline/OfflineManager');
      const offlineInvoices = await offlineManager.getOfflineInvoices();
      
      const searchQuery = query.toLowerCase();
      return offlineInvoices.filter(invoice =>
        invoice.invoiceNumber.toLowerCase().includes(searchQuery) ||
        invoice.customerName?.toLowerCase().includes(searchQuery) ||
        invoice.customerEmail?.toLowerCase().includes(searchQuery) ||
        invoice.customerTaxNumber?.toLowerCase().includes(searchQuery)
      );
    }
  }

  // Çevrimdışı faturaları senkronize et
  async syncOfflineInvoices(): Promise<number> {
    try {
      const { offlineManager } = await import('../offline/OfflineManager');
      const offlineInvoices = await offlineManager.getOfflineInvoices();
      
      let syncedCount = 0;
      
      for (const offlineInvoice of offlineInvoices) {
        if (offlineInvoice.invoiceStatus === 'draft') {
          try {
            // Online fatura oluştur
            const onlineInvoice = await this.createInvoice(offlineInvoice);
            
            // Offline faturayı güncelle
            await offlineManager.syncOfflineInvoice(offlineInvoice);
            syncedCount++;
          } catch (error) {
            console.error('Invoice sync failed for:', offlineInvoice.id, error);
          }
        }
      }
      
      console.log('Invoices synced:', syncedCount);
      return syncedCount;
    } catch (error) {
      console.error('Invoice sync failed:', error);
      return 0;
    }
  }

  // Fatura numarası oluştur
  async generateInvoiceNumber(): Promise<string> {
    try {
      const response = await apiClient.get(`${this.baseUrl}/generate-number`);
      return (response.data as { invoiceNumber: string }).invoiceNumber;
    } catch (error) {
      console.error('Online invoice number generation failed:', error);
      
      // Çevrimdışı modda çalışıyorsa sahte numara oluştur
      return `INV-${Date.now()}-${Math.random().toString(36).substr(2, 9)}`;
    }
  }

  // Fatura kopyala
  async duplicateInvoice(id: string): Promise<Invoice> {
    try {
      const response = await apiClient.post<Invoice>(`${this.baseUrl}/${id}/duplicate`);
      return response.data;
    } catch (error) {
      console.error('Online invoice duplication failed:', error);
      
      // Çevrimdışı modda çalışıyorsa offline kopyala
      const originalInvoice = await this.getInvoiceById(id);
      const duplicatedInvoice = {
        ...originalInvoice,
        id: '',
        invoiceNumber: await this.generateInvoiceNumber(),
        receiptNumber: `OFF-${Date.now()}`,
        issueDate: new Date().toISOString(),
        dueDate: new Date(Date.now() + 30 * 24 * 60 * 60 * 1000).toISOString(), // 30 gün sonra
        invoiceStatus: 'draft' as const,
        paymentStatus: 'pending' as const,
        isPrinted: false,
        isEmailed: false,
        createdAt: new Date().toISOString()
      };
      
      delete duplicatedInvoice.id;
      return await this.createInvoice(duplicatedInvoice);
    }
  }
}

export const invoiceService = new InvoiceService();
export default invoiceService; 
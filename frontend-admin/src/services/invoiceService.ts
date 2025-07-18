import api from './api';

export interface Invoice {
  id: string;
  receiptNumber: string;
  customerId: string;
  customer?: {
    id: string;
    firstName: string;
    lastName: string;
    email: string;
  };
  invoiceDate: string;
  dueDate: string;
  totalAmount: number;
  taxAmount: number;
  subtotal: number;
  status: 'draft' | 'sent' | 'paid' | 'overdue' | 'cancelled';
  paymentMethod?: 'cash' | 'card' | 'voucher';
  paymentStatus: 'pending' | 'paid' | 'overdue';
  isPrinted: boolean;
  tseSignature?: string;
  notes?: string;
  items: InvoiceItem[];
  createdAt: string;
  updatedAt: string;
}

export interface InvoiceItem {
  id: string;
  productId: string;
  product?: {
    id: string;
    name: string;
    price: number;
  };
  quantity: number;
  unitPrice: number;
  totalPrice: number;
  taxType: 'standard' | 'reduced' | 'special';
  taxRate: number;
  taxAmount: number;
}

export interface InvoiceCreateRequest {
  customerId: string;
  items: {
    productId: string;
    quantity: number;
    taxType: 'standard' | 'reduced' | 'special';
  }[];
  notes?: string;
  dueDate?: string;
}

export interface InvoiceUpdateRequest {
  customerId?: string;
  items?: {
    productId: string;
    quantity: number;
    taxType: 'standard' | 'reduced' | 'special';
  }[];
  notes?: string;
  dueDate?: string;
  status?: 'draft' | 'sent' | 'paid' | 'overdue' | 'cancelled';
}

export interface InvoiceFilterRequest {
  customerId?: string;
  status?: string;
  paymentStatus?: string;
  startDate?: string;
  endDate?: string;
  minAmount?: number;
  maxAmount?: number;
  search?: string;
}

export interface InvoiceStatistics {
  totalInvoices: number;
  totalRevenue: number;
  pendingInvoices: number;
  overdueInvoices: number;
  averageInvoiceAmount: number;
  monthlyRevenue: number;
  topCustomers: Array<{
    customerId: string;
    customerName: string;
    totalAmount: number;
    invoiceCount: number;
  }>;
}

// Fatura oluşturma
export const createInvoice = async (data: InvoiceCreateRequest): Promise<Invoice> => {
  const response = await api.post('/api/invoices', data);
  return response.data;
};

// Fatura güncelleme
export const updateInvoice = async (id: string, data: InvoiceUpdateRequest): Promise<Invoice> => {
  const response = await api.put(`/api/invoices/${id}`, data);
  return response.data;
};

// Fatura silme
export const deleteInvoice = async (id: string): Promise<void> => {
  await api.delete(`/api/invoices/${id}`);
};

// Fatura detayı getirme
export const getInvoice = async (id: string): Promise<Invoice> => {
  const response = await api.get(`/api/invoices/${id}`);
  return response.data;
};

// Faturaları listeleme (filtreli)
export const getInvoices = async (filters?: InvoiceFilterRequest): Promise<Invoice[]> => {
  const params = filters ? new URLSearchParams() : undefined;
  if (filters) {
    Object.entries(filters).forEach(([key, value]) => {
      if (value !== undefined && value !== null) {
        params?.append(key, value.toString());
      }
    });
  }
  
  const response = await api.get('/api/invoices', { params });
  return response.data;
};

// Fatura istatistikleri
export const getInvoiceStatistics = async (): Promise<InvoiceStatistics> => {
  const response = await api.get('/api/invoices/statistics');
  return response.data;
};

// Ödeme kaydetme
export const savePayment = async (id: string, paymentData: {
  paymentMethod: 'cash' | 'card' | 'voucher';
  amount: number;
  tseRequired: boolean;
}): Promise<Invoice> => {
  const response = await api.post(`/api/invoices/${id}/payment`, paymentData);
  return response.data;
};

// PDF indirme
export const downloadInvoicePdf = async (id: string): Promise<Blob> => {
  const response = await api.get(`/api/invoices/${id}/pdf`, {
    responseType: 'blob'
  });
  return response.data;
};

// Email gönderme
export const sendInvoiceEmail = async (id: string, emailData: {
  email: string;
  subject?: string;
  message?: string;
}): Promise<{ success: boolean; message: string }> => {
  const response = await api.post(`/api/invoices/${id}/email`, emailData);
  return response.data;
};

// Fatura iptal etme
export const cancelInvoice = async (id: string, reason: string): Promise<Invoice> => {
  const response = await api.post(`/api/invoices/${id}/cancel`, { reason });
  return response.data;
};

// FinanzOnline'a gönderme
export const sendToFinanzOnline = async (id: string): Promise<{ success: boolean; message: string }> => {
  const response = await api.post(`/api/invoices/${id}/finanzonline`);
  return response.data;
};

// TSE imzası doğrulama
export const verifyTseSignature = async (id: string): Promise<{ isValid: boolean; message: string }> => {
  const response = await api.post(`/api/invoices/${id}/verify-tse`);
  return response.data;
};

// Toplu işlemler
export const bulkUpdateInvoices = async (ids: string[], updates: Partial<InvoiceUpdateRequest>): Promise<Invoice[]> => {
  const response = await api.put('/api/invoices/bulk', { ids, updates });
  return response.data;
};

export const bulkDeleteInvoices = async (ids: string[]): Promise<void> => {
  await api.delete('/api/invoices/bulk', { data: { ids } });
};

// Fatura şablonları
export const getInvoiceTemplates = async (): Promise<any[]> => {
  const response = await api.get('/api/invoices/templates');
  return response.data;
};

export const createInvoiceFromTemplate = async (templateId: string, data: any): Promise<Invoice> => {
  const response = await api.post(`/api/invoices/templates/${templateId}`, data);
  return response.data;
};

// PDF'i indir ve kaydet
export const downloadAndSavePdf = async (id: string, filename?: string): Promise<void> => {
  try {
    const blob = await downloadInvoicePdf(id);
    const url = window.URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = filename || `invoice_${id}.pdf`;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
    window.URL.revokeObjectURL(url);
  } catch (error) {
    console.error('PDF indirme hatası:', error);
    throw error;
  }
}; 
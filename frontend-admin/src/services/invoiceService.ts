import api from '../services/api';

export interface InvoiceItem {
  productId: number;
  quantity: number;
  discountPercentage: number;
  // Diğer alanlar backend'e göre eklenebilir
}

export interface Invoice {
  id: string;
  receiptNumber: string;
  createdAt: string;
  customerId: number;
  paymentMethod: string;
  notes: string;
  registerId: number;
  discountAmount: number;
  status: string;
  items: InvoiceItem[];
  // Diğer alanlar backend'e göre eklenebilir
}

export interface CreateInvoiceRequest {
  customerId: number;
  paymentMethod: string;
  notes?: string;
  registerId: number;
  discountAmount?: number;
  items: InvoiceItem[];
}

export async function getInvoices() {
  const response = await api.get<Invoice[]>('/api/invoice');
  return response.data;
}

export async function getInvoice(id: string) {
  const response = await api.get<Invoice>(`/api/invoice/${id}`);
  return response.data;
}

export async function createInvoice(data: CreateInvoiceRequest) {
  const response = await api.post<Invoice>('/api/invoice', data);
  return response.data;
} 
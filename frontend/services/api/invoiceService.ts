// ❌ DEPRECATED: useFetch kaldırıldı - useApiManager kullanın
// import { useFetch } from './useFetch';
import { apiClient, API_BASE_URL } from './config';
import AsyncStorage from '@react-native-async-storage/async-storage';

// ❌ DEPRECATED: Bu hook'lar artık kullanılmamalı
// ✅ YENİ: useApiManager ile apiClient.get/post/put/delete kullanın

// export function useInvoices(params?: any) {
//   let url = '/api/invoices';
//   if (params) {
//     const queryParams = new URLSearchParams(params);
//     url += `?${queryParams.toString()}`;
//   }
//   return useFetch<any[]>(url);
// }

// export function useInvoice(id: string) {
//   return useFetch<any>(`/api/invoices/${id}`);
// }

// export function useCreateInvoice() {
//   return useFetch<any>('/api/invoices', { method: 'POST' });
// }

// export function useUpdateInvoice(id: string) {
//   return useFetch<any>(`/api/invoices/${id}`, { method: 'PUT' });
// }

// export function useDeleteInvoice(id: string) {
//   return useFetch<any>(`/api/invoices/${id}`, { method: 'DELETE' });
// }

// Tüm faturaları getir
export const getInvoices = async () => {
  return await apiClient.get('/invoice');
};

// Fatura istatistiklerini getir
export const getInvoiceStatistics = async () => {
  return await apiClient.get('/invoice/statistics');
};

// Fatura oluştur
export const createInvoice = async (data: any) => {
  return await apiClient.post('/invoice', data);
};

// Fatura sil
export const deleteInvoice = async (id: string) => {
  return await apiClient.delete(`/invoice/${id}`);
};

// Fatura PDF indir
export const downloadInvoicePdf = async (id: string) => {
  const response = await fetch(`${API_BASE_URL}/invoice/${id}/pdf`, {
    method: 'GET',
    headers: { Authorization: `Bearer ${await AsyncStorage.getItem('token')}` }
  });
  return await response.blob();
};

// Fatura CSV indir
export const downloadInvoiceCsv = async (id: string) => {
  const response = await fetch(`${API_BASE_URL}/invoice/${id}/csv`, {
    method: 'GET',
    headers: { Authorization: `Bearer ${await AsyncStorage.getItem('token')}` }
  });
  return await response.blob();
};

// Fatura email gönder
export const sendInvoiceEmail = async (id: string, data: any) => {
  return await apiClient.post(`/invoice/${id}/email`, data);
};

// Fatura iptal et
export const cancelInvoice = async (id: string, reason: string) => {
  return await apiClient.post(`/invoice/${id}/cancel`, { reason });
};

// Fatura ödeme kaydet
export const savePayment = async (id: string, data: any) => {
  return await apiClient.post(`/invoice/${id}/payment`, data);
};

// FinanzOnline'a gönder
export const sendToFinanzOnline = async (id: string) => {
  return await apiClient.post(`/invoice/${id}/finanzonline`);
}; 
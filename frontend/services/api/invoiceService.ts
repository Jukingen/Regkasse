import { useFetch } from './useFetch';

export function useInvoices(params?: any) {
  let url = '/api/invoices';
  if (params) {
    const queryParams = new URLSearchParams(params);
    url += `?${queryParams.toString()}`;
  }
  return useFetch<any[]>(url);
}

export function useInvoice(id: string) {
  return useFetch<any>(`/api/invoices/${id}`);
}

export function useCreateInvoice() {
  return useFetch<any>('/api/invoices', { method: 'POST' });
}

export function useUpdateInvoice(id: string) {
  return useFetch<any>(`/api/invoices/${id}`, { method: 'PUT' });
}

export function useDeleteInvoice(id: string) {
  return useFetch<any>(`/api/invoices/${id}`, { method: 'DELETE' });
} 
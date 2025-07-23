import { useFetch } from './useFetch';

export interface Customer {
  id: string;
  name: string;
  email: string;
  phone: string;
  address: string;
  taxNumber: string;
  category: 'Regular' | 'VIP' | 'Wholesale' | 'Corporate';
  discountPercentage: number;
  notes?: string;
}

export interface CustomerSearchParams {
  category?: string;
  search?: string;
}

export function useCustomers(params?: CustomerSearchParams) {
  let url = '/api/customers';
  if (params) {
    const queryParams = new URLSearchParams();
    if (params.category) queryParams.append('category', params.category);
    if (params.search) queryParams.append('search', params.search);
    url += `?${queryParams.toString()}`;
  }
  return useFetch<Customer[]>(url);
}

export function useCustomer(id: string) {
  return useFetch<Customer>(`/api/customers/${id}`);
}

export function useCustomersByCategory(category: string) {
  return useFetch<Customer[]>(`/api/customers?category=${category}`);
}

export function useSearchCustomers(query: string) {
  return useFetch<Customer[]>(`/api/customers?search=${encodeURIComponent(query)}`);
} 
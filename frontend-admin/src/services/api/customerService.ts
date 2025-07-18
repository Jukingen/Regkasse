import { apiClient } from './apiClient';

export interface Customer {
  id: string;
  name: string;
  email: string;
  phone: string;
  address: string;
  taxNumber: string;
  category: 'Regular' | 'Premium' | 'VIP';
  discountPercentage: number;
  isActive: boolean;
  createdAt: string;
  updatedAt: string;
  notes?: string;
  invoiceCount?: number;
  orderCount?: number;
  discountCount?: number;
}

export interface CreateCustomerRequest {
  name: string;
  email: string;
  phone: string;
  address: string;
  taxNumber: string;
  category: 'Regular' | 'Premium' | 'VIP';
  discountPercentage: number;
  notes?: string;
}

export interface UpdateCustomerRequest {
  name: string;
  email: string;
  phone: string;
  address: string;
  taxNumber: string;
  category: 'Regular' | 'Premium' | 'VIP';
  discountPercentage: number;
  notes?: string;
}

export interface CustomerSearchParams {
  search?: string;
  category?: 'Regular' | 'Premium' | 'VIP';
  isActive?: boolean;
}

class CustomerService {
  // Tüm müşterileri getir
  async getCustomers(params?: CustomerSearchParams): Promise<Customer[]> {
    const queryParams = new URLSearchParams();
    if (params?.search) queryParams.append('search', params.search);
    if (params?.category) queryParams.append('category', params.category);
    if (params?.isActive !== undefined) queryParams.append('isActive', params.isActive.toString());

    const response = await apiClient.get(`/customers?${queryParams.toString()}`);
    return response.data;
  }

  // Tek müşteri getir
  async getCustomer(id: string): Promise<Customer> {
    const response = await apiClient.get(`/customers/${id}`);
    return response.data;
  }

  // Yeni müşteri oluştur
  async createCustomer(data: CreateCustomerRequest): Promise<Customer> {
    const response = await apiClient.post('/customers', data);
    return response.data;
  }

  // Müşteri güncelle
  async updateCustomer(id: string, data: UpdateCustomerRequest): Promise<Customer> {
    const response = await apiClient.put(`/customers/${id}`, data);
    return response.data;
  }

  // Müşteri sil
  async deleteCustomer(id: string): Promise<void> {
    await apiClient.delete(`/customers/${id}`);
  }

  // Müşteri durumu güncelle
  async updateCustomerStatus(id: string, isActive: boolean): Promise<Partial<Customer>> {
    const response = await apiClient.put(`/customers/${id}/status`, { isActive });
    return response.data;
  }

  // Müşteri arama
  async searchCustomers(query: string): Promise<Customer[]> {
    const response = await apiClient.get(`/customers?search=${encodeURIComponent(query)}`);
    return response.data;
  }

  // Kategoriye göre müşteriler
  async getCustomersByCategory(category: 'Regular' | 'Premium' | 'VIP'): Promise<Customer[]> {
    const response = await apiClient.get(`/customers?category=${category}`);
    return response.data;
  }

  // İstatistikler
  async getCustomerStats(): Promise<{
    total: number;
    active: number;
    inactive: number;
    byCategory: { [key: string]: number };
  }> {
    const customers = await this.getCustomers();
    
    const stats = {
      total: customers.length,
      active: customers.filter(c => c.isActive).length,
      inactive: customers.filter(c => !c.isActive).length,
      byCategory: {
        Regular: customers.filter(c => c.category === 'Regular').length,
        Premium: customers.filter(c => c.category === 'Premium').length,
        VIP: customers.filter(c => c.category === 'VIP').length,
      }
    };

    return stats;
  }
}

export const customerService = new CustomerService(); 
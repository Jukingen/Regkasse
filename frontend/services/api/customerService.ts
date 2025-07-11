import { apiClient } from './config';

export interface Customer {
    id: string;
    customerNumber: string;
    firstName: string;
    lastName: string;
    email?: string;
    phone?: string;
    address?: string;
    city?: string;
    postalCode?: string;
    country?: string;
    taxNumber?: string;
    companyName?: string;
    customerType?: 'individual' | 'business';
    createdAt: string;
    updatedAt: string;
}

export const customerService = {
    getAllCustomers: async (): Promise<Customer[]> => {
        return apiClient.get<Customer[]>('/customers');
    },

    getCustomerById: async (id: string): Promise<Customer> => {
        return apiClient.get<Customer>(`/customers/${id}`);
    },

    createCustomer: async (customer: Omit<Customer, 'id' | 'createdAt' | 'updatedAt'>): Promise<Customer> => {
        return apiClient.post<Customer>('/customers', customer);
    },

    updateCustomer: async (id: string, customer: Partial<Customer>): Promise<Customer> => {
        return apiClient.put<Customer>(`/customers/${id}`, customer);
    },

    deleteCustomer: async (id: string): Promise<void> => {
        await apiClient.delete(`/customers/${id}`);
    },

    searchCustomers: async (query: string): Promise<Customer[]> => {
        return apiClient.get<Customer[]>(`/customers/search?query=${encodeURIComponent(query)}`);
    }
}; 
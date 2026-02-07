import { apiClient } from './config';

export interface Customer {
  id: string;
  name: string;
  customerNumber: string;
  email: string;
  phone: string;
  address: string;
  taxNumber: string;
  category: 'Regular' | 'VIP' | 'Premium' | 'Corporate' | 'Student' | 'Senior';
  discountPercentage: number;
  loyaltyPoints: number;
  totalSpent: number;
  visitCount: number;
  notes?: string;
  isVip: boolean;
}

export interface CreateCustomerRequest {
  name: string;
  email: string;
  phone: string;
  address: string;
  taxNumber?: string;
  category?: string;
  notes?: string;
}

// Well-known guest customer ID (must match backend CustomerSeedData)
export const GUEST_CUSTOMER_ID = '00000000-0000-0000-0000-000000000001';

class CustomerService {
  private baseUrl = '/Customer';

  // Get all customers
  async getAll(): Promise<Customer[]> {
    const response = await apiClient.get<Customer[]>(`${this.baseUrl}`);
    return response;
  }

  // Get customer by ID
  async getById(id: string): Promise<Customer> {
    const response = await apiClient.get<Customer>(`${this.baseUrl}/${id}`);
    return response;
  }

  // Get customer by email
  async getByEmail(email: string): Promise<Customer> {
    const response = await apiClient.get<Customer>(`${this.baseUrl}/email/${email}`);
    return response;
  }

  // Get guest customer (walk-in)
  async getGuestCustomer(): Promise<string> {
    try {
      // Try to fetch guest customer to validate it exists
      const customer = await this.getById(GUEST_CUSTOMER_ID);
      if (customer && customer.id) {
        return customer.id;
      }
    } catch (error) {
      console.warn('[CustomerService] Guest customer not found in DB, using hardcoded ID');
    }

    // Fallback to hardcoded ID if fetch fails
    return GUEST_CUSTOMER_ID;
  }

  // Create new customer
  async create(data: CreateCustomerRequest): Promise<Customer> {
    const response = await apiClient.post<Customer>(`${this.baseUrl}`, data);
    return response;
  }
}

export const customerService = new CustomerService();
export default customerService;
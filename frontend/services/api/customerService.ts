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

/** Assignment-level summary only. Not eligibility or payment applicability (PaymentService applies additional rules). */
export interface BenefitSummaryPreview {
  assignedBenefitCount: number;
}

/** Alias for POS use; assignment-level summary only. */
export type PosBenefitPreview = BenefitSummaryPreview;

/** Eligibility preview: cart/customer-based applicability. Distinct from assignment summary. Backend DTO shape. */
export interface BenefitEligibilityPreviewResponse {
  subtotalBeforeBenefits: number;
  totalDiscountAmount: number;
  subtotalAfterBenefits: number;
  applicableBenefits: ApplicableBenefitPreviewDto[];
  blockedBenefits: BlockedBenefitPreviewDto[];
}

export interface ApplicableBenefitPreviewDto {
  kind: number;
  description: string;
  amount: number;
  quantity?: number;
  benefitDefinitionCode?: string;
}

export interface BlockedBenefitPreviewDto {
  kind: number;
  benefitDefinitionCode?: string;
  blockedReasonCode: string;
  message?: string;
  requiredMoreQuantity?: number;
}

/** Request body for eligibility preview: items only (customerId from route). */
export interface BenefitEligibilityPreviewItemRequest {
  productId: string;
  quantity: number;
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

  /**
   * Get customer by customer number (e.g. for employee identification).
   * Returns null if not found or inactive (backend returns 404).
   */
  async getByCustomerNumber(customerNumber: string): Promise<Customer | null> {
    const trimmed = String(customerNumber ?? '').trim();
    if (!trimmed) return null;
    try {
      const response = await apiClient.get<any>(`${this.baseUrl}/number/${encodeURIComponent(trimmed)}`);
      // Backend SuccessResponse returns { success, message, data }; interceptor returns response.data
      const customer = (response as any)?.data ?? response;
      return customer as Customer;
    } catch (e: any) {
      if (e?.response?.status === 404) return null;
      throw e;
    }
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

  /**
   * Assignment-level summary only: count of active assignments in validity window.
   * Does not indicate eligibility or payment-time applicability (PaymentService applies additional rules).
   */
  async getBenefitSummary(customerId: string): Promise<PosBenefitPreview | null> {
    const trimmed = String(customerId ?? '').trim();
    if (!trimmed) return null;
    try {
      const response = await apiClient.get<any>(`${this.baseUrl}/${trimmed}/benefit-summary`);
      const data = (response as any)?.data ?? response;
      const count = data?.assignedBenefitCount;
      return typeof count === 'number' ? { assignedBenefitCount: count } : null;
    } catch (e: any) {
      if (e?.response?.status === 404) return null;
      throw e;
    }
  }

  /**
   * Eligibility preview for POS: which benefits would apply for this customer and cart, and which are blocked.
   * Read-only; does not create payment. Call only when customer is selected (not guest) and cart has items.
   */
  async getBenefitEligibilityPreview(
    customerId: string,
    items: BenefitEligibilityPreviewItemRequest[]
  ): Promise<BenefitEligibilityPreviewResponse | null> {
    const trimmed = String(customerId ?? '').trim();
    if (!trimmed || !Array.isArray(items) || items.length === 0) return null;
    try {
      const response = await apiClient.post<any>(
        `${this.baseUrl}/${encodeURIComponent(trimmed)}/benefit-eligibility-preview`,
        { items }
      );
      const data = (response as any)?.data ?? response;
      if (!data || typeof data.subtotalBeforeBenefits !== 'number') return null;
      return {
        subtotalBeforeBenefits: data.subtotalBeforeBenefits ?? 0,
        totalDiscountAmount: data.totalDiscountAmount ?? 0,
        subtotalAfterBenefits: data.subtotalAfterBenefits ?? 0,
        applicableBenefits: Array.isArray(data.applicableBenefits) ? data.applicableBenefits : [],
        blockedBenefits: Array.isArray(data.blockedBenefits) ? data.blockedBenefits : [],
      };
    } catch (e: any) {
      if (e?.response?.status === 404) return null;
      console.warn('[CustomerService] Benefit eligibility preview failed:', e?.message ?? e);
      return null;
    }
  }
}

export const customerService = new CustomerService();
export default customerService;
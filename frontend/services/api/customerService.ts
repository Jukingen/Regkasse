import { apiClient } from './config';
import {
  isWalkInCustomerId,
  resolveWalkInCustomerId,
  WALK_IN_CUSTOMER_ID_FALLBACK,
} from '../../constants/walkInCustomer';

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

export const GUEST_CUSTOMER_ID = WALK_IN_CUSTOMER_ID_FALLBACK;
export { isWalkInCustomerId, resolveWalkInCustomerId, WALK_IN_CUSTOMER_ID_FALLBACK };

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
  /** POS QR scan: customer:, RK:C:, RK:CU:, regkasse://customer/, number, or email. */
  async lookupByQrPayload(qrPayload: string): Promise<Customer | null> {
    const trimmed = String(qrPayload ?? '').trim();
    if (!trimmed) return null;
    try {
      const response = await apiClient.get<unknown>(
        `/pos/customers/by-qr?qrData=${encodeURIComponent(trimmed)}`
      );
      const raw = (response as { data?: Record<string, unknown> })?.data ?? response;
      if (!raw || typeof raw !== 'object') return null;
      const row = raw as Record<string, unknown>;
      return {
        id: String(row.id ?? row.Id ?? ''),
        name: String(row.name ?? row.Name ?? ''),
        customerNumber: String(row.customerNumber ?? row.CustomerNumber ?? ''),
        email: String(row.email ?? row.Email ?? ''),
        phone: String(row.phone ?? row.Phone ?? ''),
        loyaltyPoints: Number(row.loyaltyPoints ?? row.LoyaltyPoints ?? 0),
        address: '',
        taxNumber: '',
        category: 'Regular',
        discountPercentage: 0,
        totalSpent: 0,
        visitCount: 0,
        isVip: false,
      };
    } catch (e: unknown) {
      const status = (e as { response?: { status?: number } })?.response?.status;
      if (status === 404) return null;
      throw e;
    }
  }

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

  async getGuestCustomer(): Promise<string> {
    return resolveWalkInCustomerId();
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
import { apiClient } from './config';
import { useFetch } from './useFetch';

export interface Coupon {
  id: string;
  code: string;
  name: string;
  description: string;
  discountType: 'Percentage' | 'FixedAmount' | 'BuyOneGetOne' | 'FreeShipping';
  discountValue: number;
  minimumAmount: number;
  maximumDiscount: number;
  validFrom: string;
  validUntil: string;
  usageLimit: number;
  usedCount: number;
  isActive: boolean;
  isSingleUse: boolean;
  customerCategoryRestriction?: 'Regular' | 'VIP' | 'Wholesale' | 'Corporate';
  productCategoryRestriction?: string;
}

export interface CouponValidationResult {
  isValid: boolean;
  coupon?: Coupon;
  discountAmount: number;
  message?: string;
  errorMessage?: string;
}

export interface ValidateCouponRequest {
  code: string;
  totalAmount: number;
  customerId?: string;
}

class CouponService {
  private readonly baseUrl = '/coupon';

  async getActiveCoupons(): Promise<Coupon[]> {
    return apiClient.get<Coupon[]>(`${this.baseUrl}/active`);
  }

  async validateCoupon(request: ValidateCouponRequest): Promise<CouponValidationResult> {
    return apiClient.post<CouponValidationResult>(`${this.baseUrl}/validate`, request);
  }
}

export const couponService = new CouponService();

export function useCoupons() {
  return useFetch<Coupon[]>('/api/coupon');
}

export function useCoupon(id: string) {
  return useFetch<Coupon>(`/api/coupon/${id}`);
}

export function useValidateCoupon() {
  return useFetch('/api/coupon/validate', { method: 'POST' });
}

export function useUseCoupon() {
  return useFetch('/api/coupon/use', { method: 'POST' });
}

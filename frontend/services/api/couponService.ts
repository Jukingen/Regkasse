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
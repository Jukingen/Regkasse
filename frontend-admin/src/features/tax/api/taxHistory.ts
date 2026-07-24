import { customInstance } from '@/lib/axios';

export type TaxHistoryItem = {
  id: string;
  productId: string;
  productName: string;
  taxGroupId: string;
  taxGroupName?: string | null;
  oldRate: number;
  newRate: number;
  changedAt: string;
  changedBy: string;
  reason: string;
  invoiceNumber?: string | null;
};

export const taxHistoryQueryKey = ['tax-history'] as const;

export async function getTaxHistory(params?: {
  productId?: string;
  take?: number;
}): Promise<TaxHistoryItem[]> {
  return customInstance<TaxHistoryItem[]>({
    url: '/api/admin/tax-history',
    method: 'GET',
    params: {
      productId: params?.productId,
      take: params?.take ?? 100,
    },
  });
}

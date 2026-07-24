import { customInstance } from '@/lib/axios';

export type TaxRegulation = {
  effectiveDate: string;
  standardRate: number;
  reducedRate: number;
  reducedNewRate: number;
  middleRate: number;
  zeroRate: number;
  isActive: boolean;
  description: string;
  allowedRates: number[];
};

export const taxRegulationCurrentQueryKey = ['tax-regulations', 'current'] as const;
export const taxRegulationHistoryQueryKey = ['tax-regulations', 'history'] as const;

export async function getCurrentTaxRegulation(): Promise<TaxRegulation> {
  return customInstance<TaxRegulation>({
    url: '/api/admin/tax-regulations/current',
    method: 'GET',
  });
}

export async function getTaxRegulationHistory(): Promise<TaxRegulation[]> {
  return customInstance<TaxRegulation[]>({
    url: '/api/admin/tax-regulations/history',
    method: 'GET',
  });
}

/** Client-side check against regulation allowedRates (percent). */
export function isTaxRateValidForRegulation(
  regulation: TaxRegulation | undefined | null,
  rate: number | null | undefined
): boolean {
  if (regulation == null || rate == null || !Number.isFinite(Number(rate))) {
    return true;
  }
  const normalized = Math.round(Number(rate) * 100) / 100;
  const allowed = regulation.allowedRates ?? [];
  return allowed.some((r) => Math.abs(Number(r) - normalized) < 0.001);
}

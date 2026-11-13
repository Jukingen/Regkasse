/** Published starting prices. Yearly billing charges 10 months (2 months free). This page does not take payment. */
export const BILLING_MONTHS_PER_YEAR = 10;

export const PUBLISHED_TIERS = [
  { id: 'starter', monthlyEur: 29 },
  { id: 'professional', monthlyEur: 79 },
  { id: 'enterprise', monthlyEur: null },
] as const;

export type TierId = (typeof PUBLISHED_TIERS)[number]['id'];

export function tierAmount(monthlyEur: number, annual: boolean): number {
  return annual ? monthlyEur * BILLING_MONTHS_PER_YEAR : monthlyEur;
}

export function formatEuroPeriod(amount: number, period: string): string {
  return `€${amount}/${period}`;
}

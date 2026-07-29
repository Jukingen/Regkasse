import { customInstance } from '@/lib/axios';

export type PriceChangeRequest = {
  productId: string;
  newPrice: number;
  newTaxGroupId: string;
  reason: string;
  forceInPlaceUpdate?: boolean;
};

export type RksvComplianceFinding = {
  code: string;
  message: string;
  resolution: string;
};

export type RksvPriceChangeComplianceResult = {
  isCompliant: boolean;
  warnings: RksvComplianceFinding[];
  errors: RksvComplianceFinding[];
  requirements: RksvComplianceFinding[];
  hasFiscalHistory: boolean;
  requiresNewProductVersion: boolean;
  newTaxRate?: number | null;
};

export type PriceChangeValidationResult = {
  isValid: boolean;
  hasWarning: boolean;
  errorMessage?: string | null;
  warningMessage?: string | null;
  hasFiscalHistory: boolean;
  requiresNewProductVersion: boolean;
  compliance?: RksvPriceChangeComplianceResult | null;
};

export type PriceChangeResult = {
  succeeded: boolean;
  errorMessage?: string | null;
  warningMessage?: string | null;
  productId?: string | null;
  archivedProductId?: string | null;
  priceVersionId?: string | null;
  version?: string | null;
  catalogVersion?: number | null;
  createdNewProductVersion: boolean;
  oldPrice?: number | null;
  newPrice?: number | null;
  oldTaxGroupId?: string | null;
  newTaxGroupId?: string | null;
  oldTaxRate?: number | null;
  newTaxRate?: number | null;
};

export type PriceVersionItem = {
  id: string;
  productId: string;
  productName: string;
  price: number;
  taxGroupId: string;
  taxGroupName?: string | null;
  validFrom: string;
  validTo?: string | null;
  isCurrent: boolean;
  version: string;
  createdAt: string;
};

export type PriceHistoryItem = {
  id: string;
  productId: string;
  productName: string;
  oldPrice: number;
  newPrice: number;
  oldTaxGroupId: string;
  oldTaxGroupName?: string | null;
  newTaxGroupId: string;
  newTaxGroupName?: string | null;
  oldTaxRate: number;
  newTaxRate: number;
  effectiveFrom: string;
  effectiveTo?: string | null;
  isActive: boolean;
  changedBy: string;
  reason: string;
  createdAt: string;
  isRksvCompliant: boolean;
  rksvNote?: string | null;
  rksvVerifiedAt?: string | null;
};

export type PriceHistoryReport = {
  productId: string;
  productName: string;
  catalogVersion: number;
  originalProductId?: string | null;
  isArchived: boolean;
  history: Array<{
    id: string;
    oldPrice: number;
    newPrice: number;
    oldTaxGroupId: string;
    newTaxGroupId: string;
    oldTaxRate: number;
    newTaxRate: number;
    effectiveFrom: string;
    effectiveTo?: string | null;
    isActive: boolean;
    reason: string;
    isRksvCompliant: boolean;
  }>;
  versions: Array<{
    id: string;
    price: number;
    taxGroupId: string;
    taxGroupName?: string | null;
    validFrom: string;
    validTo?: string | null;
    isCurrent: boolean;
    version: string;
  }>;
};

export const priceHistoryQueryKey = ['price-history'] as const;

export function priceHistoryVersionsQueryKey(productId: string) {
  return [...priceHistoryQueryKey, 'versions', productId] as const;
}

export function priceHistoryReportQueryKey(productId: string) {
  return [...priceHistoryQueryKey, 'report', productId] as const;
}

export async function validatePriceChange(
  body: PriceChangeRequest
): Promise<PriceChangeValidationResult> {
  return customInstance<PriceChangeValidationResult>({
    url: '/api/admin/price-history/validate',
    method: 'POST',
    data: body,
  });
}

export async function changeProductPrice(body: PriceChangeRequest): Promise<PriceChangeResult> {
  return customInstance<PriceChangeResult>({
    url: '/api/admin/price-history/change',
    method: 'POST',
    data: body,
  });
}

export async function getPriceHistory(params?: {
  productId?: string;
  take?: number;
}): Promise<PriceHistoryItem[]> {
  return customInstance<PriceHistoryItem[]>({
    url: '/api/admin/price-history',
    method: 'GET',
    params: {
      productId: params?.productId,
      take: params?.take ?? 100,
    },
  });
}

export async function getPriceVersions(
  productId: string,
  take = 100
): Promise<PriceVersionItem[]> {
  return customInstance<PriceVersionItem[]>({
    url: '/api/admin/price-history/versions',
    method: 'GET',
    params: { productId, take },
  });
}

export async function getPriceHistoryReport(productId: string): Promise<PriceHistoryReport> {
  return customInstance<PriceHistoryReport>({
    url: `/api/admin/reports/rksv/price-history/${productId}`,
    method: 'GET',
  });
}

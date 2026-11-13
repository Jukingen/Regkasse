import { customInstance } from '@/lib/axios';

export type TaxGroupAdmin = {
  id: string;
  name: string;
  description?: string | null;
  rate: number;
  isActive: boolean;
  isDefault: boolean;
  isSystem: boolean;
  color?: string | null;
  icon?: string | null;
  groupType?: string | null;
  austrianCode?: string | null;
  validFrom?: string | null;
  validTo?: string | null;
  replacedBy?: string | null;
  createdAt: string;
  updatedAt?: string | null;
};

export type UpsertTaxGroupRequest = {
  name: string;
  description?: string | null;
  rate: number;
  isActive: boolean;
  isDefault?: boolean;
  color?: string | null;
  icon?: string | null;
  austrianCode?: string | null;
  validFrom?: string | null;
  validTo?: string | null;
};

export const taxGroupsQueryKey = ['tax-groups'] as const;

export async function getTaxGroups(): Promise<TaxGroupAdmin[]> {
  return customInstance<TaxGroupAdmin[]>({
    url: '/api/admin/tax-groups',
    method: 'GET',
  });
}

export async function createTaxGroup(body: UpsertTaxGroupRequest): Promise<TaxGroupAdmin> {
  return customInstance<TaxGroupAdmin>({
    url: '/api/admin/tax-groups',
    method: 'POST',
    data: body,
  });
}

export async function updateTaxGroup(
  id: string,
  body: UpsertTaxGroupRequest
): Promise<TaxGroupAdmin> {
  return customInstance<TaxGroupAdmin>({
    url: `/api/admin/tax-groups/${id}`,
    method: 'PUT',
    data: body,
  });
}

export async function deleteTaxGroup(id: string): Promise<void> {
  await customInstance<void>({
    url: `/api/admin/tax-groups/${id}`,
    method: 'DELETE',
  });
}

export type TaxBulkUpdateRequest = {
  oldTaxGroupId: string;
  newTaxGroupId: string;
  reason?: string | null;
};

export type TaxBulkUpdateResult = {
  totalProducts: number;
  updatedProducts: number;
  oldRate: number;
  newRate: number;
  oldTaxGroupId: string;
  newTaxGroupId: string;
};

export async function bulkUpdateProductTaxGroups(
  body: TaxBulkUpdateRequest
): Promise<TaxBulkUpdateResult> {
  return customInstance<TaxBulkUpdateResult>({
    url: '/api/admin/tax-groups/bulk-update',
    method: 'POST',
    data: body,
  });
}

export type TaxApplyToProductsRequest = {
  taxGroupId: string;
  productIds: string[];
  reason?: string | null;
};

export type TaxApplyToProductsResult = {
  requestedCount: number;
  updatedProducts: number;
  unchangedProducts: number;
  notFound: number;
  taxGroupId: string;
  newRate: number;
};

export async function applyTaxGroupToProducts(
  body: TaxApplyToProductsRequest
): Promise<TaxApplyToProductsResult> {
  return customInstance<TaxApplyToProductsResult>({
    url: '/api/admin/tax-groups/apply-to-products',
    method: 'POST',
    data: body,
  });
}

export type TaxGroupStat = {
  id: string;
  name: string;
  rate: number;
  color?: string | null;
  icon?: string | null;
  isActive: boolean;
  productCount: number;
  revenue: number;
  percentage: number;
};

export type TaxGroupStatsReport = {
  periodStart: string;
  periodEnd: string;
  totalProducts: number;
  totalRevenue: number;
  groups: TaxGroupStat[];
};

export const taxGroupStatsQueryKey = ['tax-groups', 'stats'] as const;

export async function getTaxGroupStats(params?: {
  fromUtc?: string;
  toUtc?: string;
}): Promise<TaxGroupStatsReport> {
  return customInstance<TaxGroupStatsReport>({
    url: '/api/admin/tax-groups/stats',
    method: 'GET',
    params,
  });
}

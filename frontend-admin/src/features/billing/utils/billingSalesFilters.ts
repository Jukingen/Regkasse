import { LICENSE_SALE_PLAN_VALUES } from '@/features/billing/constants/licensePlans';

/** UI status filter values (sent to API `status`, except `all`). */
export const LICENSE_SALES_STATUS_FILTERS = [
  'all',
  'active',
  'expired',
  'pending',
  'revoked',
] as const;

export type LicenseSalesStatusFilter = (typeof LICENSE_SALES_STATUS_FILTERS)[number];

/**
 * UI plan filter values.
 * - `trial` → API `licenseType=Trial`
 * - `oneYear` → `licensePlan=12_months`
 * - `twoYears` → `minDurationDays=700` (no dedicated 2-year plan in domain)
 * - `custom` → `licensePlan=custom`
 */
export const LICENSE_SALES_PLAN_FILTERS = [
  'all',
  'trial',
  'oneYear',
  'twoYears',
  'custom',
] as const;

export type LicenseSalesPlanFilter = (typeof LICENSE_SALES_PLAN_FILTERS)[number];

/** Approximate lower bound for a “2 Jahre” validity window. */
export const LICENSE_SALES_TWO_YEAR_MIN_DAYS = 700;

export const LICENSE_SALES_SORT_FIELDS = [
  'invoiceNumber',
  'tenant',
  'licenseKey',
  'licensePlan',
  'priceGross',
  'priceNet',
  'validUntilUtc',
  'daysRemaining',
  'soldAtUtc',
] as const;

export type LicenseSalesSortField = (typeof LICENSE_SALES_SORT_FIELDS)[number];
export type LicenseSalesSortDir = 'asc' | 'desc';

export const DEFAULT_LICENSE_SALES_SORT_BY: LicenseSalesSortField = 'validUntilUtc';
export const DEFAULT_LICENSE_SALES_SORT_DIR: LicenseSalesSortDir = 'asc';

export type BillingSalesFilterState = {
  page: number;
  pageSize: number;
  search?: string;
  status?: LicenseSalesStatusFilter;
  plan?: LicenseSalesPlanFilter;
  tenantId?: string;
  fromDate?: string;
  toDate?: string;
  sortBy?: LicenseSalesSortField;
  sortDir?: LicenseSalesSortDir;
};

export const DEFAULT_BILLING_SALES_FILTERS: BillingSalesFilterState = {
  page: 1,
  pageSize: 20,
  status: 'all',
  plan: 'all',
  sortBy: DEFAULT_LICENSE_SALES_SORT_BY,
  sortDir: DEFAULT_LICENSE_SALES_SORT_DIR,
};

export function isLicenseSalesSortField(value: string | null | undefined): value is LicenseSalesSortField {
  return (
    !!value && (LICENSE_SALES_SORT_FIELDS as readonly string[]).includes(value)
  );
}

export function parseLicenseSalesSortDir(value: string | null | undefined): LicenseSalesSortDir | undefined {
  if (value === 'asc' || value === 'desc') return value;
  return undefined;
}

/** Ant Design controlled `sortOrder` for a column. */
export function getLicenseSalesAntSortOrder(
  columnKey: LicenseSalesSortField,
  sortBy?: LicenseSalesSortField,
  sortDir?: LicenseSalesSortDir
): 'ascend' | 'descend' | null {
  if (sortBy !== columnKey) return null;
  return sortDir === 'desc' ? 'descend' : 'ascend';
}

export function countActiveBillingSalesFilters(filters: BillingSalesFilterState): number {
  let count = 0;
  if (filters.search?.trim()) count += 1;
  if (filters.status && filters.status !== 'all') count += 1;
  if (filters.plan && filters.plan !== 'all') count += 1;
  if (filters.tenantId) count += 1;
  if (filters.fromDate || filters.toDate) count += 1;
  return count;
}

export type BillingSalesListApiParams = {
  page: number;
  pageSize: number;
  tenantId?: string;
  search?: string;
  status?: string;
  licensePlan?: string;
  licenseType?: 'Trial' | 'Starter' | 'Business' | 'Plus';
  minDurationDays?: number;
  fromDate?: string;
  toDate?: string;
  sortBy?: string;
  sortDir?: string;
};

/** Maps UI filter state to GET /api/admin/billing/license-sales query params. */
export function toBillingSalesListApiParams(
  filters: BillingSalesFilterState
): BillingSalesListApiParams {
  const sortBy = filters.sortBy ?? DEFAULT_LICENSE_SALES_SORT_BY;
  const sortDir = filters.sortDir ?? DEFAULT_LICENSE_SALES_SORT_DIR;

  const params: BillingSalesListApiParams = {
    page: filters.page,
    pageSize: filters.pageSize,
    tenantId: filters.tenantId,
    search: filters.search?.trim() || undefined,
    fromDate: filters.fromDate,
    toDate: filters.toDate,
    sortBy,
    sortDir,
  };

  if (filters.status && filters.status !== 'all') {
    params.status = filters.status;
  }

  switch (filters.plan) {
    case 'trial':
      params.licenseType = 'Trial';
      break;
    case 'oneYear':
      params.licensePlan = LICENSE_SALE_PLAN_VALUES.twelveMonths;
      break;
    case 'twoYears':
      params.minDurationDays = LICENSE_SALES_TWO_YEAR_MIN_DAYS;
      break;
    case 'custom':
      params.licensePlan = LICENSE_SALE_PLAN_VALUES.custom;
      break;
    default:
      break;
  }

  return params;
}

/** Read initial list state from URL (sales page). */
export function parseBillingSalesFiltersFromSearchParams(
  params: URLSearchParams
): Partial<BillingSalesFilterState> {
  const sortByRaw = params.get('sortBy');
  const sortDirRaw = params.get('sortDir');
  const tenantId = params.get('tenantId') ?? undefined;
  const statusRaw = params.get('status');
  const planRaw = params.get('plan');
  const search = params.get('search') ?? undefined;

  return {
    tenantId: tenantId || undefined,
    search: search || undefined,
    status:
      statusRaw && (LICENSE_SALES_STATUS_FILTERS as readonly string[]).includes(statusRaw)
        ? (statusRaw as LicenseSalesStatusFilter)
        : undefined,
    plan:
      planRaw && (LICENSE_SALES_PLAN_FILTERS as readonly string[]).includes(planRaw)
        ? (planRaw as LicenseSalesPlanFilter)
        : undefined,
    sortBy: isLicenseSalesSortField(sortByRaw) ? sortByRaw : undefined,
    sortDir: parseLicenseSalesSortDir(sortDirRaw),
  };
}

/** Build query string for sales page URL persistence. */
export function billingSalesFiltersToSearchParams(filters: BillingSalesFilterState): URLSearchParams {
  const params = new URLSearchParams();
  if (filters.tenantId) params.set('tenantId', filters.tenantId);
  if (filters.search?.trim()) params.set('search', filters.search.trim());
  if (filters.status && filters.status !== 'all') params.set('status', filters.status);
  if (filters.plan && filters.plan !== 'all') params.set('plan', filters.plan);

  const sortBy = filters.sortBy ?? DEFAULT_LICENSE_SALES_SORT_BY;
  const sortDir = filters.sortDir ?? DEFAULT_LICENSE_SALES_SORT_DIR;
  params.set('sortBy', sortBy);
  params.set('sortDir', sortDir);
  return params;
}

import type { QueryClient } from '@tanstack/react-query';
import dayjs from 'dayjs';

import { adminProductsQueryKeys, getAdminProductsList } from '@/api/admin/products';
import { getGetApiAdminPaymentsQueryOptions } from '@/api/generated/admin/admin';
import { getGetApiReportsSalesQueryOptions } from '@/api/generated/reports/reports';
import { tenantStorage } from '@/features/auth/services/tenantStorage';
import { getReceiptListForensics } from '@/features/receipts/api/forensics-client';
import { RECEIPT_KEYS } from '@/features/receipts/hooks/useReceiptListQuery';
import { RECEIPT_LIST_DEFAULTS } from '@/features/receipts/types/receipts';

/** Routes that benefit from hover/focus prefetch (TanStack Query + Next route bundle). */
const PREFETCHABLE_EXACT = new Set([
  '/dashboard',
  '/payments',
  '/receipts',
  '/products',
  '/reporting',
  '/users',
  '/rksv/finanz-online-queue',
  '/rksv/finanz-online-outbox',
  '/rksv/sonderbelege',
  '/rksv/verifications',
]);

function prefetchPayments(qc: QueryClient) {
  qc.prefetchQuery(
    getGetApiAdminPaymentsQueryOptions({
      StartDate: dayjs().subtract(30, 'day').format('YYYY-MM-DD'),
      EndDate: dayjs().format('YYYY-MM-DD'),
      pageNumber: 1,
      pageSize: 50,
    })
  );
}

function prefetchProducts(qc: QueryClient) {
  const tenantSlug = tenantStorage.getTenantSlug() ?? '';
  const params = { pageNumber: 1, pageSize: 10, isActive: 'true' as const };
  qc.prefetchQuery({
    queryKey: adminProductsQueryKeys.list(tenantSlug, params),
    queryFn: () => getAdminProductsList(params),
    staleTime: 30_000,
  });
}

function prefetchReceipts(qc: QueryClient) {
  const params = { ...RECEIPT_LIST_DEFAULTS };
  qc.prefetchQuery({
    queryKey: RECEIPT_KEYS.list(params),
    queryFn: () => getReceiptListForensics(params),
    staleTime: 30_000,
  });
}

function prefetchDashboard(qc: QueryClient) {
  const startDate = dayjs().startOf('month').format('YYYY-MM-DD');
  const endDate = dayjs().endOf('month').format('YYYY-MM-DD');
  const params = { startDate, endDate };
  qc.prefetchQuery({
    ...getGetApiReportsSalesQueryOptions(params),
    staleTime: 120_000,
  });
}

/**
 * Best-effort warm-up before navigation. Safe to call repeatedly (QueryClient dedupes).
 */
export function prefetchAdminRoute(qc: QueryClient, href: string) {
  const path = href.split('?')[0];
  if (!PREFETCHABLE_EXACT.has(path)) return;

  switch (path) {
    case '/payments':
      prefetchPayments(qc);
      break;
    case '/products':
      prefetchProducts(qc);
      break;
    case '/receipts':
      prefetchReceipts(qc);
      break;
    case '/dashboard':
      prefetchDashboard(qc);
      break;
    default:
      break;
  }
}

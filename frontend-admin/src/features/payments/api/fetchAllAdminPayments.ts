import { getApiAdminPayments } from '@/api/generated/admin/admin';
import type { AdminPaymentListItemDto } from '@/api/generated/model';
import type { GetApiAdminPaymentsParams } from '@/api/generated/model/getApiAdminPaymentsParams';

const EXPORT_PAGE_SIZE = 100;
const EXPORT_MAX_PAGES = 50;

/** Fetches all pages for CSV/export flows without a single huge pageSize request. */
export async function fetchAllAdminPaymentsPages(
  params: Omit<GetApiAdminPaymentsParams, 'pageNumber' | 'pageSize'>
): Promise<AdminPaymentListItemDto[]> {
  const all: AdminPaymentListItemDto[] = [];
  let pageNumber = 1;

  while (pageNumber <= EXPORT_MAX_PAGES) {
    const res = await getApiAdminPayments({
      ...params,
      pageNumber,
      pageSize: EXPORT_PAGE_SIZE,
    });
    const batch = res.items ?? [];
    all.push(...batch);
    const total = res.totalCount ?? 0;
    if (batch.length < EXPORT_PAGE_SIZE || (total > 0 && all.length >= total)) {
      break;
    }
    pageNumber += 1;
  }

  return all;
}

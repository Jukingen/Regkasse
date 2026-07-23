import { getReceiptListForensics } from '@/features/receipts/api/forensics-client';
import type { ReceiptListItemDto, ReceiptListParams } from '@/features/receipts/types/receipts';
import { BATCH_RECEIPT_PDF_MAX } from '@/features/receipts/utils/batchDownloadReceiptPdfs';

/**
 * Loads all receipt rows matching the current filters (paginated, pageSize 100),
 * capped at {@link BATCH_RECEIPT_PDF_MAX}.
 */
export async function fetchAllFilteredReceipts(
  baseParams: Omit<ReceiptListParams, 'page' | 'pageSize'>,
  options?: { maxItems?: number; signal?: AbortSignal }
): Promise<{ items: ReceiptListItemDto[]; truncated: boolean; totalCount: number }> {
  const maxItems = options?.maxItems ?? BATCH_RECEIPT_PDF_MAX;
  const pageSize = 100;
  const all: ReceiptListItemDto[] = [];
  let page = 1;
  let totalCount = 0;
  let truncated = false;

  while (all.length < maxItems) {
    if (options?.signal?.aborted) {
      throw new DOMException('Aborted', 'AbortError');
    }
    const result = await getReceiptListForensics({
      ...baseParams,
      page,
      pageSize,
    });
    totalCount = result.totalCount;
    all.push(...result.items);
    if (result.items.length === 0 || all.length >= result.totalCount) {
      break;
    }
    if (all.length >= maxItems) {
      truncated = result.totalCount > maxItems;
      break;
    }
    page += 1;
    if (page > 50) {
      truncated = true;
      break;
    }
  }

  return {
    items: all.slice(0, maxItems),
    truncated: truncated || totalCount > maxItems,
    totalCount,
  };
}

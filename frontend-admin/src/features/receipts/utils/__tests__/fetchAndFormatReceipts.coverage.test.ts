import { beforeEach, describe, expect, it, vi } from 'vitest';

import { fetchAllFilteredReceipts } from '@/features/receipts/utils/fetchAllFilteredReceipts';
import { formatRksvSpecialReceiptKindDisplay } from '@/features/receipts/utils/formatRksvSpecialReceiptKind';

const mockGetReceiptListForensics = vi.fn();

vi.mock('@/features/receipts/api/forensics-client', () => ({
  getReceiptListForensics: (params: unknown) => mockGetReceiptListForensics(params),
}));

describe('formatRksvSpecialReceiptKindDisplay', () => {
  const t = (key: string) => key;

  it('handles empty, known, and unknown kinds', () => {
    expect(formatRksvSpecialReceiptKindDisplay(t, null)).toBe(
      'receipts.detail.card.valueSpecialKindNone'
    );
    expect(formatRksvSpecialReceiptKindDisplay(t, '  ')).toBe(
      'receipts.detail.card.valueSpecialKindNone'
    );
    expect(formatRksvSpecialReceiptKindDisplay(t, 'Startbeleg')).toBe(
      'receipts.specialKind.startbeleg'
    );
    expect(formatRksvSpecialReceiptKindDisplay(t, 'not-a-kind')).toBe(
      'receipts.specialKind.unknown'
    );
  });
});

describe('fetchAllFilteredReceipts', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('aggregates pages until complete', async () => {
    mockGetReceiptListForensics
      .mockResolvedValueOnce({
        items: [{ id: '1' }, { id: '2' }],
        totalCount: 3,
      })
      .mockResolvedValueOnce({
        items: [{ id: '3' }],
        totalCount: 3,
      });

    const result = await fetchAllFilteredReceipts({ fromUtc: '2026-01-01' } as never);
    expect(result.items.map((i) => i.id)).toEqual(['1', '2', '3']);
    expect(result.truncated).toBe(false);
    expect(result.totalCount).toBe(3);
  });

  it('marks truncated when maxItems is exceeded', async () => {
    mockGetReceiptListForensics.mockResolvedValue({
      items: Array.from({ length: 5 }, (_, i) => ({ id: String(i) })),
      totalCount: 20,
    });
    const result = await fetchAllFilteredReceipts({} as never, { maxItems: 5 });
    expect(result.items).toHaveLength(5);
    expect(result.truncated).toBe(true);
  });

  it('throws AbortError when signal already aborted', async () => {
    const controller = new AbortController();
    controller.abort();
    await expect(
      fetchAllFilteredReceipts({} as never, { signal: controller.signal })
    ).rejects.toMatchObject({ name: 'AbortError' });
  });
});

'use client';

import { useQuery } from '@tanstack/react-query';
import { useRouter } from 'next/navigation';
import { useMemo } from 'react';

import { receiptRowToCommandItem } from '@/features/command-palette/mapApiSearchToCommandItems';
import type { CommandItem } from '@/features/command-palette/types';
import { getReceiptListForensics } from '@/features/receipts/api/forensics-client';
import { useDebounce } from '@/hooks/useDebounce';
import { useI18n } from '@/i18n';
import { formatCurrency, formatDate } from '@/i18n/formatting';

const MIN_QUERY_LEN = 2;
const MAX_RESULTS = 5;
const DEBOUNCE_MS = 300;

export function useCommandPaletteReceiptSearch(
  query: string,
  options: { enabled: boolean; closePalette: () => void }
): { items: CommandItem[]; isLoading: boolean } {
  const router = useRouter();
  const { formatLocale } = useI18n();
  const debouncedQuery = useDebounce(query.trim(), DEBOUNCE_MS);
  const enabled = options.enabled && debouncedQuery.length >= MIN_QUERY_LEN;

  const listQuery = useQuery({
    queryKey: ['command-palette', 'receipts', debouncedQuery],
    queryFn: () =>
      getReceiptListForensics({
        page: 1,
        pageSize: MAX_RESULTS,
        sort: 'issuedAt:desc',
        receiptNumber: debouncedQuery,
      }),
    enabled,
    staleTime: 30_000,
  });

  const formatMoney = (value: number) => formatCurrency(value, formatLocale);
  const formatReceiptDate = (iso: string) => formatDate(iso, formatLocale);

  const items = useMemo((): CommandItem[] => {
    if (debouncedQuery.length < MIN_QUERY_LEN || !listQuery.data?.items) return [];

    const openReceipt = (receiptId: string) => {
      options.closePalette();
      router.push(`/receipts/${encodeURIComponent(receiptId)}`);
    };

    return listQuery.data.items
      .slice(0, MAX_RESULTS)
      .map((row) => receiptRowToCommandItem(row, openReceipt, formatMoney, formatReceiptDate));
  }, [debouncedQuery, listQuery.data?.items, options.closePalette, router, formatLocale]);

  return { items, isLoading: listQuery.isFetching };
}

'use client';

import { useQuery } from '@tanstack/react-query';

import type { ReceiptListItemDto } from '@/api/generated/model';
import { getApiReceiptsList } from '@/api/generated/receipts/receipts';
import { monthDateRangeIso } from '@/features/rksv/utils/monatsbelegMonthLinks';

export type ReceiptsByMonthParams = {
  cashRegisterId: string;
  year: number;
  month: number;
};

export type ReceiptsByMonthResult = {
  items: ReceiptListItemDto[];
  totalCount: number;
  /** Sum of `grandTotal` across returned items. */
  revenue: number;
  /** True when the first page covers every receipt in the month. */
  revenueIsComplete: boolean;
};

export const receiptsByMonthQueryKeys = {
  all: ['rksv', 'receipts-by-month'] as const,
  month: (params: ReceiptsByMonthParams) =>
    [...receiptsByMonthQueryKeys.all, params.cashRegisterId, params.year, params.month] as const,
};

const PAGE_SIZE = 100;

export async function fetchReceiptsByMonth(
  params: ReceiptsByMonthParams
): Promise<ReceiptsByMonthResult> {
  const { from, to } = monthDateRangeIso(params.year, params.month);
  const res = await getApiReceiptsList({
    page: 1,
    pageSize: PAGE_SIZE,
    sort: 'issuedAt:desc',
    cashRegisterId: params.cashRegisterId,
    issuedFrom: from,
    issuedTo: to,
  });
  const items = (res.items ?? []) as ReceiptListItemDto[];
  const totalCount = res.totalCount ?? items.length;
  const revenue = items.reduce((sum, row) => sum + (Number(row.grandTotal) || 0), 0);
  return {
    items,
    totalCount,
    revenue,
    revenueIsComplete: totalCount <= items.length,
  };
}

/**
 * Receipts for one cash register calendar month (Vienna date-only issuedFrom/issuedTo).
 * Disabled until cashRegisterId + year + month are set.
 */
export function useReceiptsByMonth(params: ReceiptsByMonthParams | null) {
  const enabled =
    Boolean(params?.cashRegisterId?.trim()) &&
    params != null &&
    Number.isInteger(params.year) &&
    Number.isInteger(params.month) &&
    params.month >= 1 &&
    params.month <= 12;

  return useQuery({
    queryKey: params
      ? receiptsByMonthQueryKeys.month(params)
      : [...receiptsByMonthQueryKeys.all, 'idle'],
    queryFn: () => fetchReceiptsByMonth(params as ReceiptsByMonthParams),
    enabled,
    staleTime: 30_000,
  });
}

'use client';

import { useQuery } from '@tanstack/react-query';

import type { MonatsbelegRegisterStatusItemDto, MonatsbelegStatusDto } from '@/api/generated/model';
import {
  getApiRksvMonatsbelegStatusCashRegisterId,
  getApiRksvMonatsbelegStatusOverview,
  getGetApiRksvMonatsbelegStatusCashRegisterIdQueryKey,
  getGetApiRksvMonatsbelegStatusOverviewQueryKey,
} from '@/api/generated/rksv/rksv';

export const MONATSBELEG_REFETCH_INTERVAL_MS = 5 * 60 * 1000;

export const monatsbelegQueryKeys = {
  statusOverview: getGetApiRksvMonatsbelegStatusOverviewQueryKey(),
  registerStatus: (cashRegisterId: string) =>
    getGetApiRksvMonatsbelegStatusCashRegisterIdQueryKey(cashRegisterId),
} as const;

function getHttpStatus(error: unknown): number | undefined {
  if (error && typeof error === 'object' && 'response' in error) {
    const status = (error as { response?: { status?: number } }).response?.status;
    return typeof status === 'number' ? status : undefined;
  }
  return undefined;
}

/** Skip retry on auth/not-found; otherwise retry up to 2 times with backoff. */
export function monatsbelegQueryRetry(failureCount: number, error: unknown): boolean {
  const status = getHttpStatus(error);
  if (status === 401 || status === 404 || status === 403) {
    return false;
  }
  return failureCount < 2;
}

const overviewQueryDefaults = {
  staleTime: MONATSBELEG_REFETCH_INTERVAL_MS,
  refetchInterval: MONATSBELEG_REFETCH_INTERVAL_MS,
  refetchIntervalInBackground: false,
  refetchOnWindowFocus: true,
  retry: monatsbelegQueryRetry,
  retryDelay: (attemptIndex: number) => 1000 * (attemptIndex + 1),
} as const;

type MonatsbelegQueryOptions = {
  enabled?: boolean;
};

/**
 * GET /api/rksv/monatsbeleg/status-overview — tenant-wide Monatsbeleg status per register.
 */
export function useMonatsbelegStatus(options?: MonatsbelegQueryOptions) {
  const enabled = options?.enabled ?? true;

  return useQuery<MonatsbelegRegisterStatusItemDto[], unknown>({
    queryKey: monatsbelegQueryKeys.statusOverview,
    queryFn: ({ signal }) => getApiRksvMonatsbelegStatusOverview(undefined, signal),
    enabled,
    ...overviewQueryDefaults,
  });
}

/**
 * GET /api/rksv/monatsbeleg/status/{cashRegisterId} — Monatsbeleg status for one register.
 */
export function useCashRegisterMonatsbeleg(
  cashRegisterId: string,
  options?: MonatsbelegQueryOptions
) {
  const trimmedId = cashRegisterId?.trim() ?? '';
  const enabled = (options?.enabled ?? true) && trimmedId.length > 0;

  return useQuery<MonatsbelegStatusDto, unknown>({
    queryKey: monatsbelegQueryKeys.registerStatus(trimmedId),
    queryFn: ({ signal }) =>
      getApiRksvMonatsbelegStatusCashRegisterId(trimmedId, undefined, signal),
    enabled,
    staleTime: MONATSBELEG_REFETCH_INTERVAL_MS,
    retry: monatsbelegQueryRetry,
    retryDelay: (attemptIndex: number) => 1000 * (attemptIndex + 1),
  });
}

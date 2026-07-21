'use client';

import { useCallback, useMemo } from 'react';

import {
  type RegisterMonatsbelegRow,
  useAdminMonatsbelegOverview,
} from '@/features/dashboard/hooks/useAdminMonatsbelegOverview';

function toErrorMessage(cause: unknown): string {
  if (cause instanceof Error && cause.message.trim()) {
    return cause.message;
  }
  if (cause && typeof cause === 'object' && 'message' in cause) {
    const message = (cause as { message?: unknown }).message;
    if (typeof message === 'string' && message.trim()) {
      return message;
    }
  }
  return 'Die Monatsbeleg-Übersicht konnte nicht geladen werden.';
}

/**
 * Dashboard widget hook: cash registers merged with Monatsbeleg overview rows.
 * For raw API access use {@link useMonatsbelegStatus} from `./useMonatsbeleg`.
 */
export function useMonatsbelegDashboard(enabled = true) {
  const {
    rows,
    registersLoading,
    registersFetching,
    statusPending,
    loadError,
    hasRegisters,
    refetchAll,
    overviewError,
    registersQueryError,
  } = useAdminMonatsbelegOverview(enabled);

  const isLoading = registersLoading || statusPending;
  const isFetching = registersFetching || statusPending;

  const error = useMemo(() => {
    if (!loadError) return null;
    const cause = overviewError ?? registersQueryError;
    return new Error(toErrorMessage(cause));
  }, [loadError, overviewError, registersQueryError]);

  const refetch = useCallback(async () => {
    await refetchAll();
  }, [refetchAll]);

  return {
    data: rows as RegisterMonatsbelegRow[],
    isLoading,
    isFetching,
    error,
    refetch,
    hasRegisters,
  };
}

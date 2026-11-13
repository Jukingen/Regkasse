'use client';

import { useMemo } from 'react';

import { useMonatsbelegStatus } from '@/features/rksv/hooks/useMonatsbeleg';
import { useAuthorizationGate } from '@/hooks/useAuthorizedQuery';
import { AppPermissions } from '@/shared/auth/permissions';

export type PendingMonatsbelegItem = {
  cashRegisterId: string;
  missingCount: number;
  isOverdue: boolean;
};

/**
 * Tenant registers with outstanding Monatsbeleg obligations.
 * Uses GET /api/rksv/monatsbeleg/status-overview.
 */
export function usePendingMonatsbeleg() {
  const { isAuthorized } = useAuthorizationGate({
    requiredPermission: AppPermissions.CashRegisterView,
  });
  const query = useMonatsbelegStatus({ enabled: isAuthorized });

  const data = useMemo((): PendingMonatsbelegItem[] => {
    const items: PendingMonatsbelegItem[] = [];
    for (const row of query.data ?? []) {
      const cashRegisterId = row.cashRegisterId?.trim();
      const status = row.status;
      if (!cashRegisterId || !status) {
        continue;
      }

      const missingMonths = status.missingMonths ?? [];
      const overdueCount = missingMonths.filter((month) => month.isOverdue).length;
      const missingCount = status.totalMissingCount ?? missingMonths.length;
      const requiresAttention = status.requiresAttention || missingCount > 0 || overdueCount > 0;

      if (!requiresAttention) {
        continue;
      }

      items.push({
        cashRegisterId,
        missingCount: Math.max(missingCount, missingMonths.length),
        isOverdue: overdueCount > 0 || Boolean(status.currentMonthOverdue),
      });
    }
    return items;
  }, [query.data]);

  return {
    data,
    isLoading: query.isLoading,
    isFetching: query.isFetching,
    isError: query.isError,
    error: query.error,
    refetch: query.refetch,
  };
}

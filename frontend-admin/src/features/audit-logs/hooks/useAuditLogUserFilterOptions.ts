'use client';

import { useCallback, useMemo } from 'react';

import type { UserInfo } from '@/api/generated/model';
import { useUsersList } from '@/features/users/hooks/useUsersList';
import { useI18n } from '@/i18n';

const USERS_LIST_STALE_MS = 5 * 60 * 1000;

function userDisplayName(record: UserInfo): string {
  const name = `${record.firstName ?? ''} ${record.lastName ?? ''}`.trim();
  return name || record.userName || record.email || record.id || '—';
}

/** User dropdown options for audit-log filters (cached list, sorted by display name). */
export function useAuditLogUserFilterOptions() {
  const { formatLocale } = useI18n();
  const usersListQuery = useUsersList(
    { page: 1, pageSize: 200 },
    { staleTime: USERS_LIST_STALE_MS }
  );

  const options = useMemo(() => {
    const items = usersListQuery.data?.items ?? [];
    return items
      .filter((u) => Boolean(u.id))
      .map((u) => ({
        value: u.id!,
        label: userDisplayName(u),
      }))
      .sort((a, b) => a.label.localeCompare(b.label, formatLocale));
  }, [usersListQuery.data?.items, formatLocale]);

  const resolveLabel = useCallback(
    (userId: string) => options.find((o) => o.value === userId)?.label ?? userId,
    [options]
  );

  return {
    options,
    resolveLabel,
    isLoading: usersListQuery.isLoading,
  };
}

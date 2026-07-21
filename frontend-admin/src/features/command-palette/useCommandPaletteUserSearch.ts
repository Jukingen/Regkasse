'use client';

import { useQuery } from '@tanstack/react-query';
import { useRouter } from 'next/navigation';
import { useCallback, useMemo } from 'react';

import {
  tenantUserToCommandItem,
  userDtoToCommandItem,
} from '@/features/command-palette/mapApiSearchToCommandItems';
import type { CommandItem } from '@/features/command-palette/types';
import { listAllAdminUsers } from '@/features/users/api/users';
import { getUsersList } from '@/features/users/api/usersGateway';
import { useDebounce } from '@/hooks/useDebounce';

const MIN_QUERY_LEN = 2;
const MAX_RESULTS = 5;
const DEBOUNCE_MS = 300;

export function useCommandPaletteUserSearch(
  query: string,
  options: {
    enabled: boolean;
    isSuperAdmin: boolean;
    closePalette: () => void;
    roleDisplayLabel?: (role: string) => string;
  }
): { items: CommandItem[]; isLoading: boolean } {
  const router = useRouter();
  const debouncedQuery = useDebounce(query.trim(), DEBOUNCE_MS);
  const enabled = options.enabled && debouncedQuery.length >= MIN_QUERY_LEN;

  const tenantQuery = useQuery({
    queryKey: ['command-palette', 'users', debouncedQuery],
    queryFn: () =>
      getUsersList({
        query: debouncedQuery,
        page: 1,
        pageSize: MAX_RESULTS,
      }),
    enabled: enabled && !options.isSuperAdmin,
    staleTime: 30_000,
  });

  const platformQuery = useQuery({
    queryKey: ['command-palette', 'admin-users', debouncedQuery],
    queryFn: () => listAllAdminUsers({ search: debouncedQuery }),
    enabled: enabled && options.isSuperAdmin,
    staleTime: 30_000,
  });

  const active = options.isSuperAdmin ? platformQuery : tenantQuery;

  const openUser = useCallback(
    (userId: string) => {
      options.closePalette();
      router.push(`/admin/users?userId=${encodeURIComponent(userId)}`);
    },
    [options.closePalette, router]
  );

  const roleLabel = useCallback(
    (role: string | null | undefined) => {
      if (!role) return '—';
      return options.roleDisplayLabel?.(role) ?? role;
    },
    [options.roleDisplayLabel]
  );

  const items = useMemo((): CommandItem[] => {
    if (debouncedQuery.length < MIN_QUERY_LEN) return [];

    if (options.isSuperAdmin && platformQuery.data) {
      return platformQuery.data
        .slice(0, MAX_RESULTS)
        .map((row) => userDtoToCommandItem(row, openUser, roleLabel(row.role)));
    }

    if (!options.isSuperAdmin && tenantQuery.data?.items) {
      return tenantQuery.data.items
        .slice(0, MAX_RESULTS)
        .map((user) => tenantUserToCommandItem(user, openUser, roleLabel(user.role)));
    }

    return [];
  }, [
    debouncedQuery,
    options.isSuperAdmin,
    platformQuery.data,
    tenantQuery.data?.items,
    openUser,
    roleLabel,
  ]);

  return { items, isLoading: active.isFetching };
}

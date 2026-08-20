'use client';

import { useMemo, useSyncExternalStore } from 'react';

import {
  RECENT_ADMIN_MENU_STORAGE_KEY,
  readRecentAdminMenuPaths,
  subscribeRecentAdminMenuPaths,
} from '@/features/dashboard/utils/recentAdminMenuPaths';

function getServerSnapshot(): string {
  return '[]';
}

function getClientSnapshot(): string {
  if (typeof window === 'undefined') {
    return '[]';
  }
  return window.localStorage.getItem(RECENT_ADMIN_MENU_STORAGE_KEY) ?? '[]';
}

export function useRecentAdminMenuPaths(): string[] {
  const raw = useSyncExternalStore(
    subscribeRecentAdminMenuPaths,
    getClientSnapshot,
    getServerSnapshot
  );
  return useMemo(() => readRecentAdminMenuPaths(), [raw]);
}

'use client';

/**
 * Persisted desktop sidebar width — ephemeral UI preference (Zustand).
 */
import { create } from 'zustand';

import {
  ADMIN_SIDER_WIDTH_DEFAULT,
  ADMIN_SIDER_WIDTH_STORAGE_KEY,
  clampAdminSiderWidth,
} from '@/components/admin-layout/adminSiderWidth';

type SiderWidthState = {
  width: number;
  hydrated: boolean;
  hydrate: () => void;
  setWidth: (next: number) => void;
};

export const useSiderWidthStore = create<SiderWidthState>((set) => ({
  width: ADMIN_SIDER_WIDTH_DEFAULT,
  hydrated: false,

  hydrate: () => {
    if (typeof window === 'undefined') {
      set({ hydrated: true });
      return;
    }
    try {
      const raw = localStorage.getItem(ADMIN_SIDER_WIDTH_STORAGE_KEY);
      const parsed = raw ? Number.parseInt(raw, 10) : NaN;
      if (Number.isFinite(parsed)) {
        set({ width: clampAdminSiderWidth(parsed), hydrated: true });
        return;
      }
    } catch {
      /* ignore */
    }
    set({ hydrated: true });
  },

  setWidth: (next) => {
    const clamped = clampAdminSiderWidth(next);
    set({ width: clamped });
    try {
      localStorage.setItem(ADMIN_SIDER_WIDTH_STORAGE_KEY, String(clamped));
    } catch {
      /* ignore */
    }
  },
}));

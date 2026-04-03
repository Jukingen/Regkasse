'use client';

import { useCallback, useEffect, useState } from 'react';

export const ADMIN_SIDER_WIDTH_STORAGE_KEY = 'regkasse-admin-sidebar-width-v1';

export const ADMIN_SIDER_WIDTH_DEFAULT = 256;
export const ADMIN_SIDER_WIDTH_MIN = 220;
export const ADMIN_SIDER_WIDTH_MAX = 420;

export function clampAdminSiderWidth(n: number): number {
    return Math.min(ADMIN_SIDER_WIDTH_MAX, Math.max(ADMIN_SIDER_WIDTH_MIN, Math.round(n)));
}

/**
 * Restores persisted desktop sidebar width from localStorage (client-only).
 */
export function usePersistedAdminSiderWidth() {
    const [width, setWidthState] = useState(ADMIN_SIDER_WIDTH_DEFAULT);
    const [hydrated, setHydrated] = useState(false);

    useEffect(() => {
        try {
            const raw = localStorage.getItem(ADMIN_SIDER_WIDTH_STORAGE_KEY);
            const parsed = raw ? Number.parseInt(raw, 10) : NaN;
            if (Number.isFinite(parsed)) {
                setWidthState(clampAdminSiderWidth(parsed));
            }
        } catch {
            /* ignore */
        }
        setHydrated(true);
    }, []);

    const setWidth = useCallback((next: number) => {
        const clamped = clampAdminSiderWidth(next);
        setWidthState(clamped);
        try {
            localStorage.setItem(ADMIN_SIDER_WIDTH_STORAGE_KEY, String(clamped));
        } catch {
            /* ignore */
        }
    }, []);

    return { width, setWidth, hydrated };
}

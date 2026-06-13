'use client';

import { useCallback } from 'react';
import { useRouter } from 'next/navigation';

const LAST_ALLOWED_ADMIN_PATH_KEY = 'rk_admin_last_allowed_path';

const SKIP_MEMORY_PATHS = new Set(['/403', '/login', '/force-password-change']);

/** Remember last successfully entered admin route for safe "back" navigation. */
export function rememberAllowedAdminPath(pathname: string): void {
    if (typeof window === 'undefined') return;
    const normalized = pathname.replace(/\/$/, '') || '/';
    if (SKIP_MEMORY_PATHS.has(normalized)) return;
    sessionStorage.setItem(LAST_ALLOWED_ADMIN_PATH_KEY, normalized);
}

export function useSafeNavigateBack(fallbackPath = '/dashboard') {
    const router = useRouter();

    return useCallback(() => {
        if (typeof window === 'undefined') {
            router.push(fallbackPath);
            return;
        }

        const current = window.location.pathname.replace(/\/$/, '') || '/';
        const stored = sessionStorage.getItem(LAST_ALLOWED_ADMIN_PATH_KEY);
        if (stored && stored !== current) {
            router.push(stored);
            return;
        }

        if (window.history.length > 1) {
            router.back();
            return;
        }

        router.push(fallbackPath);
    }, [router, fallbackPath]);
}

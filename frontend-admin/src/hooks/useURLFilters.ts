'use client';

import { useSearchParams, useRouter, usePathname } from 'next/navigation';
import { useCallback } from 'react';

/**
 * Hook to read and write URL search params.
 * Creates a seamless sync between URL and component state.
 */
export function useURLFilters<T extends Record<string, string | number | undefined>>() {
    const searchParams = useSearchParams();
    const router = useRouter();
    const pathname = usePathname();

    // Helper to get current params as object
    const getParams = useCallback((): Partial<T> => {
        const params: Record<string, any> = {};
        searchParams.forEach((value, key) => {
            params[key] = value;
        });
        return params as Partial<T>;
    }, [searchParams]);

    // Helper to set a single param
    const setParam = useCallback((key: keyof T, value: string | undefined | null) => {
        const current = new URLSearchParams(Array.from(searchParams.entries()));

        if (value === undefined || value === null || value === '') {
            current.delete(String(key));
        } else {
            current.set(String(key), String(value));
        }

        const search = current.toString();
        const query = search ? `?${search}` : '';

        router.push(`${pathname}${query}`, { scroll: false });
    }, [searchParams, pathname, router]);

    // Helper to set multiple params at once
    const setParams = useCallback((newParams: Partial<T>) => {
        const current = new URLSearchParams(Array.from(searchParams.entries()));

        Object.entries(newParams).forEach(([key, value]) => {
            if (value === undefined || value === null || value === '') {
                current.delete(key);
            } else {
                current.set(key, String(value));
            }
        });

        const search = current.toString();
        const query = search ? `?${search}` : '';

        router.push(`${pathname}${query}`, { scroll: false });
    }, [searchParams, pathname, router]);

    // Helper to clear all params
    const clearParams = useCallback(() => {
        router.push(pathname, { scroll: false });
    }, [pathname, router]);

    return {
        filters: getParams(),
        setParam,
        setParams,
        clearParams,
        get: (key: keyof T) => searchParams.get(String(key)),
    };
}

'use client';

import { useCallback, useState } from 'react';

export type KeysetPageMeta = {
    nextCursor?: string | null;
    hasMore?: boolean;
    totalCount?: number | null;
};

/** Opaque keyset cursor stack; page index is owned by URL or parent state. */
export function useKeysetCursors() {
    const [pageCursors, setPageCursors] = useState<Record<number, string>>({});
    const [cachedTotal, setCachedTotal] = useState<number | null>(null);

    const getAfterCursor = useCallback(
        (page: number) => (page > 1 ? pageCursors[page] : undefined),
        [pageCursors],
    );

    const shouldIncludeTotalCount = useCallback(
        (page: number) => page === 1 && !getAfterCursor(page),
        [getAfterCursor],
    );

    const ingestPageMeta = useCallback((page: number, meta: KeysetPageMeta) => {
        if (meta.totalCount != null && page === 1) {
            setCachedTotal(meta.totalCount);
        }
        if (meta.nextCursor) {
            setPageCursors((prev) => ({ ...prev, [page + 1]: meta.nextCursor! }));
        }
    }, []);

    const resetCursors = useCallback(() => {
        setPageCursors({});
        setCachedTotal(null);
    }, []);

    return {
        getAfterCursor,
        shouldIncludeTotalCount,
        cachedTotal,
        ingestPageMeta,
        resetCursors,
        hasCursorForPage: (page: number) => page <= 1 || pageCursors[page] != null,
    };
}

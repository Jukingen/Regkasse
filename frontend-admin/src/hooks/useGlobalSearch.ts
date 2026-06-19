'use client';

import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { useRouter } from 'next/navigation';
import type { InputRef } from 'antd';
import { useDebounce } from '@/hooks/useDebounce';
import { useKeyboardShortcut } from '@/hooks/useKeyboardShortcut';
import { useMenuSearchIndex } from '@/hooks/useMenuSearchIndex';
import type { GlobalSearchResultItem } from '@/components/admin-layout/GlobalSearch.types';
import { filterGlobalSearchItems } from '@/shared/searchUtils';

const SEARCH_DEBOUNCE_MS = 200;

export type UseGlobalSearchResult = {
    open: boolean;
    setOpen: (next: boolean) => void;
    query: string;
    setQuery: (next: string) => void;
    debouncedQuery: string;
    isSearching: boolean;
    results: GlobalSearchResultItem[];
    selectedIndex: number;
    setSelectedIndex: React.Dispatch<React.SetStateAction<number>>;
    inputRef: React.RefObject<InputRef | null>;
    openSearch: () => void;
    closeSearch: () => void;
    selectItem: (item: GlobalSearchResultItem) => void;
    onInputKeyDown: (event: React.KeyboardEvent<HTMLInputElement>) => void;
};

export function useGlobalSearch(): UseGlobalSearchResult {
    const router = useRouter();
    const { items: searchIndexItems } = useMenuSearchIndex();

    const [open, setOpen] = useState(false);
    const [query, setQuery] = useState('');
    const [selectedIndex, setSelectedIndex] = useState(0);
    const inputRef = useRef<InputRef>(null);
    const previousFocusRef = useRef<HTMLElement | null>(null);

    const debouncedQuery = useDebounce(query, SEARCH_DEBOUNCE_MS);

    const isSearching =
        query.trim().length > 0 && query.trim() !== debouncedQuery.trim();

    const results = useMemo(
        () => filterGlobalSearchItems(searchIndexItems, debouncedQuery),
        [searchIndexItems, debouncedQuery],
    );

    const focusInput = useCallback(() => {
        window.setTimeout(() => {
            inputRef.current?.focus({ preventScroll: true });
        }, 0);
    }, []);

    const openSearch = useCallback(() => {
        if (!open) {
            const active = document.activeElement;
            previousFocusRef.current = active instanceof HTMLElement ? active : null;
        }
        setOpen(true);
        focusInput();
    }, [open, focusInput]);

    const closeSearch = useCallback(
        (options?: { restoreFocus?: boolean }) => {
            setOpen(false);
            setQuery('');
            setSelectedIndex(0);

            if (options?.restoreFocus === false) {
                previousFocusRef.current = null;
                return;
            }

            const previous = previousFocusRef.current;
            previousFocusRef.current = null;

            if (previous?.isConnected) {
                window.setTimeout(() => {
                    previous.focus({ preventScroll: true });
                }, 0);
                return;
            }

            inputRef.current?.blur();
        },
        [],
    );

    useKeyboardShortcut('k', {
        metaOrCtrl: true,
        onTrigger: openSearch,
    });

    useEffect(() => {
        if (!open) return;

        const onDocumentKeyDown = (event: KeyboardEvent) => {
            if (event.key !== 'Escape') return;
            event.preventDefault();
            closeSearch({ restoreFocus: true });
        };

        document.addEventListener('keydown', onDocumentKeyDown);
        return () => document.removeEventListener('keydown', onDocumentKeyDown);
    }, [open, closeSearch]);

    const selectItem = useCallback(
        (item: GlobalSearchResultItem) => {
            closeSearch({ restoreFocus: false });
            router.push(item.href);
        },
        [closeSearch, router],
    );

    const onInputKeyDown = useCallback(
        (event: React.KeyboardEvent<HTMLInputElement>) => {
            if (event.key === 'ArrowDown') {
                event.preventDefault();
                if (results.length === 0) return;
                setSelectedIndex((index) => Math.min(index + 1, results.length - 1));
                return;
            }
            if (event.key === 'ArrowUp') {
                event.preventDefault();
                setSelectedIndex((index) => Math.max(index - 1, 0));
                return;
            }
            if (event.key === 'Enter') {
                event.preventDefault();
                const item = results[selectedIndex];
                if (item) selectItem(item);
                return;
            }
            if (event.key === 'Escape') {
                event.preventDefault();
                closeSearch({ restoreFocus: true });
                return;
            }
            if (event.key === 'Tab') {
                closeSearch({ restoreFocus: false });
            }
        },
        [results, selectedIndex, selectItem, closeSearch],
    );

    const handleSetQuery = useCallback((next: string) => {
        setQuery(next);
        setSelectedIndex(0);
    }, []);

    const handleSetOpen = useCallback(
        (next: boolean) => {
            if (next) {
                openSearch();
                return;
            }
            closeSearch({ restoreFocus: true });
        },
        [openSearch, closeSearch],
    );

    useEffect(() => {
        if (selectedIndex >= results.length) {
            setSelectedIndex(results.length > 0 ? results.length - 1 : 0);
        }
    }, [results.length, selectedIndex]);

    return {
        open,
        setOpen: handleSetOpen,
        query,
        setQuery: handleSetQuery,
        debouncedQuery,
        isSearching,
        results,
        selectedIndex,
        setSelectedIndex,
        inputRef,
        openSearch,
        closeSearch,
        selectItem,
        onInputKeyDown,
    };
}

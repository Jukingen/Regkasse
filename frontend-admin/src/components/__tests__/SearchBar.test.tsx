import { describe, expect, it, vi, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import { SearchBar } from '@/components/SearchBar';
import { KEYBOARD_SHORTCUT_EVENTS } from '@/shared/keyboardShortcuts';

const openSearch = vi.fn();
const closeSearch = vi.fn();

vi.mock('@/hooks/useGlobalSearch', () => ({
    useGlobalSearch: () => ({
        open: false,
        setOpen: vi.fn(),
        query: '',
        setQuery: vi.fn(),
        debouncedQuery: '',
        isSearching: false,
        results: [],
        selectedIndex: 0,
        setSelectedIndex: vi.fn(),
        inputRef: { current: null },
        openSearch,
        closeSearch,
        selectItem: vi.fn(),
        onInputKeyDown: vi.fn(),
    }),
}));

vi.mock('@/i18n', () => ({
    useI18n: () => ({
        t: (key: string) => key,
    }),
}));

vi.mock('@/shared/layout/adminHeaderDropdown', () => ({
    getAdminHeaderPopupContainer: () => document.body,
}));

describe('SearchBar', () => {
    beforeEach(() => {
        openSearch.mockReset();
        closeSearch.mockReset();
    });

    it('renders the search input', () => {
        render(<SearchBar />);
        expect(screen.getByRole('combobox')).toBeTruthy();
    });

    it('opens search on regkasse:openSearch', () => {
        render(<SearchBar />);
        document.dispatchEvent(new CustomEvent(KEYBOARD_SHORTCUT_EVENTS.openSearch));
        expect(openSearch).toHaveBeenCalledTimes(1);
    });
});

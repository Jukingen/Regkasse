'use client';

/**
 * Header menu search — Ctrl/Cmd+K opens (`regkasse:openSearch`),
 * Escape / `regkasse:closeModal` closes.
 *
 * Mount this in the protected shell header (not a second overlay on top of it).
 */
import { useCallback } from 'react';

import { type GlobalSearchProps, GlobalSearchView } from '@/components/admin-layout/GlobalSearch';
import { useGlobalSearch } from '@/hooks/useGlobalSearch';
import { useKeyboardShortcutListener } from '@/hooks/useKeyboardShortcutListener';
import { KEYBOARD_SHORTCUT_EVENTS } from '@/shared/keyboardShortcuts';

export type SearchBarProps = GlobalSearchProps;

export function SearchBar({ isMobile = false }: SearchBarProps) {
  const search = useGlobalSearch();
  const { open, openSearch, closeSearch } = search;

  const handleOpenSearch = useCallback(() => {
    openSearch();
  }, [openSearch]);

  const handleCloseSearch = useCallback(() => {
    closeSearch({ restoreFocus: true });
  }, [closeSearch]);

  useKeyboardShortcutListener(KEYBOARD_SHORTCUT_EVENTS.openSearch, handleOpenSearch);
  useKeyboardShortcutListener(KEYBOARD_SHORTCUT_EVENTS.closeModal, handleCloseSearch, open);

  return <GlobalSearchView isMobile={isMobile} search={search} />;
}

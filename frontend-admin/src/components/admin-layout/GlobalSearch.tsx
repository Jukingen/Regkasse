'use client';

import React, { useEffect, useId, useMemo, useRef } from 'react';
import { Empty, Input, Popover, Spin } from 'antd';
import { SearchOutlined } from '@ant-design/icons';
import { useI18n } from '@/i18n';
import { useGlobalSearch } from '@/hooks/useGlobalSearch';
import { getAdminHeaderPopupContainer } from '@/shared/layout/adminHeaderDropdown';
import { getGlobalSearchOptionId, getGlobalSearchShortcutLabel } from '@/shared/globalSearchA11y';
import { splitSearchHighlight } from '@/shared/searchUtils';
import type { GlobalSearchResultItem } from '@/components/admin-layout/GlobalSearch.types';

export type GlobalSearchProps = {
    isMobile?: boolean;
};

function HighlightedLabel({ label, query }: { label: string; query: string }) {
    const parts = useMemo(() => splitSearchHighlight(label, query), [label, query]);
    return (
        <>
            {parts.map((part, index) =>
                part.highlight ? (
                    <mark key={index} className="admin-header-nav-search-mark">
                        {part.text}
                    </mark>
                ) : (
                    <React.Fragment key={index}>{part.text}</React.Fragment>
                ),
            )}
        </>
    );
}

function GlobalSearchResultRow({
    item,
    query,
    selected,
    optionId,
    onSelect,
    onHover,
    optionRef,
}: {
    item: GlobalSearchResultItem;
    query: string;
    selected: boolean;
    optionId: string;
    onSelect: () => void;
    onHover: () => void;
    optionRef: (node: HTMLButtonElement | null) => void;
}) {
    return (
        <button
            ref={optionRef}
            id={optionId}
            type="button"
            role="option"
            aria-selected={selected}
            className={`admin-header-nav-search-option${selected ? ' admin-header-nav-search-option-selected' : ''}`}
            onClick={onSelect}
            onMouseEnter={onHover}
        >
            <span className="admin-header-nav-search-option-label">
                <HighlightedLabel label={item.label} query={query} />
            </span>
            {item.breadcrumb ? (
                <span className="admin-header-nav-search-option-breadcrumb">{item.breadcrumb}</span>
            ) : null}
            <span className="admin-header-nav-search-option-path">{item.href}</span>
        </button>
    );
}

/** Header menu search with dropdown results (registry-backed, i18n labels). */
export function GlobalSearch({ isMobile = false }: GlobalSearchProps) {
    const { t } = useI18n();
    const baseId = useId().replace(/:/g, '');
    const inputId = `${baseId}-input`;
    const listboxId = `${baseId}-listbox`;
    const helpId = `${baseId}-help`;
    const hintId = `${baseId}-hint`;

    const {
        open,
        setOpen,
        query,
        setQuery,
        debouncedQuery,
        isSearching,
        results,
        selectedIndex,
        setSelectedIndex,
        inputRef,
        selectItem,
        onInputKeyDown,
    } = useGlobalSearch();

    const optionRefs = useRef<Array<HTMLButtonElement | null>>([]);
    const shortcutLabel = getGlobalSearchShortcutLabel();

    const placeholder = isMobile
        ? t('adminShell.search.placeholder')
        : t('adminShell.search.placeholderWithShortcut', { shortcut: shortcutLabel });

    const activeOptionId =
        open && results.length > 0
            ? getGlobalSearchOptionId(listboxId, selectedIndex)
            : undefined;

    useEffect(() => {
        if (!open || results.length === 0) return;
        optionRefs.current[selectedIndex]?.scrollIntoView({ block: 'nearest' });
    }, [open, results.length, selectedIndex]);

    const dropdown = (
        <div
            id={listboxId}
            className="admin-header-nav-search-dropdown"
            role="listbox"
            aria-label={t('adminShell.search.resultsAria')}
            aria-busy={isSearching || undefined}
        >
            {!query.trim() ? (
                <div id={hintId} className="admin-header-nav-search-hint">
                    {t('adminShell.search.hint')}
                </div>
            ) : null}

            {query.trim() && isSearching ? (
                <div className="admin-header-nav-search-loading" role="status" aria-live="polite">
                    <Spin size="small" />
                    <span>{t('adminShell.search.searching')}</span>
                </div>
            ) : null}

            {query.trim() && !isSearching && results.length === 0 ? (
                <Empty
                    image={Empty.PRESENTED_IMAGE_SIMPLE}
                    description={t('adminShell.search.noResults')}
                    className="admin-header-nav-search-empty"
                />
            ) : null}

            {query.trim() && !isSearching && results.length > 0
                ? results.map((item, index) => (
                      <GlobalSearchResultRow
                          key={item.id}
                          item={item}
                          query={debouncedQuery}
                          selected={index === selectedIndex}
                          optionId={getGlobalSearchOptionId(listboxId, index)}
                          onSelect={() => selectItem(item)}
                          onHover={() => setSelectedIndex(index)}
                          optionRef={(node) => {
                              optionRefs.current[index] = node;
                          }}
                      />
                  ))
                : null}
        </div>
    );

    return (
        <div className={`admin-header-nav-search${isMobile ? ' admin-header-nav-search-compact' : ''}`}>
            <span id={helpId} className="admin-header-nav-search-sr-help">
                {t('adminShell.search.helpText')}
            </span>

            <Popover
                open={open}
                onOpenChange={setOpen}
                trigger="click"
                placement="bottomRight"
                content={dropdown}
                arrow={false}
                classNames={{ root: 'admin-header-nav-search-popover admin-header-dropdown' }}
                getPopupContainer={getAdminHeaderPopupContainer}
            >
                <Input
                    ref={inputRef}
                    id={inputId}
                    size="small"
                    allowClear
                    className="admin-header-nav-search-input"
                    prefix={<SearchOutlined aria-hidden />}
                    placeholder={placeholder}
                    role="combobox"
                    aria-label={t('adminShell.search.ariaLabel')}
                    aria-describedby={helpId}
                    aria-expanded={open}
                    aria-controls={listboxId}
                    aria-activedescendant={activeOptionId}
                    aria-autocomplete="list"
                    value={query}
                    onChange={(event) => setQuery(event.target.value)}
                    onKeyDown={onInputKeyDown}
                    onClick={() => setOpen(true)}
                    suffix={
                        !isMobile ? (
                            <kbd className="admin-header-nav-search-kbd" aria-hidden>
                                {shortcutLabel}
                            </kbd>
                        ) : undefined
                    }
                    autoComplete="off"
                    spellCheck={false}
                />
            </Popover>
        </div>
    );
}

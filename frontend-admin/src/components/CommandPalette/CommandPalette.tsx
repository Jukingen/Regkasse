'use client';

import React, { useEffect, useMemo, useRef, useState } from 'react';
import { Divider, Input, Modal, Spin } from 'antd';
import type { InputRef } from 'antd';
import { SearchOutlined } from '@ant-design/icons';
import { useI18n } from '@/i18n';
import { useCommands } from '@/components/CommandPalette/useCommands';
import { resolveCommandItemIcon } from '@/features/command-palette/commandItemIcons';
import type { CommandItem, CommandItemGroup } from '@/components/CommandPalette/types';
import styles from '@/components/CommandPalette/commandPalette.module.css';

export type CommandPaletteProps = {
    open: boolean;
    onClose: () => void;
};

function resolveItemGroup(item: CommandItem): CommandItemGroup {
    if (item.group) return item.group;
    switch (item.type) {
        case 'action':
            return 'Actions';
        case 'user':
            return 'Users';
        case 'receipt':
            return 'Receipts';
        case 'register':
            return 'Registers';
        default:
            return 'Navigation';
    }
}

function groupLabel(t: (key: string) => string, group: CommandItemGroup): string {
    switch (group) {
        case 'Actions':
            return t('commandPalette.group.actions');
        case 'Users':
            return t('commandPalette.group.users');
        case 'Receipts':
            return t('commandPalette.group.receipts');
        case 'Registers':
            return t('commandPalette.group.registers');
        case 'Recent':
            return t('commandPalette.group.recent');
        default:
            return t('commandPalette.group.navigation');
    }
}

/** Spotlight-style global command palette modal. */
export const CommandPalette: React.FC<CommandPaletteProps> = ({ open, onClose }) => {
    const { t } = useI18n();
    const [searchTerm, setSearchTerm] = useState('');
    const [selectedIndex, setSelectedIndex] = useState(0);
    const inputRef = useRef<InputRef>(null);

    const { results, isLoading, runCommand, refreshRecent } = useCommands({
        open,
        searchTerm,
        onClose,
    });

    const groupedResults = useMemo(() => {
        const map = new Map<CommandItemGroup, CommandItem[]>();
        for (const item of results) {
            const g = resolveItemGroup(item);
            const list = map.get(g) ?? [];
            list.push(item);
            map.set(g, list);
        }
        return map;
    }, [results]);

    const flatResults = useMemo(() => results, [results]);

    useEffect(() => {
        if (!open) return;
        setSearchTerm('');
        setSelectedIndex(0);
        refreshRecent();
        const id = window.setTimeout(() => inputRef.current?.focus(), 0);
        return () => window.clearTimeout(id);
    }, [open, refreshRecent]);

    useEffect(() => {
        setSelectedIndex(0);
    }, [searchTerm, flatResults.length]);

    const handleSelect = (item: CommandItem) => {
        runCommand(item);
        onClose();
    };

    const onInputKeyDown = (e: React.KeyboardEvent<HTMLInputElement>) => {
        if (e.key === 'ArrowDown') {
            e.preventDefault();
            setSelectedIndex((i) => Math.min(i + 1, Math.max(0, flatResults.length - 1)));
        } else if (e.key === 'ArrowUp') {
            e.preventDefault();
            setSelectedIndex((i) => Math.max(i - 1, 0));
        } else if (e.key === 'Enter' && flatResults[selectedIndex]) {
            e.preventDefault();
            handleSelect(flatResults[selectedIndex]);
        } else if (e.key === 'Escape') {
            onClose();
        }
    };

    let flatIndex = 0;

    return (
        <Modal
            open={open}
            onCancel={onClose}
            footer={null}
            closable={false}
            width={600}
            destroyOnHidden
            className={styles.commandPalette}
            title={null}
            styles={{ body: { padding: 0 } }}
        >
            <Input
                ref={inputRef}
                autoFocus
                placeholder={t('commandPalette.placeholder')}
                prefix={<SearchOutlined />}
                value={searchTerm}
                onChange={(e) => setSearchTerm(e.target.value)}
                onKeyDown={onInputKeyDown}
                size="large"
                variant="borderless"
                className={styles.searchInput}
                aria-label={t('commandPalette.placeholder')}
                autoComplete="off"
                spellCheck={false}
            />

            <Divider style={{ margin: 0 }} />

            <div
                className={styles.commandResults}
                role="listbox"
                aria-label={t('commandPalette.resultsAria')}
            >
                {isLoading ? (
                    <div className={styles.loading}>
                        <Spin size="small" />
                    </div>
                ) : null}

                {!isLoading && flatResults.length === 0 && searchTerm.trim() ? (
                    <div className={styles.emptyState}>{t('commandPalette.empty')}</div>
                ) : null}

                {!isLoading && flatResults.length === 0 && !searchTerm.trim() ? (
                    <div className={styles.emptyState}>{t('commandPalette.emptyHint')}</div>
                ) : null}

                {Array.from(groupedResults.entries()).map(([group, items]) => (
                    <div key={group}>
                        <div className={styles.groupTitle}>{groupLabel(t, group)}</div>
                        {items.map((item) => {
                            const index = flatIndex++;
                            const selected = index === selectedIndex;
                            return (
                                <div
                                    key={item.id}
                                    role="option"
                                    aria-selected={selected}
                                    className={`${styles.commandItem} ${selected ? styles.selected : ''}`}
                                    onClick={() => handleSelect(item)}
                                    onMouseEnter={() => setSelectedIndex(index)}
                                >
                                    <div className={styles.commandIcon}>
                                        {item.icon ?? resolveCommandItemIcon(item)}
                                    </div>
                                    <div className={styles.commandContent}>
                                        <div className={styles.commandLabel}>{item.label}</div>
                                        {item.description ? (
                                            <div className={styles.commandDescription}>
                                                {item.description}
                                            </div>
                                        ) : null}
                                    </div>
                                    {item.type === 'page' ? (
                                        <div className={styles.commandShortcut} aria-hidden>
                                            ↵
                                        </div>
                                    ) : null}
                                </div>
                            );
                        })}
                    </div>
                ))}
            </div>

            <div className={styles.commandFooter}>
                <span>
                    <kbd className={styles.footerKey}>↵</kbd>
                    {t('commandPalette.footer.select')}
                </span>
                <span>
                    <kbd className={styles.footerKey}>Esc</kbd>
                    {t('commandPalette.footer.close')}
                </span>
            </div>
        </Modal>
    );
};

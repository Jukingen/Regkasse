'use client';

import {
  AppstoreOutlined,
  CheckCircleOutlined,
  CloseCircleOutlined,
  ReloadOutlined,
  SearchOutlined,
  ThunderboltOutlined,
} from '@ant-design/icons';
import { Button, Dropdown, Input, Modal, Select, Space, Typography } from 'antd';
import type { MenuProps } from 'antd';
import React, { useCallback, useEffect, useMemo, useRef, useState } from 'react';

import { useI18n } from '@/i18n';

export type PermissionStatusFilter = 'all' | 'allowed' | 'denied' | 'individual';

export type PermissionQuickFilterPreset =
  | 'denied'
  | 'allowed'
  | 'individual'
  | 'allGroups'
  | 'reset';

export type PermissionCommandItem = {
  id: string;
  label: string;
  description?: string;
  group: 'permission' | 'group' | 'action';
  keywords?: string[];
  run: () => void;
};

export type PermissionCatalogToolbarProps = {
  searchValue: string;
  onSearchChange: (next: string) => void;
  /** Called on Enter to apply/flush the current search (bypass debounce). */
  onSearchApply?: (value: string) => void;
  searchPlaceholder: string;
  counterLabel: string;
  visibleCount: number;
  totalCount: number;
  shortcutEnabled?: boolean;

  groupFilter: string | 'all';
  onGroupFilterChange: (next: string | 'all') => void;
  groupOptions: { value: string; label: string }[];
  allGroupsLabel: string;

  /** Optional Menü filter (sidebar leaf path). */
  menuFilter?: string | 'all';
  onMenuFilterChange?: (next: string | 'all') => void;
  menuOptions?: { value: string; label: string }[];
  allMenusLabel?: string;
  menuFilterPlaceholder?: string;

  /** When omitted, status filter UI is hidden (e.g. role drawer without status). */
  statusFilter?: PermissionStatusFilter;
  onStatusFilterChange?: (next: PermissionStatusFilter) => void;
  statusOptions?: { value: PermissionStatusFilter; label: string }[];

  /** Quick Filter menu; when omitted, menu is hidden. */
  onQuickFilter?: (preset: PermissionQuickFilterPreset) => void;
  /** Hide presets that don't apply (e.g. individual on role draft). */
  quickFilterHidden?: PermissionQuickFilterPreset[];

  expandAllLabel: string;
  collapseAllLabel: string;
  onExpandAll: () => void;
  onCollapseAll: () => void;

  /** Command palette items (permissions, groups, actions). */
  commandItems?: PermissionCommandItem[];

  style?: React.CSSProperties;
};

function isMacPlatform(): boolean {
  if (typeof navigator === 'undefined') return false;
  return /Mac|iPhone|iPad|iPod/i.test(navigator.platform || navigator.userAgent);
}

/**
 * Shared toolbar: search + shortcuts + quick filter + optional command palette.
 * Ctrl/Cmd+F focuses search; Escape clears; Enter applies; Ctrl/Cmd+K opens palette.
 */
export function PermissionCatalogToolbar({
  searchValue,
  onSearchChange,
  onSearchApply,
  searchPlaceholder,
  counterLabel,
  visibleCount,
  totalCount,
  shortcutEnabled = true,
  groupFilter,
  onGroupFilterChange,
  groupOptions,
  allGroupsLabel,
  menuFilter,
  onMenuFilterChange,
  menuOptions,
  allMenusLabel,
  menuFilterPlaceholder,
  statusFilter,
  onStatusFilterChange,
  statusOptions,
  onQuickFilter,
  quickFilterHidden = [],
  expandAllLabel,
  collapseAllLabel,
  onExpandAll,
  onCollapseAll,
  commandItems = [],
  style,
}: PermissionCatalogToolbarProps) {
  const { t } = useI18n();
  const inputRef = useRef<HTMLInputElement | null>(null);
  const [paletteOpen, setPaletteOpen] = useState(false);
  const [paletteQuery, setPaletteQuery] = useState('');
  const isMac = useMemo(() => isMacPlatform(), []);
  const findHint = isMac ? '⌘F' : 'Ctrl+F';

  const focusSearch = useCallback(() => {
    inputRef.current?.focus();
    inputRef.current?.select();
  }, []);

  const clearSearch = useCallback(() => {
    onSearchChange('');
    onSearchApply?.('');
    inputRef.current?.blur();
  }, [onSearchChange, onSearchApply]);

  const openPalette = useCallback(() => {
    if (commandItems.length === 0) return;
    setPaletteQuery('');
    setPaletteOpen(true);
  }, [commandItems.length]);

  useEffect(() => {
    if (!shortcutEnabled) return;
    const onKeyDown = (event: KeyboardEvent) => {
      const meta = event.ctrlKey || event.metaKey;
      const key = event.key.toLowerCase();

      if (meta && key === 'f') {
        event.preventDefault();
        event.stopPropagation();
        focusSearch();
        return;
      }

      if (meta && key === 'k' && commandItems.length > 0) {
        event.preventDefault();
        event.stopPropagation();
        openPalette();
        return;
      }

      if (event.key === 'Escape' && !paletteOpen) {
        const active = document.activeElement;
        const searchFocused = active === inputRef.current;
        if (searchFocused || searchValue) {
          event.preventDefault();
          clearSearch();
        }
      }
    };
    // Capture so we can win over the global Ctrl+K command palette while editor is active.
    window.addEventListener('keydown', onKeyDown, true);
    return () => window.removeEventListener('keydown', onKeyDown, true);
  }, [
    shortcutEnabled,
    focusSearch,
    openPalette,
    commandItems.length,
    paletteOpen,
    searchValue,
    clearSearch,
  ]);

  const hidden = useMemo(() => new Set(quickFilterHidden), [quickFilterHidden]);

  const quickFilterItems: MenuProps['items'] = useMemo(() => {
    if (!onQuickFilter) return [];
    const items: MenuProps['items'] = [];
    if (!hidden.has('denied')) {
      items.push({
        key: 'denied',
        icon: <CloseCircleOutlined style={{ color: '#ff4d4f' }} />,
        label: t('users.permissionToolbar.quickFilterDenied'),
        onClick: () => onQuickFilter('denied'),
      });
    }
    if (!hidden.has('allowed')) {
      items.push({
        key: 'allowed',
        icon: <CheckCircleOutlined style={{ color: '#52c41a' }} />,
        label: t('users.permissionToolbar.quickFilterAllowed'),
        onClick: () => onQuickFilter('allowed'),
      });
    }
    if (!hidden.has('individual')) {
      items.push({
        key: 'individual',
        icon: <ThunderboltOutlined style={{ color: '#1677ff' }} />,
        label: t('users.permissionToolbar.quickFilterIndividual'),
        onClick: () => onQuickFilter('individual'),
      });
    }
    if (!hidden.has('allGroups')) {
      items.push({
        key: 'allGroups',
        icon: <AppstoreOutlined />,
        label: t('users.permissionToolbar.quickFilterAllGroups'),
        onClick: () => onQuickFilter('allGroups'),
      });
    }
    items.push({ type: 'divider' });
    items.push({
      key: 'reset',
      icon: <ReloadOutlined />,
      label: t('users.permissionToolbar.quickFilterReset'),
      onClick: () => onQuickFilter('reset'),
    });
    return items;
  }, [onQuickFilter, hidden, t]);

  const filteredCommands = useMemo(() => {
    const q = paletteQuery.trim().toLowerCase();
    if (!q) return commandItems.slice(0, 40);
    return commandItems
      .filter((item) => {
        const hay = `${item.label} ${item.description ?? ''} ${(item.keywords ?? []).join(' ')}`.toLowerCase();
        return hay.includes(q);
      })
      .slice(0, 40);
  }, [commandItems, paletteQuery]);

  return (
    <Space orientation="vertical" size={8} style={{ width: '100%', ...style }}>
      <Input
        ref={(node) => {
          inputRef.current = node?.input ?? null;
        }}
        allowClear
        value={searchValue}
        onChange={(e) => onSearchChange(e.target.value)}
        onPressEnter={() => onSearchApply?.(searchValue)}
        onKeyDown={(e) => {
          if (e.key === 'Escape') {
            e.preventDefault();
            clearSearch();
          }
        }}
        placeholder={searchPlaceholder}
        prefix={<SearchOutlined aria-hidden />}
        suffix={
          <Typography.Text type="secondary" style={{ fontSize: 11, userSelect: 'none' }}>
            {findHint}
          </Typography.Text>
        }
        aria-label={searchPlaceholder}
        aria-describedby="permission-catalog-search-count"
        data-permission-tour="search"
      />
      <Space wrap size={8} style={{ width: '100%' }}>
        <Select
          value={groupFilter}
          onChange={(v) => onGroupFilterChange(v)}
          style={{ minWidth: 200 }}
          options={[{ value: 'all', label: allGroupsLabel }, ...groupOptions]}
          aria-label={allGroupsLabel}
        />
        {menuFilter != null && onMenuFilterChange && menuOptions ? (
          <Select
            showSearch
            optionFilterProp="label"
            value={menuFilter}
            onChange={(v) => onMenuFilterChange(v)}
            style={{ minWidth: 220 }}
            placeholder={menuFilterPlaceholder}
            options={[{ value: 'all', label: allMenusLabel ?? '—' }, ...menuOptions]}
            aria-label={menuFilterPlaceholder}
          />
        ) : null}
        {statusFilter != null && onStatusFilterChange && statusOptions ? (
          <Select
            value={statusFilter}
            onChange={(v) => onStatusFilterChange(v)}
            style={{ minWidth: 160 }}
            options={statusOptions}
          />
        ) : null}
        {onQuickFilter ? (
          <Dropdown menu={{ items: quickFilterItems }} trigger={['click']}>
            <Button size="small">{t('users.permissionToolbar.quickFilter')}</Button>
          </Dropdown>
        ) : null}
        {commandItems.length > 0 ? (
          <Button size="small" onClick={openPalette}>
            {t('users.permissionToolbar.commandPalette', {
              shortcut: isMac ? '⌘K' : 'Ctrl+K',
            })}
          </Button>
        ) : null}
        <Button size="small" onClick={onExpandAll}>
          {expandAllLabel}
        </Button>
        <Button size="small" onClick={onCollapseAll}>
          {collapseAllLabel}
        </Button>
      </Space>
      <Typography.Text
        id="permission-catalog-search-count"
        type="secondary"
        style={{ fontSize: 12 }}
        aria-live="polite"
        data-visible={visibleCount}
        data-total={totalCount}
      >
        {counterLabel}
      </Typography.Text>

      <Modal
        title={t('users.permissionToolbar.paletteTitle')}
        open={paletteOpen}
        onCancel={() => setPaletteOpen(false)}
        footer={null}
        destroyOnHidden
        width={480}
      >
        <Input
          autoFocus
          allowClear
          value={paletteQuery}
          onChange={(e) => setPaletteQuery(e.target.value)}
          placeholder={t('users.permissionToolbar.palettePlaceholder')}
          prefix={<SearchOutlined />}
          style={{ marginBottom: 12 }}
        />
        <div style={{ maxHeight: 320, overflow: 'auto' }}>
          {filteredCommands.length === 0 ? (
            <Typography.Text type="secondary">{t('users.permissionToolbar.paletteEmpty')}</Typography.Text>
          ) : (
            filteredCommands.map((item) => (
              <button
                key={item.id}
                type="button"
                onClick={() => {
                  item.run();
                  setPaletteOpen(false);
                }}
                style={{
                  display: 'block',
                  width: '100%',
                  textAlign: 'left',
                  padding: '8px 10px',
                  border: 'none',
                  borderBottom: '1px solid rgba(0,0,0,0.06)',
                  background: 'transparent',
                  cursor: 'pointer',
                }}
              >
                <Typography.Text style={{ fontSize: 13 }}>{item.label}</Typography.Text>
                {item.description ? (
                  <Typography.Text
                    type="secondary"
                    style={{ display: 'block', fontSize: 11 }}
                  >
                    {item.description}
                  </Typography.Text>
                ) : null}
              </button>
            ))
          )}
        </div>
      </Modal>
    </Space>
  );
}

/** @deprecated Prefer PermissionCatalogToolbar — kept for simple search-only call sites. */
export { PermissionCatalogToolbar as PermissionCatalogSearch };

'use client';

import { SearchOutlined } from '@ant-design/icons';
import { Input } from 'antd';
import Link from 'next/link';
import { useMemo } from 'react';

import sidebarStyles from '@/app/(protected)/protected-layout-sidebar.module.css';
import { useRecentAdminMenuPaths } from '@/features/dashboard/hooks/useRecentAdminMenuPaths';
import { getSidebarLabelKeyForPath } from '@/features/dashboard/utils/recentAdminMenuPaths';
import { SidebarMenu } from '@/features/menu/SidebarMenu';
import { useI18n } from '@/i18n';
import { SIDEBAR_NAV_ITEM_CATALOG } from '@/shared/adminSidebarRegistry';

export type AdminSidebarQuickAccessProps = {
  query: string;
  onQueryChange: (value: string) => void;
  allowedMenuKeys: ReadonlySet<string>;
  visibleMenuKeys: ReadonlySet<string>;
  collapsed: boolean;
  onNavigate?: () => void;
};

function hrefForMenuKey(menuKey: string): string {
  const item = Object.values(SIDEBAR_NAV_ITEM_CATALOG).find((entry) => entry.menuKey === menuKey);
  return item?.href ?? menuKey;
}

/**
 * Sidebar filter, favorites, and recently used leaves (permission-filtered).
 * Hidden when the sider is collapsed.
 */
export function AdminSidebarQuickAccess({
  query,
  onQueryChange,
  allowedMenuKeys,
  visibleMenuKeys,
  collapsed,
  onNavigate,
}: AdminSidebarQuickAccessProps) {
  const { t } = useI18n();
  const recent = useRecentAdminMenuPaths();
  const visibleRecent = useMemo(
    () => recent.filter((key) => allowedMenuKeys.has(key)),
    [recent, allowedMenuKeys]
  );

  if (collapsed) return null;

  const isFiltering = query.trim().length > 0;

  return (
    <div className={sidebarStyles.quickAccess}>
      <Input
        allowClear
        size="small"
        value={query}
        onChange={(event) => onQueryChange(event.target.value)}
        placeholder={t('adminShell.sidebar.filterPlaceholder')}
        prefix={<SearchOutlined aria-hidden />}
        aria-label={t('adminShell.sidebar.filterAria')}
      />
      {!isFiltering ? (
        <SidebarMenu
          visibleMenuKeys={visibleMenuKeys}
          collapsed={collapsed}
          onNavigate={onNavigate}
        />
      ) : null}
      {!isFiltering && visibleRecent.length > 0 ? (
        <nav className={sidebarStyles.recentList} aria-label={t('adminShell.sidebar.recentTitle')}>
          <div className={sidebarStyles.recentTitle}>{t('adminShell.sidebar.recentTitle')}</div>
          <ul>
            {visibleRecent.map((key) => {
              const labelKey = getSidebarLabelKeyForPath(key);
              return (
                <li key={key}>
                  <Link href={hrefForMenuKey(key)} className={sidebarStyles.recentLink}>
                    {labelKey ? t(labelKey) : key}
                  </Link>
                </li>
              );
            })}
          </ul>
        </nav>
      ) : null}
    </div>
  );
}

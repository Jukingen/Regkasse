'use client';

/**
 * Dev/debug overlay for FA sidebar ↔ permission mapping.
 *
 * Enable with:
 * - Ctrl+Alt+Shift+P (toggle; Alt avoids clash with export preview Ctrl+Shift+P)
 * - Command palette → "Debug Menu Permissions"
 * - `NEXT_PUBLIC_DEBUG_MENU_PERMISSION_GROUPS=true` or `?debugMenuPermissions=1` (group gaps)
 */
import { Alert, Modal, Table, Typography } from 'antd';
import { useSearchParams } from 'next/navigation';
import React, { Suspense, useEffect, useMemo, useSyncExternalStore } from 'react';

import {
  getSidebarCatalogLeafMenuKeys,
  getWiredSidebarMenuAreas,
  SIDEBAR_NAV_ITEM_CATALOG,
} from '@/shared/adminSidebarRegistry';
import {
  getMenuPermissionDebugOpen,
  setMenuPermissionDebugOpen,
  subscribeMenuPermissionDebug,
  toggleMenuPermissionDebug,
} from '@/shared/auth/menuPermissionDebugStore';
import {
  listMenuPermissionMapRows,
  listUnwiredMenuPermissionMapKeys,
} from '@/shared/auth/menuPermissionMapping';
import {
  classifyMenuLeafPermissionGroup,
  isMenuPermissionGroupDebugEnabled,
  listMenuAreasMissingPermissionGroup,
  type MenuPermissionGroupGap,
} from '@/shared/auth/permissionGroupRegistry';
import { MENU_AREA_PRIMARY_PATH } from '@/shared/auth/menuPermissionRegistry';
import { ROUTE_PERMISSIONS } from '@/shared/auth/routePermissions';
import { KEYBOARD_SHORTCUT_EVENTS } from '@/shared/keyboardShortcuts';

function collectGaps(): MenuPermissionGroupGap[] {
  const gaps: MenuPermissionGroupGap[] = [];

  for (const area of listMenuAreasMissingPermissionGroup()) {
    gaps.push({
      menuKey: MENU_AREA_PRIMARY_PATH[area] ?? area,
      reason: 'missing_menu_area_group',
      detail: area,
    });
  }

  for (const menuKey of getSidebarCatalogLeafMenuKeys()) {
    const required = ROUTE_PERMISSIONS[menuKey];
    const { gap } = classifyMenuLeafPermissionGroup(menuKey, required);
    if (gap) gaps.push(gap);
  }

  const seen = new Set<string>();
  return gaps.filter((g) => {
    const id = `${g.menuKey}|${g.reason}`;
    if (seen.has(id)) return false;
    seen.add(id);
    return true;
  });
}

function MenuPermissionMapDebugModal() {
  const open = useSyncExternalStore(
    subscribeMenuPermissionDebug,
    getMenuPermissionDebugOpen,
    () => false
  );

  useEffect(() => {
    if (process.env.NODE_ENV !== 'development') return;
    const onToggle = () => toggleMenuPermissionDebug();
    document.addEventListener(KEYBOARD_SHORTCUT_EVENTS.debugMenuPermissions, onToggle);
    return () => {
      document.removeEventListener(KEYBOARD_SHORTCUT_EVENTS.debugMenuPermissions, onToggle);
    };
  }, []);

  const rows = useMemo(() => {
    const wired = new Set(getWiredSidebarMenuAreas());
    return listMenuPermissionMapRows().map((row) => {
      const catalogLeaf = Object.values(SIDEBAR_NAV_ITEM_CATALOG).find(
        (item) => item.menuArea === row.menuKey
      );
      return {
        key: row.menuKey,
        menuKey: row.menuKey,
        menuLabel: row.menuLabel,
        permissionGroup: row.permissionGroup ?? '—',
        permissionKey: row.permissionKey,
        permissionKeysAnyOf: row.permissionKeysAnyOf?.join(' ∨ ') ?? '—',
        catalogPath: catalogLeaf?.menuKey ?? '—',
        wired: wired.has(row.menuKey) ? 'yes' : 'no',
      };
    });
  }, []);

  const unwired = useMemo(
    () => listUnwiredMenuPermissionMapKeys(getWiredSidebarMenuAreas()),
    []
  );

  return (
    <Modal
      title="Debug Menu Permissions"
      open={open}
      onCancel={() => setMenuPermissionDebugOpen(false)}
      footer={null}
      width={960}
      destroyOnHidden
    >
      <Typography.Paragraph type="secondary" style={{ marginBottom: 12 }}>
        Source: <code>MENU_PERMISSION_MAP</code> → catalog <code>menuArea</code>. Toggle with
        Ctrl+Alt+Shift+P.
      </Typography.Paragraph>
      {unwired.length > 0 && (
        <Alert
          type="warning"
          showIcon
          style={{ marginBottom: 12 }}
          message="Unwired map keys"
          description={<code>{unwired.join(', ')}</code>}
        />
      )}
      <Table
        size="small"
        pagination={false}
        scroll={{ y: 420 }}
        dataSource={rows}
        columns={[
          { title: 'Menu area', dataIndex: 'menuKey', width: 140 },
          { title: 'Label', dataIndex: 'menuLabel', width: 180 },
          { title: 'Group', dataIndex: 'permissionGroup', width: 160 },
          { title: 'Permission', dataIndex: 'permissionKey', width: 180 },
          { title: 'Any-of', dataIndex: 'permissionKeysAnyOf', ellipsis: true },
          { title: 'Catalog path', dataIndex: 'catalogPath', width: 180 },
          { title: 'Wired', dataIndex: 'wired', width: 70 },
        ]}
      />
    </Modal>
  );
}

function MenuPermissionGroupDebugInner() {
  const searchParams = useSearchParams();
  const enabled = isMenuPermissionGroupDebugEnabled(searchParams);

  const gaps = useMemo(() => (enabled ? collectGaps() : []), [enabled]);

  if (!enabled || gaps.length === 0) {
    if (enabled) {
      return (
        <Alert
          type="success"
          showIcon
          style={{ margin: 8 }}
          message="Menu ↔ permission groups OK"
          description="No catalog leaf classified as missing / other-only."
        />
      );
    }
    return null;
  }

  return (
    <Alert
      type="warning"
      showIcon
      style={{ margin: 8, maxHeight: 220, overflow: 'auto' }}
      message={`Menu permission group gaps (${gaps.length})`}
      description={
        <Typography.Paragraph style={{ marginBottom: 0, fontSize: 12 }}>
          <ul style={{ paddingLeft: 18, margin: 0 }}>
            {gaps.map((g) => (
              <li key={`${g.menuKey}-${g.reason}`}>
                <code>{g.menuKey}</code> — {g.reason}
                {g.detail ? ` (${g.detail})` : ''}
              </li>
            ))}
          </ul>
        </Typography.Paragraph>
      }
    />
  );
}

/** Suspense-safe debug panel + Ctrl+Alt+Shift+P modal for Admin sidebar. */
export function MenuPermissionGroupDebugPanel() {
  return (
    <>
      <Suspense fallback={null}>
        <MenuPermissionGroupDebugInner />
      </Suspense>
      {process.env.NODE_ENV === 'development' ? <MenuPermissionMapDebugModal /> : null}
    </>
  );
}

'use client';

import { SearchOutlined } from '@ant-design/icons';
import { Drawer, Input, List, Tag, Typography } from 'antd';
import React, { useEffect, useMemo, useState, useSyncExternalStore } from 'react';

import { useRolesWithPermissions } from '@/features/users/hooks/useRolesWithPermissions';
import { resolvePermissionDisplayLabel } from '@/features/users/utils/permissionDisplayLabel';
import {
  getPermissionsAffectingMenu,
  listRolesHoldingPermission,
  listSidebarMenuFilterOptions,
} from '@/features/users/utils/permissionMenuImpact';
import { useI18n } from '@/i18n';
import {
  closePermissionExplorer,
  getPermissionExplorerMenuKey,
  getPermissionExplorerOpen,
  setPermissionExplorerMenuKey,
  subscribeMenuPermissionInfo,
} from '@/shared/auth/menuPermissionInfoStore';
import { resolvePermissionGroupSlugForPermissionKey } from '@/shared/auth/permissionGroupRegistry';

export type PermissionExplorerDrawerProps = {
  /** When false, skip roles query (e.g. insufficient permission). Default true when open. */
  loadRoles?: boolean;
};

/**
 * Search menus → show gating permission(s), group, and roles that hold them.
 */
export function PermissionExplorerDrawer({ loadRoles = true }: PermissionExplorerDrawerProps) {
  const { t } = useI18n();
  const open = useSyncExternalStore(
    subscribeMenuPermissionInfo,
    getPermissionExplorerOpen,
    () => false
  );
  const selectedMenuKey = useSyncExternalStore(
    subscribeMenuPermissionInfo,
    getPermissionExplorerMenuKey,
    () => null
  );

  const rolesQuery = useRolesWithPermissions({ enabled: open && loadRoles });
  const roles = rolesQuery.data ?? [];

  const [query, setQuery] = useState('');

  useEffect(() => {
    if (!open) setQuery('');
  }, [open]);

  const options = useMemo(() => listSidebarMenuFilterOptions(), []);

  const filtered = useMemo(() => {
    const q = query.trim().toLowerCase();
    if (!q) return options;
    return options.filter((opt) => {
      const label = t(opt.labelKey).toLowerCase();
      return (
        label.includes(q) ||
        opt.value.toLowerCase().includes(q) ||
        (opt.primaryPermission?.toLowerCase().includes(q) ?? false)
      );
    });
  }, [options, query, t]);

  const selected = useMemo(
    () => options.find((o) => o.value === selectedMenuKey) ?? null,
    [options, selectedMenuKey]
  );

  const requirements = useMemo(
    () => (selectedMenuKey ? getPermissionsAffectingMenu(selectedMenuKey) : []),
    [selectedMenuKey]
  );

  return (
    <Drawer
      title={t('adminShell.menuPermission.explorerTitle')}
      open={open}
      onClose={closePermissionExplorer}
      width={420}
      destroyOnHidden
    >
      <Input
        allowClear
        prefix={<SearchOutlined />}
        placeholder={t('adminShell.menuPermission.explorerSearch')}
        value={query}
        onChange={(e) => setQuery(e.target.value)}
        style={{ marginBottom: 12 }}
      />
      <List
        size="small"
        bordered
        style={{ maxHeight: 220, overflow: 'auto', marginBottom: 16 }}
        dataSource={filtered}
        locale={{ emptyText: t('adminShell.menuPermission.explorerEmpty') }}
        renderItem={(item) => (
          <List.Item
            key={item.value}
            onClick={() => setPermissionExplorerMenuKey(item.value)}
            style={{
              cursor: 'pointer',
              background: item.value === selectedMenuKey ? '#e6f4ff' : undefined,
            }}
          >
            <div style={{ minWidth: 0 }}>
              <Typography.Text strong style={{ display: 'block' }}>
                {t(item.labelKey)}
              </Typography.Text>
              <Typography.Text type="secondary" style={{ fontSize: 11 }}>
                {item.value}
                {item.missingPermission ? (
                  <>
                    {' · '}
                    <Typography.Text type="warning" style={{ fontSize: 11 }}>
                      {t('adminShell.menuPermission.missingMapping')}
                    </Typography.Text>
                  </>
                ) : null}
              </Typography.Text>
            </div>
          </List.Item>
        )}
      />

      {selected ? (
        <div>
          <Typography.Title level={5} style={{ marginTop: 0 }}>
            {t(selected.labelKey)}
          </Typography.Title>
          {requirements.map((req) => {
            const group = resolvePermissionGroupSlugForPermissionKey(req.key);
            const holdingRoles = listRolesHoldingPermission(req.key, roles);
            return (
              <div key={req.key} style={{ marginBottom: 16 }}>
                <Typography.Text style={{ display: 'block', marginBottom: 4 }}>
                  {t('adminShell.menuPermission.permissionLabel')}{' '}
                  <Tag>
                    <code>{req.key}</code>
                  </Tag>
                </Typography.Text>
                <Typography.Text type="secondary" style={{ display: 'block', fontSize: 12 }}>
                  {resolvePermissionDisplayLabel(req.key, t)}
                </Typography.Text>
                <Typography.Text type="secondary" style={{ display: 'block', fontSize: 12 }}>
                  {t('adminShell.menuPermission.groupLabel')}{' '}
                  {t(`users.roleDrawer.groups.${group}`)}
                </Typography.Text>
                <Typography.Text
                  style={{ display: 'block', marginTop: 8, marginBottom: 4, fontSize: 12 }}
                >
                  {t('adminShell.menuPermission.rolesHolding')}
                </Typography.Text>
                {holdingRoles.length === 0 ? (
                  <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                    {rolesQuery.isError || (!loadRoles && roles.length === 0)
                      ? t('adminShell.menuPermission.rolesUnavailable')
                      : t('adminShell.menuPermission.rolesNone')}
                  </Typography.Text>
                ) : (
                  <div style={{ display: 'flex', flexWrap: 'wrap', gap: 4 }}>
                    {holdingRoles.map((r) => (
                      <Tag key={r.roleName}>{r.displayName}</Tag>
                    ))}
                  </div>
                )}
              </div>
            );
          })}
        </div>
      ) : (
        <Typography.Text type="secondary">
          {t('adminShell.menuPermission.explorerSelectHint')}
        </Typography.Text>
      )}
    </Drawer>
  );
}

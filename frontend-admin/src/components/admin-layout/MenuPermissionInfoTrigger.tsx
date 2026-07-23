'use client';

import { CheckSquareOutlined, BorderOutlined, LinkOutlined } from '@ant-design/icons';
import { Button, Popover, Typography } from 'antd';
import { useRouter } from 'next/navigation';
import React, { useMemo } from 'react';

import { resolvePermissionDisplayLabel } from '@/features/users/utils/permissionDisplayLabel';
import { getPermissionsAffectingMenu } from '@/features/users/utils/permissionMenuImpact';
import { usePermissions } from '@/hooks/usePermissions';
import { useI18n } from '@/i18n';
import {
  closeMenuPermissionPopover,
  openPermissionExplorer,
} from '@/shared/auth/menuPermissionInfoStore';
import { permissionImplied } from '@/shared/auth/permissionImplication';
import { resolvePermissionGroupSlugForPermissionKey } from '@/shared/auth/permissionGroupRegistry';

export type MenuPermissionInfoContentProps = {
  menuKey: string;
  menuLabel: string;
  onClose?: () => void;
};

/**
 * Popover body: which permissions gate a sidebar menu leaf.
 */
export function MenuPermissionInfoContent({
  menuKey,
  menuLabel,
  onClose,
}: MenuPermissionInfoContentProps) {
  const { t } = useI18n();
  const router = useRouter();
  const { userPermissions, isSuperAdmin } = usePermissions();

  const requirements = useMemo(() => getPermissionsAffectingMenu(menuKey), [menuKey]);
  const primary = requirements[0]?.key ?? null;
  const groupSlug = primary
    ? resolvePermissionGroupSlugForPermissionKey(primary)
    : null;

  const held = userPermissions;

  return (
    <div style={{ maxWidth: 340 }} data-testid="menu-permission-info">
      <Typography.Text strong style={{ display: 'block', marginBottom: 8 }}>
        {menuLabel}
      </Typography.Text>
      {primary ? (
        <Typography.Paragraph style={{ marginBottom: 8, fontSize: 12 }}>
          {t('adminShell.menuPermission.permissionLabel')}{' '}
          <code>{primary}</code>
          {groupSlug && groupSlug !== 'other' ? (
            <>
              <br />
              {t('adminShell.menuPermission.groupLabel')}{' '}
              <Typography.Text type="secondary">
                {t(`users.roleDrawer.groups.${groupSlug}`)}
              </Typography.Text>
            </>
          ) : null}
        </Typography.Paragraph>
      ) : (
        <Typography.Paragraph type="warning" style={{ marginBottom: 8, fontSize: 12 }}>
          {t('adminShell.menuPermission.missingMapping')}
        </Typography.Paragraph>
      )}
      <Typography.Text type="secondary" style={{ display: 'block', marginBottom: 6, fontSize: 12 }}>
        {t('adminShell.menuPermission.requiredIntro')}
      </Typography.Text>
      <ul style={{ margin: '0 0 12px', paddingLeft: 0, listStyle: 'none' }}>
        {requirements.length === 0 ? (
          <li>
            <Typography.Text type="secondary" style={{ fontSize: 12 }}>
              {t('adminShell.menuPermission.noRequirements')}
            </Typography.Text>
          </li>
        ) : (
          requirements.map((req) => {
            const allowed = isSuperAdmin || permissionImplied(req.key, held);
            const Icon = allowed ? CheckSquareOutlined : BorderOutlined;
            return (
              <li
                key={req.key}
                style={{
                  display: 'flex',
                  gap: 8,
                  alignItems: 'flex-start',
                  marginBottom: 4,
                  fontSize: 12,
                }}
              >
                <Icon
                  style={{ color: allowed ? '#52c41a' : 'rgba(0,0,0,0.35)', marginTop: 2 }}
                  aria-hidden
                />
                <span>
                  <code>{req.key}</code>
                  <Typography.Text type="secondary" style={{ display: 'block', fontSize: 11 }}>
                    {resolvePermissionDisplayLabel(req.key, t)}
                    {req.primary ? ` · ${t('adminShell.menuPermission.primaryGate')}` : ''}
                  </Typography.Text>
                </span>
              </li>
            );
          })
        )}
      </ul>
      <Button
        type="link"
        size="small"
        icon={<LinkOutlined />}
        style={{ paddingInline: 0 }}
        onClick={() => {
          onClose?.();
          closeMenuPermissionPopover();
          const q = new URLSearchParams();
          q.set('menu', menuKey);
          if (primary) q.set('permission', primary);
          router.push(`/admin/access/roles?${q.toString()}`);
        }}
      >
        {t('adminShell.menuPermission.editPermission')}
      </Button>
      <Button
        type="link"
        size="small"
        style={{ paddingInline: 0, marginLeft: 8 }}
        onClick={() => {
          onClose?.();
          closeMenuPermissionPopover();
          openPermissionExplorer(menuKey);
        }}
      >
        {t('adminShell.menuPermission.openExplorer')}
      </Button>
    </div>
  );
}

export type MenuPermissionInfoTriggerProps = {
  menuKey: string;
  menuLabel: string;
  /** Development: highlight leaves without a permission mapping. */
  missingPermission?: boolean;
  children: React.ReactNode;
};

/**
 * Wraps a sidebar leaf label with ℹ️ popover + Ctrl/Cmd+Click permission info.
 */
export function MenuPermissionInfoTrigger({
  menuKey,
  menuLabel,
  missingPermission = false,
  children,
}: MenuPermissionInfoTriggerProps) {
  const { t } = useI18n();
  const [open, setOpen] = React.useState(false);
  const openInfo = React.useCallback(() => setOpen(true), []);

  const highlightMissing =
    missingPermission && process.env.NODE_ENV === 'development';

  const linkedChildren = React.isValidElement<{
    onModifierClick?: (e: React.MouseEvent) => void;
  }>(children)
    ? React.cloneElement(children, {
        onModifierClick: (e: React.MouseEvent) => {
          children.props.onModifierClick?.(e);
          openInfo();
        },
      })
    : children;

  return (
    <span
      className={
        highlightMissing
          ? 'admin-sidebar-leaf-with-info admin-sidebar-leaf--missing-permission'
          : 'admin-sidebar-leaf-with-info'
      }
      style={{
        display: 'inline-flex',
        alignItems: 'center',
        gap: 4,
        maxWidth: '100%',
        outline: highlightMissing ? '1px dashed #faad14' : undefined,
        borderRadius: 4,
      }}
      title={
        highlightMissing ? t('adminShell.menuPermission.missingMappingDevHint') : undefined
      }
    >
      <span style={{ minWidth: 0, flex: 1 }}>{linkedChildren}</span>
      <Popover
        open={open}
        onOpenChange={setOpen}
        trigger="click"
        placement="rightTop"
        title={t('adminShell.menuPermission.popoverTitle')}
        content={
          <MenuPermissionInfoContent
            menuKey={menuKey}
            menuLabel={menuLabel}
            onClose={() => setOpen(false)}
          />
        }
      >
        <button
          type="button"
          className="admin-sidebar-menu-permission-info"
          aria-label={t('adminShell.menuPermission.infoAria', { menu: menuLabel })}
          onClick={(e) => {
            e.preventDefault();
            e.stopPropagation();
            setOpen(true);
          }}
          style={{
            border: 'none',
            background: 'transparent',
            padding: 0,
            margin: 0,
            cursor: 'pointer',
            lineHeight: 1,
            color: 'rgba(0,0,0,0.45)',
            flexShrink: 0,
            fontSize: 12,
          }}
        >
          ℹ️
        </button>
      </Popover>
    </span>
  );
}

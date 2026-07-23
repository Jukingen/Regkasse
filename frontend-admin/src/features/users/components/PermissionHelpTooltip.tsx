'use client';

import { QuestionCircleOutlined } from '@ant-design/icons';
import { Drawer, Tooltip, Typography } from 'antd';
import React, { useMemo, useState } from 'react';

import { resolvePermissionDisplayLabel } from '@/features/users/utils/permissionDisplayLabel';
import { getMenuItemsAffectedByPermission } from '@/features/users/utils/permissionMenuImpact';
import { useI18n } from '@/i18n';
import { USER_FACING_MISSING_TRANSLATION_LABEL } from '@/i18n/translationFallback';

type PermissionHelpContentProps = {
  permission: string;
  catalogDescription?: string | null;
  /** Dark tooltip styling vs light drawer panel. */
  variant?: 'tooltip' | 'panel';
};

function resolvePermissionDescription(
  permission: string,
  catalogDescription: string | null | undefined,
  t: (key: string, options?: Record<string, string | number>) => string
): string {
  const trimmed = catalogDescription?.trim();
  if (trimmed) return trimmed;

  const leaf = permission.replace(/[.-]/g, '_');
  const fromI18n = t(`users.roleDrawer.permissionDescriptions.${leaf}`);
  if (fromI18n !== USER_FACING_MISSING_TRANSLATION_LABEL) return fromI18n;

  return t('users.permissionsModal.helpDescriptionFallback', {
    label: resolvePermissionDisplayLabel(permission, t),
  });
}

export function PermissionHelpContent({
  permission,
  catalogDescription,
  variant = 'panel',
}: PermissionHelpContentProps) {
  const { t } = useI18n();
  const isTooltip = variant === 'tooltip';
  const muted = isTooltip ? 'rgba(255,255,255,0.65)' : 'rgba(0,0,0,0.45)';
  const body = isTooltip ? 'rgba(255,255,255,0.85)' : 'rgba(0,0,0,0.88)';
  const titleColor = isTooltip ? '#fff' : undefined;

  const description = useMemo(
    () => resolvePermissionDescription(permission, catalogDescription, t),
    [permission, catalogDescription, t]
  );

  const menuItems = useMemo(
    () => getMenuItemsAffectedByPermission(permission),
    [permission]
  );

  const label = resolvePermissionDisplayLabel(permission, t);

  return (
    <div style={{ maxWidth: isTooltip ? 280 : undefined }}>
      <Typography.Text strong style={{ display: 'block', marginBottom: 6, color: titleColor }}>
        {t('users.permissionsModal.helpTitle')}
      </Typography.Text>
      <Typography.Text type="secondary" style={{ display: 'block', fontSize: 11, marginBottom: 8 }}>
        {label} · <code style={{ fontSize: 11 }}>{permission}</code>
      </Typography.Text>
      <Typography.Paragraph style={{ marginBottom: 8, color: body, fontSize: 12 }}>
        {description}
      </Typography.Paragraph>
      {menuItems.length > 0 ? (
        <>
          <Typography.Text style={{ display: 'block', marginBottom: 4, color: muted, fontSize: 11 }}>
            {t('users.permissionsModal.helpAffectsMenus')}
          </Typography.Text>
          <ul style={{ margin: 0, paddingLeft: 16, fontSize: 12, color: body }}>
            {menuItems.slice(0, variant === 'panel' ? 20 : 8).map((item) => (
              <li key={item.path}>{t(item.labelKey)}</li>
            ))}
          </ul>
          {menuItems.length > (variant === 'panel' ? 20 : 8) ? (
            <Typography.Text style={{ fontSize: 11, color: muted }}>
              {t('users.permissionsModal.helpMoreMenus', {
                count: menuItems.length - (variant === 'panel' ? 20 : 8),
              })}
            </Typography.Text>
          ) : null}
        </>
      ) : (
        <Typography.Text style={{ fontSize: 11, color: muted }}>
          {t('users.permissionsModal.helpNoMenus')}
        </Typography.Text>
      )}
    </div>
  );
}

type PermissionHelpTooltipProps = {
  permission: string;
  catalogDescription?: string | null;
};

/**
 * Hover "?" → compact tooltip; click "?" → help drawer panel.
 */
export function PermissionHelpTooltip({
  permission,
  catalogDescription,
}: PermissionHelpTooltipProps) {
  const { t } = useI18n();
  const [panelOpen, setPanelOpen] = useState(false);

  return (
    <>
      <Tooltip
        title={
          <PermissionHelpContent
            permission={permission}
            catalogDescription={catalogDescription}
            variant="tooltip"
          />
        }
      >
        <Typography.Link
          aria-label={t('users.permissionsModal.helpTitle')}
          onClick={(e) => {
            e.preventDefault();
            e.stopPropagation();
            setPanelOpen(true);
          }}
          style={{ color: 'rgba(0,0,0,0.45)', fontSize: 13 }}
        >
          <QuestionCircleOutlined />
        </Typography.Link>
      </Tooltip>
      <Drawer
        title={t('users.permissionOnboarding.helpPanelTitle')}
        open={panelOpen}
        onClose={() => setPanelOpen(false)}
        width={360}
        destroyOnHidden
      >
        <PermissionHelpContent
          permission={permission}
          catalogDescription={catalogDescription}
          variant="panel"
        />
      </Drawer>
    </>
  );
}

type PermissionHoverHelpProps = {
  permission: string;
  catalogDescription?: string | null;
  children: React.ReactNode;
};

/** Wrap permission label: hover shows contextual help tooltip. */
export function PermissionHoverHelp({
  permission,
  catalogDescription,
  children,
}: PermissionHoverHelpProps) {
  return (
    <Tooltip
      title={
        <PermissionHelpContent
          permission={permission}
          catalogDescription={catalogDescription}
          variant="tooltip"
        />
      }
    >
      <span style={{ cursor: 'help' }}>{children}</span>
    </Tooltip>
  );
}

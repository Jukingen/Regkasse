'use client';

import { Tag, Tooltip } from 'antd';
import React, { useMemo } from 'react';

import type {
  PermissionGroupMenuChip,
  PermissionMenuImpactItem,
} from '@/features/users/utils/permissionMenuImpact';
import { useI18n } from '@/i18n';
import { resolveSidebarIconElement } from '@/shared/buildAdminSidebar';

const MAX_VISIBLE_TAGS = 3;

type MenuTagModel = {
  key: string;
  label: string;
  icon?: React.ReactNode;
};

function toTagModels(
  items: ReadonlyArray<PermissionMenuImpactItem | PermissionGroupMenuChip>,
  t: (key: string) => string
): MenuTagModel[] {
  return items.map((item) => ({
    key: 'path' in item ? item.path : item.key,
    label: t(item.labelKey),
    icon: resolveSidebarIconElement(item.icon),
  }));
}

export type PermissionMenuTagsProps = {
  items: ReadonlyArray<PermissionMenuImpactItem | PermissionGroupMenuChip>;
  /** When true, use stronger highlight styling. */
  highlighted?: boolean;
  maxVisible?: number;
  /** Compact tags for group headers. */
  size?: 'default' | 'small';
  /** Override default tooltip (joined labels). */
  tooltipTitle?: React.ReactNode;
};

/**
 * Renders related sidebar menu chips (icon + localized label).
 */
export function PermissionMenuTags({
  items,
  highlighted = false,
  maxVisible = MAX_VISIBLE_TAGS,
  size = 'default',
  tooltipTitle,
}: PermissionMenuTagsProps) {
  const { t } = useI18n();
  const models = useMemo(() => toTagModels(items, t), [items, t]);

  if (models.length === 0) {
    return (
      <span style={{ color: 'rgba(0,0,0,0.25)', fontSize: 12 }}>
        {t('users.roleDrawer.menuColumnNone')}
      </span>
    );
  }

  const visible = models.slice(0, maxVisible);
  const overflow = models.length - visible.length;
  const resolvedTooltip = tooltipTitle ?? models.map((m) => m.label).join(' · ');

  return (
    <Tooltip title={resolvedTooltip}>
      <span
        style={{
          display: 'inline-flex',
          flexWrap: 'wrap',
          gap: 4,
          alignItems: 'center',
          maxWidth: '100%',
        }}
      >
        {visible.map((menu) => (
          <Tag
            key={menu.key}
            icon={menu.icon}
            color={highlighted ? 'processing' : undefined}
            style={{
              margin: 0,
              fontSize: size === 'small' ? 11 : 12,
              maxWidth: 140,
              overflow: 'hidden',
              textOverflow: 'ellipsis',
              borderColor: highlighted ? undefined : 'rgba(0,0,0,0.15)',
              background: highlighted ? undefined : 'rgba(22,119,255,0.06)',
            }}
          >
            {menu.label}
          </Tag>
        ))}
        {overflow > 0 ? (
          <Tag style={{ margin: 0, fontSize: 11 }}>
            {t('users.permissionsModal.helpMoreMenus', { count: overflow })}
          </Tag>
        ) : null}
      </span>
    </Tooltip>
  );
}

'use client';

import { Tag, Tooltip } from 'antd';
import React from 'react';

import { useI18n } from '@/i18n';
import {
  findImplicationSources,
  isPermissionImpliedOnly,
} from '@/shared/auth/permissionImplications';

type ImpliedPermissionTagProps = {
  permission: string;
  /** Directly assigned / JWT permissions used as implication sources. */
  heldPermissions: Iterable<string>;
  /** Compact tag for dense checklists. */
  size?: 'small' | 'default';
};

/**
 * Shows when a permission is granted only via implication (e.g. user.view ← user.manage).
 */
export function ImpliedPermissionTag({
  permission,
  heldPermissions,
  size = 'small',
}: ImpliedPermissionTagProps) {
  const { t } = useI18n();

  if (!isPermissionImpliedOnly(permission, heldPermissions)) {
    return null;
  }

  const sources = findImplicationSources(permission, heldPermissions);
  if (!sources.length) return null;

  const label =
    sources.length === 1
      ? t('users.roleDrawer.impliedBy', { source: sources[0] })
      : t('users.roleDrawer.impliedByMany', { sources: sources.join(', ') });

  return (
    <Tooltip title={label}>
      <Tag color="purple" style={{ marginInlineStart: 6, fontSize: size === 'small' ? 11 : 12 }}>
        {t('users.roleDrawer.impliedTag')}
      </Tag>
    </Tooltip>
  );
}

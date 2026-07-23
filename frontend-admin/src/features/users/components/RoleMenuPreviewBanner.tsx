'use client';

import { Alert, Button } from 'antd';
import React, { useSyncExternalStore } from 'react';

import {
  getRoleMenuPreviewSession,
  stopRoleMenuPreview,
  subscribeRoleMenuPreview,
} from '@/features/users/utils/roleMenuPreviewSession';
import { useI18n } from '@/i18n';

/**
 * Banner while FA sidebar is filtered by a role menu preview session.
 */
export function RoleMenuPreviewBanner() {
  const { t } = useI18n();
  const session = useSyncExternalStore(
    subscribeRoleMenuPreview,
    getRoleMenuPreviewSession,
    () => null
  );

  if (!session) return null;

  return (
    <Alert
      type="warning"
      banner
      showIcon
      message={t('users.roleDrawer.menuPreview.activeBanner', { role: session.roleName })}
      action={
        <Button size="small" onClick={() => stopRoleMenuPreview()}>
          {t('users.roleDrawer.menuPreview.stopPreview')}
        </Button>
      }
    />
  );
}

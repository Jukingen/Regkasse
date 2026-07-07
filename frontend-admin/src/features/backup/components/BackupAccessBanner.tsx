'use client';

import React from 'react';
import { Alert, Tag } from 'antd';
import { useI18n } from '@/i18n';
import { useBackupManagementAccess } from '@/features/backup-management/hooks/useBackupManagementAccess';

export function BackupAccessBanner() {
    const { t } = useI18n();
    const access = useBackupManagementAccess();

    const roleTag = access.isSuperAdmin
        ? t('backupDr.management.role.superAdmin')
        : access.canManageBackup
          ? t('backupDr.management.role.tenantAdmin')
          : t('backupDr.management.role.readOnly');

    return (
        <Alert
            type={access.isReadOnly ? 'info' : 'success'}
            showIcon
            style={{ marginBottom: 8 }}
            title={
                <>
                    {t('backupDr.management.accessBanner', { role: roleTag })}{' '}
                    <Tag>{roleTag}</Tag>
                </>
            }
            description={
                access.isReadOnly
                    ? t('backupDr.management.accessReadOnlyHint')
                    : access.isSuperAdmin
                      ? t('backupDr.management.accessSuperAdminHint')
                      : t('backupDr.management.accessManageHint')
            }
        />
    );
}

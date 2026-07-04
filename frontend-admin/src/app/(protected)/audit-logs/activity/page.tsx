'use client';

import { Alert, Typography } from 'antd';

import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { AdminPageShell } from '@/components/admin-layout/AdminPageShell';
import { ActivityLog } from '@/features/audit/components/ActivityLog';
import { AuditLogsSubNav } from '@/features/audit/components/AuditLogsSubNav';
import { ADMIN_NAV_LABEL_KEYS, adminOverviewCrumb } from '@/shared/adminShellLabels';
import { usePermissions } from '@/shared/auth/usePermissions';
import { PERMISSIONS } from '@/shared/auth/permissions';
import { useI18n } from '@/i18n';

export default function AuditLogsActivityPage() {
    const { t } = useI18n();
    const { hasPermission } = usePermissions();
    const canView = hasPermission(PERMISSIONS.AUDIT_VIEW);

    if (!canView) {
        return (
            <Alert
                type="warning"
                showIcon
                title={t('adminShell.staffPerformance.noPermission')}
                style={{ margin: 24 }}
            />
        );
    }

    return (
        <AdminPageShell>
            <AdminPageHeader
                title={t('activity.title')}
                breadcrumbs={[
                    adminOverviewCrumb(t),
                    { title: t(ADMIN_NAV_LABEL_KEYS.auditLogs), href: '/audit-logs' },
                    { title: t('activity.title') },
                ]}
            >
                <Typography.Paragraph type="secondary" style={{ marginBottom: 0, maxWidth: 720 }}>
                    {t('activity.pageIntro')}
                </Typography.Paragraph>
            </AdminPageHeader>

            <AuditLogsSubNav />

            <ActivityLog />
        </AdminPageShell>
    );
}

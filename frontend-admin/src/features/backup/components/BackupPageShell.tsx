'use client';

import React from 'react';
import { Typography } from 'antd';
import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { adminOverviewCrumb } from '@/shared/adminShellLabels';
import { BACKUP_HUB_LANDING_PATH } from '@/shared/backupAreaRoutes';
import { useI18n } from '@/i18n';
import { BackupAccessBanner } from '@/features/backup/components/BackupAccessBanner';

export type BackupPageShellProps = {
    titleKey: string;
    sectionLabelKey: string;
    sectionHref: string;
    subtitleKey?: string;
    actions?: React.ReactNode;
    children: React.ReactNode;
    showAccessBanner?: boolean;
};

export function BackupPageShell({
    titleKey,
    sectionLabelKey,
    sectionHref,
    subtitleKey,
    actions,
    children,
    showAccessBanner = true,
}: BackupPageShellProps) {
    const { t } = useI18n();

    return (
        <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
            <AdminPageHeader
                title={t(titleKey)}
                breadcrumbs={[
                    adminOverviewCrumb(t),
                    { title: t('nav.backupDisasterRecovery'), href: BACKUP_HUB_LANDING_PATH },
                    { title: t(sectionLabelKey), href: sectionHref },
                ]}
                actions={actions}
            />
            {subtitleKey ? (
                <Typography.Paragraph type="secondary" style={{ marginBottom: 0 }}>
                    {t(subtitleKey)}
                </Typography.Paragraph>
            ) : null}
            {showAccessBanner ? <BackupAccessBanner /> : null}
            {children}
        </div>
    );
}

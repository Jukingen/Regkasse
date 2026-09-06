'use client';

import { Alert, Card, Space, Typography } from 'antd';
import Link from 'next/link';

import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { FiskalyStatisticsDashboard } from '@/features/fiskaly/FiskalyStatisticsDashboard';
import { TseActiveTenantTag } from '@/features/tse-shared/components/TseTenantContextUi';
import { useI18n } from '@/i18n';
import { buildPlatformAdminBreadcrumbs } from '@/shared/adminPlatformBreadcrumbs';
import { PERMISSIONS } from '@/shared/auth/permissions';
import { usePermissions } from '@/shared/auth/usePermissions';

export default function FiskalyStatisticsPage() {
  const { t } = useI18n();
  const { hasPermission } = usePermissions();
  const allowed = hasPermission(PERMISSIONS.FISKALY_HISTORY_VIEW);
  const canConfigure = hasPermission(PERMISSIONS.FISKALY_OPERATIONS_CONFIG);

  if (!allowed) {
    return <Alert type="error" showIcon title={t('tseFiskaly.forbidden')} />;
  }

  const breadcrumbs = canConfigure
    ? buildPlatformAdminBreadcrumbs(t, 'securityTse', [
        { title: t('tseFiskaly.title'), href: '/admin/tse/fiskaly' },
        { title: t('tseFiskaly.test.pageTitle'), href: '/admin/tse/fiskaly/test' },
        { title: t('tseFiskaly.statistics.pageTitle') },
      ])
    : [
        { title: t('nav.rksvOperationsOverview'), href: '/rksv' },
        { title: t('tseFiskaly.statistics.pageTitle') },
      ];

  return (
    <div>
      <AdminPageHeader
        title={t('tseFiskaly.statistics.pageTitle')}
        breadcrumbs={breadcrumbs}
        extra={<TseActiveTenantTag />}
      />
      <Typography.Paragraph type="secondary">{t('tseFiskaly.statistics.pageSubtitle')}</Typography.Paragraph>
      <Card size="small" style={{ marginBottom: 16 }}>
        <Space>
          {canConfigure ? (
            <Link href="/admin/tse/fiskaly/test">{t('tseFiskaly.test.openPage')}</Link>
          ) : null}
          <Link href="/admin/fiskaly/history">{t('tseFiskaly.history.openPage')}</Link>
          <Link href="/admin/fiskaly/errors">{t('tseFiskaly.errors.openPage')}</Link>
        </Space>
      </Card>
      <FiskalyStatisticsDashboard />
    </div>
  );
}

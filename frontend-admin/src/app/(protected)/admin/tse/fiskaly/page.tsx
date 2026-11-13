'use client';

import { useQuery } from '@tanstack/react-query';
import { Alert, Button, Card, Form, Select, Space, Tag, Typography } from 'antd';
import Link from 'next/link';

import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { getFiskalySettings } from '@/features/dashboard/api/fiskalyStatus';
import { FiskalyEnabledSwitch } from '@/features/settings/components/FiskalyEnabledSwitch';
import { TseStatusIndicator } from '@/features/rksv/components/TseStatusIndicator';
import { useI18n } from '@/i18n/I18nProvider';
import { buildPlatformAdminBreadcrumbs } from '@/shared/adminPlatformBreadcrumbs';
import { PERMISSIONS } from '@/shared/auth/permissions';
import { usePermissions } from '@/shared/auth/usePermissions';

export default function FiskalySettingsPage() {
  const { t } = useI18n();
  const { hasPermission } = usePermissions();
  const allowed = hasPermission(PERMISSIONS.SYSTEM_CRITICAL);

  const query = useQuery({
    queryKey: ['admin', 'fiskaly', 'settings'],
    queryFn: ({ signal }) => getFiskalySettings(signal),
    enabled: allowed,
    staleTime: 15_000,
  });

  if (!allowed) {
    return <Alert type="error" showIcon title={t('tseFiskaly.forbidden')} />;
  }

  return (
    <div>
      <AdminPageHeader
        title={t('tseFiskaly.title')}
        breadcrumbs={buildPlatformAdminBreadcrumbs(t, 'securityTse', { title: t('tseFiskaly.title') })}
      />
      <Typography.Paragraph type="secondary">{t('tseFiskaly.subtitle')}</Typography.Paragraph>
      <div style={{ marginBottom: 16 }}>
        <TseStatusIndicator />
      </div>
      <Card loading={query.isLoading}>
        <Form layout="vertical">
          <FiskalyEnabledSwitch />
          <Form.Item label={t('tseFiskaly.environmentLabel')}>
            <Select
              value={query.data?.environment ?? 'TEST'}
              disabled
              options={[
                { value: 'TEST', label: t('tseFiskaly.envTest') },
                { value: 'LIVE', label: t('tseFiskaly.envLive') },
              ]}
              style={{ maxWidth: 240 }}
            />
          </Form.Item>
          <Form.Item label={t('tseFiskaly.statusLabel')}>
            <Space>
              <Tag color={query.data?.isConfigured ? 'green' : 'red'}>
                {query.data?.isConfigured
                  ? t('tseFiskaly.configured')
                  : t('tseFiskaly.notConfigured')}
              </Tag>
              <Typography.Text type="secondary">
                {t('tseFiskaly.source', { source: query.data?.source ?? 'config' })}
              </Typography.Text>
            </Space>
          </Form.Item>
        </Form>
        <Alert type="info" showIcon title={t('tseFiskaly.secretsHint')} />
        <Space style={{ marginTop: 16 }}>
          <Link href="/admin/tse/fiskaly/setup">
            <Button type="primary">{t('tseFiskaly.setup.openWizard')}</Button>
          </Link>
          <Link href="/admin/tse/fiskaly/test">
            <Button>{t('tseFiskaly.test.openPage')}</Button>
          </Link>
        </Space>
      </Card>
    </div>
  );
}

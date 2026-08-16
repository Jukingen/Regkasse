'use client';

import { Button, Descriptions, Drawer, Spin, Tag, Typography } from 'antd';
import { useQuery } from '@tanstack/react-query';

import { getApiLicenseInfo } from '@/api/generated/license/license';
import type { LicenseInfo } from '@/api/generated/model';
import { formatLicenseValidUntil } from '@/features/license/utils/licenseValidUntil';
import { useI18n } from '@/i18n';

type UnifiedLicenseDetailDrawerProps = {
  licenseKey: string | null;
  onClose: () => void;
};

function kindTagColor(kind: string | null | undefined): string {
  if (kind === 'system') return 'geekblue';
  if (kind === 'tenant') return 'purple';
  return 'default';
}

export function UnifiedLicenseDetailDrawer({ licenseKey, onClose }: UnifiedLicenseDetailDrawerProps) {
  const { t } = useI18n();
  const open = Boolean(licenseKey);

  const infoQuery = useQuery({
    queryKey: ['license', 'info', licenseKey],
    queryFn: () => getApiLicenseInfo({ licenseKey: licenseKey ?? undefined }),
    enabled: open,
  });

  const info = infoQuery.data as LicenseInfo | undefined;

  return (
    <Drawer
      title={t('license.management.detailsTitle')}
      open={open}
      onClose={onClose}
      destroyOnHidden
      width={480}
    >
      {infoQuery.isFetching ? (
        <Spin />
      ) : info ? (
        <Descriptions column={1} size="small">
          <Descriptions.Item label={t('license.management.kind')}>
            <Tag color={kindTagColor(info.licenseKind)}>{info.licenseKind ?? '—'}</Tag>
          </Descriptions.Item>
          <Descriptions.Item label={t('license.mandant.licenseKey')}>
            <Typography.Text code copyable={{ text: info.canonicalLicenseKey || info.licenseKey || '' }}>
              {info.canonicalLicenseKey || info.licenseKey || '—'}
            </Typography.Text>
          </Descriptions.Item>
          <Descriptions.Item label={t('license.management.customer')}>
            {info.customerName?.trim() || info.tenantSlug || '—'}
          </Descriptions.Item>
          <Descriptions.Item label={t('license.extendModal.validUntilLabel')}>
            {formatLicenseValidUntil(info.validUntilUtc)}
          </Descriptions.Item>
          <Descriptions.Item label={t('license.mandant.status')}>
            {info.status ?? (info.isValid ? t('license.mandant.valid') : t('license.mandant.expired'))}
          </Descriptions.Item>
          <Descriptions.Item label={t('license.management.exists')}>
            {info.exists ? t('common.buttons.yes') : t('common.buttons.no')}
          </Descriptions.Item>
        </Descriptions>
      ) : (
        <Typography.Text type="secondary">{t('license.management.detailsEmpty')}</Typography.Text>
      )}
      <Button style={{ marginTop: 16 }} onClick={onClose}>
        {t('common.buttons.close')}
      </Button>
    </Drawer>
  );
}

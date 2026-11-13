'use client';

import { useQuery } from '@tanstack/react-query';
import { Alert, Card, Descriptions, Space, Tag } from 'antd';

import {
  getTenantLicensePublicStatus,
  licenseQueryKeys,
} from '@/api/manual/adminLicense';
import {
  isSystemActiveTenantLocked,
  resolveLicenseLayerLabelKey,
} from '@/features/license/utils/licenseLayerStatus';
import { formatLicenseValidUntil } from '@/features/license/utils/licenseValidUntil';
import { useI18n } from '@/i18n';

const LAYER_LABEL_KEYS = {
  active: 'license.statusBadge.labels.active',
  grace: 'license.statusBadge.labels.grace',
  locked: 'license.statusBadge.labels.locked',
  expired: 'license.statusBadge.labels.expired',
} as const;

const LAYER_TAG_COLOR = {
  active: 'green',
  grace: 'gold',
  locked: 'red',
  expired: 'red',
} as const;

type UnifiedLicenseLayersCardProps = {
  tenantId?: string | null;
};

export function UnifiedLicenseLayersCard({ tenantId }: UnifiedLicenseLayersCardProps) {
  const { t } = useI18n();
  const query = useQuery({
    queryKey: [...licenseQueryKeys.publicStatus, tenantId ?? 'current'],
    queryFn: () => getTenantLicensePublicStatus(tenantId),
  });

  const data = query.data;
  const systemActiveTenantLocked = isSystemActiveTenantLocked({
    systemLicense: data?.systemLicense,
    tenantLicense: data?.tenantLicense,
  });
  const systemLabelKey = resolveLicenseLayerLabelKey(data?.systemLicense);
  const tenantLabelKey = resolveLicenseLayerLabelKey(data?.tenantLicense);

  return (
    <Card variant="borderless" title={t('license.management.layersTitle')} loading={query.isLoading}>
      <Space orientation="vertical" size="middle" style={{ width: '100%' }}>
        <Alert
          type="info"
          showIcon
          title={t('license.management.systemDoesNotUnlockTenant')}
        />
        {systemActiveTenantLocked ? (
          <Alert
            type="error"
            showIcon
            title={t('license.management.systemActiveTenantLocked')}
            description={t('license.management.systemDoesNotUnlockTenant')}
          />
        ) : null}
        <Descriptions bordered column={1} size="small">
          <Descriptions.Item label={t('license.management.systemLayer')}>
            <Space wrap>
              <Tag color={LAYER_TAG_COLOR[systemLabelKey]}>
                {t(LAYER_LABEL_KEYS[systemLabelKey])}
              </Tag>
              {formatLicenseValidUntil(data?.systemLicense?.validUntil)}
            </Space>
          </Descriptions.Item>
          <Descriptions.Item label={t('license.management.tenantLayer')}>
            <Space wrap>
              <Tag color={LAYER_TAG_COLOR[tenantLabelKey]}>
                {t(LAYER_LABEL_KEYS[tenantLabelKey])}
              </Tag>
              {formatLicenseValidUntil(data?.tenantLicense?.validUntil)}
            </Space>
          </Descriptions.Item>
        </Descriptions>
      </Space>
    </Card>
  );
}

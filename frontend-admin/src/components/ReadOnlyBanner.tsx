'use client';

import { Alert } from 'antd';

import { useLicense } from '@/features/license/hooks/useLicense';
import { useI18n } from '@/i18n';

export function ReadOnlyBanner() {
  const { t } = useI18n();
  const { licenseStatus } = useLicense();

  if (!licenseStatus?.isReadOnly) {
    return null;
  }

  return (
    <Alert
      type="warning"
      title={t('license.banner.readOnly.title')}
      description={t('license.banner.readOnly.message')}
      showIcon
      closable
      banner
      role="status"
      style={{ marginBottom: 12 }}
    />
  );
}

'use client';

import { Alert } from 'antd';

import { useMaintenanceMode } from '@/hooks/useMaintenanceMode';
import { useI18n } from '@/i18n';

/**
 * Shell alert while platform maintenance limited mode is active (read-mostly access).
 */
export function MaintenanceLimitedModeAlert() {
  const { t } = useI18n();
  const { isMaintenanceMode } = useMaintenanceMode();

  if (!isMaintenanceMode) {
    return null;
  }

  return (
    <Alert
      type="warning"
      title={t('maintenance.limitedMode.title')}
      description={t('maintenance.limitedMode.description')}
      showIcon
      banner
      role="status"
      style={{ marginBottom: 12 }}
      className="mb-4"
    />
  );
}

'use client';

import { Alert, Button } from 'antd';

import { useMaintenanceMode } from '@/hooks/useMaintenanceMode';
import { useMaintenanceNotifications } from '@/hooks/useMaintenanceNotifications';
import { useNotify } from '@/hooks/useNotify';
import { usePermissions } from '@/hooks/usePermissions';
import { useI18n } from '@/i18n';

/**
 * Shell alert while platform maintenance limited mode is active (read-mostly access).
 * Super Admin sees a non-blocking warning with an end-maintenance action (API writes already bypassed).
 * Skipped when MaintenanceBanner already shows a Super Admin force-window banner (avoids duplicate UI).
 */
export function MaintenanceLimitedModeAlert() {
  const { t } = useI18n();
  const notify = useNotify();
  const { isSuperAdmin } = usePermissions();
  const { isMaintenanceMode, status, disableMaintenance, isDisabling } = useMaintenanceMode();
  const { activeNotification, isForceDisplay } = useMaintenanceNotifications();

  if (!isMaintenanceMode) {
    return null;
  }

  // Force-window Super Admin banner is owned by MaintenanceBanner.
  if (isSuperAdmin && activeNotification && isForceDisplay) {
    return null;
  }

  if (isSuperAdmin) {
    return (
      <Alert
        type="warning"
        title={t('maintenance.superAdmin.bannerTitle')}
        description={
          status?.message?.trim()
            ? status.message
            : t('maintenance.superAdmin.bannerDescription')
        }
        showIcon
        banner
        role="status"
        style={{ marginBottom: 12 }}
        className="mb-4"
        action={
          <Button
            type="primary"
            size="small"
            loading={isDisabling}
            onClick={() => {
              void disableMaintenance()
                .then(() => {
                  notify.successKey('maintenance.manage.ended');
                })
                .catch((err: unknown) => {
                  notify.apiError(err, {
                    logContext: 'MaintenanceLimitedModeAlert.disableMaintenance',
                    fallbackKey: 'maintenance.manage.actionFailed',
                  });
                });
            }}
          >
            {t('maintenance.manage.endAction')}
          </Button>
        }
      />
    );
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

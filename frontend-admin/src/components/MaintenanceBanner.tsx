'use client';

import { ClockCircleOutlined, WarningOutlined } from '@ant-design/icons';
import { Alert, Button, Modal, Space, Statistic, Typography } from 'antd';
import { useMemo } from 'react';

import { useMaintenanceNotifications } from '@/hooks/useMaintenanceNotifications';
import { useMaintenanceMode } from '@/hooks/useMaintenanceMode';
import { useNotify } from '@/hooks/useNotify';
import { usePermissions } from '@/hooks/usePermissions';
import { useI18n } from '@/i18n';

const { Countdown } = Statistic;

/**
 * Shell banner / force modal for published platform maintenance notices.
 * Mounted in the protected admin layout (non-blocking when dismissible).
 * Super Admin never gets the blocking force modal — backend already bypasses write locks;
 * they see a warning banner with an optional "end maintenance" action instead.
 */
export function MaintenanceBanner() {
  const { t, formatLocale } = useI18n();
  const notify = useNotify();
  const { isSuperAdmin } = usePermissions();
  const { disableMaintenance, isDisabling } = useMaintenanceMode();
  const {
    activeNotification,
    dismissNotification,
    isDismissing,
    isForceDisplay,
  } = useMaintenanceNotifications();

  const formatOpts = useMemo(
    () => ({ dateStyle: 'short' as const, timeStyle: 'short' as const }),
    [],
  );

  if (!activeNotification) {
    return null;
  }

  const startMs = new Date(activeNotification.scheduledStartAt).getTime();
  const endMs = new Date(activeNotification.scheduledEndAt).getTime();
  const forceDisplay = isForceDisplay;
  const canDismiss = activeNotification.canDismiss && !forceDisplay;

  const startLabel = new Date(activeNotification.scheduledStartAt).toLocaleString(
    formatLocale,
    formatOpts,
  );
  const endLabel = new Date(activeNotification.scheduledEndAt).toLocaleString(
    formatLocale,
    formatOpts,
  );

  const scheduleBlock = (
    <div
      style={{
        background: 'var(--ant-color-fill-alter)',
        padding: 12,
        borderRadius: 8,
      }}
    >
      <Space wrap>
        <ClockCircleOutlined />
        <span>
          {t('maintenance.starts')}: {startLabel}
        </span>
        <span aria-hidden>•</span>
        <span>
          {t('maintenance.ends')}: {endLabel}
        </span>
      </Space>
      {startMs > Date.now() ? (
        <div style={{ marginTop: 8 }}>
          <Countdown
            title={t('maintenance.countdown')}
            value={startMs}
            format="D [d] HH:mm:ss"
          />
        </div>
      ) : endMs > Date.now() ? (
        <div style={{ marginTop: 8 }}>
          <Countdown
            title={t('maintenance.countdownEnds')}
            value={endMs}
            format="HH:mm:ss"
          />
        </div>
      ) : null}
    </div>
  );

  const handleEndMaintenance = () => {
    void disableMaintenance()
      .then(() => {
        notify.successKey('maintenance.manage.ended');
      })
      .catch((err: unknown) => {
        notify.apiError(err, {
          logContext: 'MaintenanceBanner.disableMaintenance',
          fallbackKey: 'maintenance.manage.actionFailed',
        });
      });
  };

  // Super Admin bypass: never block the shell with a force modal.
  if (forceDisplay && isSuperAdmin) {
    return (
      <Alert
        type="warning"
        banner
        showIcon
        style={{ marginBottom: 12 }}
        title={
          <Space>
            <WarningOutlined />
            <span>{t('maintenance.superAdmin.bannerTitle')}</span>
          </Space>
        }
        description={
          <Space orientation="vertical" size="middle" style={{ width: '100%' }}>
            <Typography.Paragraph style={{ marginBottom: 0 }}>
              {activeNotification.message || t('maintenance.superAdmin.bannerDescription')}
            </Typography.Paragraph>
            {scheduleBlock}
            <Space wrap>
              <Button type="primary" size="small" loading={isDisabling} onClick={handleEndMaintenance}>
                {t('maintenance.manage.endAction')}
              </Button>
              <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                {t('maintenance.scheduledBySuperAdmin')}
              </Typography.Text>
            </Space>
          </Space>
        }
      />
    );
  }

  if (forceDisplay) {
    return (
      <Modal
        title={
          <Space>
            <WarningOutlined style={{ color: 'var(--ant-color-warning)' }} />
            <Typography.Text strong>{t('maintenance.modalTitle')}</Typography.Text>
          </Space>
        }
        open
        closable={false}
        mask={{ closable: false }}
        keyboard={false}
        footer={null}
        styles={{ wrapper: { zIndex: 1200 } }}
      >
        <Alert
          type="warning"
          showIcon
          title={activeNotification.title}
          description={
            <Space orientation="vertical" size="middle" style={{ width: '100%' }}>
              <Typography.Paragraph style={{ marginBottom: 0 }}>
                {activeNotification.message}
              </Typography.Paragraph>
              {scheduleBlock}
              <Typography.Text type="secondary">{t('maintenance.forceNotice')}</Typography.Text>
            </Space>
          }
        />
        <Typography.Text
          type="secondary"
          style={{ display: 'block', marginTop: 16, textAlign: 'center', fontSize: 12 }}
        >
          {t('maintenance.scheduledBySuperAdmin')}
        </Typography.Text>
      </Modal>
    );
  }

  return (
    <Alert
      type="info"
      banner
      showIcon
      style={{ marginBottom: 12 }}
      closable={canDismiss}
      onClose={() => {
        if (canDismiss) {
          void dismissNotification(activeNotification.id);
        }
      }}
      title={
        <Space>
          <ClockCircleOutlined />
          <span>{activeNotification.title}</span>
        </Space>
      }
      description={
        <Space
          style={{ width: '100%', justifyContent: 'space-between' }}
          wrap
          size="middle"
        >
          <span>{activeNotification.message}</span>
          <Space>
            <Typography.Text type="secondary">
              {t('maintenance.starts')}: {startLabel}
            </Typography.Text>
            {canDismiss ? (
              <Button
                type="text"
                size="small"
                loading={isDismissing}
                onClick={() => void dismissNotification(activeNotification.id)}
              >
                {t('maintenance.dismiss')}
              </Button>
            ) : null}
          </Space>
        </Space>
      }
    />
  );
}

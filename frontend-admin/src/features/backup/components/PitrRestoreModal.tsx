'use client';

import {
  CalendarOutlined,
  CheckCircleOutlined,
  ClockCircleOutlined,
  WarningOutlined,
} from '@ant-design/icons';
import {
  Alert,
  Button,
  Col,
  DatePicker,
  Modal,
  Row,
  Space,
  Spin,
  Statistic,
  Tag,
  Timeline,
  Typography,
} from 'antd';
import dayjs, { type Dayjs } from 'dayjs';
import { useCallback, useEffect, useState } from 'react';

import {
  usePitrAvailability,
  useValidatePitrRestorePoint,
} from '@/features/backup/hooks/useBackupPitr';
import type { RestorePointValidationResult } from '@/features/backup/logic/backupPitrApi';
import { buildPitrDateTimeConstraints } from '@/features/backup/logic/pitrDateTimeConstraints';
import { useAntdApp } from '@/hooks/useAntdApp';
import { useI18n } from '@/i18n';
import { formatDateTime } from '@/i18n/formatting';
import { normalizeApiError } from '@/shared/errors/normalizedApiError';

export type PitrRestorePayload = {
  targetTime: Date;
  validation: RestorePointValidationResult;
};

export interface PitrRestoreModalProps {
  open: boolean;
  onClose: () => void;
  onRestore: (payload: PitrRestorePayload) => void | Promise<void>;
  restoreSubmitting?: boolean;
}

function formatPitrDateTime(iso: string | null | undefined, formatLocale: string): string {
  if (!iso) return '—';
  return formatDateTime(iso, formatLocale);
}

const MAX_SUPPORTED_POINTS_IN_TIMELINE = 6;

export function PitrRestoreModal({
  open,
  onClose,
  onRestore,
  restoreSubmitting = false,
}: PitrRestoreModalProps) {
  const { modal } = useAntdApp();

  const { t, formatLocale } = useI18n();
  const [selectedTime, setSelectedTime] = useState<Dayjs | null>(null);
  const [validation, setValidation] = useState<RestorePointValidationResult | null>(null);

  const availabilityQuery = usePitrAvailability(open);
  const validateMutation = useValidatePitrRestorePoint();

  const availability = availabilityQuery.data;
  const loadingAvailability = availabilityQuery.isLoading || availabilityQuery.isFetching;
  const validating = validateMutation.isPending;

  useEffect(() => {
    if (!open) {
      setSelectedTime(null);
      setValidation(null);
    }
  }, [open]);

  const validateTime = useCallback(
    async (time: Dayjs | null) => {
      setSelectedTime(time);
      setValidation(null);
      if (!time) return;

      try {
        const result = await validateMutation.mutateAsync({
          targetTimeUtc: time.toISOString(),
        });
        setValidation(result);
      } catch {
        setValidation({
          isValid: false,
          message: t('backupDr.pitr.validateFailed'),
          baseBackupId: null,
          baseBackupTimeUtc: null,
          targetTimeUtc: time.toISOString(),
          estimatedDataLossSeconds: null,
          recoveryMethod: null,
        });
      }
    },
    [validateMutation, t]
  );

  const handleRestore = () => {
    if (!validation?.isValid || !selectedTime) return;

    modal.confirm({
      title: t('backupDr.pitr.confirmTitle'),
      content: (
        <div>
          <Typography.Paragraph>{t('backupDr.pitr.confirmIntro')}</Typography.Paragraph>
          <Typography.Paragraph strong>
            {selectedTime.format('DD.MM.YYYY HH:mm:ss')}
          </Typography.Paragraph>
          <Typography.Paragraph>{t('backupDr.pitr.confirmIrreversible')}</Typography.Paragraph>
          <Typography.Paragraph type="danger">
            {t('backupDr.pitr.confirmSecondApproval')}
          </Typography.Paragraph>
        </div>
      ),
      okText: t('backupDr.pitr.confirmOk'),
      cancelText: t('backupDr.pitr.cancel'),
      okButtonProps: { danger: true },
      onOk: () =>
        onRestore({
          targetTime: selectedTime.toDate(),
          validation,
        }),
    });
  };

  const { disabledDate, disabledTime } = buildPitrDateTimeConstraints(
    availability?.earliestRestorePointUtc,
    availability?.latestRestorePointUtc
  );

  const supportedPoints = availability?.supportedTimePointsUtc ?? [];
  const visibleSupportedPoints = supportedPoints.slice(0, MAX_SUPPORTED_POINTS_IN_TIMELINE);
  const hiddenSupportedCount = Math.max(0, supportedPoints.length - visibleSupportedPoints.length);

  const tenantError =
    availabilityQuery.isError &&
    normalizeApiError(availabilityQuery.error).code === 'TENANT_REQUIRED';

  const recoveryMethodLabel = (method: string | null | undefined) => {
    if (method === 'PITR') return t('backupDr.pitr.recoveryMethodPitr');
    if (method === 'FullBackupOnly') return t('backupDr.pitr.recoveryMethodFullBackup');
    return '—';
  };

  return (
    <Modal
      title={
        <Space>
          <CalendarOutlined />
          <span>{t('backupDr.pitr.modalTitle')}</span>
          <Tag color="orange">{t('backupDr.pitr.highRiskTag')}</Tag>
        </Space>
      }
      open={open}
      onCancel={onClose}
      width={700}
      destroyOnHidden
      footer={[
        <Button key="cancel" onClick={onClose}>
          {t('backupDr.pitr.cancel')}
        </Button>,
        <Button
          key="restore"
          type="primary"
          danger
          onClick={handleRestore}
          disabled={!validation?.isValid || validating || tenantError || restoreSubmitting}
          loading={validating || restoreSubmitting}
        >
          {t('backupDr.pitr.restore')}
        </Button>,
      ]}
    >
      <Alert
        title={t('backupDr.pitr.alertTitle')}
        description={t('backupDr.pitr.alertDescription')}
        type="warning"
        showIcon
        style={{ marginBottom: 16 }}
      />

      {tenantError ? (
        <Alert
          title={t('backupDr.pitr.tenantRequiredTitle')}
          description={t('backupDr.pitr.tenantRequiredDescription')}
          type="error"
          showIcon
        />
      ) : loadingAvailability && !availability ? (
        <div style={{ textAlign: 'center', padding: 24 }}>
          <Spin />
        </div>
      ) : (
        <>
          {availability?.message ? (
            <Alert
              type="info"
              showIcon
              title={t('backupDr.pitr.scopeNoteTitle')}
              description={availability.message}
              style={{ marginBottom: 16 }}
            />
          ) : null}

          <div
            style={{
              marginBottom: 24,
              padding: 16,
              background: 'var(--ant-color-fill-quaternary)',
            }}
          >
            <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: 8 }}>
              <span>
                <ClockCircleOutlined /> {t('backupDr.pitr.availablePointsTitle')}
              </span>
              {availability?.walArchivingEnabled ? (
                <Tag color="green">{t('backupDr.pitr.walArchivingActive')}</Tag>
              ) : null}
            </div>
            <Timeline>
              <Timeline.Item color="green">
                <strong>{t('backupDr.pitr.earliestPoint')}:</strong>{' '}
                {formatPitrDateTime(availability?.earliestRestorePointUtc ?? null, formatLocale)}
              </Timeline.Item>
              <Timeline.Item color="blue">
                <strong>{t('backupDr.pitr.latestPoint')}:</strong>{' '}
                {formatPitrDateTime(availability?.latestRestorePointUtc ?? null, formatLocale)}
              </Timeline.Item>
              {availability?.walArchiveLagMinutes != null ? (
                <Timeline.Item color="orange">
                  <strong>{t('backupDr.pitr.walLag')}:</strong>{' '}
                  {t('backupDr.pitr.walLagMinutes', {
                    minutes: String(availability.walArchiveLagMinutes),
                  })}
                </Timeline.Item>
              ) : null}
              {visibleSupportedPoints.map((pointUtc) => (
                <Timeline.Item key={pointUtc} color="gray">
                  {formatPitrDateTime(pointUtc, formatLocale)}
                </Timeline.Item>
              ))}
              {hiddenSupportedCount > 0 ? (
                <Timeline.Item color="gray">
                  {t('backupDr.pitr.moreSupportedPoints', {
                    count: String(hiddenSupportedCount),
                  })}
                </Timeline.Item>
              ) : null}
            </Timeline>
          </div>

          <div style={{ marginBottom: 24 }}>
            <Typography.Text strong style={{ display: 'block', marginBottom: 8 }}>
              {t('backupDr.pitr.pickTimeTitle')}
            </Typography.Text>
            <DatePicker
              showTime={{ format: 'HH:mm:ss' }}
              format="DD.MM.YYYY HH:mm:ss"
              value={selectedTime}
              onChange={validateTime}
              disabledDate={disabledDate}
              disabledTime={disabledTime}
              showNow={false}
              disabled={!availability?.earliestRestorePointUtc}
              style={{ width: '100%' }}
              placeholder={t('backupDr.pitr.pickTimePlaceholder')}
            />
          </div>

          {validating ? (
            <div style={{ textAlign: 'center', padding: 12 }}>
              <Spin size="small" />
            </div>
          ) : null}

          {validation ? (
            <div
              style={{
                padding: 16,
                borderRadius: 8,
                background: validation.isValid
                  ? 'var(--ant-color-success-bg)'
                  : 'var(--ant-color-error-bg)',
              }}
            >
              {validation.isValid ? (
                <>
                  <div style={{ display: 'flex', alignItems: 'center', marginBottom: 12 }}>
                    <CheckCircleOutlined
                      style={{ color: 'var(--ant-color-success)', marginRight: 8 }}
                    />
                    <Typography.Text strong>{t('backupDr.pitr.validationOkTitle')}</Typography.Text>
                  </div>
                  <Row gutter={16}>
                    <Col span={12}>
                      <Statistic
                        title={t('backupDr.pitr.baseBackup')}
                        value={formatPitrDateTime(validation.baseBackupTimeUtc, formatLocale)}
                        styles={{ content: { fontSize: 14 } }}
                      />
                    </Col>
                    <Col span={12}>
                      <Statistic
                        title={t('backupDr.pitr.targetTime')}
                        value={formatPitrDateTime(validation.targetTimeUtc, formatLocale)}
                        styles={{ content: { fontSize: 14 } }}
                      />
                    </Col>
                  </Row>
                  <div style={{ marginTop: 12 }}>
                    {validation.estimatedDataLossSeconds != null ? (
                      <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                        {t('backupDr.pitr.estimatedDataLoss', {
                          seconds: String(validation.estimatedDataLossSeconds),
                        })}
                      </Typography.Text>
                    ) : null}
                    {validation.recoveryMethod ? (
                      <div style={{ marginTop: 8 }}>
                        <Tag>{recoveryMethodLabel(validation.recoveryMethod)}</Tag>
                      </div>
                    ) : null}
                  </div>
                </>
              ) : (
                <>
                  <div style={{ display: 'flex', alignItems: 'center', marginBottom: 12 }}>
                    <WarningOutlined style={{ color: 'var(--ant-color-error)', marginRight: 8 }} />
                    <Typography.Text strong>
                      {t('backupDr.pitr.validationFailedTitle')}
                    </Typography.Text>
                  </div>
                  <Typography.Paragraph style={{ marginBottom: 0 }}>
                    {validation.message ?? t('backupDr.pitr.validationFailedGeneric')}
                  </Typography.Paragraph>
                </>
              )}
            </div>
          ) : null}

          {!availability?.walArchivingEnabled ? (
            <Alert
              title={t('backupDr.pitr.walDisabledTitle')}
              description={t('backupDr.pitr.walDisabledDescription')}
              type="info"
              showIcon
              style={{ marginTop: 16 }}
            />
          ) : null}
        </>
      )}
    </Modal>
  );
}

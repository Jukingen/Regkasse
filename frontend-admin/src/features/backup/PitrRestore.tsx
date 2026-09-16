'use client';

import {
  CalendarOutlined,
  CheckCircleOutlined,
  WarningOutlined,
} from '@ant-design/icons';
import {
  Alert,
  Button,
  Card,
  Col,
  DatePicker,
  Descriptions,
  Row,
  Space,
  Spin,
  Statistic,
  Tag,
  Typography,
} from 'antd';
import { type Dayjs } from 'dayjs';
import { useState } from 'react';

import { BackupChain } from '@/features/backup/BackupChain';
import { PitrRestoreWorkflow } from '@/features/backup/components/PitrRestoreWorkflow';
import { useBackupPermissions } from '@/features/backup/hooks/useBackupPermissions';
import {
  useBackupChain,
  usePitrAvailability,
  usePitrDryRun,
  usePitrPreRestoreValidate,
  useWalArchiveStatus,
} from '@/features/backup/hooks/useBackupPitr';
import { buildPitrDateTimeConstraints } from '@/features/backup/logic/pitrDateTimeConstraints';
import type { PitrPreRestoreValidation } from '@/features/backup/logic/backupPitrApi';
import { useAntdApp } from '@/hooks/useAntdApp';
import { useNotify } from '@/hooks/useNotify';
import { useI18n } from '@/i18n';
import { formatDateTime } from '@/i18n/formatting';

export function PitrRestore() {
  const { t, formatLocale } = useI18n();
  const notify = useNotify();
  const { modal } = useAntdApp();
  const { canRestore, isSuperAdmin } = useBackupPermissions();
  const [selectedTime, setSelectedTime] = useState<Dayjs | null>(null);
  const [report, setReport] = useState<PitrPreRestoreValidation | null>(null);

  const availabilityQuery = usePitrAvailability(true);
  const walQuery = useWalArchiveStatus(true);
  const chainQuery = useBackupChain(true);
  const preValidate = usePitrPreRestoreValidate();
  const dryRun = usePitrDryRun();

  const availability = availabilityQuery.data;
  const wal = walQuery.data;
  const { disabledDate, disabledTime } = buildPitrDateTimeConstraints(
    availability?.earliestRestorePointUtc,
    availability?.latestRestorePointUtc
  );

  const onValidate = async (time: Dayjs | null) => {
    setSelectedTime(time);
    setReport(null);
    if (!time) return;
    try {
      const result = await preValidate.mutateAsync({ targetTimeUtc: time.toISOString() });
      setReport(result);
    } catch (err) {
      notify.apiError(err, {
        logContext: 'PitrRestore.preValidate',
        fallbackKey: 'backupDr.pitr.validateFailed',
      });
    }
  };

  const onDryRun = () => {
    if (!selectedTime || !report) return;
    modal.confirm({
      title: t('backupDr.pitrPage.dryRunConfirmTitle'),
      content: t('backupDr.pitrPage.dryRunConfirmBody'),
      okText: t('backupDr.pitrPage.dryRunOk'),
      cancelText: t('backupDr.pitr.cancel'),
      onOk: async () => {
        try {
          const result = await dryRun.mutateAsync({ targetTimeUtc: selectedTime.toISOString() });
          if (result.accepted) {
            notify.success(t('backupDr.pitrPage.dryRunAccepted'));
          } else {
            notify.error(result.message ?? t('backupDr.pitr.validationFailedGeneric'));
          }
        } catch (err) {
          notify.apiError(err, {
            logContext: 'PitrRestore.dryRun',
            fallbackKey: 'backupDr.pitr.validateFailed',
          });
        }
      },
    });
  };

  const checkTag = (passed: boolean, status: string) => (
    <Tag color={passed ? 'green' : status === 'skipped' ? 'default' : 'red'}>
      {passed ? t('backupDr.pitrPage.checkPassed') : t('backupDr.pitrPage.checkFailed')}
    </Tag>
  );

  return (
    <Space orientation="vertical" size={16} style={{ width: '100%' }}>
      <Alert
        type="warning"
        showIcon
        title={t('backupDr.pitrPage.isolatedTitle')}
        description={t('backupDr.pitrPage.isolatedDescription')}
      />

      <Row gutter={[16, 16]}>
        <Col xs={24} md={8}>
          <Card>
            <Statistic
              title={t('backupDr.pitr.earliestPoint')}
              value={
                availability?.earliestRestorePointUtc
                  ? formatDateTime(availability.earliestRestorePointUtc, formatLocale)
                  : '—'
              }
              styles={{ content: { fontSize: 16 } }}
            />
          </Card>
        </Col>
        <Col xs={24} md={8}>
          <Card>
            <Statistic
              title={t('backupDr.pitr.latestPoint')}
              value={
                availability?.latestRestorePointUtc
                  ? formatDateTime(availability.latestRestorePointUtc, formatLocale)
                  : '—'
              }
              styles={{ content: { fontSize: 16 } }}
            />
          </Card>
        </Col>
        <Col xs={24} md={8}>
          <Card>
            <Statistic
              title={t('backupDr.pitrPage.walFiles')}
              value={wal?.fileCount ?? availability?.walFileCount ?? 0}
              suffix={
                wal?.enabled || availability?.walArchivingEnabled ? (
                  <Tag color="green">{t('backupDr.pitr.walArchivingActive')}</Tag>
                ) : (
                  <Tag>{t('backupDr.pitr.walDisabledTitle')}</Tag>
                )
              }
            />
          </Card>
        </Col>
      </Row>

      {wal?.message ? (
        <Alert type="info" showIcon title={t('backupDr.pitrPage.walOpsTitle')} description={wal.message} />
      ) : null}

      <BackupChain chain={chainQuery.data} loading={chainQuery.isLoading} />

      <Card
        title={
          <Space>
            <CalendarOutlined />
            {t('backupDr.pitr.pickTimeTitle')}
          </Space>
        }
        extra={canRestore ? <PitrRestoreWorkflow /> : null}
      >
        <Space orientation="vertical" size={16} style={{ width: '100%' }}>
          <DatePicker
            showTime={{ format: 'HH:mm:ss' }}
            format="DD.MM.YYYY HH:mm:ss"
            value={selectedTime}
            onChange={onValidate}
            disabledDate={disabledDate}
            disabledTime={disabledTime}
            showNow={false}
            disabled={!availability?.earliestRestorePointUtc}
            style={{ width: '100%', maxWidth: 360 }}
            placeholder={t('backupDr.pitr.pickTimePlaceholder')}
          />

          {preValidate.isPending ? <Spin /> : null}

          {report ? (
            <div
              style={{
                padding: 16,
                borderRadius: 8,
                background: report.passed
                  ? 'var(--ant-color-success-bg)'
                  : 'var(--ant-color-error-bg)',
              }}
            >
              <Space align="start">
                {report.passed ? (
                  <CheckCircleOutlined style={{ color: 'var(--ant-color-success)' }} />
                ) : (
                  <WarningOutlined style={{ color: 'var(--ant-color-error)' }} />
                )}
                <Typography.Text strong>
                  {report.passed
                    ? t('backupDr.pitrPage.reportPassed')
                    : t('backupDr.pitrPage.reportFailed')}
                </Typography.Text>
              </Space>
              <Typography.Paragraph style={{ marginTop: 8 }}>{report.message}</Typography.Paragraph>
              <Descriptions size="small" column={1}>
                <Descriptions.Item label={t('backupDr.pitrPage.dataLossLabel')}>
                  {t('backupDr.pitr.estimatedDataLoss', {
                    seconds: String(report.estimatedDataLossSeconds),
                  })}
                </Descriptions.Item>
                <Descriptions.Item label={t('backupDr.pitrPage.checkHash')}>
                  {checkTag(report.hash.passed, report.hash.status)} {report.hash.detail}
                </Descriptions.Item>
                <Descriptions.Item label={t('backupDr.pitrPage.checkSchema')}>
                  {checkTag(report.schema.passed, report.schema.status)} {report.schema.detail}
                </Descriptions.Item>
                <Descriptions.Item label={t('backupDr.pitrPage.checkTse')}>
                  {checkTag(report.tseChain.passed, report.tseChain.status)} {report.tseChain.detail}
                </Descriptions.Item>
              </Descriptions>
              {isSuperAdmin ? (
                <Button
                  type="primary"
                  style={{ marginTop: 12 }}
                  onClick={onDryRun}
                  loading={dryRun.isPending}
                  disabled={!selectedTime}
                >
                  {t('backupDr.pitrPage.dryRun')}
                </Button>
              ) : null}
            </div>
          ) : null}
        </Space>
      </Card>
    </Space>
  );
}

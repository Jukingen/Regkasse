'use client';

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  Alert,
  Button,
  Card,
  Descriptions,
  InputNumber,
  Space,
  Spin,
  Switch,
  Typography,
} from 'antd';
import axios from 'axios';
import React, { useEffect, useMemo, useState } from 'react';

import { useGetApiAdminBackupStatusLatest } from '@/api/generated/admin-backup/admin-backup';
import { FormSkeleton } from '@/components/Skeleton';
import {
  BACKUP_ACTIVE_POLL_MS,
  isBackupLatestRunActiveStatus,
} from '@/features/backup-dr/logic/backupRunDetailPollPolicy';
import {
  type BackupSettingsPutRequestDto,
  getBackupScheduleSettings,
  getBackupScheduleSettingsQueryKey,
  getBackupScheduleStatus,
  getBackupScheduleStatusQueryKey,
  putBackupScheduleSettings,
} from '@/features/backup-dr/logic/backupScheduleSettingsApi';
import { BackupSchedulePlanner } from '@/features/backup/components/BackupSchedulePlanner';
import {
  type BackupSchedulePlannerState,
  apiScheduleToPlannerState,
  buildCronFromPlannerState,
  isPlannerStateValid,
  plannerStateToPutSchedule,
} from '@/features/backup/logic/backupScheduleCronCodec';
import { useAntdApp } from '@/hooks/useAntdApp';
import { useI18n } from '@/i18n';
import { formatDateTime } from '@/i18n/formatting';

const RETENTION_UI_MAX = 90;
const RETENTION_UI_MIN = 7;
const BACKUP_STATUS_IDLE_POLL_MS = 60_000;

export interface BackupScheduleSettingsProps {
  canManage: boolean;
}

function axiosNormalizedMessage(err: unknown): string | undefined {
  if (!axios.isAxiosError(err)) return undefined;
  const withNorm = err as { normalized?: { message?: string } };
  return withNorm.normalized?.message;
}

export function BackupScheduleSettings({ canManage }: BackupScheduleSettingsProps) {
  const { message } = useAntdApp();

  const { t, formatLocale } = useI18n();
  const queryClient = useQueryClient();

  const statusPoll = useGetApiAdminBackupStatusLatest({
    query: { refetchInterval: BACKUP_STATUS_IDLE_POLL_MS, refetchOnWindowFocus: true },
  });

  const schedulePollingMs = useMemo(() => {
    const s = statusPoll.data?.latestRun?.status;
    return isBackupLatestRunActiveStatus(s) ? BACKUP_ACTIVE_POLL_MS : BACKUP_STATUS_IDLE_POLL_MS;
  }, [statusPoll.data?.latestRun?.status]);

  const settingsQuery = useQuery({
    queryKey: getBackupScheduleSettingsQueryKey(),
    queryFn: getBackupScheduleSettings,
    staleTime: 20_000,
    refetchOnWindowFocus: true,
  });

  const scheduleStatusQuery = useQuery({
    queryKey: getBackupScheduleStatusQueryKey(),
    queryFn: getBackupScheduleStatus,
    staleTime: 20_000,
    refetchOnWindowFocus: true,
    refetchInterval: schedulePollingMs,
  });

  const [enabled, setEnabled] = useState(false);
  const [planner, setPlanner] = useState<BackupSchedulePlannerState>(() =>
    apiScheduleToPlannerState(null, '0 2 * * *')
  );
  const [retentionDays, setRetentionDays] = useState(30);
  const [serverRetentionRaw, setServerRetentionRaw] = useState<number | null>(null);
  const [cronTouchedInvalid, setCronTouchedInvalid] = useState(false);

  useEffect(() => {
    const d = settingsQuery.data;
    if (!d) return;
    setEnabled(Boolean(d.enabled));
    setPlanner(apiScheduleToPlannerState(d.schedule ?? null, d.scheduleCron || '0 2 * * *'));
    setRetentionDays(
      Math.min(Math.max(d.retentionDays ?? RETENTION_UI_MIN, RETENTION_UI_MIN), RETENTION_UI_MAX)
    );
    setServerRetentionRaw(d.retentionDays ?? null);
  }, [
    settingsQuery.data?.updatedAtUtc,
    settingsQuery.data?.enabled,
    settingsQuery.data?.scheduleCron,
    settingsQuery.data?.schedule,
    settingsQuery.data?.retentionDays,
    settingsQuery.isSuccess,
  ]);

  const effectiveCron = useMemo(() => buildCronFromPlannerState(planner), [planner]);
  const plannerOk = isPlannerStateValid(planner);

  const putMutation = useMutation({
    mutationFn: (body: BackupSettingsPutRequestDto) => putBackupScheduleSettings(body),
    onSuccess: async () => {
      message.success(t('backupDr.scheduleSettings.saveSuccess'));
      await queryClient.invalidateQueries({ queryKey: getBackupScheduleSettingsQueryKey() });
      await queryClient.invalidateQueries({ queryKey: getBackupScheduleStatusQueryKey() });
    },
    onError: (err: unknown) => {
      const extra = axiosNormalizedMessage(err);
      message.error(
        extra
          ? `${t('backupDr.scheduleSettings.saveError')} ${extra}`
          : t('backupDr.scheduleSettings.saveError')
      );
    },
  });

  const onSave = () => {
    if (!canManage) return;
    if (!plannerOk) {
      setCronTouchedInvalid(true);
      message.error(t('backupDr.scheduleSettings.customCronInvalid'));
      return;
    }
    const body: BackupSettingsPutRequestDto = {
      enabled,
      schedule: plannerStateToPutSchedule(planner),
      scheduleCron: effectiveCron,
      retentionDays,
    };
    putMutation.mutate(body);
  };

  const fmt = (iso: string | null | undefined) => {
    if (!iso) return t('backupDr.scheduleSettings.noRunsYet');
    return formatDateTime(iso, formatLocale);
  };

  const status = scheduleStatusQuery.data;
  const nextDisplay = status?.computedNextRunAtUtc ?? status?.storedNextRunAtUtc ?? null;

  const serverRetentionOverUi = serverRetentionRaw != null && serverRetentionRaw > RETENTION_UI_MAX;

  if (settingsQuery.isError) {
    return (
      <Card
        id="backup-dr-schedule-settings"
        size="small"
        title={t('backupDr.scheduleSettings.cardTitle')}
      >
        <Alert type="error" showIcon title={t('backupDr.scheduleSettings.loadError')} />
      </Card>
    );
  }

  return (
    <Card
      id="backup-dr-schedule-settings"
      size="small"
      title={t('backupDr.scheduleSettings.cardTitle')}
      extra={
        canManage ? (
          <Button type="primary" loading={putMutation.isPending} onClick={onSave}>
            {t('backupDr.scheduleSettings.save')}
          </Button>
        ) : null
      }
    >
      {settingsQuery.isLoading ? (
        <FormSkeleton fields={5} loading />
      ) : (
        <Space orientation="vertical" size="middle" style={{ width: '100%' }}>
          <Typography.Paragraph type="secondary" style={{ marginBottom: 0 }}>
            {t('backupDr.scheduleSettings.cardHint')}
          </Typography.Paragraph>
          {!canManage ? (
            <Typography.Text type="secondary">{t('backupDr.permission.noManage')}</Typography.Text>
          ) : null}

          {serverRetentionOverUi ? (
            <Alert
              type="warning"
              showIcon
              title={t('backupDr.scheduleSettings.serverRetentionHigher', {
                days: String(serverRetentionRaw),
              })}
            />
          ) : null}

          <Space align="center" wrap>
            <Typography.Text>{t('backupDr.scheduleSettings.enabled')}</Typography.Text>
            <Switch checked={enabled} onChange={setEnabled} disabled={!canManage} />
          </Space>

          <BackupSchedulePlanner
            value={planner}
            onChange={(next) => {
              setPlanner(next);
              setCronTouchedInvalid(false);
            }}
            disabled={!canManage}
            showCustomCronInvalid={cronTouchedInvalid && !plannerOk}
          />

          <Typography.Text type="secondary" code style={{ fontSize: 12 }}>
            {t('backupDr.scheduleSettings.cronPreview', { cron: effectiveCron })}
          </Typography.Text>

          <div>
            <Typography.Text strong>
              {t('backupDr.scheduleSettings.retentionLabel')}
            </Typography.Text>
            <Typography.Paragraph type="secondary" style={{ marginBottom: 8, marginTop: 4 }}>
              {t('backupDr.scheduleSettings.retentionHint')}
            </Typography.Paragraph>
            <InputNumber
              min={RETENTION_UI_MIN}
              max={RETENTION_UI_MAX}
              value={retentionDays}
              onChange={(v) =>
                setRetentionDays(
                  typeof v === 'number' && Number.isFinite(v)
                    ? Math.min(RETENTION_UI_MAX, Math.max(RETENTION_UI_MIN, Math.round(v)))
                    : RETENTION_UI_MIN
                )
              }
              disabled={!canManage}
              style={{ width: 120 }}
            />
          </div>

          <Descriptions
            size="small"
            column={1}
            title={t('backupDr.scheduleSettings.statusTitle')}
            bordered
          >
            <Descriptions.Item label={t('backupDr.scheduleSettings.nextRunComputed')}>
              {scheduleStatusQuery.isLoading ? <Spin size="small" /> : fmt(nextDisplay)}
            </Descriptions.Item>
            <Descriptions.Item label={t('backupDr.scheduleSettings.nextRunStored')}>
              {scheduleStatusQuery.isLoading ? (
                <Spin size="small" />
              ) : (
                fmt(status?.storedNextRunAtUtc)
              )}
            </Descriptions.Item>
            <Descriptions.Item label={t('backupDr.scheduleSettings.lastRunStored')}>
              {scheduleStatusQuery.isLoading ? (
                <Spin size="small" />
              ) : (
                fmt(status?.storedLastRunAtUtc)
              )}
            </Descriptions.Item>
            <Descriptions.Item label={t('backupDr.scheduleSettings.lastScheduledRunLabel')}>
              {scheduleStatusQuery.isLoading ? (
                <Spin size="small" />
              ) : status?.latestScheduledBackupRun ? (
                <Typography.Text>
                  {fmt(status.latestScheduledBackupRun.requestedAt)}{' '}
                  {t('backupDr.scheduleSettings.runStatusSuffix', {
                    status: String(status.latestScheduledBackupRun.status),
                  })}
                </Typography.Text>
              ) : (
                t('backupDr.scheduleSettings.noRunsYet')
              )}
            </Descriptions.Item>
          </Descriptions>
        </Space>
      )}
    </Card>
  );
}

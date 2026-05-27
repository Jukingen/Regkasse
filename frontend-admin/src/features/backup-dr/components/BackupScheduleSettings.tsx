"use client";

import React, { useEffect, useMemo, useState } from "react";
import axios from "axios";
import {
  Alert,
  Button,
  Card,
  Descriptions,
  Input,
  InputNumber,
  Radio,
  Slider,
  Space,
  Spin,
  Switch,
  Typography,
  message,
} from "antd";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useI18n } from "@/i18n";
import {
  BACKUP_SCHEDULE_PRESET_CRONS,
  type BackupSchedulePresetId,
  type BackupSettingsPutRequestDto,
  detectSchedulePreset,
  getBackupScheduleSettings,
  getBackupScheduleSettingsQueryKey,
  getBackupScheduleStatus,
  getBackupScheduleStatusQueryKey,
  isPlausibleStandardCron,
  normalizeCronWhitespace,
  putBackupScheduleSettings,
} from "@/features/backup-dr/logic/backupScheduleSettingsApi";
import { BACKUP_ACTIVE_POLL_MS, isBackupLatestRunActiveStatus } from "@/features/backup-dr/logic/backupRunDetailPollPolicy";
import { useGetApiAdminBackupStatusLatest } from "@/api/generated/admin-backup/admin-backup";

const RETENTION_UI_MAX = 90;
const RETENTION_UI_MIN = 1;
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
  const [preset, setPreset] = useState<BackupSchedulePresetId>("daily");
  const [customCron, setCustomCron] = useState<string>(BACKUP_SCHEDULE_PRESET_CRONS.daily);
  const [retentionDays, setRetentionDays] = useState(30);
  const [serverRetentionRaw, setServerRetentionRaw] = useState<number | null>(null);
  const [cronTouchedInvalid, setCronTouchedInvalid] = useState(false);

  useEffect(() => {
    const d = settingsQuery.data;
    if (!d) return;
    setEnabled(Boolean(d.enabled));
    const cron = normalizeCronWhitespace(d.scheduleCron || "");
    const p = detectSchedulePreset(cron);
    setPreset(p);
    setCustomCron(p === "custom" ? d.scheduleCron : cron);
    setRetentionDays(Math.min(Math.max(d.retentionDays, RETENTION_UI_MIN), RETENTION_UI_MAX));
    setServerRetentionRaw(d.retentionDays);
  }, [
    settingsQuery.data?.updatedAtUtc,
    settingsQuery.data?.enabled,
    settingsQuery.data?.scheduleCron,
    settingsQuery.data?.retentionDays,
    settingsQuery.isSuccess,
  ]);

  const effectiveCron = useMemo(() => {
    if (preset === "custom") return normalizeCronWhitespace(customCron);
    return BACKUP_SCHEDULE_PRESET_CRONS[preset];
  }, [preset, customCron]);

  const customCronOk = preset !== "custom" || isPlausibleStandardCron(customCron);

  const putMutation = useMutation({
    mutationFn: putBackupScheduleSettings,
    onSuccess: async () => {
      message.success(t("backupDr.scheduleSettings.saveSuccess"));
      await queryClient.invalidateQueries({ queryKey: getBackupScheduleSettingsQueryKey() });
      await queryClient.invalidateQueries({ queryKey: getBackupScheduleStatusQueryKey() });
    },
    onError: (err: unknown) => {
      const extra = axiosNormalizedMessage(err);
      message.error(extra ? `${t("backupDr.scheduleSettings.saveError")} ${extra}` : t("backupDr.scheduleSettings.saveError"));
    },
  });

  const onSave = () => {
    if (!canManage) return;
    if (!customCronOk) {
      setCronTouchedInvalid(true);
      message.error(t("backupDr.scheduleSettings.customCronInvalid"));
      return;
    }
    const body: BackupSettingsPutRequestDto = {
      enabled,
      scheduleCron: effectiveCron,
      retentionDays,
    };
    putMutation.mutate(body);
  };

  const fmt = (iso: string | null | undefined) => {
    if (!iso) return t("backupDr.scheduleSettings.noRunsYet");
    try {
      return new Date(iso).toLocaleString(formatLocale);
    } catch {
      return iso;
    }
  };

  const status = scheduleStatusQuery.data;
  const nextDisplay =
    status?.computedNextRunAtUtc ?? status?.storedNextRunAtUtc ?? null;

  const serverRetentionOverUi =
    serverRetentionRaw != null && serverRetentionRaw > RETENTION_UI_MAX;

  const onRetentionSlider = (v: number) => {
    const n = Number.isFinite(v) ? Math.round(v) : RETENTION_UI_MIN;
    setRetentionDays(Math.min(RETENTION_UI_MAX, Math.max(RETENTION_UI_MIN, n)));
  };

  if (settingsQuery.isError) {
    return (
      <Card
        id="backup-dr-schedule-settings"
        size="small"
        title={t("backupDr.scheduleSettings.cardTitle")}
      >
        <Alert type="error" showIcon message={t("backupDr.scheduleSettings.loadError")} />
      </Card>
    );
  }

  return (
    <Card
      id="backup-dr-schedule-settings"
      size="small"
      title={t("backupDr.scheduleSettings.cardTitle")}
      extra={
        canManage ? (
          <Button type="primary" loading={putMutation.isPending} onClick={onSave}>
            {t("backupDr.scheduleSettings.save")}
          </Button>
        ) : null
      }
    >
      {settingsQuery.isLoading ? (
        <Spin />
      ) : (
        <Space direction="vertical" size="middle" style={{ width: "100%" }}>
          <Typography.Paragraph type="secondary" style={{ marginBottom: 0 }}>
            {t("backupDr.scheduleSettings.cardHint")}
          </Typography.Paragraph>
          {!canManage ? (
            <Typography.Text type="secondary">{t("backupDr.permission.noManage")}</Typography.Text>
          ) : null}

          {serverRetentionOverUi ? (
            <Alert
              type="warning"
              showIcon
              message={t("backupDr.scheduleSettings.serverRetentionHigher", {
                days: String(serverRetentionRaw),
              })}
            />
          ) : null}

          <Space align="center" wrap>
            <Typography.Text>{t("backupDr.scheduleSettings.enabled")}</Typography.Text>
            <Switch checked={enabled} onChange={setEnabled} disabled={!canManage} />
          </Space>

          <div>
            <Typography.Text strong>{t("backupDr.scheduleSettings.scheduleLabel")}</Typography.Text>
            <Radio.Group
              style={{ display: "block", marginTop: 8 }}
              value={preset}
              onChange={(e) => {
                setPreset(e.target.value as BackupSchedulePresetId);
                setCronTouchedInvalid(false);
              }}
              disabled={!canManage}
            >
              <Space direction="vertical">
                <Radio value="daily">{t("backupDr.scheduleSettings.presetDaily")}</Radio>
                <Radio value="weeklyMon">{t("backupDr.scheduleSettings.presetWeeklyMon")}</Radio>
                <Radio value="monthly1">{t("backupDr.scheduleSettings.presetMonthly1")}</Radio>
                <Radio value="custom">{t("backupDr.scheduleSettings.presetCustom")}</Radio>
              </Space>
            </Radio.Group>
            {preset === "custom" ? (
              <div style={{ marginTop: 8, maxWidth: 480 }}>
                <Input
                  value={customCron}
                  onChange={(e) => {
                    setCustomCron(e.target.value);
                    setCronTouchedInvalid(false);
                  }}
                  placeholder={t("backupDr.scheduleSettings.customCronPlaceholder")}
                  disabled={!canManage}
                  status={cronTouchedInvalid && !customCronOk ? "error" : undefined}
                />
                {cronTouchedInvalid && !customCronOk ? (
                  <Typography.Text type="danger" style={{ display: "block", marginTop: 4 }}>
                    {t("backupDr.scheduleSettings.customCronInvalid")}
                  </Typography.Text>
                ) : null}
              </div>
            ) : null}
          </div>

          <div>
            <Typography.Text strong>{t("backupDr.scheduleSettings.retentionLabel")}</Typography.Text>
            <Typography.Paragraph type="secondary" style={{ marginBottom: 8, marginTop: 4 }}>
              {t("backupDr.scheduleSettings.retentionHint")}
            </Typography.Paragraph>
            <Space wrap align="center" style={{ width: "100%" }}>
              <Slider
                min={RETENTION_UI_MIN}
                max={RETENTION_UI_MAX}
                value={retentionDays}
                onChange={onRetentionSlider}
                disabled={!canManage}
                style={{ minWidth: 200, flex: 1 }}
              />
              <InputNumber
                min={RETENTION_UI_MIN}
                max={RETENTION_UI_MAX}
                value={retentionDays}
                onChange={(v) =>
                  setRetentionDays(
                    typeof v === "number" && Number.isFinite(v)
                      ? Math.min(RETENTION_UI_MAX, Math.max(RETENTION_UI_MIN, Math.round(v)))
                      : RETENTION_UI_MIN,
                  )
                }
                disabled={!canManage}
              />
            </Space>
          </div>

          <Descriptions
            size="small"
            column={1}
            title={t("backupDr.scheduleSettings.statusTitle")}
            bordered
          >
            <Descriptions.Item label={t("backupDr.scheduleSettings.nextRunComputed")}>
              {scheduleStatusQuery.isLoading ? <Spin size="small" /> : fmt(nextDisplay)}
            </Descriptions.Item>
            <Descriptions.Item label={t("backupDr.scheduleSettings.nextRunStored")}>
              {scheduleStatusQuery.isLoading ? <Spin size="small" /> : fmt(status?.storedNextRunAtUtc)}
            </Descriptions.Item>
            <Descriptions.Item label={t("backupDr.scheduleSettings.lastRunStored")}>
              {scheduleStatusQuery.isLoading ? <Spin size="small" /> : fmt(status?.storedLastRunAtUtc)}
            </Descriptions.Item>
            <Descriptions.Item label={t("backupDr.scheduleSettings.lastScheduledRunLabel")}>
              {scheduleStatusQuery.isLoading ? (
                <Spin size="small" />
              ) : status?.latestScheduledBackupRun ? (
                <Typography.Text>
                  {fmt(status.latestScheduledBackupRun.requestedAt)}{" "}
                  {t("backupDr.scheduleSettings.runStatusSuffix", {
                    status: String(status.latestScheduledBackupRun.status),
                  })}
                </Typography.Text>
              ) : (
                t("backupDr.scheduleSettings.noRunsYet")
              )}
            </Descriptions.Item>
          </Descriptions>
        </Space>
      )}
    </Card>
  );
}

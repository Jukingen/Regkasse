"use client";

/**
 * Backup automation + execution mode form (settings PUT + execution-mode PUT).
 */

import React, { useEffect, useMemo, useState } from "react";
import axios from "axios";
import {
  Alert,
  Button,
  Col,
  Divider,
  Form,
  Input,
  InputNumber,
  Modal,
  Row,
  Select,
  Spin,
  Switch,
  Typography,
  message,
} from "antd";
import { useQuery } from "@tanstack/react-query";
import { useI18n } from "@/i18n";
import { useBackupPermissions } from "@/features/backup/hooks/useBackupPermissions";
import { useBackupSettings } from "@/features/backup/hooks/useBackupSettings";
import { useUpdateBackupSettings } from "@/features/backup/hooks/useUpdateBackupSettings";
import {
  getBackupExecutionMode,
  getGetApiAdminBackupExecutionModeQueryKey,
  putBackupExecutionMode,
} from "@/features/backup-dr/logic/backupExecutionModeApi";
import {
  findSelectableRow,
  fakeSwitchNeedsStrongWarning,
  type BackupExecutionModeRadioValue,
} from "@/features/backup-dr/logic/backupDrExecutionModePresentation";
import {
  executionModeSelectLabel,
  initialExecutionModeSelection,
  toPutExecutionModeString,
} from "@/features/backup/logic/backupExecutionModeFormMapping";
import {
  getBackupScheduleStatus,
  getBackupScheduleStatusQueryKey,
  isPlausibleStandardCron,
  normalizeCronWhitespace,
} from "@/features/backup-dr/logic/backupScheduleSettingsApi";
import { useGetApiAdminBackupStatusLatest } from "@/api/generated/admin-backup/admin-backup";

const RETENTION_MIN = 1;
const RETENTION_MAX = 3650;

export type BackupConfigurationFormValues = {
  enabled: boolean;
  scheduleCron: string;
  retentionDays: number;
  executionMode: BackupExecutionModeRadioValue;
};

function axiosNormalizedMessage(err: unknown): string | undefined {
  if (!axios.isAxiosError(err)) return undefined;
  const withNorm = err as { normalized?: { message?: string } };
  return withNorm.normalized?.message;
}

export function BackupConfigurationForm() {
  const { t, formatLocale } = useI18n();
  const { canConfigure: canEdit, canEditExecutionMode, isSuperAdmin: superAdmin } =
    useBackupPermissions();
  const [form] = Form.useForm<BackupConfigurationFormValues>();
  const [initialExecutionMode, setInitialExecutionMode] =
    useState<BackupExecutionModeRadioValue>("InheritFromConfiguration");

  const settingsQuery = useBackupSettings();
  const updateSettings = useUpdateBackupSettings();

  const executionModeQuery = useQuery({
    queryKey: getGetApiAdminBackupExecutionModeQueryKey(),
    queryFn: getBackupExecutionMode,
    staleTime: 20_000,
  });

  const scheduleStatusQuery = useQuery({
    queryKey: getBackupScheduleStatusQueryKey(),
    queryFn: getBackupScheduleStatus,
    staleTime: 20_000,
  });

  const statusLatest = useGetApiAdminBackupStatusLatest({
    query: { staleTime: 30_000 },
  });

  const health = statusLatest.data?.configurationHealth;
  const policy = statusLatest.data?.artifactPipelinePolicy;

  useEffect(() => {
    const s = settingsQuery.data;
    const e = executionModeQuery.data;
    if (!s && !e) return;

    const executionMode = e ? initialExecutionModeSelection(e) : "InheritFromConfiguration";
    if (e) setInitialExecutionMode(executionMode);

    form.setFieldsValue({
      enabled: s?.enabled ?? false,
      scheduleCron: normalizeCronWhitespace(s?.scheduleCron ?? "0 2 * * *"),
      retentionDays: s?.retentionDays ?? 30,
      executionMode,
    });
  }, [executionModeQuery.data, form, settingsQuery.data]);

  const executionModeOptions = useMemo(() => {
    const modes: BackupExecutionModeRadioValue[] = [
      "InheritFromConfiguration",
      "SimulatedFake",
      "PostgreSqlPgDump",
    ];
    const selectable = executionModeQuery.data?.selectableModes;
    return modes.map((value) => {
      const apiName =
        value === "SimulatedFake"
          ? "Fake"
          : value === "PostgreSqlPgDump"
            ? "RealPgDump"
            : "UseConfigurationDefault";
      const row = findSelectableRow(selectable, apiName);
      return {
        value,
        label: executionModeSelectLabel(value, t),
        disabled: row?.selectable === false,
      };
    });
  }, [executionModeQuery.data?.selectableModes, t]);

  const persistExecutionMode = async (
    mode: BackupExecutionModeRadioValue,
    confirmFakeInProduction = false,
  ) => {
    await putBackupExecutionMode({
      mode: toPutExecutionModeString(mode),
      confirmSimulatedOnlyOperationalRiskInProduction: confirmFakeInProduction,
    });
    await executionModeQuery.refetch();
    setInitialExecutionMode(mode);
  };

  const handleSubmit = async (values: BackupConfigurationFormValues) => {
    const cron = normalizeCronWhitespace(values.scheduleCron);
    if (!isPlausibleStandardCron(cron)) {
      message.error(t("backupDr.scheduleSettings.customCronInvalid"));
      return;
    }

    try {
      await updateSettings.mutateAsync({
        enabled: values.enabled,
        scheduleCron: cron,
        retentionDays: values.retentionDays,
      });

      if (canEditExecutionMode && values.executionMode !== initialExecutionMode) {
        const fakeRow = findSelectableRow(executionModeQuery.data?.selectableModes, "Fake");
        const needsStrong = fakeSwitchNeedsStrongWarning(values.executionMode, fakeRow);

        if (values.executionMode === "SimulatedFake" && needsStrong) {
          await new Promise<void>((resolve, reject) => {
            Modal.confirm({
              title: t("backupDr.executionMode.saveConfirmTitleFakeStrong"),
              content: t("backupDr.executionMode.saveConfirmBodyFakeStrong"),
              okText: t("backupDr.executionMode.saveConfirmOk"),
              cancelText: t("common.buttons.cancel"),
              okButtonProps: { danger: true },
              onOk: async () => {
                try {
                  await persistExecutionMode(values.executionMode, true);
                  resolve();
                } catch (e) {
                  reject(e);
                }
              },
              onCancel: () => reject(new Error("cancelled")),
            });
          }).catch((e) => {
            if ((e as Error).message === "cancelled") {
              message.info(t("backupDr.configForm.savePartialSettingsOnly"));
              return;
            }
            throw e;
          });
        } else {
          await persistExecutionMode(values.executionMode);
        }
      }

      message.success(t("backupDr.configForm.saveSuccess"));
      await settingsQuery.refetch();
      await scheduleStatusQuery.refetch();
    } catch (err) {
      const extra = axiosNormalizedMessage(err);
      message.error(
        extra ? `${t("backupDr.configForm.saveError")} ${extra}` : t("backupDr.configForm.saveError"),
      );
    }
  };

  const fmt = (iso: string | null | undefined) => {
    if (!iso) return t("backupDr.scheduleSettings.noRunsYet");
    try {
      return new Date(iso).toLocaleString(formatLocale);
    } catch {
      return iso;
    }
  };

  if (settingsQuery.isError || executionModeQuery.isError) {
    return <Alert type="error" showIcon message={t("backupDr.scheduleSettings.loadError")} />;
  }

  if (settingsQuery.isLoading || executionModeQuery.isLoading) {
    return <Spin />;
  }

  const nextRun =
    scheduleStatusQuery.data?.computedNextRunAtUtc ??
    scheduleStatusQuery.data?.storedNextRunAtUtc;

  return (
    <Form
      form={form}
      layout="vertical"
      onFinish={handleSubmit}
      disabled={!canEdit}
    >
      <Alert
        type="info"
        message={t("backupDr.configForm.alertTitle")}
        description={t(
          superAdmin
            ? "backupDr.configForm.alertDescriptionSuperAdmin"
            : "backupDr.configForm.alertDescription",
        )}
        showIcon
        style={{ marginBottom: 16 }}
      />

      <Row gutter={16}>
        <Col xs={24} md={12}>
          <Form.Item
            name="enabled"
            label={t("backupDr.configForm.enabled")}
            valuePropName="checked"
            tooltip={t("backupDr.configForm.enabledTooltip")}
          >
            <Switch
              checkedChildren={t("backupDr.configForm.switchOn")}
              unCheckedChildren={t("backupDr.configForm.switchOff")}
            />
          </Form.Item>
        </Col>
        <Col xs={24} md={12}>
          <Form.Item
            name="scheduleCron"
            label={t("backupDr.configForm.scheduleCron")}
            tooltip={t("backupDr.configForm.scheduleCronTooltip")}
            rules={[
              {
                validator: async (_, value) => {
                  if (!isPlausibleStandardCron(normalizeCronWhitespace(value ?? ""))) {
                    throw new Error(t("backupDr.scheduleSettings.customCronInvalid"));
                  }
                },
              },
            ]}
          >
            <Input placeholder="0 2 * * *" />
          </Form.Item>
        </Col>
      </Row>

      <Row gutter={16}>
        <Col xs={24} md={12}>
          <Form.Item
            name="retentionDays"
            label={t("backupDr.configForm.retentionDays")}
            tooltip={t("backupDr.configForm.retentionDaysTooltip")}
            rules={[
              {
                type: "number",
                min: RETENTION_MIN,
                max: RETENTION_MAX,
                message: t("backupDr.configForm.retentionRange", {
                  min: String(RETENTION_MIN),
                  max: String(RETENTION_MAX),
                }),
              },
            ]}
          >
            <InputNumber min={RETENTION_MIN} max={RETENTION_MAX} style={{ width: "100%" }} />
          </Form.Item>
        </Col>
        {canEditExecutionMode ? (
          <Col xs={24} md={12}>
            <Form.Item
              name="executionMode"
              label={t("backupDr.configForm.executionModeLabel")}
              tooltip={t("backupDr.configForm.executionModeTooltip")}
            >
              <Select options={executionModeOptions} />
            </Form.Item>
          </Col>
        ) : null}
      </Row>

      {canEditExecutionMode ? (
        <>
          <Divider />
          <Alert
            type="warning"
            message={t("backupDr.configForm.superAdminTitle")}
            description={t("backupDr.configForm.superAdminDescription")}
            showIcon
            style={{ marginBottom: 16 }}
          />
          <Row gutter={16}>
            <Col xs={24} md={12}>
              <Form.Item label={t("backupDr.configForm.externalArchiveRoot")}>
                <Input
                  disabled
                  value={
                    policy?.externalArchiveRootConfigured
                      ? t("backupDr.configForm.pathConfigured")
                      : t("backupDr.configForm.pathNotConfigured")
                  }
                  placeholder="/backup/archive"
                />
              </Form.Item>
            </Col>
            <Col xs={24} md={12}>
              <Form.Item label={t("backupDr.configForm.stagingRoot")}>
                <Input
                  disabled
                  value={health?.effectiveAdapterKind?.trim() || "—"}
                  placeholder="/backup/staging"
                />
              </Form.Item>
            </Col>
          </Row>
          <Typography.Paragraph type="secondary" style={{ fontSize: 12 }}>
            {t("backupDr.configForm.deploymentPathsHint")}
          </Typography.Paragraph>
        </>
      ) : null}

      <Typography.Paragraph type="secondary" style={{ marginBottom: 16 }}>
        {t("backupDr.scheduleSettings.nextRunComputed")}: {fmt(nextRun)}
      </Typography.Paragraph>

      {canEdit ? (
        <Form.Item style={{ marginBottom: 0 }}>
          <Button
            type="primary"
            htmlType="submit"
            loading={updateSettings.isPending || executionModeQuery.isFetching}
          >
            {t("backupDr.configForm.save")}
          </Button>
        </Form.Item>
      ) : (
        <Typography.Text type="secondary">{t("backupDr.permission.noManage")}</Typography.Text>
      )}
    </Form>
  );
}

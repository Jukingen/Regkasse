"use client";

import { useAntdApp } from '@/hooks/useAntdApp';
/**
 * Backup execution mode form (execution-mode PUT only; schedule is BackupScheduleSettings).
 */

import React, { useEffect, useMemo, useState } from "react";
import axios from "axios";
import { Alert, Button, Col, Divider, Form, Input, InputNumber, Row, Select, Spin, Typography } from 'antd';
import { useQuery } from "@tanstack/react-query";
import { useI18n } from "@/i18n";
import { useBackupPermissions } from "@/features/backup/hooks/useBackupPermissions";
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
import { useGetApiAdminBackupStatusLatest } from "@/api/generated/admin-backup/admin-backup";

export type BackupConfigurationFormValues = {
  executionMode: BackupExecutionModeRadioValue;
};

function axiosNormalizedMessage(err: unknown): string | undefined {
  if (!axios.isAxiosError(err)) return undefined;
  const withNorm = err as { normalized?: { message?: string } };
  return withNorm.normalized?.message;
}

export function BackupConfigurationForm() {
  const { message, modal } = useAntdApp();

  const { t } = useI18n();
  const { canConfigure: canEdit, canEditExecutionMode, isSuperAdmin: superAdmin } =
    useBackupPermissions();
  const [form] = Form.useForm<BackupConfigurationFormValues>();
  const [initialExecutionMode, setInitialExecutionMode] =
    useState<BackupExecutionModeRadioValue>("InheritFromConfiguration");
  const [saving, setSaving] = useState(false);
  const executionModeQuery = useQuery({
    queryKey: getGetApiAdminBackupExecutionModeQueryKey(),
    queryFn: getBackupExecutionMode,
    staleTime: 20_000,
  });

  const statusLatest = useGetApiAdminBackupStatusLatest({
    query: { staleTime: 30_000 },
  });

  const health = statusLatest.data?.configurationHealth;
  const policy = statusLatest.data?.artifactPipelinePolicy;

  useEffect(() => {
    const e = executionModeQuery.data;
    if (!e) return;

    const executionMode = initialExecutionModeSelection(e);
    setInitialExecutionMode(executionMode);
    form.setFieldsValue({ executionMode });
  }, [executionModeQuery.data, form]);

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
    setSaving(true);
    try {
      if (canEditExecutionMode && values.executionMode !== initialExecutionMode) {
        const fakeRow = findSelectableRow(executionModeQuery.data?.selectableModes, "Fake");
        const needsStrong = fakeSwitchNeedsStrongWarning(values.executionMode, fakeRow);

        if (values.executionMode === "SimulatedFake" && needsStrong) {
          await new Promise<void>((resolve, reject) => {
            modal.confirm({
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
      await executionModeQuery.refetch();
    } catch (err) {
      const extra = axiosNormalizedMessage(err);
      message.error(
        extra ? `${t("backupDr.configForm.saveError")} ${extra}` : t("backupDr.configForm.saveError"),
      );
    } finally {
      setSaving(false);
    }
  };

  if (executionModeQuery.isError) {
    return (
      <>
        <Form form={form} style={{ display: 'none' }} preserve />
        <Alert type="error" showIcon title={t("backupDr.scheduleSettings.loadError")} />
      </>
    );
  }

  if (executionModeQuery.isLoading) {
    return (
      <>
        <Form form={form} style={{ display: 'none' }} preserve />
        <Spin />
      </>
    );
  }

  return (
    <Form
      form={form}
      layout="vertical"
      onFinish={handleSubmit}
      disabled={!canEdit}
    >
      <Alert
        type="info"
        title={t("backupDr.configForm.alertTitle")}
        description={t(
          superAdmin
            ? "backupDr.configForm.alertDescriptionSuperAdmin"
            : "backupDr.configForm.alertDescription",
        )}
        showIcon
        style={{ marginBottom: 16 }}
      />

      <Row gutter={16}>
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
            title={t("backupDr.configForm.superAdminTitle")}
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

      {canEdit ? (
        <Form.Item style={{ marginBottom: 0 }}>
          <Button
            type="primary"
            htmlType="submit"
            loading={saving || executionModeQuery.isFetching}
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

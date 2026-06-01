"use client";

/**
 * Backup starten: Dropdown + Modal (optional Mandant für Super Admin).
 * API: POST /api/admin/backup/trigger — nur idempotencyKey; Mandant nur im Schlüssel zur Nachverfolgung.
 */

import React, { useCallback, useMemo, useState } from "react";
import { App, Modal, Alert, Button, Dropdown, Form, Select, Tooltip, Typography } from 'antd';
import type { MenuProps } from 'antd';
import { CloudUploadOutlined, DatabaseOutlined } from "@ant-design/icons";
import { useI18n } from "@/i18n";
import { useBackupPermissions } from "@/features/backup/hooks/useBackupPermissions";
import { useTriggerBackup } from "@/features/backup/hooks/useTriggerBackup";
import { useTenants } from "@/features/backup/hooks/useTenants";
import { triggerErrorMessageBackupDashboard } from "@/features/backup-dr/logic/backupManualTriggerMessaging";
import { describeBackupTriggerOutcome } from "@/features/backup-dr/logic/backupTriggerOutcome";

export interface TriggerBackupButtonProps {
  canManage: boolean;
}

export function TriggerBackupButton({ canManage }: TriggerBackupButtonProps) {
  const { message, modal } = App.useApp();

  const { t } = useI18n();
  const { isSuperAdmin: superAdmin } = useBackupPermissions();
  const [isModalOpen, setIsModalOpen] = useState(false);
  const [selectedTenantId, setSelectedTenantId] = useState<string | undefined>();

  const triggerBackup = useTriggerBackup();
  const { tenants, isLoading: tenantsLoading } = useTenants({
    enabled: superAdmin && isModalOpen,
  });

  const tenantOptions = useMemo(
    () =>
      tenants.map((row) => ({
        label: `${row.name} (${row.slug})`,
        value: row.id,
      })),
    [tenants],
  );

  const handleTrigger = useCallback(async () => {
    try {
      const res = await triggerBackup.mutateAsync({
        tenantId: selectedTenantId,
        note: "Manuell ausgelöst",
      });
      const fb = describeBackupTriggerOutcome(res);
      const suffix = res.orchestrationState?.trim()
        ? ` ${t("backupDr.messages.orchestrationStateSuffix", { state: res.orchestrationState })}`
        : "";
      const text = `${t(fb.messageKey)}${suffix}`;
      if (fb.level === "success") message.success(text);
      else message.info(text);
      setIsModalOpen(false);
      setSelectedTenantId(undefined);
    } catch (err) {
      message.error(triggerErrorMessageBackupDashboard(err, t));
    }
  }, [selectedTenantId, t, triggerBackup]);

  const handleAllTenantsTrigger = useCallback(() => {
    modal.confirm({
      title: t("backupDr.triggerButton.allTenantsConfirmTitle"),
      content: (
        <Typography.Paragraph style={{ marginBottom: 0 }}>
          {t("backupDr.triggerButton.allTenantsConfirmBody")}
        </Typography.Paragraph>
      ),
      okText: t("backupDr.manual.confirmBackupOk"),
      cancelText: t("common.buttons.cancel"),
      okButtonProps: { loading: triggerBackup.isPending },
      onOk: async () => {
        try {
          const res = await triggerBackup.mutateAsync({ allTenants: true });
          const fb = describeBackupTriggerOutcome(res);
          if (fb.level === "success") {
            message.success(t("backupDr.triggerButton.allTenantsSuccess"));
          } else {
            message.info(t(fb.messageKey));
          }
        } catch (err) {
          message.error(triggerErrorMessageBackupDashboard(err, t));
          throw err;
        }
      },
    });
  }, [t, triggerBackup]);

  const menuItems: MenuProps["items"] = useMemo(() => {
    const items: MenuProps["items"] = [
      {
        key: "now",
        label: t("backupDr.triggerButton.menuStartNow"),
        icon: <CloudUploadOutlined />,
        onClick: () => setIsModalOpen(true),
      },
    ];
    if (superAdmin) {
      items.push({
        key: "all-tenants",
        label: t("backupDr.triggerButton.menuAllTenants"),
        icon: <DatabaseOutlined />,
        onClick: () => handleAllTenantsTrigger(),
      });
    }
    return items;
  }, [handleAllTenantsTrigger, superAdmin, t]);

  const disabled = !canManage || triggerBackup.isPending;
  const permissionTip = t("backupDr.permission.manualActionsTooltip");

  const triggerControl = (
    <Dropdown menu={{ items: menuItems }} placement="bottomRight" disabled={disabled}>
      <Button type="primary" icon={<CloudUploadOutlined />} loading={triggerBackup.isPending}>
        {t("backupDr.triggerButton.label")}
      </Button>
    </Dropdown>
  );

  if (!canManage) {
    return (
      <Tooltip title={permissionTip}>
        <span style={{ display: "inline-block", cursor: "not-allowed" }}>{triggerControl}</span>
      </Tooltip>
    );
  }

  return (
    <>
      {triggerControl}

      <Modal
        title={t("backupDr.triggerButton.modalTitle")}
        open={isModalOpen}
        onOk={() => void handleTrigger()}
        onCancel={() => {
          setIsModalOpen(false);
          setSelectedTenantId(undefined);
        }}
        confirmLoading={triggerBackup.isPending}
        okText={t("backupDr.triggerButton.modalOk")}
        cancelText={t("common.buttons.cancel")}
        destroyOnHidden
      >
        {superAdmin ? (
          <Alert
            type="info"
            showIcon
            style={{ marginBottom: 16 }}
            title={t("backupDr.triggerButton.instanceScopeHint")}
          />
        ) : null}

        {superAdmin ? (
          <Form layout="vertical">
            <Form.Item label={t("backupDr.triggerButton.tenantLabel")}>
              <Select
                allowClear
                showSearch
                placeholder={t("backupDr.triggerButton.tenantPlaceholder")}
                value={selectedTenantId}
                onChange={setSelectedTenantId}
                loading={tenantsLoading}
                options={tenantOptions}
                optionFilterProp="label"
              />
            </Form.Item>
          </Form>
        ) : null}

        <Typography.Paragraph type="secondary" style={{ marginBottom: 0, fontSize: 13 }}>
          {t("backupDr.triggerButton.modalHint")}
        </Typography.Paragraph>
      </Modal>
    </>
  );
}

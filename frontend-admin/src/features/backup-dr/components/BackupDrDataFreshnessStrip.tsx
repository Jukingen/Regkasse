"use client";

/**
 * Kısmi API hatası: sayfa sağlıklı görünmesin diye tek satır veri sağlığı — tam sayfa fatal hata değil.
 */

import React from "react";
import { Alert, Button, Space, Typography } from "antd";

export interface BackupDrDataFreshnessStripProps {
  /** Ana omurga yüklü; destekleyici uçlardan biri veya birkaçı başarısız. */
  show: boolean;
  recoverabilityFailed: boolean;
  verificationFailed: boolean;
  restoreLatestFailed: boolean;
  onRetry: () => void;
  t: (key: string, options?: Record<string, string | number>) => string;
}

export function BackupDrDataFreshnessStrip({
  show,
  recoverabilityFailed,
  verificationFailed,
  restoreLatestFailed,
  onRetry,
  t,
}: BackupDrDataFreshnessStripProps) {
  if (!show) return null;

  const parts: string[] = [];
  if (recoverabilityFailed) parts.push(t("backupDr.dataFreshness.sliceRecoverability"));
  if (verificationFailed) parts.push(t("backupDr.dataFreshness.sliceVerification"));
  if (restoreLatestFailed) parts.push(t("backupDr.dataFreshness.sliceRestoreLatest"));

  return (
    <Alert
      type="warning"
      showIcon
      style={{ marginBottom: 0 }}
      title={
        <Space wrap size="small" align="center">
          <Typography.Text strong>
            {t("backupDr.dataFreshness.title")}
          </Typography.Text>
          <Typography.Text type="secondary" style={{ fontSize: 13 }}>
            {parts.length > 0
              ? parts.join(" · ")
              : t("backupDr.dataFreshness.generic")}
          </Typography.Text>
        </Space>
      }
      description={
        <Typography.Text type="secondary" style={{ fontSize: 12 }}>
          {t("backupDr.dataFreshness.hint")}
        </Typography.Text>
      }
      action={
        <Button size="small" type="default" onClick={onRetry}>
          {t("backupDr.actions.refresh")}
        </Button>
      }
    />
  );
}

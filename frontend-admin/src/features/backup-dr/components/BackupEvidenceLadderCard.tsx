"use client";

/**
 * Yedek kanıt basamakları: teknik başarı ile gerçek pg_dump / tatbikat kanıtını ayırır.
 */

import React from "react";
import { Alert, Card, List, Space, Tag, Typography } from "antd";
import type {
  BackupEvidenceLadderModel,
  EvidenceStepStatus,
} from "@/features/backup-dr/logic/backupDrEvidenceLadder";
import { mapEvidenceHeadlineToneToAlertType } from "@/features/backup-dr/logic/backupDrGlancePresentation";

function tagColor(s: EvidenceStepStatus): string {
  switch (s) {
    case "pass":
      return "success";
    case "fail":
      return "error";
    case "limited":
      return "warning";
    case "na":
      return "default";
    default:
      return "processing";
  }
}

export interface BackupEvidenceLadderContentProps {
  model: BackupEvidenceLadderModel;
  t: (k: string) => string;
}

/** Kart çerçevesi olmadan — birleşik kanıt yüzeyinde yeniden kullanım. */
export function BackupEvidenceLadderContent({
  model,
  t,
}: BackupEvidenceLadderContentProps) {
  const alertType = mapEvidenceHeadlineToneToAlertType(model.headlineTone);

  return (
    <>
      <Alert
        type={alertType}
        showIcon
        style={{ marginBottom: 16 }}
        title={t(model.headlineKey)}
      />
      <List
        size="small"
        dataSource={model.steps}
        renderItem={(item) => (
          <List.Item>
            <Space align="start" wrap>
              <Tag color={tagColor(item.status)}>
                {t(`backupDr.evidence.status.${item.status}`)}
              </Tag>
              <div>
                <Typography.Text strong>{t(item.labelKey)}</Typography.Text>
                <Typography.Paragraph
                  type="secondary"
                  style={{ marginBottom: 0, marginTop: 4 }}
                >
                  {t(item.detailKey)}
                </Typography.Paragraph>
              </div>
            </Space>
          </List.Item>
        )}
      />
      <Typography.Title level={5} style={{ marginTop: 16, marginBottom: 8 }}>
        {t("backupDr.evidence.backendGapsTitle")}
      </Typography.Title>
      <ul style={{ marginBottom: 8, paddingLeft: 20 }}>
        {model.backendSignalGaps.map((k) => (
          <li key={k}>
            <Typography.Text type="secondary">{t(k)}</Typography.Text>
          </li>
        ))}
      </ul>
      <Typography.Paragraph
        type="secondary"
        style={{ marginBottom: 0, fontSize: 12 }}
      >
        {t("backupDr.evidence.footer")}
      </Typography.Paragraph>
    </>
  );
}

export interface BackupEvidenceLadderCardProps {
  model: BackupEvidenceLadderModel;
  t: (k: string) => string;
}

export function BackupEvidenceLadderCard({
  model,
  t,
}: BackupEvidenceLadderCardProps) {
  return (
    <Card title={t("backupDr.evidence.title")} size="small">
      <BackupEvidenceLadderContent model={model} t={t} />
    </Card>
  );
}

"use client";

/**
 * Üst karar şeridi: DR kanıt düzeyi, şerit (kritik boşluk/uyarı), sonraki adım — drProofLevelPresentation tek kaynak.
 */

import React from "react";
import { Alert, Card, Col, Row, Space, Tag, Typography } from "antd";
import type {
  DrProofPresentationModel,
  DrProofScanTag,
  DrProofScanTagTone,
} from "@/features/backup-dr/logic/drProofLevelPresentation";

function tagToneToColor(tone: DrProofScanTagTone): string {
  return tone;
}

export interface BackupDrDecisionStripProps {
  model: DrProofPresentationModel;
  /** Hızlı tarama etiketleri — `buildDrProofScanTags` ile üretilir. */
  scanTags?: DrProofScanTag[];
  t: (key: string, options?: Record<string, string | number>) => string;
}

export function BackupDrDecisionStrip({
  model,
  scanTags = [],
  t,
}: BackupDrDecisionStripProps) {
  const strip = model.decisionStrip;
  const stripIsError = strip.alertType === "error";

  return (
    <Card
      size="small"
      styles={{ body: { padding: 16 } }}
      style={
        stripIsError
          ? { borderColor: "#cf1322", borderWidth: 1, borderStyle: "solid" }
          : undefined
      }
    >
      {scanTags.length > 0 ? (
        <Space wrap size={[8, 8]} style={{ marginBottom: 14 }}>
          {scanTags.map((tag, i) => (
            <Tag key={`${tag.labelKey}-${i}`} color={tagToneToColor(tag.tone)}>
              {t(tag.labelKey)}
            </Tag>
          ))}
        </Space>
      ) : null}
      <Row gutter={[16, 16]} align="top">
        <Col xs={24} lg={7}>
          <Typography.Text type="secondary" style={{ fontSize: 12 }}>
            {t("backupDr.ia.columns.proofLevel")}
          </Typography.Text>
          <Typography.Title level={3} style={{ margin: "6px 0 0" }}>
            {t("backupDr.confidenceDashboard.levelLabel", {
              level: model.highestFullyProvenLevel,
            })}
          </Typography.Title>
        </Col>
        <Col xs={24} lg={10}>
          <Typography.Text
            type="secondary"
            style={{ fontSize: 12, display: "block", marginBottom: 6 }}
          >
            {t("backupDr.ia.columns.blocker")}
          </Typography.Text>
          {strip.alertType === "error" || strip.alertType === "warning" ? (
            <Alert
              type={strip.alertType}
              showIcon
              message={t(strip.titleKey)}
              description={
                <Typography.Paragraph style={{ marginBottom: 0 }}>
                  {t(strip.bodyKey)}
                </Typography.Paragraph>
              }
            />
          ) : (
            <div
              style={{
                padding: "10px 12px",
                borderRadius: 6,
                border: "1px solid #d9d9d9",
                background: "#fafafa",
              }}
            >
              <Typography.Text strong style={{ display: "block", marginBottom: 6 }}>
                {t(strip.titleKey)}
              </Typography.Text>
              <Typography.Paragraph type="secondary" style={{ marginBottom: 0 }}>
                {t(strip.bodyKey)}
              </Typography.Paragraph>
            </div>
          )}
        </Col>
        <Col xs={24} lg={7}>
          <Typography.Text type="secondary" style={{ fontSize: 12 }}>
            {t("backupDr.ia.columns.nextStep")}
          </Typography.Text>
          <Typography.Title level={5} style={{ marginTop: 8, marginBottom: 0, fontWeight: 600 }}>
            {t(model.nextStepKey)}
          </Typography.Title>
        </Col>
      </Row>
    </Card>
  );
}

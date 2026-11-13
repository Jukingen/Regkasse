'use client';

/**
 * Üst özet: tek bakışta duruş, son yedek/drill, LKG kanıtı, çalıştırma modu, sonraki adım — yeşil “her şey güvenli” ima etmez.
 */
import { Card, Col, Row, Space, Tag, Typography } from 'antd';
import React from 'react';

import type {
  BackupRecoverabilitySummaryResponseDto,
  BackupRunResponseDto,
  RestoreVerificationRunResponseDto,
} from '@/api/generated/model';
import type { BackupExecutionModeTruth } from '@/features/backup-dr/logic/backupDrExecutionModeTruth';
import { formatRecoverabilityTimestampOrProofGap } from '@/features/backup-dr/logic/backupDrOperatorTruth';
import {
  type DrProofPresentationModel,
  type DrProofScanTag,
  mapDrProofScanTagToneToAntdTagColor,
} from '@/features/backup-dr/logic/drProofLevelPresentation';

export interface BackupDrPostureSummaryProps {
  drProof: DrProofPresentationModel;
  /** Hızlı sinyaller — `buildDrProofScanTags` çıktısı */
  scanTags?: DrProofScanTag[];
  latestRun: BackupRunResponseDto | undefined;
  restoreLatest: RestoreVerificationRunResponseDto | undefined;
  recoverability: BackupRecoverabilitySummaryResponseDto | undefined;
  executionMode: BackupExecutionModeTruth;
  simulatedOperationalMode: boolean;
  backupStatusLabel: (status: number | undefined) => string;
  restoreStatusLabel: (status: number | undefined) => string;
  formatDt: (iso: string | undefined | null, locale: string) => string;
  formatLocale: string;
  t: (key: string, options?: Record<string, string | number>) => string;
}

function stripTone(alertType: DrProofPresentationModel['decisionStrip']['alertType']): {
  border: string;
  bg: string;
} {
  if (alertType === 'error') {
    return { border: '#ffccc7', bg: '#fff2f0' };
  }
  if (alertType === 'warning') {
    return { border: '#ffe58f', bg: '#fffbe6' };
  }
  return { border: '#d9d9d9', bg: '#fafafa' };
}

export function BackupDrPostureSummary({
  drProof,
  scanTags = [],
  latestRun,
  restoreLatest,
  recoverability,
  executionMode,
  simulatedOperationalMode,
  backupStatusLabel,
  restoreStatusLabel,
  formatDt,
  formatLocale,
  t,
}: BackupDrPostureSummaryProps) {
  const strip = drProof.decisionStrip;
  const tone = stripTone(strip.alertType);
  const lkgLine = formatRecoverabilityTimestampOrProofGap(
    recoverability?.lastSuccessfulBackupAt,
    formatDt,
    formatLocale,
    t
  );

  return (
    <Card
      size="small"
      title={t('backupDr.postureSummary.title')}
      styles={{ body: { paddingTop: 12 } }}
      style={{
        position: 'sticky',
        top: 8,
        zIndex: 6,
        borderColor: tone.border,
        background: tone.bg,
      }}
    >
      {simulatedOperationalMode ? (
        <Tag color="orange" style={{ marginBottom: 10 }}>
          {t('backupDr.postureSummary.simulatedModeTag')}
        </Tag>
      ) : null}

      {scanTags.length > 0 ? (
        <Space wrap size={[6, 6]} style={{ marginBottom: 12 }}>
          {scanTags.map((tag, i) => (
            <Tag key={`${tag.labelKey}-${i}`} color={mapDrProofScanTagToneToAntdTagColor(tag.tone)}>
              {t(tag.labelKey)}
            </Tag>
          ))}
        </Space>
      ) : null}

      <Row gutter={[12, 12]}>
        <Col xs={24} lg={8}>
          <Typography.Text
            type="secondary"
            style={{ fontSize: 12, display: 'block', marginBottom: 4 }}
          >
            {t('backupDr.postureSummary.postureLabel')}
          </Typography.Text>
          <Typography.Title level={4} style={{ margin: '0 0 6px' }}>
            {t('backupDr.confidenceDashboard.levelLabel', {
              level: drProof.highestFullyProvenLevel,
            })}
          </Typography.Title>
          <Typography.Text strong style={{ display: 'block' }}>
            {t(strip.titleKey)}
          </Typography.Text>
          <Typography.Paragraph
            type="secondary"
            style={{ marginTop: 8, marginBottom: 0, fontSize: 13 }}
          >
            {t(strip.bodyKey)}
          </Typography.Paragraph>
        </Col>
        <Col xs={24} lg={8}>
          <Typography.Text
            type="secondary"
            style={{ fontSize: 12, display: 'block', marginBottom: 4 }}
          >
            {t('backupDr.postureSummary.backupAndDrillLabel')}
          </Typography.Text>
          <Space orientation="vertical" size={6} style={{ width: '100%' }}>
            <div>
              <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                {t('backupDr.ia.columns.latestBackupRun')}:{' '}
              </Typography.Text>
              <Typography.Text>
                {latestRun?.id
                  ? backupStatusLabel(latestRun.status)
                  : t('backupDr.summary.unknown')}
              </Typography.Text>
            </div>
            <div>
              <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                {t('backupDr.ia.columns.latestRestoreDrill')}:{' '}
              </Typography.Text>
              <Typography.Text>
                {restoreLatest
                  ? restoreStatusLabel(restoreLatest.status)
                  : t('backupDr.summary.unknown')}
              </Typography.Text>
            </div>
          </Space>
        </Col>
        <Col xs={24} lg={8}>
          <Typography.Text
            type="secondary"
            style={{ fontSize: 12, display: 'block', marginBottom: 4 }}
          >
            {t('backupDr.postureSummary.lkgAndModeLabel')}
          </Typography.Text>
          <Space orientation="vertical" size={6} style={{ width: '100%' }}>
            <div>
              <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                {t('backupDr.ia.columns.lkgProof')}:{' '}
              </Typography.Text>
              <Typography.Text>{lkgLine}</Typography.Text>
            </div>
            <div>
              <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                {t('backupDr.postureSummary.executionModeShort')}:{' '}
              </Typography.Text>
              <Typography.Text>
                {executionMode.loaded
                  ? executionMode.effectiveUserFacingMode || '—'
                  : t('backupDr.postureSummary.executionModeLoading')}
              </Typography.Text>
            </div>
          </Space>
        </Col>
      </Row>

      <div
        style={{
          marginTop: 12,
          paddingTop: 12,
          borderTop: `1px solid ${tone.border}`,
        }}
      >
        <Typography.Text
          type="secondary"
          style={{ fontSize: 12, display: 'block', marginBottom: 4 }}
        >
          {t('backupDr.postureSummary.nextActionLabel')}
        </Typography.Text>
        <Typography.Text>{t(drProof.nextStepKey)}</Typography.Text>
      </div>
    </Card>
  );
}

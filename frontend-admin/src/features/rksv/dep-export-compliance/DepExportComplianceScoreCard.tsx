'use client';

import { ReloadOutlined, TrophyOutlined } from '@ant-design/icons';
import { Alert, Button, Card, Progress, Space, Tag, Typography } from 'antd';
import React from 'react';

import {
  factorStrokeColor,
  gradeTagColor,
  scoreColor,
  useDepExportComplianceScore,
} from '@/features/rksv/hooks/useDepExportComplianceScore';
import { useI18n } from '@/i18n';
import { ApiErrorAlertDescription } from '@/shared/errors/ApiErrorAlertDescription';

type Props = {
  style?: React.CSSProperties;
};

export function DepExportComplianceScoreCard({ style }: Props) {
  const { t } = useI18n();
  const { data, isLoading, isFetching, error, refetch } = useDepExportComplianceScore();

  const score = data?.score ?? 0;
  const grade = data?.grade ?? 'F';
  const factors = data?.factors ?? [];
  const criticalIssues = data?.criticalIssues ?? [];
  const warnings = data?.warnings ?? [];

  return (
    <Card
      title={
        <Space>
          <TrophyOutlined />
          <span>{t('rksvHub.depExportComplianceScore.title')}</span>
        </Space>
      }
      loading={isLoading}
      style={style}
      extra={
        <Button
          icon={<ReloadOutlined />}
          loading={isFetching}
          onClick={() => {
            void refetch();
          }}
        >
          {t('rksvHub.depExportComplianceScore.refresh')}
        </Button>
      }
    >
      {error ? (
        <Alert
          type="error"
          showIcon
          style={{ marginBottom: 16 }}
          title={t('rksvHub.depExportComplianceScore.loadFailed')}
          description={
            <ApiErrorAlertDescription
              t={t}
              error={error}
              logContext="DepExportComplianceScore.load"
              fallbackKey="rksvHub.depExportComplianceScore.loadFailed"
            />
          }
        />
      ) : null}

      <div
        style={{
          display: 'flex',
          alignItems: 'flex-start',
          gap: 32,
          flexWrap: 'wrap',
        }}
      >
        <div style={{ textAlign: 'center', minWidth: 140 }}>
          <div
            style={{
              fontSize: 48,
              fontWeight: 700,
              lineHeight: 1.1,
              color: scoreColor(score),
            }}
          >
            {score}
          </div>
          <Typography.Text type="secondary">
            {t('rksvHub.depExportComplianceScore.scoreLabel')}
          </Typography.Text>
          <div style={{ marginTop: 8 }}>
            <Tag color={gradeTagColor(grade)}>
              {t('rksvHub.depExportComplianceScore.grade', { grade })}
            </Tag>
          </div>
        </div>

        <div style={{ flex: 1, minWidth: 260 }}>
          {factors.map((factor) => (
            <div
              key={factor.name}
              style={{
                display: 'flex',
                alignItems: 'center',
                gap: 8,
                paddingBlock: 4,
              }}
            >
              <Typography.Text style={{ width: 150, flexShrink: 0, fontSize: 13 }} ellipsis>
                {factorLabel(t, factor.name)}
              </Typography.Text>
              <Progress
                percent={factor.score}
                size="small"
                strokeColor={factorStrokeColor(factor.status)}
                style={{ flex: 1, marginBottom: 0 }}
              />
              <Typography.Text type="secondary" style={{ width: 40, textAlign: 'right', fontSize: 12 }}>
                {factor.weight}%
              </Typography.Text>
            </div>
          ))}
        </div>
      </div>

      {criticalIssues.length > 0 ? (
        <Alert
          type="error"
          showIcon
          style={{ marginTop: 16 }}
          title={t('rksvHub.depExportComplianceScore.criticalTitle')}
          description={
            <ul style={{ margin: 0, paddingLeft: 18 }}>
              {criticalIssues.map((issue) => (
                <li key={issue}>{issue}</li>
              ))}
            </ul>
          }
        />
      ) : null}

      {warnings.length > 0 ? (
        <Alert
          type="warning"
          showIcon
          style={{ marginTop: 12 }}
          title={t('rksvHub.depExportComplianceScore.warningsTitle')}
          description={
            <ul style={{ margin: 0, paddingLeft: 18 }}>
              {warnings.map((issue) => (
                <li key={issue}>{issue}</li>
              ))}
            </ul>
          }
        />
      ) : null}

      <Typography.Paragraph type="secondary" style={{ marginTop: 12, marginBottom: 0, fontSize: 12 }}>
        {data?.disclaimer ?? t('rksvHub.depExportComplianceScore.disclaimer')}
      </Typography.Paragraph>
    </Card>
  );
}

function factorLabel(t: (key: string) => string, name: string): string {
  switch (name) {
    case 'Legal obligations':
      return t('rksvHub.depExportComplianceScore.factors.legal');
    case 'Overdue clearance':
      return t('rksvHub.depExportComplianceScore.factors.overdue');
    case 'Requirements completion':
      return t('rksvHub.depExportComplianceScore.factors.requirements');
    case 'Validation health':
      return t('rksvHub.depExportComplianceScore.factors.validation');
    case 'Archive coverage':
      return t('rksvHub.depExportComplianceScore.factors.archive');
    default:
      return name;
  }
}

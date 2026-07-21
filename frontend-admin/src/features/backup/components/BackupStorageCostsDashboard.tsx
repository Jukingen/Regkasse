'use client';

/**
 * Indicative backup storage cost dashboard (ops estimate — not a cloud invoice).
 */
import { CheckCircleOutlined } from '@ant-design/icons';
import { Alert, Card, Col, List, Progress, Row, Statistic, Table, Tag, Typography } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import React, { useMemo } from 'react';

import { PageSkeleton } from '@/components/Skeleton';
import { useStorageCosts } from '@/features/backup/hooks/useStorageCosts';
import type {
  BackupStorageCostRecommendationDto,
  BackupStorageTierCostRowDto,
} from '@/features/backup/logic/backupStorageCostsApi';
import { useI18n } from '@/i18n';

function accessLabelKey(access: string): string {
  switch (access) {
    case 'fast':
      return 'backupDr.costs.access.fast';
    case 'medium':
      return 'backupDr.costs.access.medium';
    case 'slow':
      return 'backupDr.costs.access.slow';
    default:
      return 'backupDr.costs.access.unknown';
  }
}

function recommendationTitleKey(code: string): string | null {
  switch (code) {
    case 'storage_pressure':
      return 'backupDr.costs.recommendations.storagePressure.title';
    case 'enable_smart_retention':
      return 'backupDr.costs.recommendations.enableSmartRetention.title';
    case 'enable_storage_tiers':
      return 'backupDr.costs.recommendations.enableStorageTiers.title';
    case 'rebalance_hot':
      return 'backupDr.costs.recommendations.rebalanceHot.title';
    case 'tier_savings_low':
      return 'backupDr.costs.recommendations.tierSavingsLow.title';
    case 'healthy':
      return 'backupDr.costs.recommendations.healthy.title';
    default:
      return null;
  }
}

function recommendationDescKey(code: string): string | null {
  switch (code) {
    case 'storage_pressure':
      return 'backupDr.costs.recommendations.storagePressure.description';
    case 'enable_smart_retention':
      return 'backupDr.costs.recommendations.enableSmartRetention.description';
    case 'enable_storage_tiers':
      return 'backupDr.costs.recommendations.enableStorageTiers.description';
    case 'rebalance_hot':
      return 'backupDr.costs.recommendations.rebalanceHot.description';
    case 'tier_savings_low':
      return 'backupDr.costs.recommendations.tierSavingsLow.description';
    case 'healthy':
      return 'backupDr.costs.recommendations.healthy.description';
    default:
      return null;
  }
}

export function BackupStorageCostsDashboard() {
  const { t } = useI18n();
  const { data: costs, isLoading, isError } = useStorageCosts();

  const columns: ColumnsType<BackupStorageTierCostRowDto> = useMemo(
    () => [
      {
        title: t('backupDr.costs.columns.tier'),
        dataIndex: 'name',
        key: 'name',
      },
      {
        title: t('backupDr.costs.columns.sizeGb'),
        dataIndex: 'sizeGb',
        key: 'sizeGb',
        render: (v: number) => v.toFixed(3),
      },
      {
        title: t('backupDr.costs.columns.costEur'),
        dataIndex: 'costEur',
        key: 'costEur',
        render: (v: number) => v.toFixed(4),
      },
      {
        title: t('backupDr.costs.columns.access'),
        dataIndex: 'access',
        key: 'access',
        render: (access: string) => t(accessLabelKey(access)),
      },
      {
        title: t('backupDr.costs.columns.retention'),
        dataIndex: 'retention',
        key: 'retention',
      },
      {
        title: t('backupDr.costs.columns.artifacts'),
        dataIndex: 'artifactCount',
        key: 'artifactCount',
      },
    ],
    [t]
  );

  if (isLoading) return <PageSkeleton widgets={4} />;

  if (isError) {
    return <Alert type="error" showIcon title={t('backupDr.costs.loadFailed')} />;
  }

  if (!costs) {
    return <Alert type="info" showIcon title={t('backupDr.costs.empty')} />;
  }

  const usageHigh = costs.usagePercentage > 80;

  return (
    <div>
      <Alert
        type="info"
        showIcon
        title={t('backupDr.costs.disclaimerTitle')}
        description={costs.disclaimer || t('backupDr.costs.disclaimerDefault')}
        style={{ marginBottom: 16 }}
      />

      {usageHigh ? (
        <Alert
          type="warning"
          showIcon
          title={t('backupDr.costs.capacityWarningTitle')}
          description={t('backupDr.costs.capacityWarningDescription', {
            percent: costs.usagePercentage,
          })}
          style={{ marginBottom: 16 }}
        />
      ) : null}

      <Row gutter={[16, 16]}>
        <Col xs={24} sm={12} lg={6}>
          <Card>
            <Statistic
              title={t('backupDr.costs.stats.totalStorage')}
              value={costs.totalStorageGb}
              precision={3}
              suffix="GB"
            />
            <Typography.Text type="secondary" style={{ fontSize: 12 }}>
              {t('backupDr.costs.stats.budget', { budget: costs.budgetGb })}
            </Typography.Text>
            <Progress
              percent={Math.min(100, costs.usagePercentage)}
              status={usageHigh ? 'exception' : 'active'}
              style={{ marginTop: 8 }}
            />
          </Card>
        </Col>
        <Col xs={24} sm={12} lg={6}>
          <Card>
            <Statistic
              title={t('backupDr.costs.stats.monthlyCost')}
              value={costs.monthlyCostEur}
              precision={2}
              prefix="€"
            />
            <Statistic
              title={t('backupDr.costs.stats.costPerGb')}
              value={costs.costPerGbEur}
              precision={4}
              prefix="€"
              suffix="/GB"
              style={{ marginTop: 12 }}
            />
          </Card>
        </Col>
        <Col xs={24} sm={12} lg={6}>
          <Card>
            <Statistic title={t('backupDr.costs.stats.backupCount')} value={costs.backupCount} />
            <Statistic
              title={t('backupDr.costs.stats.averageSize')}
              value={costs.averageSizeMb}
              precision={1}
              suffix="MB"
              style={{ marginTop: 12 }}
            />
          </Card>
        </Col>
        <Col xs={24} sm={12} lg={6}>
          <Card>
            <Statistic
              title={t('backupDr.costs.stats.retentionSavings')}
              value={costs.retentionSavingsPercent}
              precision={1}
              suffix="%"
              valueStyle={{ color: '#16a34a' }}
            />
            <Statistic
              title={t('backupDr.costs.stats.projectedMonthly')}
              value={costs.projectedMonthlyEur}
              precision={2}
              prefix="€"
              style={{ marginTop: 12 }}
            />
          </Card>
        </Col>
      </Row>

      <Card title={t('backupDr.costs.tiersTitle')} style={{ marginTop: 16 }}>
        <Table<BackupStorageTierCostRowDto>
          rowKey="name"
          size="small"
          pagination={false}
          dataSource={costs.tiers}
          columns={columns}
        />
      </Card>

      <Card title={t('backupDr.costs.recommendationsTitle')} style={{ marginTop: 16 }}>
        <List
          dataSource={costs.recommendations}
          renderItem={(item: BackupStorageCostRecommendationDto) => {
            const titleKey = recommendationTitleKey(item.code);
            const descKey = recommendationDescKey(item.code);
            return (
              <List.Item
                extra={
                  item.savingsPercent > 0 ? (
                    <Tag color="green">
                      {t('backupDr.costs.saveTag', { percent: item.savingsPercent })}
                    </Tag>
                  ) : null
                }
              >
                <List.Item.Meta
                  avatar={<CheckCircleOutlined />}
                  title={titleKey ? t(titleKey) : item.title}
                  description={descKey ? t(descKey) : item.description}
                />
              </List.Item>
            );
          }}
        />
      </Card>
    </div>
  );
}

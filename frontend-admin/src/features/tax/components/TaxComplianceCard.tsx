'use client';

import { CheckCircleOutlined, ReloadOutlined, WarningOutlined } from '@ant-design/icons';
import { useQuery } from '@tanstack/react-query';
import { Alert, Badge, Button, Card, List, Space, Spin, Tag, Typography } from 'antd';
import React from 'react';

import {
  getTaxComplianceReport,
  taxComplianceQueryKey,
  type ComplianceIssue,
} from '@/features/tax/api/taxCompliance';
import { useI18n } from '@/i18n';

function severityAlertType(severity: string): 'error' | 'warning' | 'info' {
  const s = severity.toLowerCase();
  if (s === 'critical' || s === 'error') return 'error';
  if (s === 'warning') return 'warning';
  return 'info';
}

function issueTitle(t: (key: string, values?: Record<string, string | number>) => string, issue: ComplianceIssue) {
  const key = `settings.taxGroups.compliance.codes.${issue.code}`;
  const localized = t(key, { count: issue.affectedCount });
  if (localized !== key) return localized;
  return issue.message;
}

function issueAction(t: (key: string, values?: Record<string, string | number>) => string, issue: ComplianceIssue) {
  const key = `settings.taxGroups.compliance.actions.${issue.code}`;
  const localized = t(key);
  if (localized !== key) return localized;
  return issue.action;
}

export function TaxComplianceCard() {
  const { t } = useI18n();
  const { data, isLoading, isFetching, isError, refetch } = useQuery({
    queryKey: taxComplianceQueryKey,
    queryFn: getTaxComplianceReport,
    staleTime: 30_000,
  });

  const statusColor = data?.isCompliant ? 'success' : data ? 'error' : 'default';
  const statusText = data?.isCompliant
    ? t('settings.taxGroups.compliance.statusOk')
    : data
      ? t('settings.taxGroups.compliance.statusIssues')
      : t('settings.taxGroups.compliance.statusUnknown');

  return (
    <Card
      title={t('settings.taxGroups.compliance.cardTitle')}
      extra={
        <Button
          icon={<ReloadOutlined />}
          loading={isFetching}
          onClick={() => void refetch()}
        >
          {t('settings.taxGroups.compliance.runCheck')}
        </Button>
      }
    >
      {isLoading ? (
        <div style={{ textAlign: 'center', padding: 24 }}>
          <Spin />
        </div>
      ) : isError ? (
        <Alert type="error" showIcon title={t('settings.taxGroups.compliance.loadFailed')} />
      ) : data ? (
        <>
          <Space wrap size="middle" style={{ marginBottom: 16 }} align="center">
            <Badge
              status={statusColor === 'success' ? 'success' : statusColor === 'error' ? 'error' : 'default'}
              text={
                <Space size={6}>
                  {data.isCompliant ? <CheckCircleOutlined /> : <WarningOutlined />}
                  <span>{statusText}</span>
                </Space>
              }
            />
            <Typography.Text>
              {t('settings.taxGroups.compliance.summary', {
                compliant: data.compliantProducts,
                total: data.totalProducts,
              })}
            </Typography.Text>
            <Tag color={data.isCompliant ? 'success' : 'error'}>
              {data.nonCompliantProducts} {t('settings.taxGroups.compliance.nonCompliantLabel')}
            </Tag>
          </Space>

          {data.issues.length > 0 ? (
            <List
              dataSource={data.issues}
              renderItem={(issue) => (
                <List.Item style={{ paddingInline: 0, border: 'block' }}>
                  <Alert
                    type={severityAlertType(issue.severity)}
                    showIcon
                    title={issueTitle(t, issue)}
                    description={issueAction(t, issue)}
                    style={{ width: '100%' }}
                  />
                </List.Item>
              )}
            />
          ) : (
            <Alert
              type="success"
              showIcon
              title={t('settings.taxGroups.compliance.allGood')}
            />
          )}
        </>
      ) : null}
    </Card>
  );
}

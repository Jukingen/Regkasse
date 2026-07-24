'use client';

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  Alert,
  Button,
  Card,
  Col,
  Divider,
  List,
  Row,
  Select,
  Space,
  Statistic,
  Tag,
  Timeline,
  Typography,
} from 'antd';
import dayjs from 'dayjs';
import relativeTime from 'dayjs/plugin/relativeTime';
import { useState } from 'react';

import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { generateTseDrRunbook, getTseDrStatus, runTseDrDrill } from '@/features/tse-dr/api/dr';
import type { TseDrReport, TseDrRunbook } from '@/features/tse-dr/types';
import { listAdminTenants } from '@/features/super-admin/api/adminTenants';
import { useNotify } from '@/hooks/useNotify';
import { useI18n } from '@/i18n/I18nProvider';
import { adminOverviewCrumb } from '@/shared/adminShellLabels';
import { PERMISSIONS } from '@/shared/auth/permissions';
import { usePermissions } from '@/shared/auth/usePermissions';

dayjs.extend(relativeTime);

const DR_KEY = ['admin', 'tse-dr'] as const;

export default function TseDisasterRecoveryPage() {
  const { t } = useI18n();
  const notify = useNotify();
  const { hasPermission } = usePermissions();
  const allowed = hasPermission(PERMISSIONS.SYSTEM_CRITICAL);
  const queryClient = useQueryClient();
  const [tenantId, setTenantId] = useState<string | undefined>();
  const [scenario, setScenario] = useState('TSEFailure');
  const [lastReport, setLastReport] = useState<TseDrReport | null>(null);
  const [localRunbook, setLocalRunbook] = useState<TseDrRunbook | null>(null);

  const tenantsQuery = useQuery({
    queryKey: ['admin', 'tenants', 'tse-dr'],
    queryFn: () => listAdminTenants(false),
    enabled: allowed,
    staleTime: 60_000,
  });

  const statusQuery = useQuery({
    queryKey: [...DR_KEY, 'status', tenantId],
    queryFn: ({ signal }) => getTseDrStatus(tenantId!, signal),
    enabled: allowed && !!tenantId,
  });

  const drillMutation = useMutation({
    mutationFn: () => runTseDrDrill(tenantId!, scenario),
    onSuccess: async (report) => {
      setLastReport(report);
      setLocalRunbook(report.execution.runbook);
      if (report.success) {
        notify.success(t('tseDr.drillSuccess'));
      } else {
        notify.warning(t('tseDr.drillFailed'), { mode: 'notification', description: report.summary });
      }
      await queryClient.invalidateQueries({ queryKey: [...DR_KEY, 'status', tenantId] });
    },
    onError: (err) => {
      notify.apiError(err, {
        logContext: 'TseDr.runDrill',
        fallbackKey: 'common.errorGeneric',
      });
    },
  });

  const generateMutation = useMutation({
    mutationFn: () => generateTseDrRunbook(tenantId!, scenario),
    onSuccess: (runbook) => {
      setLocalRunbook(runbook);
      notify.success(t('tseDr.generateRunbook'));
    },
    onError: (err) => {
      notify.apiError(err, {
        logContext: 'TseDr.generateRunbook',
        fallbackKey: 'common.errorGeneric',
      });
    },
  });

  const status = statusQuery.data;
  const runbook = localRunbook ?? status?.latestRunbook ?? null;

  if (!allowed) {
    return <Alert type="error" showIcon message={t('tseDr.forbidden')} />;
  }

  return (
    <>
      <AdminPageHeader
        title={t('tseDr.title')}
        breadcrumbs={[adminOverviewCrumb(t), { title: t('tseDr.title') }]}
        extra={
          <Space wrap>
            <Select
              showSearch
              optionFilterProp="label"
              style={{ minWidth: 240 }}
              placeholder={t('tseDr.tenantLabel')}
              loading={tenantsQuery.isLoading}
              value={tenantId}
              onChange={(value) => {
                setTenantId(value);
                setLastReport(null);
                setLocalRunbook(null);
              }}
              options={(tenantsQuery.data ?? []).map((tenant) => ({
                value: tenant.id,
                label: `${tenant.name} (${tenant.slug})`,
              }))}
            />
            <Select
              style={{ minWidth: 180 }}
              value={scenario}
              onChange={setScenario}
              options={[
                { value: 'TSEFailure', label: t('tseDr.scenarioTseFailure') },
                { value: 'NetworkIsolation', label: t('tseDr.scenarioNetwork') },
                { value: 'DataCorruption', label: t('tseDr.scenarioCorruption') },
              ]}
            />
            <Button
              disabled={!tenantId}
              loading={generateMutation.isPending}
              onClick={() => generateMutation.mutate()}
            >
              {t('tseDr.generateRunbook')}
            </Button>
            <Button
              type="primary"
              danger
              disabled={!tenantId}
              loading={drillMutation.isPending}
              onClick={() => drillMutation.mutate()}
            >
              {t('tseDr.runDrill')}
            </Button>
          </Space>
        }
      >
        <Typography.Paragraph type="secondary" style={{ marginBottom: 0 }}>
          {t('tseDr.subtitle')}
        </Typography.Paragraph>
      </AdminPageHeader>

      {!tenantId ? (
        <Alert type="info" showIcon message={t('tseDr.emptySelect')} />
      ) : (
        <Card title={t('tseDr.cardTitle')} loading={statusQuery.isLoading}>
          <Alert type="info" showIcon message={t('tseDr.simulationNote')} style={{ marginBottom: 16 }} />

          {statusQuery.isError ? (
            <Alert type="error" showIcon message={t('tseDr.loadError')} />
          ) : status ? (
            <>
              <Row gutter={16} style={{ marginBottom: 16 }}>
                <Col xs={12} md={6}>
                  <Statistic
                    title={t('tseDr.drReady')}
                    value={status.isReady ? t('tseDr.yes') : t('tseDr.no')}
                    valueStyle={{ color: status.isReady ? '#52c41a' : '#cf1322' }}
                  />
                </Col>
                <Col xs={12} md={6}>
                  <Statistic
                    title={t('tseDr.lastDrill')}
                    value={
                      status.lastDrillAt ? dayjs(status.lastDrillAt).fromNow() : t('tseDr.never')
                    }
                  />
                </Col>
                <Col xs={12} md={6}>
                  <Statistic
                    title={t('tseDr.rtoTarget')}
                    value={`${status.rtoTargetMinutes} ${t('tseDr.minutes')}`}
                  />
                </Col>
                <Col xs={12} md={6}>
                  <Statistic
                    title={t('tseDr.rtoActual')}
                    value={`${status.rtoActualMinutes} ${t('tseDr.minutes')}`}
                  />
                </Col>
              </Row>

              <Typography.Text type="secondary">
                {t('tseDr.readiness')}: {status.readinessMessage}
              </Typography.Text>

              <Divider />

              <Button
                type="primary"
                danger
                onClick={() => drillMutation.mutate()}
                loading={drillMutation.isPending}
                style={{ marginBottom: 16 }}
              >
                {t('tseDr.runDrill')}
              </Button>

              {runbook ? (
                <Timeline
                  items={runbook.steps.map((step) => ({
                    color: step.isCompleted ? 'green' : step.error ? 'red' : 'gray',
                    children: (
                      <div>
                        <div style={{ fontWeight: 600 }}>{step.action}</div>
                        <div style={{ fontSize: 13, color: 'rgba(0,0,0,0.45)' }}>
                          {step.description}
                        </div>
                        <Space size={4} style={{ marginTop: 4 }}>
                          <Tag color={step.isAutomated ? 'blue' : 'default'}>
                            {step.isAutomated ? t('tseDr.automated') : t('tseDr.manual')}
                          </Tag>
                          {step.result ? <Typography.Text type="secondary">{step.result}</Typography.Text> : null}
                          {step.error ? <Typography.Text type="danger">{step.error}</Typography.Text> : null}
                        </Space>
                      </div>
                    ),
                  }))}
                />
              ) : null}

              {lastReport ? (
                <>
                  <Divider />
                  <Typography.Title level={5}>{t('tseDr.findingsTitle')}</Typography.Title>
                  <List
                    size="small"
                    dataSource={lastReport.findings}
                    renderItem={(item) => <List.Item>{item}</List.Item>}
                  />
                </>
              ) : null}
            </>
          ) : null}
        </Card>
      )}
    </>
  );
}

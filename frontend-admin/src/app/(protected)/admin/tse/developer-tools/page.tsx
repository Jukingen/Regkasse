'use client';

import { useMutation, useQuery } from '@tanstack/react-query';
import {
  Alert,
  Button,
  Card,
  InputNumber,
  Space,
  Tabs,
  Typography,
} from 'antd';
import { useState } from 'react';

import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import {
  TseActiveTenantTag,
  TseTenantRequiredAlert,
} from '@/features/tse-shared/components/TseTenantContextUi';
import { useTsePageTenant } from '@/features/tse-shared/hooks/useTsePageTenant';
import {
  generateTseTestData,
  getTseDeveloperToolsAvailability,
  runTseDiagnostics,
  simulateTseTraffic,
  validateTseConfig,
} from '@/features/tse-developer-tools/api/developerTools';
import type { TseDevToolResult } from '@/features/tse-developer-tools/types';
import { useNotify } from '@/hooks/useNotify';
import { useI18n } from '@/i18n/I18nProvider';
import { buildPlatformAdminBreadcrumbs } from '@/shared/adminPlatformBreadcrumbs';
import { PERMISSIONS } from '@/shared/auth/permissions';
import { usePermissions } from '@/shared/auth/usePermissions';

function alertType(severity: string, isSuccess: boolean): 'success' | 'warning' | 'error' | 'info' {
  if (severity === 'Error' || !isSuccess) return 'error';
  if (severity === 'Warning') return 'warning';
  if (isSuccess) return 'success';
  return 'info';
}

function ResultList({ result }: { result: TseDevToolResult | null }) {
  const { t } = useI18n();
  if (!result) return null;

  return (
    <Space orientation="vertical" style={{ width: '100%', marginTop: 16 }}>
      <Alert
        type={result.success ? 'success' : 'error'}
        showIcon
        title={t('tseDeveloperTools.summary')}
        description={result.summary}
      />
      {result.results.map((item) => (
        <Alert
          key={item.id}
          type={alertType(item.severity, item.isSuccess)}
          showIcon
          title={item.name}
          description={item.details}
        />
      ))}
    </Space>
  );
}

export default function TseDeveloperToolsPage() {
  const { t } = useI18n();
  const notify = useNotify();
  const { hasPermission } = usePermissions();
  const allowed = hasPermission(PERMISSIONS.SYSTEM_CRITICAL);
  const { tenantId, isReady } = useTsePageTenant();
  const [trafficCount, setTrafficCount] = useState<number>(25);
  const [lastResult, setLastResult] = useState<TseDevToolResult | null>(null);

  const availabilityQuery = useQuery({
    queryKey: ['admin', 'tse-developer-tools', 'availability'],
    queryFn: ({ signal }) => getTseDeveloperToolsAvailability(signal),
    enabled: allowed,
    staleTime: 30_000,
  });

  const toolsEnabled = availabilityQuery.data?.enabled === true;

  const onToolSuccess = (result: TseDevToolResult, successKey: string) => {
    setLastResult(result);
    if (result.success) {
      notify.success(t(successKey));
    } else {
      notify.warning(result.summary, { mode: 'notification' });
    }
  };

  const onToolError = (err: unknown, logContext: string) => {
    notify.apiError(err, {
      logContext,
      fallbackKey: 'common.errorGeneric',
    });
  };

  const diagnosticsMutation = useMutation({
    mutationFn: () => runTseDiagnostics(tenantId!),
    onSuccess: (result) => onToolSuccess(result, 'tseDeveloperTools.successDiagnostics'),
    onError: (err) => onToolError(err, 'TseDeveloperTools.diagnostics'),
  });

  const trafficMutation = useMutation({
    mutationFn: () => simulateTseTraffic(tenantId!, trafficCount),
    onSuccess: (result) => onToolSuccess(result, 'tseDeveloperTools.successTraffic'),
    onError: (err) => onToolError(err, 'TseDeveloperTools.simulateTraffic'),
  });

  const configMutation = useMutation({
    mutationFn: () => validateTseConfig(tenantId!),
    onSuccess: (result) => onToolSuccess(result, 'tseDeveloperTools.successConfig'),
    onError: (err) => onToolError(err, 'TseDeveloperTools.validateConfig'),
  });

  const testDataMutation = useMutation({
    mutationFn: () => generateTseTestData(tenantId!),
    onSuccess: (result) => onToolSuccess(result, 'tseDeveloperTools.successTestData'),
    onError: (err) => onToolError(err, 'TseDeveloperTools.generateTestData'),
  });

  if (!allowed) {
    return <Alert type="error" showIcon title={t('tseDeveloperTools.forbidden')} />;
  }

  return (
    <>
      <AdminPageHeader
        title={t('tseDeveloperTools.title')}
        breadcrumbs={buildPlatformAdminBreadcrumbs(t, 'securityTse', { title: t('tseDeveloperTools.title') })}
        extra={<TseActiveTenantTag />}
      >
        <Typography.Paragraph type="secondary" style={{ marginBottom: 0 }}>
          {t('tseDeveloperTools.subtitle')}
        </Typography.Paragraph>
      </AdminPageHeader>

      <Alert
        type="info"
        showIcon
        style={{ marginBottom: 16 }}
        title={t('tseDeveloperTools.devOnly')}
        description={
          availabilityQuery.data
            ? `${availabilityQuery.data.environmentName}: ${availabilityQuery.data.message}`
            : undefined
        }
      />

      {!toolsEnabled && availabilityQuery.isSuccess ? (
        <Alert type="warning" showIcon title={t('tseDeveloperTools.disabledEnv')} />
      ) : null}

      {!isReady ? (
        <TseTenantRequiredAlert emptySelectKey="tseDeveloperTools.emptySelect" />
      ) : (
        <Card title={t('tseDeveloperTools.cardTitle')}>
          <Tabs
            items={[
              {
                key: 'diagnostics',
                label: t('tseDeveloperTools.tabDiagnostics'),
                children: (
                  <>
                    <Button
                      type="primary"
                      disabled={!toolsEnabled}
                      loading={diagnosticsMutation.isPending}
                      onClick={() => diagnosticsMutation.mutate()}
                    >
                      {t('tseDeveloperTools.runDiagnostics')}
                    </Button>
                    {lastResult?.operation === 'Diagnostics' ? (
                      <ResultList result={lastResult} />
                    ) : null}
                  </>
                ),
              },
              {
                key: 'traffic',
                label: t('tseDeveloperTools.tabTraffic'),
                children: (
                  <Space orientation="vertical" size="middle">
                    <Typography.Text type="secondary">
                      {t('tseDeveloperTools.trafficHint')}
                    </Typography.Text>
                    <Space wrap>
                      <InputNumber
                        min={1}
                        max={1000}
                        value={trafficCount}
                        onChange={(value) => setTrafficCount(value ?? 25)}
                        addonBefore={t('tseDeveloperTools.trafficCount')}
                      />
                      <Button
                        type="primary"
                        disabled={!toolsEnabled}
                        loading={trafficMutation.isPending}
                        onClick={() => trafficMutation.mutate()}
                      >
                        {t('tseDeveloperTools.simulateTraffic')}
                      </Button>
                    </Space>
                    {lastResult?.operation === 'SimulateTraffic' ? (
                      <ResultList result={lastResult} />
                    ) : null}
                  </Space>
                ),
              },
              {
                key: 'config',
                label: t('tseDeveloperTools.tabConfig'),
                children: (
                  <>
                    <Button
                      type="primary"
                      disabled={!toolsEnabled}
                      loading={configMutation.isPending}
                      onClick={() => configMutation.mutate()}
                    >
                      {t('tseDeveloperTools.validateConfig')}
                    </Button>
                    {lastResult?.operation === 'ValidateConfig' ? (
                      <ResultList result={lastResult} />
                    ) : null}
                  </>
                ),
              },
              {
                key: 'test-data',
                label: t('tseDeveloperTools.tabTestData'),
                children: (
                  <Space orientation="vertical" size="middle">
                    <Typography.Text type="secondary">
                      {t('tseDeveloperTools.testDataHint')}
                    </Typography.Text>
                    <Button
                      type="primary"
                      disabled={!toolsEnabled}
                      loading={testDataMutation.isPending}
                      onClick={() => testDataMutation.mutate()}
                    >
                      {t('tseDeveloperTools.generateTestData')}
                    </Button>
                    {lastResult?.operation === 'GenerateTestData' ? (
                      <ResultList result={lastResult} />
                    ) : null}
                  </Space>
                ),
              },
            ]}
          />
        </Card>
      )}
    </>
  );
}

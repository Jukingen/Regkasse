'use client';

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  Alert,
  Button,
  Card,
  List,
  Select,
  Space,
  Tabs,
  Typography,
} from 'antd';
import { useState } from 'react';

import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import {
  clearTseTrainingConsole,
  getTseTrainingConsole,
  getTseTrainingEnvironment,
  resetTseTrainingSimulation,
  simulateTseTrainingFailure,
  startTseTrainingModule,
} from '@/features/tse-training/api/training';
import { SimulationConsole } from '@/features/tse-training/components/SimulationConsole';
import type { TseTrainingFailureType } from '@/features/tse-training/types';
import { getTseDevices } from '@/features/tse-management/api/tseManagement';
import { useNotify } from '@/hooks/useNotify';
import { useI18n } from '@/i18n/I18nProvider';
import { adminOverviewCrumb } from '@/shared/adminShellLabels';
import { PERMISSIONS } from '@/shared/auth/permissions';
import { usePermissions } from '@/shared/auth/usePermissions';

const KEY = ['admin', 'tse-training'] as const;

function moduleTitle(t: (k: string) => string, id: string, fallback: string): string {
  const key = `tseTraining.modules.${id}.title`;
  const translated = t(key);
  return translated === key ? fallback : translated;
}

function moduleDescription(t: (k: string) => string, id: string, fallback: string): string {
  const key = `tseTraining.modules.${id}.description`;
  const translated = t(key);
  return translated === key ? fallback : translated;
}

export default function TseTrainingPage() {
  const { t } = useI18n();
  const notify = useNotify();
  const queryClient = useQueryClient();
  const { hasPermission } = usePermissions();
  const allowed = hasPermission(PERMISSIONS.SYSTEM_CRITICAL);
  const [deviceId, setDeviceId] = useState<string | undefined>();

  const envQuery = useQuery({
    queryKey: [...KEY, 'env'],
    queryFn: ({ signal }) => getTseTrainingEnvironment(signal),
    enabled: allowed,
  });

  const consoleQuery = useQuery({
    queryKey: [...KEY, 'console'],
    queryFn: ({ signal }) => getTseTrainingConsole(100, signal),
    enabled: allowed,
    refetchInterval: 5_000,
  });

  const devicesQuery = useQuery({
    queryKey: [...KEY, 'devices'],
    queryFn: ({ signal }) => getTseDevices(signal),
    enabled: allowed,
    staleTime: 30_000,
  });

  const startMutation = useMutation({
    mutationFn: (moduleId: string) => startTseTrainingModule(moduleId),
    onSuccess: async () => {
      notify.success(t('tseTraining.moduleCompleted'));
      await queryClient.invalidateQueries({ queryKey: [...KEY, 'env'] });
    },
    onError: (err) => {
      notify.apiError(err, {
        logContext: 'TseTraining.start',
        fallbackKey: 'common.errorGeneric',
      });
    },
  });

  const simulateMutation = useMutation({
    mutationFn: (failureType: TseTrainingFailureType) =>
      simulateTseTrainingFailure(deviceId!, failureType),
    onSuccess: async (result) => {
      if (result.success) {
        notify.success(t('tseTraining.simulateSuccess'));
      } else {
        notify.warning(result.error || result.message, { mode: 'notification' });
      }
      await queryClient.invalidateQueries({ queryKey: [...KEY, 'console'] });
    },
    onError: (err) => {
      notify.apiError(err, {
        logContext: 'TseTraining.simulate',
        fallbackKey: 'common.errorGeneric',
      });
    },
  });

  const resetMutation = useMutation({
    mutationFn: () => resetTseTrainingSimulation(deviceId!),
    onSuccess: async (result) => {
      if (result.success) {
        notify.success(t('tseTraining.resetSuccess'));
      } else {
        notify.warning(result.error || result.message, { mode: 'notification' });
      }
      await queryClient.invalidateQueries({ queryKey: [...KEY, 'console'] });
    },
    onError: (err) => {
      notify.apiError(err, {
        logContext: 'TseTraining.reset',
        fallbackKey: 'common.errorGeneric',
      });
    },
  });

  const clearMutation = useMutation({
    mutationFn: () => clearTseTrainingConsole(),
    onSuccess: async () => {
      notify.success(t('tseTraining.consoleCleared'));
      await queryClient.invalidateQueries({ queryKey: [...KEY, 'console'] });
    },
    onError: (err) => {
      notify.apiError(err, {
        logContext: 'TseTraining.clearConsole',
        fallbackKey: 'common.errorGeneric',
      });
    },
  });

  const runSim = (failureType: TseTrainingFailureType) => {
    if (!deviceId) {
      notify.error(t('tseTraining.deviceRequired'));
      return;
    }
    simulateMutation.mutate(failureType);
  };

  if (!allowed) {
    return <Alert type="error" showIcon message={t('tseTraining.forbidden')} />;
  }

  const env = envQuery.data;
  const modules = env?.modules ?? [];
  const simulationEnabled = env?.simulationEnabled === true;

  return (
    <div className="space-y-4">
      <AdminPageHeader
        title={t('tseTraining.title')}
        breadcrumbs={[adminOverviewCrumb(t), { title: t('tseTraining.title') }]}
      />

      <Typography.Paragraph type="secondary">{t('tseTraining.subtitle')}</Typography.Paragraph>
      <Alert type="info" showIcon message={t('tseTraining.diagnosticNote')} />

      {envQuery.isError ? (
        <Alert type="error" showIcon message={t('tseTraining.loadError')} />
      ) : (
        <Card title={t('tseTraining.cardTitle')} loading={envQuery.isLoading}>
          {env ? (
            <Typography.Text type="secondary" className="mb-3 block">
              {t('tseTraining.progress', {
                completed: String(env.completedCount),
                total: String(env.totalCount),
              })}
            </Typography.Text>
          ) : null}

          <Tabs
            items={[
              {
                key: 'modules',
                label: t('tseTraining.tabModules'),
                children: (
                  <List
                    dataSource={modules}
                    renderItem={(module) => (
                      <List.Item>
                        <div className="flex w-full items-center justify-between gap-4">
                          <div>
                            <div className="font-medium">
                              {moduleTitle(t, module.id, module.title)}
                            </div>
                            <div className="text-sm text-neutral-500">
                              {moduleDescription(t, module.id, module.description)}
                            </div>
                          </div>
                          <Button
                            type="primary"
                            size="small"
                            disabled={module.isCompleted}
                            loading={
                              startMutation.isPending &&
                              startMutation.variables === module.id
                            }
                            onClick={() => startMutation.mutate(module.id)}
                          >
                            {module.isCompleted
                              ? t('tseTraining.completed')
                              : t('tseTraining.start')}
                          </Button>
                        </div>
                      </List.Item>
                    )}
                  />
                ),
              },
              {
                key: 'simulation',
                label: t('tseTraining.tabSimulation'),
                children: (
                  <Space direction="vertical" size="middle" style={{ width: '100%' }}>
                    {!simulationEnabled ? (
                      <Alert
                        type="warning"
                        showIcon
                        message={t('tseTraining.simulationDevOnly')}
                      />
                    ) : null}

                    <Select
                      showSearch
                      allowClear
                      style={{ minWidth: 320, maxWidth: 480 }}
                      placeholder={t('tseTraining.selectDevice')}
                      value={deviceId}
                      onChange={(v) => setDeviceId(v)}
                      options={(devicesQuery.data ?? []).map((d) => ({
                        value: d.id,
                        label: `${d.serialNumber}${d.tenantName ? ` — ${d.tenantName}` : ''}`,
                      }))}
                      optionFilterProp="label"
                    />

                    <div className="grid grid-cols-1 gap-3 sm:grid-cols-3">
                      <Button
                        danger
                        disabled={!simulationEnabled}
                        loading={simulateMutation.isPending}
                        onClick={() => runSim('NetworkTimeout')}
                      >
                        {t('tseTraining.btnNetworkTimeout')}
                      </Button>
                      <Button
                        danger
                        disabled={!simulationEnabled}
                        loading={simulateMutation.isPending}
                        onClick={() => runSim('CertificateExpiry')}
                      >
                        {t('tseTraining.btnCertificateExpiry')}
                      </Button>
                      <Button
                        danger
                        disabled={!simulationEnabled}
                        loading={simulateMutation.isPending}
                        onClick={() => runSim('SignatureError')}
                      >
                        {t('tseTraining.btnSignatureError')}
                      </Button>
                    </div>

                    <Space wrap>
                      <Button
                        disabled={!simulationEnabled || !deviceId}
                        loading={resetMutation.isPending}
                        onClick={() => resetMutation.mutate()}
                      >
                        {t('tseTraining.reset')}
                      </Button>
                      <Button
                        loading={clearMutation.isPending}
                        onClick={() => clearMutation.mutate()}
                      >
                        {t('tseTraining.clearConsole')}
                      </Button>
                    </Space>

                    <div className="mt-2">
                      <Typography.Text strong className="mb-2 block">
                        {t('tseTraining.consoleTitle')}
                      </Typography.Text>
                      <SimulationConsole
                        entries={consoleQuery.data ?? []}
                        loading={consoleQuery.isLoading}
                      />
                    </div>
                  </Space>
                ),
              },
            ]}
          />
        </Card>
      )}
    </div>
  );
}

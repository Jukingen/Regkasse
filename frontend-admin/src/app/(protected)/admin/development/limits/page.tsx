'use client';

import { useQuery } from '@tanstack/react-query';
import { Alert, Button, Card, Form, Select, Space } from 'antd';
import { useEffect } from 'react';

import { isDevelopment } from '@/features/auth/services/devTenant';
import { LimitEventLog } from '@/features/dev-limits/components/LimitEventLog';
import { LimitQuickEdit } from '@/features/dev-limits/components/LimitQuickEdit';
import { LimitStatusGrid } from '@/features/dev-limits/components/LimitStatusGrid';
import { LimitTestScenarios } from '@/features/dev-limits/components/LimitTestScenarios';
import {
  DEV_LIMIT_FIELD_META,
  DEV_LIMIT_SCENARIO_LABEL_KEYS,
  type DevLimitKey,
  type DevLimitScenario,
} from '@/features/dev-limits/constants/limitKeys';
import { useDevLimitMutations, useDevLimitStatus } from '@/features/dev-limits/hooks/useDevLimits';
import { useLimitEventLog } from '@/features/dev-limits/hooks/useLimitEventLog';
import { listAdminTenants } from '@/features/super-admin/api/adminTenants';
import { useTenant } from '@/features/tenancy/providers/TenantProvider';
import { useNotify } from '@/hooks/useNotify';
import { useI18n } from '@/i18n';
import { NotFoundAccessView } from '@/shared/auth/NotFoundAccessView';
import { ApiErrorAlertDescription } from '@/shared/errors/ApiErrorAlertDescription';

type PanelForm = { tenantId: string };

export default function DevelopmentLimitsPage() {
  const { t } = useI18n();
  const notify = useNotify();
  const [form] = Form.useForm<PanelForm>();
  const { tenant: contextTenant } = useTenant();
  const tenantId = Form.useWatch('tenantId', form);
  const statusQuery = useDevLimitStatus(tenantId);
  const mutations = useDevLimitMutations();
  const log = useLimitEventLog();

  useEffect(() => {
    if (!contextTenant?.id) return;
    const current = form.getFieldValue('tenantId') as string | undefined;
    if (current !== contextTenant.id) {
      form.setFieldsValue({ tenantId: contextTenant.id });
    }
  }, [contextTenant?.id, form]);

  const tenantsQuery = useQuery({
    queryKey: ['admin', 'tenants', false],
    queryFn: () => listAdminTenants(false),
    enabled: isDevelopment(),
  });

  if (!isDevelopment()) {
    return (
      <div style={{ padding: 24 }}>
        <Alert type="warning" title={t('tenants.limits.devPanel.productionBlocked')} showIcon />
        <NotFoundAccessView compact />
      </div>
    );
  }

  const requireTenantId = (): string | null => {
    const id = form.getFieldValue('tenantId') as string | undefined;
    if (!id) {
      notify.warning('tenants.limits.devPanel.noTenantSelected');
      return null;
    }
    return id;
  };

  const handleSet = async (limitKey: DevLimitKey, value: number) => {
    const id = requireTenantId();
    if (!id) return;
    try {
      await mutations.setMutation.mutateAsync({ tenantId: id, limitKey, value });
      log.append('set', `${limitKey}=${value}`);
      notify.successKey('tenants.limits.devPanel.saved');
    } catch (err) {
      notify.apiError(err, {
        logContext: 'DevLimits.set',
        fallbackKey: 'tenants.limits.devPanel.error',
      });
    }
  };

  const handleResetAll = async () => {
    const id = requireTenantId();
    if (!id) return;
    try {
      await mutations.resetMutation.mutateAsync(id);
      log.append('reset', t('tenants.limits.devPanel.resetAll'));
      notify.successKey('tenants.limits.devPanel.saved');
    } catch (err) {
      notify.apiError(err, {
        logContext: 'DevLimits.reset',
        fallbackKey: 'tenants.limits.devPanel.error',
      });
    }
  };

  const handleScenario = async (scenario: DevLimitScenario, limitKey?: DevLimitKey) => {
    const id = requireTenantId();
    if (!id) return;
    try {
      await mutations.scenarioMutation.mutateAsync({ tenantId: id, scenario, limitKey });
      const keyLabel = limitKey
        ? t(DEV_LIMIT_FIELD_META[limitKey].labelKey)
        : t('tenants.limits.devPanel.allKeys');
      log.append('scenario', `${t(DEV_LIMIT_SCENARIO_LABEL_KEYS[scenario])} · ${keyLabel}`);
      notify.successKey('tenants.limits.devPanel.scenarioSuccess');
    } catch (err) {
      notify.apiError(err, {
        logContext: 'DevLimits.scenario',
        fallbackKey: 'tenants.limits.devPanel.error',
      });
    }
  };

  const handleClearCache = async () => {
    const id = requireTenantId();
    if (!id) return;
    try {
      await mutations.cacheMutation.mutateAsync(id);
      log.append('cache', t('tenants.limits.devPanel.clearCache'));
      notify.successKey('tenants.limits.devPanel.cacheCleared');
    } catch (err) {
      notify.apiError(err, {
        logContext: 'DevLimits.cache',
        fallbackKey: 'tenants.limits.devPanel.error',
      });
    }
  };

  return (
    <div style={{ padding: 24, display: 'flex', flexDirection: 'column', gap: 16 }}>
      <Alert
        title={t('tenants.limits.devPanel.alertTitle')}
        description={t('tenants.limits.devPanel.alertDescription')}
        type="warning"
        showIcon
      />
      <Form form={form} layout="vertical" initialValues={{ tenantId: contextTenant?.id }}>
        <Card
          title={t('tenants.limits.devPanel.selectTenant')}
          extra={
            <Space wrap>
              <Button onClick={() => void statusQuery.refetch()} loading={statusQuery.isFetching}>
                {t('tenants.limits.devPanel.refresh')}
              </Button>
              <Button onClick={() => void handleClearCache()} loading={mutations.cacheMutation.isPending}>
                {t('tenants.limits.devPanel.clearCache')}
              </Button>
            </Space>
          }
        >
          {!contextTenant ? (
            <Alert
              type="warning"
              showIcon
              title={t('tenants.limits.devPanel.noTenantSelected')}
              style={{ marginBottom: 16 }}
            />
          ) : null}
          <Form.Item
            name="tenantId"
            label={t('tenants.limits.devPanel.selectTenant')}
            rules={[{ required: true, message: t('tenants.limits.devPanel.noTenantSelected') }]}
            extra={
              contextTenant
                ? t('tenants.limits.devPanel.contextTenantHint', {
                    name: contextTenant.name,
                    slug: contextTenant.slug,
                  })
                : undefined
            }
          >
            <Select
              showSearch
              optionFilterProp="label"
              placeholder={t('tenants.limits.devPanel.selectTenantPlaceholder')}
              loading={tenantsQuery.isLoading}
              options={(tenantsQuery.data ?? [])
                .filter((row) => row.status === 'active')
                .map((row) => ({
                  value: row.id,
                  label: `${row.name} (${row.slug})`,
                }))}
            />
          </Form.Item>
        </Card>
      </Form>

      {statusQuery.isError ? (
        <Alert
          type="error"
          showIcon
          title={t('tenants.limits.devPanel.loadFailed')}
          description={
            <ApiErrorAlertDescription
              t={t}
              error={statusQuery.error}
              logContext="DevLimits.status"
              fallbackKey="tenants.limits.devPanel.loadFailedHint"
            />
          }
          action={
            <Button size="small" loading={statusQuery.isFetching} onClick={() => void statusQuery.refetch()}>
              {t('common.buttons.retry')}
            </Button>
          }
        />
      ) : (
        <LimitStatusGrid usage={statusQuery.data} loading={statusQuery.isLoading} />
      )}
      <LimitQuickEdit
        usage={statusQuery.data}
        disabled={!tenantId}
        saving={mutations.setMutation.isPending}
        resetting={mutations.resetMutation.isPending}
        onApply={handleSet}
        onResetAll={handleResetAll}
      />
      <LimitTestScenarios
        disabled={!tenantId}
        loading={mutations.scenarioMutation.isPending}
        onTrigger={handleScenario}
      />
      <LimitEventLog entries={log.entries} onClear={log.clear} />
    </div>
  );
}

'use client';

import { useEffect, useMemo, useState } from 'react';
import {
  Alert,
  Badge,
  Button,
  Card,
  DatePicker,
  Input,
  Select,
  Space,
  Typography,
} from 'antd';
import type { Dayjs } from 'dayjs';
import dayjs from 'dayjs';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { useNotify } from '@/hooks/useNotify';
import {
  updateTenantOperationMode,
  type AdminTenantDetail,
  type TenantOperationMode,
} from '../api/adminTenants';
import {
  ADMIN_TENANTS_QUERY_KEY,
  TENANT_DETAIL_QUERY_KEY,
} from '../utils/invalidateTenantLifecycleQueries';

const { Text } = Typography;
const { TextArea } = Input;

type Props = {
  tenant: AdminTenantDetail;
  onUpdated?: () => void;
};

type ModeOption = {
  value: TenantOperationMode;
  label: string;
  color: string;
};

function toLocalInput(iso: string | null | undefined): Dayjs | null {
  if (!iso) return null;
  const d = dayjs(iso);
  return d.isValid() ? d : null;
}

export function TenantOperationModeCard({ tenant, onUpdated }: Props) {
  const { t } = useTranslation('tenants');
  const notify = useNotify();
  const queryClient = useQueryClient();

  const [mode, setMode] = useState<TenantOperationMode>(
    (tenant.operationMode as TenantOperationMode) || 'active'
  );
  const [maintenanceMessage, setMaintenanceMessage] = useState(
    tenant.maintenanceMessage ?? ''
  );
  const [maintenanceEnds, setMaintenanceEnds] = useState<Dayjs | null>(
    toLocalInput(tenant.maintenanceEndsAt)
  );

  useEffect(() => {
    setMode((tenant.operationMode as TenantOperationMode) || 'active');
    setMaintenanceMessage(tenant.maintenanceMessage ?? '');
    setMaintenanceEnds(toLocalInput(tenant.maintenanceEndsAt));
  }, [
    tenant.operationMode,
    tenant.maintenanceMessage,
    tenant.maintenanceEndsAt,
    tenant.id,
  ]);

  const modeOptions = useMemo<ModeOption[]>(
    () => [
      {
        value: 'active',
        label: t('detail.operationMode.options.active'),
        color: 'green',
      },
      {
        value: 'readonly',
        label: t('detail.operationMode.options.readonly'),
        color: 'orange',
      },
      {
        value: 'maintenance',
        label: t('detail.operationMode.options.maintenance'),
        color: 'red',
      },
    ],
    [t]
  );

  const mutation = useMutation({
    mutationFn: () =>
      updateTenantOperationMode(tenant.id, {
        operationMode: mode,
        maintenanceMessage:
          mode === 'maintenance' ? maintenanceMessage.trim() || null : null,
        maintenanceEndsAt:
          mode === 'maintenance' && maintenanceEnds
            ? maintenanceEnds.toISOString()
            : null,
        maintenanceStartedAt:
          mode === 'maintenance' ? (tenant.maintenanceStartedAt ?? null) : null,
      }),
    onSuccess: async () => {
      notify.success(t('detail.operationMode.saveSuccess'));
      await queryClient.invalidateQueries({
        queryKey: [...TENANT_DETAIL_QUERY_KEY, tenant.id],
      });
      await queryClient.invalidateQueries({ queryKey: ADMIN_TENANTS_QUERY_KEY });
      onUpdated?.();
    },
    onError: (err) => {
      notify.apiError(err, {
        logContext: 'TenantOperationMode.save',
        fallbackKey: 'tenants:detail.operationMode.saveError',
      });
    },
  });

  const dirty =
    mode !== ((tenant.operationMode as TenantOperationMode) || 'active') ||
    (mode === 'maintenance' &&
      (maintenanceMessage !== (tenant.maintenanceMessage ?? '') ||
        (maintenanceEnds?.toISOString() ?? null) !==
          (tenant.maintenanceEndsAt
            ? dayjs(tenant.maintenanceEndsAt).toISOString()
            : null)));

  return (
    <Card title={t('detail.operationMode.title')} size="small">
      <Space orientation="vertical" size="middle" style={{ width: '100%' }}>
        <div>
          <Text type="secondary" style={{ display: 'block', marginBottom: 8 }}>
            {t('detail.operationMode.description')}
          </Text>
          <Select
            value={mode}
            onChange={(value) => setMode(value)}
            style={{ width: '100%', maxWidth: 360 }}
            options={modeOptions}
            optionRender={(option) => (
              <Space>
                <Badge color={option.data?.color} />
                {option.data?.label}
              </Space>
            )}
            labelRender={(props) => {
              const opt = modeOptions.find((o) => o.value === props.value);
              return (
                <Space>
                  <Badge color={opt?.color} />
                  {opt?.label ?? props.label}
                </Space>
              );
            }}
          />
        </div>

        {mode === 'readonly' && (
          <Alert
            type="warning"
            showIcon
            message={t('detail.operationMode.readonlyAlertTitle')}
            description={t('detail.operationMode.readonlyAlertDescription')}
          />
        )}

        {mode === 'maintenance' && (
          <Alert
            type="warning"
            showIcon
            message={t('detail.operationMode.maintenanceAlertTitle')}
            description={
              <Space orientation="vertical" size="small" style={{ width: '100%' }}>
                <Text>{t('detail.operationMode.maintenanceAlertDescription')}</Text>
                <TextArea
                  rows={3}
                  placeholder={t('detail.operationMode.messagePlaceholder')}
                  value={maintenanceMessage}
                  onChange={(e) => setMaintenanceMessage(e.target.value)}
                  maxLength={2000}
                  showCount
                />
                <Space wrap>
                  <Text>{t('detail.operationMode.endsAtLabel')}</Text>
                  <DatePicker
                    showTime
                    value={maintenanceEnds}
                    onChange={(value) => setMaintenanceEnds(value)}
                    placeholder={t('detail.operationMode.endsAtPlaceholder')}
                  />
                </Space>
              </Space>
            }
          />
        )}

        <Button
          type="primary"
          onClick={() => mutation.mutate()}
          loading={mutation.isPending}
          disabled={!dirty && !mutation.isPending}
        >
          {t('detail.operationMode.save')}
        </Button>
      </Space>
    </Card>
  );
}

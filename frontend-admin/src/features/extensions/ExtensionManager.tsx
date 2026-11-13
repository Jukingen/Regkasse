'use client';

import { useMutation, useQueryClient } from '@tanstack/react-query';
import { Alert, Input, InputNumber, Select, Switch, Table, Tag } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import Link from 'next/link';

import {
  setTenantExtension,
  tenantExtensionQueryKeys,
  useTenantExtensions,
  type LateCheckoutFeeOptions,
  type TenantExtensionStatus,
} from '@/api/admin/tenantExtensions';
import { HAIR_SALON_CAMPAIGN_EXTENSION } from '@/api/admin/hairSalonCampaigns';
import { useAuth } from '@/features/auth/hooks/useAuth';
import { useNotify } from '@/hooks/useNotify';
import { useI18n } from '@/i18n';
import { PERMISSIONS, hasPermission } from '@/shared/auth/permissions';

const MINIBAR_AUTO_CHARGE_EXTENSION = 'minibar_auto_charge';
const BREAKFAST_INCLUDED_EXTENSION = 'breakfast_included';
const LATE_CHECKOUT_FEE_EXTENSION = 'late_checkout_fee';

const DEFAULT_LATE_CHECKOUT: LateCheckoutFeeOptions = {
  freeUntilLocalTime: '11:00',
  fee: 0,
  timeZone: 'Europe/Vienna',
};

/** Keep in step with LateCheckoutFeePolicy.CommonTimeZones. */
const LATE_CHECKOUT_TIME_ZONES = [
  'Europe/Vienna',
  'Europe/Berlin',
  'Europe/Zurich',
  'Europe/Paris',
  'Europe/Rome',
  'Europe/Amsterdam',
  'Europe/London',
  'Europe/Prague',
  'Europe/Warsaw',
  'Europe/Budapest',
  'UTC',
] as const;

function minibarRequiresSettlement(row: TenantExtensionStatus): boolean {
  const options = row.options;
  if (options && 'requireSettlementBeforeClose' in options)
    return options.requireSettlementBeforeClose !== false;
  return true;
}

function breakfastPrice(row: TenantExtensionStatus): number {
  const options = row.options;
  if (options && 'pricePerPersonPerNight' in options)
    return options.pricePerPersonPerNight;
  return 0;
}

function lateCheckoutOptions(row: TenantExtensionStatus): LateCheckoutFeeOptions {
  const options = row.options;
  if (options && 'freeUntilLocalTime' in options && 'timeZone' in options) {
    return {
      freeUntilLocalTime: options.freeUntilLocalTime || DEFAULT_LATE_CHECKOUT.freeUntilLocalTime,
      fee: typeof options.fee === 'number' ? options.fee : 0,
      timeZone: options.timeZone || DEFAULT_LATE_CHECKOUT.timeZone,
    };
  }
  return DEFAULT_LATE_CHECKOUT;
}

const DESCRIPTION_KEYS: Record<string, string> = {
  [HAIR_SALON_CAMPAIGN_EXTENSION]: 'admin.extensions.descriptions.hair_salon_campaign',
};

export function extensionDescription(
  t: (key: string) => string,
  name: string
): string {
  const key = DESCRIPTION_KEYS[name];
  return key ? t(key) : name;
}

export function ExtensionManager() {
  const { t } = useI18n();
  const { user } = useAuth();
  const notify = useNotify();
  const queryClient = useQueryClient();
  const canWrite = hasPermission(user, PERMISSIONS.SETTINGS_MANAGE);
  const extensions = useTenantExtensions();

  const toggle = useMutation({
    mutationFn: (body: { name: string; enabled: boolean }) => setTenantExtension(body),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: tenantExtensionQueryKeys.all });
      notify.successKey('admin.extensions.saved');
    },
    onError: (err) =>
      notify.apiError(err, { logContext: 'ExtensionManager.toggle', fallbackKey: 'admin.extensions.saveError' }),
  });

  const settlement = useMutation({
    mutationFn: (input: { row: TenantExtensionStatus; requireSettlementBeforeClose: boolean }) =>
      setTenantExtension({
        name: input.row.name,
        enabled: input.row.enabled,
        options: { requireSettlementBeforeClose: input.requireSettlementBeforeClose },
      }),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: tenantExtensionQueryKeys.all });
      notify.successKey('admin.extensions.saved');
    },
    onError: (err) =>
      notify.apiError(err, {
        logContext: 'ExtensionManager.minibarOptions',
        fallbackKey: 'admin.extensions.saveError',
      }),
  });

  const breakfast = useMutation({
    mutationFn: (input: { row: TenantExtensionStatus; pricePerPersonPerNight: number }) =>
      setTenantExtension({
        name: input.row.name,
        enabled: input.row.enabled,
        options: { pricePerPersonPerNight: input.pricePerPersonPerNight },
      }),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: tenantExtensionQueryKeys.all });
      notify.successKey('admin.extensions.saved');
    },
    onError: (err) =>
      notify.apiError(err, {
        logContext: 'ExtensionManager.breakfastOptions',
        fallbackKey: 'admin.extensions.saveError',
      }),
  });

  const lateCheckout = useMutation({
    mutationFn: (input: { row: TenantExtensionStatus; options: LateCheckoutFeeOptions }) =>
      setTenantExtension({
        name: input.row.name,
        enabled: input.row.enabled,
        options: input.options,
      }),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: tenantExtensionQueryKeys.all });
      notify.successKey('admin.extensions.saved');
    },
    onError: (err) =>
      notify.apiError(err, {
        logContext: 'ExtensionManager.lateCheckoutOptions',
        fallbackKey: 'admin.extensions.saveError',
      }),
  });

  const columns: ColumnsType<TenantExtensionStatus> = [
    {
      title: t('admin.extensions.colName'),
      dataIndex: 'name',
      key: 'name',
    },
    {
      title: t('admin.extensions.colDescription'),
      key: 'description',
      render: (_value, row) => extensionDescription(t, row.name),
    },
    {
      title: t('admin.extensions.colStatus'),
      dataIndex: 'enabled',
      key: 'status',
      render: (enabled: boolean) => (
        <Tag color={enabled ? 'green' : 'default'}>
          {enabled ? t('admin.extensions.enabled') : t('admin.extensions.disabled')}
        </Tag>
      ),
    },
    {
      title: t('admin.extensions.colActions'),
      key: 'actions',
      render: (_value, row) => (
        <>
          <Switch
            checked={row.enabled}
            disabled={!canWrite || toggle.isPending}
            aria-label={row.name}
            onChange={(enabled) => toggle.mutate({ name: row.name, enabled })}
          />
          {row.name === MINIBAR_AUTO_CHARGE_EXTENSION ? (
            <div style={{ marginTop: 8 }}>
              <Alert
                type="info"
                showIcon
                data-testid="minibar-settlement-alert"
                title={t('admin.extensions.minibarSettlementInfo')}
                style={{ marginBottom: 8 }}
              />
              <Switch
                checked={minibarRequiresSettlement(row)}
                disabled={!canWrite || settlement.isPending}
                aria-label={t('admin.extensions.minibarSettlementSwitch')}
                onChange={(requireSettlementBeforeClose) =>
                  settlement.mutate({ row, requireSettlementBeforeClose })
                }
              />
            </div>
          ) : null}
          {row.name === BREAKFAST_INCLUDED_EXTENSION && row.enabled ? (
            <div style={{ marginTop: 8 }}>
              <InputNumber
                min={0}
                step={0.01}
                style={{ width: '100%' }}
                aria-label={t('admin.extensions.breakfastPriceLabel')}
                value={breakfastPrice(row)}
                disabled={!canWrite || breakfast.isPending}
                onChange={(value) =>
                  breakfast.mutate({
                    row,
                    pricePerPersonPerNight: typeof value === 'number' ? value : 0,
                  })
                }
              />
            </div>
          ) : null}
          {row.name === LATE_CHECKOUT_FEE_EXTENSION && row.enabled ? (
            <div style={{ marginTop: 8, display: 'grid', gap: 8 }}>
              <Input
                type="time"
                aria-label={t('admin.extensions.lateCheckoutFreeUntil')}
                value={lateCheckoutOptions(row).freeUntilLocalTime}
                disabled={!canWrite || lateCheckout.isPending}
                onChange={(event) =>
                  lateCheckout.mutate({
                    row,
                    options: {
                      ...lateCheckoutOptions(row),
                      freeUntilLocalTime: event.target.value || DEFAULT_LATE_CHECKOUT.freeUntilLocalTime,
                    },
                  })
                }
              />
              <InputNumber
                min={0}
                step={0.01}
                style={{ width: '100%' }}
                aria-label={t('admin.extensions.lateCheckoutFee')}
                value={lateCheckoutOptions(row).fee}
                disabled={!canWrite || lateCheckout.isPending}
                onChange={(value) =>
                  lateCheckout.mutate({
                    row,
                    options: {
                      ...lateCheckoutOptions(row),
                      fee: typeof value === 'number' ? value : 0,
                    },
                  })
                }
              />
              <Select
                aria-label={t('admin.extensions.lateCheckoutTimeZone')}
                value={lateCheckoutOptions(row).timeZone}
                disabled={!canWrite || lateCheckout.isPending}
                options={LATE_CHECKOUT_TIME_ZONES.map((zone) => ({ value: zone, label: zone }))}
                onChange={(timeZone) =>
                  lateCheckout.mutate({
                    row,
                    options: { ...lateCheckoutOptions(row), timeZone },
                  })
                }
              />
            </div>
          ) : null}
          {row.name === HAIR_SALON_CAMPAIGN_EXTENSION && row.enabled ? (
            <Link href="/admin/extensions/hair-salon-campaigns" style={{ marginLeft: 12 }}>
              {t('admin.extensions.manageCampaigns')}
            </Link>
          ) : null}
        </>
      ),
    },
  ];

  if (extensions.isError) {
    return <Alert type="error" showIcon title={t('admin.extensions.loadError')} />;
  }

  return (
    <>
      {!canWrite ? (
        <Alert type="info" showIcon title={t('admin.extensions.readOnly')} style={{ marginBottom: 16 }} />
      ) : null}
      <Table
        rowKey="name"
        loading={extensions.isLoading}
        columns={columns}
        dataSource={extensions.data ?? []}
        pagination={false}
        locale={{ emptyText: t('admin.extensions.empty') }}
      />
    </>
  );
}

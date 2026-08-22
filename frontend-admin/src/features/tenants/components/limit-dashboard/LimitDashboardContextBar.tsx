'use client';

import { Card, Descriptions, Select, Space, Typography } from 'antd';

import type { AdminCashRegisterListItem } from '@/features/cash-registers/api/cashRegisters';
import type { AdminTenantListItem } from '@/features/super-admin/api/adminTenants';
import { LIMIT_DASHBOARD_ALL_TENANTS_VALUE } from '@/features/tenants/components/limit-dashboard/limitDashboardUrl';
import { useI18n } from '@/i18n';

export function LimitDashboardContextBar({
  isSuperAdmin,
  allTenants,
  tenantName,
  tenantSlug,
  tenants,
  tenantsLoading,
  selectedTenantId,
  onTenantChange,
  register,
  registerOptions,
  registersLoading,
  onRegisterChange,
  viewerName,
  viewerUserName,
}: {
  isSuperAdmin: boolean;
  allTenants: boolean;
  tenantName?: string | null;
  tenantSlug?: string | null;
  tenants: AdminTenantListItem[];
  tenantsLoading: boolean;
  selectedTenantId?: string;
  onTenantChange: (tenantId: string) => void;
  register: AdminCashRegisterListItem | null;
  registerOptions: { value: string; label: string }[];
  registersLoading: boolean;
  onRegisterChange: (registerId: string | undefined) => void;
  viewerName: string;
  viewerUserName?: string | null;
}) {
  const { t } = useI18n();
  const tenantLabel =
    tenantName && tenantSlug
      ? t('tenants.limits.dashboard.context.tenantValue', { name: tenantName, slug: tenantSlug })
      : tenantName || tenantSlug || t('tenants.limits.dashboard.context.noTenant');
  const registerLabel = register
    ? `${register.registerNumber} — ${register.location}`.trim()
    : t('tenants.limits.dashboard.context.allRegisters');
  const cashierLabel = viewerUserName ? `${viewerName} (${viewerUserName})` : viewerName;

  return (
    <Card variant="borderless" size="small">
      <Space orientation="vertical" size={12} style={{ width: '100%' }}>
        <Space wrap size={12}>
          {isSuperAdmin ? (
            <Select
              showSearch
              optionFilterProp="label"
              style={{ minWidth: 280 }}
              loading={tenantsLoading}
              value={allTenants ? LIMIT_DASHBOARD_ALL_TENANTS_VALUE : selectedTenantId}
              onChange={onTenantChange}
              aria-label={t('tenants.limits.dashboard.context.tenant')}
              options={[
                {
                  value: LIMIT_DASHBOARD_ALL_TENANTS_VALUE,
                  label: t('tenants.limits.dashboard.allTenants'),
                },
                ...tenants.map((row) => ({
                  value: row.id,
                  label: `${row.name} (${row.slug})`,
                })),
              ]}
            />
          ) : (
            <Typography.Text>
              {t('tenants.limits.dashboard.context.mandantLine', {
                name: tenantName || '—',
                slug: tenantSlug || '—',
              })}
            </Typography.Text>
          )}
          {allTenants ? null : (
            <Select
              allowClear
              showSearch
              optionFilterProp="label"
              style={{ minWidth: 240 }}
              loading={registersLoading}
              placeholder={t('tenants.limits.dashboard.context.allRegisters')}
              value={register?.id}
              onChange={(value) => onRegisterChange(value || undefined)}
              aria-label={t('tenants.limits.dashboard.context.register')}
              options={registerOptions}
            />
          )}
        </Space>

        <Descriptions size="small" column={{ xs: 1, sm: 3 }}>
          <Descriptions.Item label={t('tenants.limits.dashboard.context.tenant')}>
            {allTenants ? t('tenants.limits.dashboard.allTenants') : tenantLabel}
          </Descriptions.Item>
          <Descriptions.Item label={t('tenants.limits.dashboard.context.register')}>
            {allTenants ? t('tenants.limits.dashboard.context.allRegisters') : registerLabel}
          </Descriptions.Item>
          <Descriptions.Item label={t('tenants.limits.dashboard.context.cashier')}>
            {cashierLabel}
          </Descriptions.Item>
        </Descriptions>
      </Space>
    </Card>
  );
}

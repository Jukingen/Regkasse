'use client';

import { ClearOutlined } from '@ant-design/icons';
import { Badge, Button, Input, Select, Space } from 'antd';
import { useMemo, type ReactNode } from 'react';

import {
  LICENSE_SALES_PLAN_FILTERS,
  LICENSE_SALES_STATUS_FILTERS,
  type BillingSalesFilterState,
  type LicenseSalesPlanFilter,
  type LicenseSalesStatusFilter,
  countActiveBillingSalesFilters,
} from '@/features/billing/utils/billingSalesFilters';
import { useI18n } from '@/i18n';

export type BillingSalesFilterBarProps = {
  filters: BillingSalesFilterState;
  onChange: (next: BillingSalesFilterState) => void;
  onClear: () => void;
  tenantOptions: Array<{ value: string; label: string }>;
  tenantsLoading?: boolean;
  /** Extra controls (e.g. date range / refresh) rendered after the core filters. */
  extra?: ReactNode;
};

export function BillingSalesFilterBar({
  filters,
  onChange,
  onClear,
  tenantOptions,
  tenantsLoading,
  extra,
}: BillingSalesFilterBarProps) {
  const { t } = useI18n();
  const activeCount = countActiveBillingSalesFilters(filters);

  const statusOptions = useMemo(
    () =>
      LICENSE_SALES_STATUS_FILTERS.map((value) => ({
        value,
        label: t(`billing.licenseSales.filters.status.${value}`),
      })),
    [t]
  );

  const planOptions = useMemo(
    () =>
      LICENSE_SALES_PLAN_FILTERS.map((value) => ({
        value,
        label: t(`billing.licenseSales.filters.plan.${value}`),
      })),
    [t]
  );

  const patch = (partial: Partial<BillingSalesFilterState>) => {
    onChange({ ...filters, ...partial, page: 1 });
  };

  return (
    <Space wrap style={{ marginBottom: 16, width: '100%' }} size="middle">
      <Input.Search
        key={`search-${filters.search ?? ''}`}
        allowClear
        defaultValue={filters.search ?? ''}
        placeholder={t('billing.licenseSales.filters.searchPlaceholder')}
        onSearch={(value) => patch({ search: value.trim() || undefined })}
        style={{ width: 280 }}
      />
      <Select
        value={filters.status ?? 'all'}
        style={{ width: 160 }}
        options={statusOptions}
        aria-label={t('billing.licenseSales.filters.statusLabel')}
        onChange={(status: LicenseSalesStatusFilter) => patch({ status })}
      />
      <Select
        value={filters.plan ?? 'all'}
        style={{ width: 180 }}
        options={planOptions}
        aria-label={t('billing.licenseSales.filters.planLabel')}
        onChange={(plan: LicenseSalesPlanFilter) => patch({ plan })}
      />
      <Select
        allowClear
        showSearch
        optionFilterProp="label"
        loading={tenantsLoading}
        value={filters.tenantId}
        placeholder={t('billing.licenseSales.filters.tenantPlaceholder')}
        style={{ width: 240 }}
        options={tenantOptions}
        aria-label={t('billing.licenseSales.filters.tenantLabel')}
        onChange={(tenantId: string | undefined) => patch({ tenantId })}
      />
      {extra}
      <Badge count={activeCount} size="small" offset={[0, 4]}>
        <Button
          icon={<ClearOutlined />}
          disabled={activeCount === 0}
          onClick={onClear}
        >
          {t('billing.licenseSales.filters.clear')}
        </Button>
      </Badge>
      {activeCount > 0 ? (
        <span style={{ color: '#64748b', fontSize: 13 }}>
          {t('billing.licenseSales.filters.activeCount', { count: activeCount })}
        </span>
      ) : null}
    </Space>
  );
}

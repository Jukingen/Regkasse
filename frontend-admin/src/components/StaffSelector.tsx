'use client';

import type { SelectProps } from 'antd';
import { Select } from 'antd';

import { useCashierFilterOptions } from '@/features/reporting/hooks/useCashierFilterOptions';
import { useI18n } from '@/i18n';

export type StaffSelectorProps = {
  value?: string;
  onChange?: (value: string | undefined) => void;
  disabled?: boolean;
  placeholder?: string;
  style?: SelectProps['style'];
  className?: string;
};

/**
 * Server-backed staff/cashier picker for operational reporting filters.
 */
export function StaffSelector({
  value,
  onChange,
  disabled = false,
  placeholder,
  style,
  className,
}: StaffSelectorProps) {
  const { t } = useI18n();
  const { options, loading, onSearch } = useCashierFilterOptions();

  return (
    <Select
      className={className}
      style={{ minWidth: 200, ...style }}
      value={value}
      onChange={(next) => onChange?.(next ?? undefined)}
      disabled={disabled}
      allowClear
      showSearch
      filterOption={false}
      placeholder={placeholder ?? t('adminShell.reporting.cashierAll')}
      options={options}
      loading={loading}
      onSearch={onSearch}
    />
  );
}

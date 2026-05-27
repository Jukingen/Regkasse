'use client';

import { Select } from 'antd';
import type { SelectProps } from 'antd';
import { useI18n } from '@/i18n';
import { useAuditLogUserFilterOptions } from '@/features/audit-logs/hooks/useAuditLogUserFilterOptions';

export type UserFilterSelectProps = {
    value?: string;
    onChange: (userId: string | undefined) => void;
    placeholder?: string;
} & Pick<SelectProps, 'style' | 'className'>;

export function UserFilterSelect({ value, onChange, placeholder, style, className }: UserFilterSelectProps) {
    const { t } = useI18n();
    const { options, isLoading } = useAuditLogUserFilterOptions();

    return (
        <Select
            placeholder={placeholder ?? t('common.auditLogs.filterByUser')}
            style={{ minWidth: 180, ...style }}
            className={className}
            allowClear
            showSearch
            optionFilterProp="label"
            loading={isLoading}
            value={value}
            onChange={(next) => onChange(next ?? undefined)}
            options={options}
        />
    );
}

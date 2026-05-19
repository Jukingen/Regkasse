'use client';

import { useMemo } from 'react';
import { Select } from 'antd';
import type { SelectProps } from 'antd';
import { useI18n } from '@/i18n';
import {
    AUDIT_LOG_STATUS_FILTER_VALUES,
    type AuditLogStatusFilter,
} from '@/features/audit-logs/constants/auditLogFilters';

export type StatusFilterSelectProps = {
    value?: AuditLogStatusFilter;
    onChange: (status: AuditLogStatusFilter | undefined) => void;
} & Pick<SelectProps, 'style' | 'className'>;

export function StatusFilterSelect({ value, onChange, style, className }: StatusFilterSelectProps) {
    const { t } = useI18n();

    const options = useMemo(
        () =>
            AUDIT_LOG_STATUS_FILTER_VALUES.map((status) => ({
                value: status,
                label: t(`common.auditLogs.statusLabels.${status}` as 'common.auditLogs.statusLabels.Success'),
            })),
        [t],
    );

    return (
        <Select
            placeholder={t('common.auditLogs.filterByStatus')}
            style={{ width: '100%', minWidth: 140, ...style }}
            className={className}
            allowClear
            value={value}
            onChange={(next) => onChange((next ?? undefined) as AuditLogStatusFilter | undefined)}
            options={options}
        />
    );
}

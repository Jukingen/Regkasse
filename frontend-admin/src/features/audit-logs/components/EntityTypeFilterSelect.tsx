'use client';

import { useMemo } from 'react';
import { Select } from 'antd';
import type { SelectProps } from 'antd';
import { useI18n } from '@/i18n';
import {
    AUDIT_LOG_ENTITY_TYPE_FILTER_VALUES,
    type AuditLogEntityTypeFilter,
} from '@/features/audit-logs/constants/auditLogFilters';

export type EntityTypeFilterSelectProps = {
    value?: AuditLogEntityTypeFilter;
    onChange: (entityType: AuditLogEntityTypeFilter | undefined) => void;
} & Pick<SelectProps, 'style' | 'className'>;

export function EntityTypeFilterSelect({ value, onChange, style, className }: EntityTypeFilterSelectProps) {
    const { t } = useI18n();

    const options = useMemo(
        () =>
            AUDIT_LOG_ENTITY_TYPE_FILTER_VALUES.map((entityType) => ({
                value: entityType,
                label: entityType,
            })),
        [],
    );

    return (
        <Select
            placeholder={t('common.auditLogs.filterByEntity')}
            style={{ width: '100%', minWidth: 160, ...style }}
            className={className}
            allowClear
            showSearch
            optionFilterProp="label"
            value={value}
            onChange={(next) => onChange((next ?? undefined) as AuditLogEntityTypeFilter | undefined)}
            options={options}
        />
    );
}

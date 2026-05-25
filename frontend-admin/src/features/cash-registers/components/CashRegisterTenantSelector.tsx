'use client';

import { useMemo } from 'react';
import { Select } from 'antd';

import type { AdminTenantListItem } from '@/features/super-admin/api/adminTenants';

const ALL_TENANTS_VALUE = '__all_tenants__';

export type CashRegisterTenantSelectorProps = {
    value?: string;
    onChange: (tenantId: string | undefined) => void;
    tenants: AdminTenantListItem[];
    loading?: boolean;
};

export function CashRegisterTenantSelector({
    value,
    onChange,
    tenants,
    loading = false,
}: CashRegisterTenantSelectorProps) {
    const options = useMemo(
        () => [
            { label: '📋 Alle Mandanten', value: ALL_TENANTS_VALUE },
            ...tenants.map((tenant) => ({
                label: `🏢 ${tenant.name} (${tenant.slug})`,
                value: tenant.id,
            })),
        ],
        [tenants],
    );

    return (
        <Select
            placeholder="Alle Mandanten anzeigen"
            style={{ width: 280 }}
            allowClear
            value={value}
            onChange={(nextValue) => onChange(nextValue === ALL_TENANTS_VALUE ? undefined : nextValue)}
            showSearch
            optionFilterProp="label"
            options={options}
            loading={loading}
        />
    );
}

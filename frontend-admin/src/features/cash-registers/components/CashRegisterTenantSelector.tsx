'use client';

import { useMemo } from 'react';
import { Select } from 'antd';

import type { AdminTenantListItem } from '@/features/super-admin/api/adminTenants';
import { useI18n } from '@/i18n';

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
    const { t } = useI18n();

    const options = useMemo(
        () => [
            { label: t('cashRegisters.tenantSelector.allTenants'), value: ALL_TENANTS_VALUE },
            ...tenants.map((tenant) => ({
                label: t('cashRegisters.tenantSelector.tenantOption', {
                    name: tenant.name,
                    slug: tenant.slug,
                }),
                value: tenant.id,
            })),
        ],
        [t, tenants],
    );

    return (
        <Select
            placeholder={t('cashRegisters.tenantSelector.placeholder')}
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

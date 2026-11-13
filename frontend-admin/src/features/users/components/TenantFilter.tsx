'use client';

import type { SelectProps } from 'antd';
import { Select } from 'antd';
import React, { useMemo } from 'react';

import { useGetApiAdminTenants } from '@/features/tenancy/api/getApiAdminTenants';
import {
  ADMIN_USERS_FILTER_PLATFORM,
  TENANT_FILTER_ALL_UI,
} from '@/features/users/utils/adminUsersPageUrl';
import { isBusinessTenantSlug } from '@/features/users/utils/userScope';
import { useI18n } from '@/i18n';

export type TenantFilterProps = {
  value: string;
  onChange: (value: string) => void;
  /** When true, adds a «Plattform» option (Super Admin operators only). */
  includePlatformOption?: boolean;
  /**
   * When set, the list is locked to this mandant (Mandanten-Admin).
   * Omits «all» and platform. Empty string yields no options.
   */
  scopeToTenantId?: string;
} & Pick<SelectProps, 'style' | 'className' | 'disabled'>;

/** Mandant filter for the user list (`all` | `__platform__` | tenant UUID). */
export function TenantFilter({
  value,
  onChange,
  includePlatformOption = false,
  scopeToTenantId,
  style,
  className,
  disabled,
}: TenantFilterProps) {
  const { t } = useI18n();
  const tenantsQuery = useGetApiAdminTenants();

  const businessTenants = useMemo(
    () =>
      (tenantsQuery.data ?? [])
        .filter((row) => row.isActive && isBusinessTenantSlug(row.slug))
        .sort((a, b) => a.slug.localeCompare(b.slug)),
    [tenantsQuery.data]
  );

  const options = useMemo(() => {
    if (scopeToTenantId !== undefined) {
      const tenantId = scopeToTenantId.trim();
      if (!tenantId) return [];
      const tenant = businessTenants.find((row) => row.id === tenantId);
      return [
        {
          value: tenantId,
          label: tenant
            ? t('users.create.tenantOption', { name: tenant.name, slug: tenant.slug })
            : tenantId,
        },
      ];
    }

    const rows: { value: string; label: string }[] = [
      { value: TENANT_FILTER_ALL_UI, label: t('users.unified.filterAllTenants') },
    ];
    if (includePlatformOption) {
      rows.push({
        value: ADMIN_USERS_FILTER_PLATFORM,
        label: t('users.unified.filterPlatformOption'),
      });
    }
    for (const tenant of businessTenants) {
      rows.push({
        value: tenant.id,
        label: t('users.create.tenantOption', { name: tenant.name, slug: tenant.slug }),
      });
    }
    return rows;
  }, [businessTenants, includePlatformOption, scopeToTenantId, t]);

  return (
    <Select
      className={className}
      style={{ width: 250, ...style }}
      placeholder={t('users.unified.filterTenantPlaceholder')}
      value={value}
      onChange={(next) => onChange(next ?? TENANT_FILTER_ALL_UI)}
      options={options}
      loading={tenantsQuery.isLoading}
      disabled={disabled}
      allowClear
      showSearch
      optionFilterProp="label"
    />
  );
}

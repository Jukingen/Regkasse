'use client';

import type { SelectProps } from 'antd';
import { Flex, Select, Tag, Typography } from 'antd';
import React, { useMemo } from 'react';

import type { AdminTenantListItem } from '@/features/super-admin/api/adminTenants';
import { getMandantLicenseBadgeDisplay } from '@/features/tenant/utils/mandantLicenseBadge';
import {
  buildTenantPortalHost,
  formatInviteTenantLicenseShort,
} from '@/features/users/utils/inviteTenantDisplay';
import { useI18n } from '@/i18n';

export type TenantSelectorProps = {
  tenants: AdminTenantListItem[];
  value?: string;
  onChange?: (tenantId: string) => void;
  loading?: boolean;
  disabled?: boolean;
  placeholder?: string;
  id?: string;
};

type TenantSelectOption = {
  value: string;
  label: string;
  searchText: string;
  tenant: AdminTenantListItem;
};

function TenantSelectorOption({ tenant }: { tenant: AdminTenantListItem }) {
  const { t } = useI18n();
  const host = buildTenantPortalHost(tenant.slug);
  const licenseBadge = getMandantLicenseBadgeDisplay(
    tenant.licenseValidUntilUtc,
    tenant.licenseKey,
    t
  );
  const licenseText = licenseBadge?.label ?? formatInviteTenantLicenseShort(tenant, t);
  const licenseColor = licenseBadge?.color ?? 'default';

  return (
    <Flex vertical gap={2} style={{ padding: '4px 0' }}>
      <Typography.Text strong>
        {t('users.create.tenantOption', { name: tenant.name, slug: tenant.slug })}
      </Typography.Text>
      <Flex gap={6} wrap align="center">
        <Typography.Text type="secondary" style={{ fontSize: 12 }}>
          {host}
        </Typography.Text>
        <Tag color={licenseColor} style={{ margin: 0, fontSize: 11 }}>
          {licenseText}
        </Tag>
      </Flex>
    </Flex>
  );
}

/** Searchable tenant dropdown with domain and license per option (Super Admin invite). */
export function TenantSelector({
  tenants,
  value,
  onChange,
  loading,
  disabled,
  placeholder,
  id,
}: TenantSelectorProps) {
  const { t } = useI18n();

  const options = useMemo(
    (): TenantSelectOption[] =>
      tenants.map((tenant) => {
        const label = t('users.create.tenantOption', { name: tenant.name, slug: tenant.slug });
        const host = buildTenantPortalHost(tenant.slug);
        const license = formatInviteTenantLicenseShort(tenant, t);
        return {
          value: tenant.id,
          label,
          searchText: `${tenant.name} ${tenant.slug} ${host} ${license}`.toLowerCase(),
          tenant,
        };
      }),
    [tenants, t]
  );

  const searchByValue = useMemo(
    () => new Map(options.map((row) => [row.value, row.searchText])),
    [options]
  );

  const filterOption: SelectProps['filterOption'] = (input, option) => {
    const key = String(option?.value ?? '');
    const text = searchByValue.get(key) ?? '';
    return text.includes(input.trim().toLowerCase());
  };

  return (
    <Select
      id={id}
      showSearch
      loading={loading}
      disabled={disabled}
      value={value}
      onChange={onChange}
      placeholder={placeholder ?? t('users.create.tenantPlaceholder')}
      filterOption={filterOption}
      options={options}
      optionRender={(option) => {
        const row = options.find((item) => item.value === option.value);
        return row ? <TenantSelectorOption tenant={row.tenant} /> : null;
      }}
      style={{ width: '100%' }}
    />
  );
}

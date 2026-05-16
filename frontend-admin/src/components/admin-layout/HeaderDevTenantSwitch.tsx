'use client';

/**
 * Development-only tenant slug switcher (localStorage dev_tenant_id + reload).
 * Alternative: hosts-file subdomains (dev/cafe/bar.regkasse.local) per browser profile.
 */
import { useCallback, useEffect, useMemo, useState } from 'react';
import { Select, Tooltip } from 'antd';

import { DEV_TENANT_PRESETS } from '@/features/auth/constants/devTenantPresets';
import {
  DEV_TENANT_LOCAL_STORAGE_KEY,
  getDevTenant,
  isLocalDevHostname,
} from '@/features/auth/services/devTenant';
import { tenantStorage } from '@/features/auth/services/tenantStorage';

export function HeaderDevTenantSwitch() {
  const [currentTenant, setCurrentTenant] = useState<string>(() =>
    typeof window !== 'undefined' ? getDevTenant() : 'dev',
  );

  const hostHint = useMemo(() => {
    if (typeof window === 'undefined') return null;
    const host = window.location.hostname;
    if (!isLocalDevHostname(host)) return null;
    return host;
  }, []);

  useEffect(() => {
    setCurrentTenant(getDevTenant());
  }, []);

  const onChange = useCallback((value: string) => {
    if (typeof window === 'undefined') return;
    localStorage.setItem(DEV_TENANT_LOCAL_STORAGE_KEY, value);
    tenantStorage.persistBootstrap({ tenantSlug: value });
    window.location.reload();
  }, []);

  if (process.env.NODE_ENV !== 'development') {
    return null;
  }

  const select = (
    <Select
      size="small"
      style={{ minWidth: 220 }}
      value={currentTenant}
      onChange={onChange}
      options={DEV_TENANT_PRESETS.map((preset) => ({
        value: preset.value,
        label: preset.label,
      }))}
      aria-label="Development tenant"
    />
  );

  return (
    <Tooltip
      title={
        hostHint
          ? `Tenant from host: ${hostHint}. Dropdown overrides localStorage (per browser profile).`
          : 'Tenant slug for X-Tenant-Id. Or use hosts: dev/cafe/bar.regkasse.local with separate browser profiles.'
      }
    >
      {select}
    </Tooltip>
  );
}

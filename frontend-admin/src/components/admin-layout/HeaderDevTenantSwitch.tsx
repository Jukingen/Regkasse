'use client';

/**
 * Development-only tenant slug switcher (localStorage dev_tenant_id + reload).
 * Alternative: hosts-file subdomains (dev/cafe/bar.regkasse.local) per browser profile.
 */
import { useCallback, useEffect, useMemo, useState } from 'react';
import { Flex, Select, Tooltip, Typography } from 'antd';

import { DEV_TENANT_PRESETS } from '@/features/auth/constants/devTenantPresets';
import { getDevTenant, isLocalDevHostname } from '@/features/auth/services/devTenant';
import { persistTenantSlugAndRefresh } from '@/features/tenancy/services/setTenantAndRefresh';
import { useTenantContext } from '@/features/tenancy/hooks/useTenantContext';
import { useI18n } from '@/i18n';

export function HeaderDevTenantSwitch() {
    const { t } = useI18n();
    const { isPlatformAdminHost } = useTenantContext();
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
        persistTenantSlugAndRefresh(value);
    }, []);

    if (process.env.NODE_ENV !== 'development') {
        return null;
    }

    const tooltipTitle = hostHint
        ? t('adminShell.tenant.devSwitcher.tooltipHost', { host: hostHint })
        : t('adminShell.tenant.devSwitcher.tooltip');

    const select = (
        <Select
            size="small"
            style={{ minWidth: isPlatformAdminHost ? 260 : 220 }}
            value={currentTenant}
            onChange={onChange}
            options={DEV_TENANT_PRESETS.map((preset) => ({
                value: preset.value,
                label: preset.label,
            }))}
            aria-label={t('adminShell.tenant.devSwitcher.ariaLabel')}
        />
    );

    return (
        <Flex align="center" gap={8} style={{ maxWidth: isPlatformAdminHost ? 360 : 320 }}>
            <Flex vertical gap={0} style={{ lineHeight: 1.2, flexShrink: 0 }}>
                <Typography.Text style={{ fontSize: 11, fontWeight: 600 }}>
                    {t('common.tenant.switchTenant')}
                </Typography.Text>
                <Typography.Text type="secondary" style={{ fontSize: 10 }}>
                    {t('common.tenant.switchTenantTechnical')}
                </Typography.Text>
            </Flex>
            <Tooltip title={tooltipTitle}>{select}</Tooltip>
        </Flex>
    );
}

'use client';

/**
 * Development-only tenant switcher: all DB tenants (Super Admin) or membership-scoped list.
 * Search, status/admin/license rows, no-admin confirmation with impersonate or quick invite.
 */
import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { Alert, Button, Dropdown, Flex, Input, Spin, Tooltip, Typography } from 'antd';
import type { InputRef } from 'antd';
import { DownOutlined, ReloadOutlined, SearchOutlined } from '@ant-design/icons';

import { TenantSwitcherNoAdminFlow } from '@/features/auth/components/TenantSwitcherNoAdminFlow';
import { isSuperAdmin } from '@/features/auth/constants/roles';
import { useAuth } from '@/features/auth/hooks/useAuth';
import { getDevTenant, isLocalDevHostname } from '@/features/auth/services/devTenant';
import {
    filterTenantsForHeaderSearch,
    findTenantBySlug,
    getTenantHeaderDetailLines,
    getTenantHeaderTitle,
    sortTenantsForHeaderSwitcher,
} from '@/features/super-admin/utils/tenantHeaderSwitcher';
import { persistTenantSlugAndRefresh } from '@/features/tenancy/services/setTenantAndRefresh';
import {
    tenantNeedsNoAdminWarning,
    useTenantListForSwitcher,
    type TenantListItemForSwitcher,
} from '@/features/tenancy/hooks/useTenantListForSwitcher';
import { useTenantContext } from '@/features/tenancy/hooks/useTenantContext';
import { useI18n } from '@/i18n';

const SEARCH_FOCUS_TENANT_THRESHOLD = 20;

type TenantSwitcherRowProps = {
    tenant: TenantListItemForSwitcher;
    onSwitch: (tenant: TenantListItemForSwitcher) => void;
};

function TenantSwitcherRow({ tenant, onSwitch }: TenantSwitcherRowProps) {
    const { t } = useI18n();
    const title = getTenantHeaderTitle(tenant.source, t);
    const { adminLine, licenseLine } = getTenantHeaderDetailLines(tenant.source, t);

    return (
        <Flex
            align="flex-start"
            justify="space-between"
            gap={8}
            style={{
                padding: '8px 4px',
                borderBottom: '1px solid rgba(0,0,0,0.06)',
            }}
        >
            <Flex vertical gap={2} style={{ minWidth: 0, flex: 1 }}>
                <Typography.Text strong style={{ fontSize: 13 }}>
                    {title}
                </Typography.Text>
                {adminLine ? (
                    <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                        {adminLine}
                    </Typography.Text>
                ) : null}
                {licenseLine ? (
                    <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                        {licenseLine}
                    </Typography.Text>
                ) : null}
            </Flex>
            <Button size="small" type="link" onClick={() => onSwitch(tenant)}>
                {t('adminShell.tenant.devSwitcher.switchAction')}
            </Button>
        </Flex>
    );
}

function TenantSwitcherDropdown({
    tenants,
    loading,
    isFetching,
    isError,
    tenantCount,
    currentSlug,
    onRequestSwitch,
    onRetry,
    minWidth,
}: {
    tenants: TenantListItemForSwitcher[];
    loading: boolean;
    isFetching: boolean;
    isError: boolean;
    tenantCount: number;
    currentSlug: string;
    onRequestSwitch: (tenant: TenantListItemForSwitcher) => void;
    onRetry: () => void;
    minWidth: number;
}) {
    const { t } = useI18n();
    const [open, setOpen] = useState(false);
    const [search, setSearch] = useState('');
    const searchInputRef = useRef<InputRef>(null);

    const apiRows = useMemo(() => tenants.map((row) => row.source), [tenants]);
    const sortedTenants = useMemo(() => sortTenantsForHeaderSwitcher(apiRows), [apiRows]);
    const filteredApiRows = useMemo(
        () => filterTenantsForHeaderSearch(sortedTenants, search),
        [sortedTenants, search],
    );
    const filteredTenants = useMemo(() => {
        const byId = new Map(tenants.map((row) => [row.id, row]));
        return filteredApiRows
            .map((row) => byId.get(row.id))
            .filter((row): row is TenantListItemForSwitcher => row != null);
    }, [filteredApiRows, tenants]);

    const currentRow = findTenantBySlug(apiRows, currentSlug);
    const triggerLabel = currentRow
        ? getTenantHeaderTitle(currentRow, t)
        : `${currentSlug} (${t('adminShell.tenant.devSwitcher.unknownTenant')})`;

    const handleOpenChange = useCallback(
        (next: boolean) => {
            setOpen(next);
            if (!next) {
                setSearch('');
                return;
            }
            if (tenantCount >= SEARCH_FOCUS_TENANT_THRESHOLD) {
                window.setTimeout(() => searchInputRef.current?.focus(), 0);
            }
        },
        [tenantCount],
    );

    const handleRequestSwitch = useCallback(
        (tenant: TenantListItemForSwitcher) => {
            setOpen(false);
            setSearch('');
            onRequestSwitch(tenant);
        },
        [onRequestSwitch],
    );

    const dropdownContent = (
        <div
            role="listbox"
            aria-label={t('adminShell.tenant.devSwitcher.ariaLabel')}
            style={{
                width: Math.max(minWidth, 520),
                padding: 8,
                background: '#fff',
                borderRadius: 8,
                boxShadow:
                    '0 6px 16px 0 rgba(0, 0, 0, 0.08), 0 3px 6px -4px rgba(0, 0, 0, 0.12), 0 9px 28px 8px rgba(0, 0, 0, 0.05)',
            }}
            onClick={(e) => e.stopPropagation()}
        >
            <Input
                ref={searchInputRef}
                allowClear
                prefix={<SearchOutlined aria-hidden />}
                placeholder={t('adminShell.tenant.devSwitcher.searchPlaceholder')}
                value={search}
                onChange={(e) => setSearch(e.target.value)}
                aria-label={t('adminShell.tenant.devSwitcher.searchPlaceholder')}
            />
            {tenantCount >= SEARCH_FOCUS_TENANT_THRESHOLD ? (
                <Typography.Text type="secondary" style={{ fontSize: 11, display: 'block', marginTop: 6 }}>
                    {t('adminShell.tenant.devSwitcher.tenantCount', { count: tenantCount })}
                </Typography.Text>
            ) : null}
            <div style={{ maxHeight: 360, overflowY: 'auto', marginTop: 8 }}>
                {loading ? (
                    <Flex justify="center" style={{ padding: 16 }}>
                        <Spin size="small" tip={t('common.loading.data')} />
                    </Flex>
                ) : null}
                {isError ? (
                    <Alert
                        type="error"
                        showIcon
                        message={t('superadmin.loadFailed')}
                        action={
                            <Button size="small" icon={<ReloadOutlined />} onClick={onRetry}>
                                {t('adminShell.tenant.devSwitcher.retryLoad')}
                            </Button>
                        }
                        style={{ marginBottom: 8 }}
                    />
                ) : null}
                {isFetching && !loading ? (
                    <Flex justify="center" style={{ padding: 8 }}>
                        <Spin size="small" />
                    </Flex>
                ) : null}
                {!loading && !isError && filteredTenants.length === 0 ? (
                    <Typography.Text type="secondary" style={{ display: 'block', padding: 8 }}>
                        {t('adminShell.tenant.devSwitcher.emptySearch')}
                    </Typography.Text>
                ) : null}
                {!loading && !isError
                    ? filteredTenants.map((row) => (
                          <TenantSwitcherRow key={row.id} tenant={row} onSwitch={handleRequestSwitch} />
                      ))
                    : null}
            </div>
        </div>
    );

    return (
        <Dropdown
            open={open}
            onOpenChange={handleOpenChange}
            trigger={['click']}
            dropdownRender={() => dropdownContent}
        >
            <Button
                size="small"
                loading={loading}
                style={{ minWidth, maxWidth: minWidth, textAlign: 'left' }}
                aria-label={t('adminShell.tenant.devSwitcher.ariaLabel')}
            >
                <Flex align="center" justify="space-between" gap={8} style={{ width: '100%' }}>
                    <Typography.Text ellipsis style={{ flex: 1, fontSize: 12 }}>
                        {loading ? t('common.loading.data') : triggerLabel}
                    </Typography.Text>
                    <DownOutlined style={{ fontSize: 10 }} />
                </Flex>
            </Button>
        </Dropdown>
    );
}

export function HeaderDevTenantSwitch() {
    const { t } = useI18n();
    const { user } = useAuth();
    const { isPlatformAdminHost } = useTenantContext();
    const isSuperAdminUser = isSuperAdmin(user?.role);
    const { tenants, isLoading, isFetching, isError, refetch, tenantCount } = useTenantListForSwitcher();
    const [currentTenant, setCurrentTenant] = useState<string>(() =>
        typeof window !== 'undefined' ? getDevTenant() : 'dev',
    );
    const [noAdminTenant, setNoAdminTenant] = useState<TenantListItemForSwitcher | null>(null);

    const hostHint = useMemo(() => {
        if (typeof window === 'undefined') return null;
        const host = window.location.hostname;
        if (!isLocalDevHostname(host)) return null;
        return host;
    }, []);

    useEffect(() => {
        setCurrentTenant(getDevTenant());
    }, []);

    const applySlugSwitch = useCallback((slug: string) => {
        persistTenantSlugAndRefresh(slug);
    }, []);

    const requestSwitch = useCallback(
        (tenant: TenantListItemForSwitcher) => {
            if (isSuperAdminUser && tenantNeedsNoAdminWarning(tenant)) {
                setNoAdminTenant(tenant);
                return;
            }
            applySlugSwitch(tenant.slug);
        },
        [isSuperAdminUser, applySlugSwitch],
    );

    const controlMinWidth = isPlatformAdminHost ? 420 : 360;

    if (process.env.NODE_ENV !== 'development') {
        return null;
    }

    const tooltipTitle = hostHint
        ? t('adminShell.tenant.devSwitcher.tooltipHost', { host: hostHint })
        : t('adminShell.tenant.devSwitcher.tooltipSuperAdmin');

    const control = isError && !isLoading ? (
        <Button size="small" icon={<ReloadOutlined />} onClick={() => void refetch()}>
            {t('adminShell.tenant.devSwitcher.retryLoad')}
        </Button>
    ) : (
        <TenantSwitcherDropdown
            tenants={tenants}
            loading={isLoading}
            isFetching={isFetching}
            isError={isError}
            tenantCount={tenantCount}
            currentSlug={currentTenant}
            onRequestSwitch={requestSwitch}
            onRetry={() => void refetch()}
            minWidth={controlMinWidth}
        />
    );

    return (
        <>
            <Flex align="center" gap={8} style={{ maxWidth: isPlatformAdminHost ? 460 : 400 }}>
                <Flex vertical gap={0} style={{ lineHeight: 1.2, flexShrink: 0 }}>
                    <Typography.Text style={{ fontSize: 11, fontWeight: 600 }}>
                        {t('common.tenant.switchTenant')}
                    </Typography.Text>
                    <Typography.Text type="secondary" style={{ fontSize: 10 }}>
                        {t('common.tenant.switchTenantTechnical')}
                    </Typography.Text>
                </Flex>
                <Tooltip title={tooltipTitle}>{control}</Tooltip>
            </Flex>
            <TenantSwitcherNoAdminFlow
                tenant={noAdminTenant}
                open={noAdminTenant != null}
                onClose={() => setNoAdminTenant(null)}
            />
        </>
    );
}

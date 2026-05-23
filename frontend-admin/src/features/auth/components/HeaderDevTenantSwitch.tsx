'use client';

/**
 * Development-only tenant switcher: all DB tenants (Super Admin) or membership-scoped list.
 * Search, status/admin/license rows, no-admin confirmation with impersonate or quick invite.
 */
import { useCallback, useMemo, useRef, useState } from 'react';
import { Alert, Button, Checkbox, Dropdown, Flex, Input, Spin, Tag, Tooltip, Typography } from 'antd';
import type { InputRef } from 'antd';
import { CheckOutlined, DownOutlined, QuestionCircleOutlined, ReloadOutlined } from '@ant-design/icons';

import { TenantSwitcherNoAdminFlow } from '@/features/auth/components/TenantSwitcherNoAdminFlow';
import { isSuperAdmin } from '@/features/auth/constants/roles';
import { useAuth } from '@/features/auth/hooks/useAuth';
import { isLocalDevHostname } from '@/features/auth/services/devTenant';
import { TenantNoAdminWarningPill } from '@/features/super-admin/components/TenantNoAdminWarningPill';
import {
    dedupeAdminTenantsById,
    findTenantById,
    getTenantHeaderDetailLines,
    getTenantHeaderTitle,
    getTenantSwitcherLicenseBadge,
    shouldShowHeaderDevTenantSwitch,
    tenantHeaderShowsNoAdminWarning,
} from '@/features/super-admin/utils/tenantHeaderSwitcher';
import { persistTenantSlugAndRefresh } from '@/features/tenancy/services/setTenantAndRefresh';
import { useCurrentTenant } from '@/features/tenancy/hooks/useCurrentTenant';
import {
    filterTenantSwitcherItems,
    tenantNeedsNoAdminWarning,
    useTenantListForSwitcher,
    type TenantListItemForSwitcher,
} from '@/features/tenancy/hooks/useTenantListForSwitcher';
import { useTenantContext } from '@/features/tenancy/hooks/useTenantContext';
import { useI18n } from '@/i18n';

const { Search } = Input;

function dedupeSwitcherItems(items: TenantListItemForSwitcher[]): TenantListItemForSwitcher[] {
    const sources = dedupeAdminTenantsById(items.map((row) => row.source));
    const byId = new Map(items.map((row) => [row.id, row]));
    return sources
        .map((source) => byId.get(source.id))
        .filter((row): row is TenantListItemForSwitcher => row != null);
}

type TenantSwitcherRowProps = {
    tenant: TenantListItemForSwitcher;
    isActiveTenant: boolean;
    onSwitch: (tenant: TenantListItemForSwitcher) => void;
};

function TenantSwitcherRow({ tenant, isActiveTenant, onSwitch }: TenantSwitcherRowProps) {
    const { t } = useI18n();
    const title = getTenantHeaderTitle(tenant.source, t);
    const { adminLine } = getTenantHeaderDetailLines(tenant.source, t);
    const licenseBadge = getTenantSwitcherLicenseBadge(tenant.source, t);
    const showNoAdminPill = tenantHeaderShowsNoAdminWarning(tenant.source);

    return (
        <Flex
            align="flex-start"
            justify="space-between"
            gap={8}
            style={{
                padding: '8px 4px',
                borderBottom: '1px solid rgba(0,0,0,0.06)',
                background: isActiveTenant ? 'rgba(22, 119, 255, 0.06)' : undefined,
            }}
        >
            <Flex vertical gap={2} style={{ minWidth: 0, flex: 1 }}>
                <Flex align="center" gap={6} wrap="wrap">
                    <Typography.Text strong style={{ fontSize: 13 }}>
                        {title}
                    </Typography.Text>
                    {isActiveTenant ? (
                        <Tag color="blue" icon={<CheckOutlined />} style={{ marginInlineEnd: 0 }}>
                            {t('adminShell.tenant.info.active')}
                        </Tag>
                    ) : null}
                    {showNoAdminPill ? <TenantNoAdminWarningPill tenantId={tenant.id} /> : null}
                </Flex>
                {adminLine ? (
                    <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                        {adminLine}
                    </Typography.Text>
                ) : null}
                {licenseBadge ? (
                    <Tooltip title={licenseBadge.tooltip}>
                        <Tag
                            color={licenseBadge.color}
                            style={{ marginInlineEnd: 0, width: 'fit-content', cursor: 'help' }}
                        >
                            {licenseBadge.label}
                        </Tag>
                    </Tooltip>
                ) : null}
            </Flex>
            {isActiveTenant ? (
                <Typography.Text type="secondary" style={{ fontSize: 12, flexShrink: 0 }}>
                    ✓
                </Typography.Text>
            ) : (
                <Button size="small" type="link" onClick={() => onSwitch(tenant)}>
                    {t('adminShell.tenant.devSwitcher.switchAction')}
                </Button>
            )}
        </Flex>
    );
}

function TenantSwitcherDropdown({
    tenants,
    loading,
    isFetching,
    isError,
    tenantCount,
    currentTenantId,
    onRequestSwitch,
    onRetry,
    minWidth,
    showIncludeDeletedToggle = false,
    includeDeleted = false,
    onIncludeDeletedChange,
}: {
    tenants: TenantListItemForSwitcher[];
    loading: boolean;
    isFetching: boolean;
    isError: boolean;
    tenantCount: number;
    currentTenantId: string | null | undefined;
    onRequestSwitch: (tenant: TenantListItemForSwitcher) => void;
    onRetry: () => void;
    minWidth: number;
    showIncludeDeletedToggle?: boolean;
    includeDeleted?: boolean;
    onIncludeDeletedChange?: (checked: boolean) => void;
}) {
    const { t } = useI18n();
    const [open, setOpen] = useState(false);
    const [search, setSearch] = useState('');
    const searchInputRef = useRef<InputRef>(null);

    const filteredTenants = useMemo(
        () => filterTenantSwitcherItems(tenants, search),
        [tenants, search],
    );
    const apiRows = useMemo(() => tenants.map((row) => row.source), [tenants]);
    const searchQuery = search.trim();
    const isFiltering = searchQuery.length > 0;

    const normalizedCurrentId = currentTenantId?.trim().toLowerCase() ?? '';
    const currentRow = normalizedCurrentId
        ? findTenantById(apiRows, normalizedCurrentId)
        : undefined;
    const triggerLabel = currentRow
        ? getTenantHeaderTitle(currentRow, t)
        : t('adminShell.tenant.devSwitcher.unknownTenant');

    const handleOpenChange = useCallback(
        (next: boolean) => {
            setOpen(next);
            if (!next) {
                setSearch('');
                return;
            }
            window.setTimeout(() => searchInputRef.current?.focus(), 0);
        },
        [],
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
            <Search
                ref={searchInputRef}
                allowClear
                placeholder={t('adminShell.tenant.devSwitcher.searchPlaceholder')}
                value={search}
                onChange={(e) => setSearch(e.target.value)}
                onSearch={(value) => setSearch(value)}
                aria-label={t('adminShell.tenant.devSwitcher.searchPlaceholder')}
            />
            <Typography.Text type="secondary" style={{ fontSize: 11, display: 'block', marginTop: 6 }}>
                {isFiltering
                    ? t('adminShell.tenant.devSwitcher.searchResultsCount', {
                          shown: filteredTenants.length,
                          total: tenantCount,
                      })
                    : t('adminShell.tenant.devSwitcher.tenantCount', { count: tenantCount })}
            </Typography.Text>
            {showIncludeDeletedToggle ? (
                <Checkbox
                    checked={includeDeleted}
                    onChange={(e) => onIncludeDeletedChange?.(e.target.checked)}
                    style={{ marginTop: 8 }}
                >
                    {t('tenants.filters.includeDeleted')}
                </Checkbox>
            ) : null}
            <Flex align="center" gap={6} style={{ marginTop: 8 }}>
                <Typography.Text type="secondary" style={{ fontSize: 11 }}>
                    {t('adminShell.tenant.devSwitcher.licenseColumnHint')}
                </Typography.Text>
                <Tooltip title={t('adminShell.tenant.devSwitcher.licenseHintTooltip')}>
                    <QuestionCircleOutlined
                        aria-label={t('adminShell.tenant.devSwitcher.licenseHintTooltip')}
                        style={{ fontSize: 12, color: 'rgba(0, 0, 0, 0.45)', cursor: 'help' }}
                    />
                </Tooltip>
            </Flex>
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
                          <TenantSwitcherRow
                              key={row.id}
                              tenant={row}
                              isActiveTenant={
                                  normalizedCurrentId.length > 0 &&
                                  row.id.trim().toLowerCase() === normalizedCurrentId
                              }
                              onSwitch={handleRequestSwitch}
                          />
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
    const { tenantId: currentTenantId } = useCurrentTenant();
    const isSuperAdminUser = isSuperAdmin(user?.role);
    const [includeDeleted, setIncludeDeleted] = useState(false);
    const { tenants: rawTenants, isLoading, isFetching, isError, refetch, tenantCount } =
        useTenantListForSwitcher({
            includeDeleted: isSuperAdminUser && includeDeleted,
        });
    const [noAdminTenant, setNoAdminTenant] = useState<TenantListItemForSwitcher | null>(null);

    const tenants = useMemo(() => dedupeSwitcherItems(rawTenants), [rawTenants]);
    const uniqueTenantCount = tenants.length;

    const hostHint = useMemo(() => {
        if (typeof window === 'undefined') return null;
        const host = window.location.hostname;
        if (!isLocalDevHostname(host)) return null;
        return host;
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

    if (!shouldShowHeaderDevTenantSwitch()) {
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
            tenantCount={uniqueTenantCount}
            currentTenantId={currentTenantId}
            onRequestSwitch={requestSwitch}
            onRetry={() => void refetch()}
            minWidth={controlMinWidth}
            showIncludeDeletedToggle={isSuperAdminUser}
            includeDeleted={includeDeleted}
            onIncludeDeletedChange={setIncludeDeleted}
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

'use client';

/**
 * Development-only tenant switcher: searchable list, license/no-admin hints, footer link.
 * Open state is shared with TenantBadge via HeaderTenantSwitcherContext.
 */
import { useCallback, useMemo, useRef, useState } from 'react';
import { useRouter } from 'next/navigation';
import {
    Alert,
    Button,
    Checkbox,
    Dropdown,
    Input,
    Spin,
    Tag,
    Tooltip,
    Typography,
} from 'antd';
import type { InputRef } from 'antd';
import { PlusOutlined, ReloadOutlined, SearchOutlined, SwapOutlined, WarningOutlined } from '@ant-design/icons';

import { TenantSwitcherNoAdminFlow } from '@/features/auth/components/TenantSwitcherNoAdminFlow';
import { useHeaderTenantSwitcher } from '@/features/auth/components/HeaderTenantSwitcherContext';
import { isSuperAdmin } from '@/features/auth/constants/roles';
import { useAuth } from '@/features/auth/hooks/useAuth';
import { isLocalDevHostname } from '@/features/auth/services/devTenant';
import {
    dedupeAdminTenantsById,
    getTenantSwitcherLicenseBadge,
    partitionTenantsForSwitcher,
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
import { formatTenantDisplay } from '@/features/super-admin/utils/tenantHeaderSwitcher';
import { useI18n } from '@/i18n';
import { getAdminHeaderPopupContainer } from '@/shared/layout/adminHeaderDropdown';

function dedupeSwitcherItems(items: TenantListItemForSwitcher[]): TenantListItemForSwitcher[] {
    const sources = dedupeAdminTenantsById(items.map((row) => row.source));
    const byId = new Map(items.map((row) => [row.id, row]));
    return sources
        .map((source) => byId.get(source.id))
        .filter((row): row is TenantListItemForSwitcher => row != null);
}

type TenantSwitcherItemProps = {
    tenant: TenantListItemForSwitcher;
    isActiveTenant: boolean;
    onSwitch: (tenant: TenantListItemForSwitcher) => void;
};

type SwitcherTenantSectionProps = {
    title: string;
    tenants: TenantListItemForSwitcher[];
    normalizedCurrentId: string;
    onSwitch: (tenant: TenantListItemForSwitcher) => void;
};

function SwitcherTenantSection({
    title,
    tenants,
    normalizedCurrentId,
    onSwitch,
}: SwitcherTenantSectionProps) {
    if (tenants.length === 0) {
        return null;
    }

    return (
        <div className="switcher-section" role="group" aria-label={title}>
            <div className="switcher-section-label">{title}</div>
            {tenants.map((row) => (
                <TenantSwitcherItem
                    key={row.id}
                    tenant={row}
                    isActiveTenant={
                        normalizedCurrentId.length > 0 &&
                        row.id.trim().toLowerCase() === normalizedCurrentId
                    }
                    onSwitch={onSwitch}
                />
            ))}
        </div>
    );
}

function TenantSwitcherItem({ tenant, isActiveTenant, onSwitch }: TenantSwitcherItemProps) {
    const { t } = useI18n();
    const source = tenant.source;
    const licenseBadge = getTenantSwitcherLicenseBadge(source, t);
    const showNoAdmin = tenantHeaderShowsNoAdminWarning(source);
    const { displayName, displaySlug } = formatTenantDisplay(source);
    const isDeleted = source.status === 'deleted' || !source.isActive;

    return (
        <div
            role="option"
            aria-selected={isActiveTenant}
            className={`switcher-item${isActiveTenant ? ' active' : ''}${isDeleted ? ' switcher-item-deleted' : ''}`}
            onClick={() => onSwitch(tenant)}
        >
            <div className="switcher-item-info">
                <span className="tenant-name">{displayName}</span>
                <span className="tenant-slug">{displaySlug}</span>
            </div>
            <div className="switcher-item-meta">
                {isDeleted ? (
                    <Tag style={{ marginInlineEnd: 0 }}>{t('tenants.status.deleted')}</Tag>
                ) : null}
                {isActiveTenant ? (
                    <Tag color="green" style={{ marginInlineEnd: 0 }}>
                        {t('adminShell.tenant.info.active')}
                    </Tag>
                ) : null}
                {showNoAdmin ? (
                    <Tooltip title={t('adminShell.tenant.devSwitcher.noAdminPillTooltip')}>
                        <WarningOutlined
                            className="warning-icon"
                            aria-label={t('adminShell.tenant.devSwitcher.noAdminPill')}
                        />
                    </Tooltip>
                ) : null}
                <Tooltip title={licenseBadge.tooltip}>
                    <Tag color={licenseBadge.color} className="tenant-switcher-license-tag">
                        {licenseBadge.label}
                    </Tag>
                </Tooltip>
            </div>
        </div>
    );
}

export type HeaderDevTenantSwitchProps = {
    compact?: boolean;
};

export function HeaderDevTenantSwitch({ compact = false }: HeaderDevTenantSwitchProps) {
    const { t } = useI18n();
    const router = useRouter();
    const { user } = useAuth();
    const { tenantId: currentTenantId } = useCurrentTenant();
    const { open, setOpen } = useHeaderTenantSwitcher();
    const isSuperAdminUser = isSuperAdmin(user?.role);
    const [includeDeleted, setIncludeDeleted] = useState(false);
    const [search, setSearch] = useState('');
    const searchInputRef = useRef<InputRef>(null);

    const { tenants: rawTenants, isLoading, isFetching, isError, refetch, tenantCount } =
        useTenantListForSwitcher({
            includeDeleted: isSuperAdminUser && includeDeleted,
        });
    const [noAdminTenant, setNoAdminTenant] = useState<TenantListItemForSwitcher | null>(null);

    const tenants = useMemo(() => dedupeSwitcherItems(rawTenants), [rawTenants]);
    const filteredTenants = useMemo(
        () => filterTenantSwitcherItems(tenants, search),
        [tenants, search],
    );
    const { development: developmentTenants, production: productionTenants } = useMemo(
        () => partitionTenantsForSwitcher(filteredTenants),
        [filteredTenants],
    );
    const searchQuery = search.trim();
    const isFiltering = searchQuery.length > 0;
    const normalizedCurrentId = currentTenantId?.trim().toLowerCase() ?? '';

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
                setOpen(false);
                setSearch('');
                setNoAdminTenant(tenant);
                return;
            }
            setOpen(false);
            setSearch('');
            applySlugSwitch(tenant.slug);
        },
        [isSuperAdminUser, applySlugSwitch, setOpen],
    );

    const handleOpenChange = useCallback(
        (next: boolean) => {
            setOpen(next);
            if (!next) {
                setSearch('');
                return;
            }
            window.setTimeout(() => searchInputRef.current?.focus(), 0);
        },
        [setOpen],
    );

    const openTenantManagement = useCallback(() => {
        setOpen(false);
        router.push('/admin/tenants');
    }, [router, setOpen]);

    if (!shouldShowHeaderDevTenantSwitch()) {
        return null;
    }

    const switchLabel = t('common.tenant.switchTenant');
    const tooltipTitle = hostHint
        ? t('adminShell.tenant.devSwitcher.tooltipHost', { host: hostHint })
        : t('adminShell.tenant.devSwitcher.tooltipSuperAdmin');

    const dropdownContent = (
        <div
            role="listbox"
            aria-label={t('adminShell.tenant.devSwitcher.ariaLabel')}
            className="tenant-switcher-panel"
            onClick={(event) => event.stopPropagation()}
        >
            <div className="switcher-search">
                <Input
                    ref={searchInputRef}
                    allowClear
                    prefix={<SearchOutlined aria-hidden />}
                    placeholder={t('adminShell.tenant.devSwitcher.searchPlaceholder')}
                    value={search}
                    onChange={(event) => setSearch(event.target.value)}
                    aria-label={t('adminShell.tenant.devSwitcher.searchPlaceholder')}
                />
            </div>
            <Typography.Text type="secondary" className="switcher-search-meta">
                {isFiltering
                    ? t('adminShell.tenant.devSwitcher.searchResultsCount', {
                          shown: filteredTenants.length,
                          total: tenantCount,
                      })
                    : t('adminShell.tenant.devSwitcher.tenantCount', { count: tenants.length })}
            </Typography.Text>

            {isSuperAdminUser ? (
                <div className="switcher-filters">
                    <Checkbox
                        checked={includeDeleted}
                        onChange={(event) => setIncludeDeleted(event.target.checked)}
                    >
                        {t('tenants.filters.includeDeleted')}
                    </Checkbox>
                </div>
            ) : null}

            <div className="switcher-list">
                {isLoading ? (
                    <div className="tenant-switcher-loading">
                        <Spin size="small" description={t('common.loading.data')} />
                    </div>
                ) : null}
                {isError ? (
                    <Alert
                        type="error"
                        showIcon
                        title={t('superadmin.loadFailed')}
                        action={
                            <Button size="small" icon={<ReloadOutlined />} onClick={() => void refetch()}>
                                {t('adminShell.tenant.devSwitcher.retryLoad')}
                            </Button>
                        }
                        style={{ marginBottom: 8 }}
                    />
                ) : null}
                {isFetching && !isLoading ? (
                    <div className="tenant-switcher-loading">
                        <Spin size="small" />
                    </div>
                ) : null}
                {!isLoading && !isError && filteredTenants.length === 0 ? (
                    <Typography.Text type="secondary" className="tenant-switcher-empty">
                        {t('adminShell.tenant.devSwitcher.emptySearch')}
                    </Typography.Text>
                ) : null}
                {!isLoading && !isError && isFiltering
                    ? filteredTenants.map((row) => (
                          <TenantSwitcherItem
                              key={row.id}
                              tenant={row}
                              isActiveTenant={
                                  normalizedCurrentId.length > 0 &&
                                  row.id.trim().toLowerCase() === normalizedCurrentId
                              }
                              onSwitch={requestSwitch}
                          />
                      ))
                    : null}
                {!isLoading && !isError && !isFiltering ? (
                    <>
                        <SwitcherTenantSection
                            title={t('adminShell.tenant.devSwitcher.sectionDevelopment')}
                            tenants={developmentTenants}
                            normalizedCurrentId={normalizedCurrentId}
                            onSwitch={requestSwitch}
                        />
                        {developmentTenants.length > 0 && productionTenants.length > 0 ? (
                            <div className="switcher-section-divider" role="separator" />
                        ) : null}
                        <SwitcherTenantSection
                            title={t('adminShell.tenant.devSwitcher.sectionProduction')}
                            tenants={productionTenants}
                            normalizedCurrentId={normalizedCurrentId}
                            onSwitch={requestSwitch}
                        />
                    </>
                ) : null}
            </div>

            {isSuperAdminUser ? (
                <div className="switcher-footer">
                    <Button type="link" icon={<PlusOutlined />} onClick={openTenantManagement}>
                        {t('tenants.actions.create')}
                    </Button>
                </div>
            ) : null}
        </div>
    );

    if (isError && !isLoading) {
        return (
            <Button size="small" icon={<ReloadOutlined />} onClick={() => void refetch()}>
                {t('adminShell.tenant.devSwitcher.retryLoad')}
            </Button>
        );
    }

    return (
        <>
            <Dropdown
                open={open}
                onOpenChange={handleOpenChange}
                trigger={['click']}
                placement="bottomRight"
                popupRender={() => dropdownContent}
                classNames={{ root: "tenant-switcher-dropdown admin-header-dropdown" }}
                getPopupContainer={getAdminHeaderPopupContainer}
            >
                <Tooltip title={tooltipTitle}>
                    <span className="tenant-switcher-trigger-wrap">
                        <Button
                            size="small"
                            loading={isLoading}
                            className={`tenant-switcher-trigger${compact ? ' tenant-switcher-compact-trigger' : ''}`}
                            icon={<SwapOutlined />}
                            aria-label={t('adminShell.tenant.devSwitcher.ariaLabel')}
                            aria-expanded={open}
                        >
                            <span className="trigger-text">{switchLabel}</span>
                        </Button>
                    </span>
                </Tooltip>
            </Dropdown>
            <TenantSwitcherNoAdminFlow
                tenant={noAdminTenant}
                open={noAdminTenant != null}
                onClose={() => setNoAdminTenant(null)}
            />
        </>
    );
}

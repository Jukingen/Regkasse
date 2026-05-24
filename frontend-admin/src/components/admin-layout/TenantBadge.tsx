'use client';

import { useCallback, useMemo, type KeyboardEvent } from 'react';
import { BankOutlined, DownOutlined } from '@ant-design/icons';
import { Tag, Tooltip } from 'antd';
import { useRouter } from 'next/navigation';

import { useHeaderTenantSwitcher } from '@/features/auth/components/HeaderTenantSwitcherContext';
import { isDevelopment } from '@/features/auth/services/devTenant';
import { shouldShowHeaderDevTenantSwitch } from '@/features/super-admin/utils/tenantHeaderSwitcher';
import { useCurrentTenant } from '@/features/tenancy/hooks/useCurrentTenant';
import { useI18n } from '@/i18n';

export type TenantBadgeProps = {
    compact?: boolean;
};

type TenantBadgeViewModel = {
    displayName: string;
    tooltipText: string;
    navigatesToTenants: boolean;
    iconClassName: string;
};

function buildTenantBadgeViewModel(input: {
    t: (key: string, params?: Record<string, string | number>) => string;
    tenantSlug: string | null | undefined;
    tenantId: string | null | undefined;
    tenantName: string | null | undefined;
    isSuperAdminPlatformMode: boolean;
    isPlatformAdminHost: boolean;
    isImpersonating: boolean;
    isDevTenantOverride: boolean;
}): TenantBadgeViewModel {
    const {
        t,
        tenantSlug,
        tenantId,
        tenantName,
        isSuperAdminPlatformMode,
        isPlatformAdminHost,
        isImpersonating,
        isDevTenantOverride,
    } = input;

    const tooltipParts: string[] = [];
    let displayName: string;
    let iconClassName = 'tenant-badge-icon';
    let navigatesToTenants = false;

    if (isSuperAdminPlatformMode) {
        displayName = t('adminShell.tenant.badgeSuperAdminMode');
        tooltipParts.push(t('adminShell.tenant.superAdminModeBanner'));
        tooltipParts.push(t('license.badge.superAdminMode.tooltip'));
        iconClassName = 'tenant-badge-icon tenant-badge-icon-super-admin';
        navigatesToTenants = !isImpersonating;
    } else if (isPlatformAdminHost && tenantSlug === 'admin') {
        displayName = t('adminShell.tenant.badgePlatformAdmin');
        tooltipParts.push(t('adminShell.tenant.badgePlatformAdminTooltip'));
        iconClassName = 'tenant-badge-icon tenant-badge-icon-platform';
        navigatesToTenants = !isImpersonating;
    } else {
        const slugForLabel = tenantSlug ?? '—';
        const resolvedName = tenantName?.trim();
        displayName = resolvedName || slugForLabel;
        tooltipParts.push(t('common.tenant.tenantDescription'));
        tooltipParts.push(t('common.tenant.activeCompanyTooltip'));
        if (resolvedName && resolvedName !== slugForLabel) {
            tooltipParts.push(`${t('adminShell.tenant.info.slug')}: ${slugForLabel}`);
        }
        tooltipParts.push(t('adminShell.tenant.badge.tooltip', { id: tenantId?.trim() || '—' }));
    }

    if (isImpersonating) {
        iconClassName = 'tenant-badge-icon tenant-badge-icon-impersonating';
        tooltipParts.push(t('adminShell.tenant.badgeImpersonatingTooltip'));
    } else if (isDevTenantOverride) {
        tooltipParts.push(t('adminShell.tenant.badgeDevOverrideTooltip'));
    }

    return {
        displayName,
        tooltipText: tooltipParts.join(' · '),
        navigatesToTenants,
        iconClassName,
    };
}

export function TenantBadge({ compact = false }: TenantBadgeProps) {
    const { t } = useI18n();
    const router = useRouter();
    const switcher = useHeaderTenantSwitcher();
    const devSwitcherAvailable = shouldShowHeaderDevTenantSwitch() && switcher.isAvailable;
    const {
        tenantSlug,
        tenantId,
        tenantName,
        hasAuthToken,
        isSuperAdminPlatformMode,
        isPlatformAdminHost,
        isImpersonating,
        isDevTenantOverride,
    } = useCurrentTenant();

    const view = useMemo(
        () =>
            buildTenantBadgeViewModel({
                t,
                tenantSlug,
                tenantId,
                tenantName,
                isSuperAdminPlatformMode,
                isPlatformAdminHost,
                isImpersonating,
                isDevTenantOverride,
            }),
        [
            t,
            tenantSlug,
            tenantId,
            tenantName,
            isSuperAdminPlatformMode,
            isPlatformAdminHost,
            isImpersonating,
            isDevTenantOverride,
        ],
    );

    const isInteractive = devSwitcherAvailable || view.navigatesToTenants;

    const handleClick = useCallback(() => {
        if (devSwitcherAvailable) {
            switcher.toggle();
            return;
        }
        if (view.navigatesToTenants) {
            router.push('/admin/tenants');
        }
    }, [devSwitcherAvailable, router, switcher, view.navigatesToTenants]);

    const handleKeyDown = useCallback(
        (event: KeyboardEvent<HTMLDivElement>) => {
            if (!isInteractive) {
                return;
            }
            if (event.key === 'Enter' || event.key === ' ') {
                event.preventDefault();
                handleClick();
            }
        },
        [handleClick, isInteractive],
    );

    if (!hasAuthToken) {
        return null;
    }

    const badgeClassName = [
        'tenant-badge',
        isInteractive ? 'tenant-badge-clickable' : '',
        devSwitcherAvailable && switcher.open ? 'tenant-badge-expanded' : '',
        compact ? 'tenant-badge-compact' : '',
    ]
        .filter(Boolean)
        .join(' ');

    const badge = (
        <div
            className={badgeClassName}
            data-expanded={devSwitcherAvailable && switcher.open ? true : undefined}
            role={isInteractive ? 'button' : undefined}
            tabIndex={isInteractive ? 0 : undefined}
            aria-label={view.displayName}
            aria-expanded={devSwitcherAvailable ? switcher.open : undefined}
            aria-haspopup={devSwitcherAvailable ? 'listbox' : undefined}
            onClick={isInteractive ? handleClick : undefined}
            onKeyDown={isInteractive ? handleKeyDown : undefined}
        >
            <BankOutlined className={view.iconClassName} aria-hidden />
            <div className="tenant-badge-info">
                <span className="tenant-badge-name">{view.displayName}</span>
                {isImpersonating ? (
                    <Tag color="purple" className="tenant-badge-impersonation-tag">
                        {t('adminShell.tenant.badgeSupportModeTag')}
                    </Tag>
                ) : null}
                {isDevelopment() ? (
                    <Tooltip title={t('adminShell.tenant.badgeDevModeTooltip')}>
                        <Tag color="orange" className="tenant-badge-dev-mode-tag">
                            {t('adminShell.tenant.badgeDevModeTag')}
                        </Tag>
                    </Tooltip>
                ) : null}
            </div>
            <DownOutlined className="tenant-badge-chevron" aria-hidden />
        </div>
    );

    return (
        <Tooltip title={view.tooltipText} placement="bottom" mouseEnterDelay={0.35}>
            <span className="tenant-badge-tooltip-trigger">{badge}</span>
        </Tooltip>
    );
}

/** @deprecated Use `TenantBadge` */
export const TenantContextBadge = TenantBadge;

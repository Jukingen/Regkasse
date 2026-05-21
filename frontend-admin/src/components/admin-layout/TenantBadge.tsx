'use client';

import { Tag, Tooltip } from 'antd';
import Link from 'next/link';

import { useCurrentTenant } from '@/features/tenancy/hooks/useCurrentTenant';
import { useI18n } from '@/i18n';

export function TenantBadge() {
    const { t } = useI18n();
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

    if (!hasAuthToken) {
        return null;
    }

    let color: string | undefined = 'blue';
    let label: string;
    const tooltipParts: string[] = [];

    if (isSuperAdminPlatformMode) {
        color = 'processing';
        label = t('adminShell.tenant.badgeSuperAdminMode');
        tooltipParts.push(t('adminShell.tenant.superAdminModeBanner'));
        tooltipParts.push(t('license.badge.superAdminMode.tooltip'));
    } else if (isPlatformAdminHost && tenantSlug === 'admin') {
        color = 'orange';
        label = t('adminShell.tenant.badgePlatformAdmin');
        tooltipParts.push(t('adminShell.tenant.badgePlatformAdminTooltip'));
    } else {
        const slugForLabel = tenantSlug ?? '—';
        const displayName = tenantName?.trim();
        const nameForLabel = displayName || slugForLabel;
        label = t('common.tenant.badgeDualLabel', { name: nameForLabel });
        tooltipParts.push(t('common.tenant.tenantDescription'));
        tooltipParts.push(t('common.tenant.activeCompanyTooltip'));
        if (displayName && displayName !== slugForLabel) {
            tooltipParts.push(`${t('adminShell.tenant.info.slug')}: ${slugForLabel}`);
        }
        tooltipParts.push(
            t('adminShell.tenant.badge.tooltip', { id: tenantId?.trim() || '—' }),
        );
    }

    if (isImpersonating) {
        color = 'purple';
        tooltipParts.push(t('adminShell.tenant.badgeImpersonatingTooltip'));
    } else if (isDevTenantOverride) {
        tooltipParts.push(t('adminShell.tenant.badgeDevOverrideTooltip'));
    }

    const tag = (
        <Tag color={color} style={{ marginInlineEnd: 0, maxWidth: 300 }} className="tenant-badge">
            <span style={{ overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>{label}</span>
        </Tag>
    );

    if ((isPlatformAdminHost || isSuperAdminPlatformMode) && !isImpersonating) {
        return (
            <Tooltip title={tooltipParts.join(' · ')}>
                <Link href="/admin/tenants" style={{ display: 'inline-flex' }}>
                    {tag}
                </Link>
            </Tooltip>
        );
    }

    return <Tooltip title={tooltipParts.join(' · ')}>{tag}</Tooltip>;
}

/** @deprecated Use `TenantBadge` */
export const TenantContextBadge = TenantBadge;

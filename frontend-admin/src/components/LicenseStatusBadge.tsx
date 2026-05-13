'use client';

/**
 * Compact license mode indicator for the admin shell header (GET /api/admin/license/status).
 * Modes: expired, trial, licensed, demo account, or OpenAPI export dev snapshot.
 */

import { Tag, Tooltip } from 'antd';
import { useQuery } from '@tanstack/react-query';
import { useI18n } from '@/i18n';
import { useAuth } from '@/features/auth/hooks/useAuth';
import {
    getLicenseStatus,
    licenseQueryKeys,
    type LicenseStatusResponse,
} from '@/api/manual/adminLicense';

const REFETCH_INTERVAL_MS = 60_000;

export function LicenseStatusBadge() {
    const { t } = useI18n();
    const { user } = useAuth();

    const query = useQuery<LicenseStatusResponse>({
        queryKey: licenseQueryKeys.status,
        queryFn: getLicenseStatus,
        retry: false,
        refetchInterval: REFETCH_INTERVAL_MS,
        refetchOnWindowFocus: true,
        staleTime: 60 * 1000,
    });

    if (query.isPending && !query.data) {
        return (
            <Tag aria-busy="true" aria-live="polite">
                {t('license.badge.loading')}
            </Tag>
        );
    }

    if (query.isError || !query.data) {
        return (
            <Tooltip title={t('license.badge.unavailableTooltip')}>
                <Tag>{t('license.badge.unavailable')}</Tag>
            </Tooltip>
        );
    }

    const data = query.data;
    const daysRemaining = data.daysRemaining ?? 0;
    const isDemoUser = user?.isDemo === true;
    const isDevExport = (data.machineHash ?? '').toLowerCase() === 'openapi-export';

    if (data.isExpired) {
        return (
            <Tooltip title={t('license.badge.expired.tooltip')}>
                <Tag color="red">{t('license.badge.expired.label')}</Tag>
            </Tooltip>
        );
    }

    if (isDevExport) {
        return (
            <Tooltip title={t('license.badge.devExport.tooltip')}>
                <Tag color="purple">{t('license.badge.devExport.label')}</Tag>
            </Tooltip>
        );
    }

    if (data.isTrial) {
        const color = daysRemaining <= 7 ? 'orange' : 'blue';
        const trialHint = t('license.badge.trial.tooltip', { days: daysRemaining });
        const title = isDemoUser ? `${t('license.badge.demo.tooltip')} — ${trialHint}` : trialHint;
        return (
            <Tooltip title={title}>
                <Tag color={color}>{t('license.badge.trial.label', { days: daysRemaining })}</Tag>
            </Tooltip>
        );
    }

    if (isDemoUser) {
        const title = `${t('license.badge.demo.tooltip')} — ${t('license.badge.licensed.tooltip')}`;
        return (
            <Tooltip title={title}>
                <Tag color="gold">{t('license.badge.demo.label')}</Tag>
            </Tooltip>
        );
    }

    if (data.isValid && !data.isTrial) {
        return (
            <Tooltip title={t('license.badge.licensed.tooltip')}>
                <Tag color="green">{t('license.badge.licensed.label')}</Tag>
            </Tooltip>
        );
    }

    return (
        <Tooltip title={t('license.badge.unavailableTooltip')}>
            <Tag>{t('license.badge.unavailable')}</Tag>
        </Tooltip>
    );
}

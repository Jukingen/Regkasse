'use client';

import {
    CheckCircleOutlined,
    CloseCircleOutlined,
    ExclamationCircleOutlined,
    QuestionCircleOutlined,
} from '@ant-design/icons';
import { Popover, Space, Tag, Typography } from 'antd';
import { useQuery } from '@tanstack/react-query';
import { useI18n, formatDateTime } from '@/i18n';
import { getCashRegisterTseHealth } from '@/features/cash-registers/api/cashRegisters';
import type { TseHealthStatus } from '@/features/cash-registers/types/enhancedCashRegister';
import {
    normalizeTseHealthStatus,
    tseHealthStatusIcon,
    tseHealthTagColor,
} from '@/features/cash-registers/utils/tseHealthStatus';

export type TseHealthBadgeProps = {
    status: TseHealthStatus | string | null | undefined;
    registerId?: string | null;
    offlineQueueCount?: number;
    showDetails?: boolean;
};

function statusIcon(status: TseHealthStatus) {
    switch (tseHealthStatusIcon(status)) {
        case 'healthy':
            return <CheckCircleOutlined />;
        case 'degraded':
            return <ExclamationCircleOutlined />;
        case 'offline':
            return <CloseCircleOutlined />;
        case 'unknown':
        default:
            return <QuestionCircleOutlined />;
    }
}

export function TseHealthBadge({
    status,
    registerId,
    offlineQueueCount,
    showDetails = true,
}: TseHealthBadgeProps) {
    const { t, formatLocale } = useI18n();
    const normalized = normalizeTseHealthStatus(status);
    const id = registerId?.trim();

    const healthQuery = useQuery({
        queryKey: ['admin', 'cash-registers', id, 'tse-health'],
        queryFn: () => getCashRegisterTseHealth(id!),
        enabled: showDetails && Boolean(id),
        staleTime: 15_000,
    });

    const badge = (
        <Tag color={tseHealthTagColor(normalized)} icon={statusIcon(normalized)} style={{ cursor: showDetails ? 'pointer' : undefined }}>
            {t(`cashRegisters.tseHealth.${normalized}`)}
        </Tag>
    );

    if (!showDetails) {
        return badge;
    }

    const detail = healthQuery.data;
    const queue = detail?.offlineQueueCount ?? offlineQueueCount;

    const content = (
        <Space direction="vertical" size={4} style={{ maxWidth: 280 }}>
            <Typography.Text strong>{t(`cashRegisters.tseHealth.${normalized}`)}</Typography.Text>
            {detail?.lastCheckUtc ? (
                <Typography.Text type="secondary">
                    {t('cashRegisters.tseHealth.lastCheck')}:{' '}
                    {formatDateTime(detail.lastCheckUtc, formatLocale)}
                </Typography.Text>
            ) : null}
            {detail?.message ? (
                <Typography.Text type="secondary">{detail.message}</Typography.Text>
            ) : null}
            {typeof queue === 'number' && queue > 0 ? (
                <Typography.Text type="warning">
                    {t('cashRegisters.offlineQueue.tooltip', { count: queue })}
                </Typography.Text>
            ) : null}
        </Space>
    );

    return (
        <Popover content={content} title={t('cashRegisters.detail.tseStatus')} trigger={['hover', 'click']}>
            {badge}
        </Popover>
    );
}

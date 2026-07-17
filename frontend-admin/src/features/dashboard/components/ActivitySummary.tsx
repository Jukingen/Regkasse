'use client';

import Link from 'next/link';
import { BellOutlined } from '@ant-design/icons';
import { Card, Space, Tag, Typography } from 'antd';
import { SimpleList as List } from '@/components/ui/SimpleList';

import type { ActivitySeverity } from '@/api/manual/activityEvents';
import { useRecentActivities } from '@/features/audit/hooks/useRecentActivities';
import { formatRelativeTime } from '@/features/cash-registers/utils/formatRelativeTime';
import { useI18n } from '@/i18n/I18nProvider';

type Props = {
    limit?: number;
    viewAllHref?: string;
};

function getActivityTypeTagColor(type: string, severity: ActivitySeverity): string {
    if (severity === 'Error' || severity === 'Critical') {
        return 'red';
    }
    if (severity === 'Warning') {
        return 'orange';
    }

    const normalized = type.trim();
    const colors: Record<string, string> = {
        UserCreated: 'green',
        UserUpdated: 'geekblue',
        UserDeleted: 'default',
        CashRegisterOpened: 'purple',
        CashRegisterClosed: 'orange',
        CashRegisterDecommissioned: 'default',
        BackupSucceeded: 'green',
        BackupFailed: 'red',
        RestoreDrillSucceeded: 'green',
        RestoreDrillFailed: 'red',
        DailyClosingBackdatedCreated: 'orange',
        DailyClosingPendingReminder: 'gold',
        LicenseExpiringSoon: 'gold',
        LicenseExpired: 'red',
        FinanzOnlineSubmissionFailed: 'red',
        OfflineQueueGrowing: 'gold',
        OfflineOrdersBacklogGrowing: 'gold',
        OfflineOrdersExpiringSoon: 'gold',
        OfflineSyncStalled: 'red',
    };

    return colors[normalized] ?? 'blue';
}

export function ActivitySummary({ limit = 5, viewAllHref = '/audit-logs/activity' }: Props) {
    const { t, formatLocale } = useI18n();
    const { data: activities = [], isLoading } = useRecentActivities(limit);

    const formatTypeLabel = (type: string) => {
        const key = `activityNotifications.eventTypes.${type}` as const;
        const translated = t(key);
        return translated === key ? type : translated;
    };

    return (
        <Card
            title={
                <Space>
                    <BellOutlined />
                    {t('activity.summary.title')}
                </Space>
            }
            size="small"
            loading={isLoading}
            extra={<Link href={viewAllHref}>{t('activity.summary.viewAll')}</Link>}
        >
            <List
                dataSource={activities}
                renderItem={(item) => (
                    <List.Item style={{ padding: '8px 0' }}>
                        <Space orientation="vertical" size={2} style={{ width: '100%' }}>
                            <Space wrap>
                                <Tag color={getActivityTypeTagColor(item.type, item.severity)}>
                                    {formatTypeLabel(item.type)}
                                </Tag>
                                {item.actorName ? (
                                    <Typography.Text strong>{item.actorName}</Typography.Text>
                                ) : null}
                            </Space>
                            <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                                {item.description || item.title || '—'}
                            </Typography.Text>
                            <Typography.Text type="secondary" style={{ fontSize: 11, color: '#94a3b8' }}>
                                {formatRelativeTime(item.createdAtUtc, formatLocale)}
                            </Typography.Text>
                        </Space>
                    </List.Item>
                )}
                locale={{ emptyText: t('activity.summary.noActivities') }}
            />
        </Card>
    );
}

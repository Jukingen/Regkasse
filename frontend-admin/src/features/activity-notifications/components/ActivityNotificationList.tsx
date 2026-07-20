'use client';

import { Button, Empty, Tag } from 'antd';
import { SimpleList as List } from '@/components/ui/SimpleList';
import { CheckOutlined } from '@ant-design/icons';

import type { ActivityDto, ActivitySeverity } from '@/api/manual/activityEvents';
import { ListSkeleton } from '@/components/Skeleton';
import { formatRelativeTime } from '@/features/cash-registers/utils/formatRelativeTime';
import { NotificationIcon } from '@/features/activity-notifications/components/NotificationIcon';
import styles from '@/features/activity-notifications/components/activityNotifications.module.css';
import { useI18n } from '@/i18n/I18nProvider';

function severityColor(severity: ActivitySeverity): string {
    switch (severity) {
        case 'Critical':
        case 'Error':
            return 'error';
        case 'Warning':
            return 'warning';
        default:
            return 'default';
    }
}

type Props = {
    items: ActivityDto[];
    loading: boolean;
    emptyLabel: string;
    onMarkRead: (id: string) => void;
    markReadPendingId?: string | null;
};

export function ActivityNotificationList({ items, loading, emptyLabel, onMarkRead, markReadPendingId }: Props) {
    const { t, formatLocale } = useI18n();

    if (loading) {
        return <ListSkeleton count={5} />;
    }

    if (items.length === 0) {
        return <Empty description={emptyLabel} image={Empty.PRESENTED_IMAGE_SIMPLE} />;
    }

    return (
        <List
            itemLayout="horizontal"
            dataSource={items}
            renderItem={(activity) => (
                <List.Item
                    className={!activity.isRead ? styles.unreadItem : undefined}
                    role={activity.isRead ? undefined : 'button'}
                    tabIndex={activity.isRead ? undefined : 0}
                    onClick={() => {
                        if (!activity.isRead) onMarkRead(activity.id);
                    }}
                    onKeyDown={(event) => {
                        if (activity.isRead) return;
                        if (event.key === 'Enter' || event.key === ' ') {
                            event.preventDefault();
                            onMarkRead(activity.id);
                        }
                    }}
                    style={{ cursor: activity.isRead ? undefined : 'pointer' }}
                    actions={[
                        <Button
                            key="read"
                            type="text"
                            size="small"
                            icon={<CheckOutlined />}
                            aria-label={t('activityNotifications.markRead')}
                            hidden={activity.isRead}
                            loading={markReadPendingId === activity.id}
                            onClick={(event) => {
                                event.stopPropagation();
                                onMarkRead(activity.id);
                            }}
                        />,
                    ]}
                >
                    <div className={styles.iconWrap}>
                        <NotificationIcon activity={activity} />
                    </div>
                    <List.Item.Meta
                        className={styles.listMeta}
                        title={
                            <span style={{ display: 'flex', gap: 8, alignItems: 'center', flexWrap: 'wrap' }}>
                                <span style={{ fontWeight: activity.isRead ? 400 : 600 }}>{activity.title}</span>
                                <Tag color={severityColor(activity.severity)} style={{ margin: 0 }}>
                                    {activity.severity}
                                </Tag>
                            </span>
                        }
                        description={
                            <>
                                {activity.description ? <div>{activity.description}</div> : null}
                                <small style={{ color: 'rgba(0,0,0,0.45)' }}>
                                    {formatRelativeTime(activity.createdAtUtc, formatLocale)}
                                    {activity.actorName
                                        ? ` · ${t('activityNotifications.byActor', { name: activity.actorName })}`
                                        : null}
                                </small>
                            </>
                        }
                    />
                </List.Item>
            )}
        />
    );
}

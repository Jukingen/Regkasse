'use client';

import { useMemo, useState } from 'react';
import { Button, Drawer, Tabs } from 'antd';

import type { ActivityDto } from '@/api/manual/activityEvents';
import { ActivityNotificationList } from '@/features/activity-notifications/components/ActivityNotificationList';
import { NotificationSettingsForm } from '@/features/activity-notifications/components/NotificationSettingsForm';
import styles from '@/features/activity-notifications/components/activityNotifications.module.css';
import {
    useActivitiesList,
    useActivityNotificationsAccess,
    useMarkActivityRead,
    useMarkAllActivitiesRead,
} from '@/features/activity-notifications/hooks/useActivityNotifications';
import { useActivityStream } from '@/features/activity-notifications/hooks/useActivityStream';
import { useI18n } from '@/i18n/I18nProvider';
import { usePermissions } from '@/shared/auth/usePermissions';
import { PERMISSIONS } from '@/shared/auth/permissions';

type Props = {
    open: boolean;
    onClose: () => void;
};

export function NotificationCenterDrawer({ open, onClose }: Props) {
    const { t } = useI18n();
    const { hasPermission } = usePermissions();
    const canManageSettings = hasPermission(PERMISSIONS.SETTINGS_MANAGE);
    const canSee = useActivityNotificationsAccess();

    const [activeTab, setActiveTab] = useState('all');
    const [markingId, setMarkingId] = useState<string | null>(null);

    const { data: list, isLoading: listLoading } = useActivitiesList(canSee && open);
    const { liveItems } = useActivityStream(canSee && open);
    const markRead = useMarkActivityRead();
    const markAllRead = useMarkAllActivitiesRead();

    const feedItems = useMemo(() => {
        const byId = new Map<string, ActivityDto>();
        for (const item of liveItems) {
            byId.set(item.id, item);
        }
        for (const item of list?.items ?? []) {
            if (!byId.has(item.id)) {
                byId.set(item.id, item);
            }
        }
        return [...byId.values()].sort(
            (a, b) => new Date(b.createdAtUtc).getTime() - new Date(a.createdAtUtc).getTime(),
        );
    }, [list?.items, liveItems]);

    const unreadItems = useMemo(() => feedItems.filter((a) => !a.isRead), [feedItems]);

    const handleMarkRead = (id: string) => {
        setMarkingId(id);
        markRead.mutate(id, {
            onSettled: () => setMarkingId(null),
        });
    };

    const listProps = {
        loading: listLoading && feedItems.length === 0,
        onMarkRead: handleMarkRead,
        markReadPendingId: markingId,
    };

    const tabItems = [
        {
            key: 'all',
            label: t('activityNotifications.tabs.all'),
            children: (
                <ActivityNotificationList
                    items={feedItems}
                    emptyLabel={t('activityNotifications.empty')}
                    {...listProps}
                />
            ),
        },
        {
            key: 'unread',
            label: t('activityNotifications.tabs.unread', { count: unreadItems.length }),
            children: (
                <ActivityNotificationList
                    items={unreadItems}
                    emptyLabel={t('activityNotifications.emptyUnread')}
                    {...listProps}
                />
            ),
        },
    ];

    if (canManageSettings) {
        tabItems.push({
            key: 'settings',
            label: t('activityNotifications.tabs.settings'),
            children: <NotificationSettingsForm />,
        });
    }

    if (!canSee) {
        return null;
    }

    return (
        <Drawer
            title={t('activityNotifications.title')}
            open={open}
            onClose={onClose}
            size={480}
            classNames={{ body: styles.drawerBody }}
            extra={
                unreadItems.length > 0 ? (
                    <Button
                        type="link"
                        size="small"
                        loading={markAllRead.isPending}
                        onClick={() => markAllRead.mutate()}
                    >
                        {t('activityNotifications.markAllRead')}
                    </Button>
                ) : null
            }
        >
            <Tabs activeKey={activeTab} onChange={setActiveTab} items={tabItems} />
        </Drawer>
    );
}

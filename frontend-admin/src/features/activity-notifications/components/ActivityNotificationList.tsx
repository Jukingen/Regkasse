'use client';

import { CheckOutlined } from '@ant-design/icons';
import { Button, Empty, Tag } from 'antd';

import type { ActivityDto, ActivitySeverity } from '@/api/manual/activityEvents';
import { ListSkeleton } from '@/components/Skeleton';
import { SimpleList as List } from '@/components/ui/SimpleList';
import { NotificationIcon } from '@/features/activity-notifications/components/NotificationIcon';
import styles from '@/features/activity-notifications/components/activityNotifications.module.css';
import {
  formatActivityTitle,
  formatActivityWhatChanged,
} from '@/features/activity-notifications/formatActivityTitle';
import { isPermissionActivityType } from '@/features/activity-notifications/activityTypes';
import { formatRelativeTime } from '@/features/cash-registers/utils/formatRelativeTime';
import { useI18n } from '@/i18n/I18nProvider';

function severityColor(severity: ActivitySeverity): string {
  switch (severity) {
    case 'Critical':
    case 'Error':
      return 'error';
    case 'Warning':
      return 'warning';
    default:
      return 'success';
  }
}

function severityMarker(severity: ActivitySeverity): string {
  switch (severity) {
    case 'Critical':
    case 'Error':
      return '🔴';
    case 'Warning':
      return '🟡';
    default:
      return '🟢';
  }
}

function formatClock(iso: string, locale: string): string {
  try {
    return new Intl.DateTimeFormat(locale, { hour: '2-digit', minute: '2-digit' }).format(
      new Date(iso)
    );
  } catch {
    return '';
  }
}

type Props = {
  items: ActivityDto[];
  loading: boolean;
  emptyLabel: string;
  onMarkRead: (id: string) => void;
  markReadPendingId?: string | null;
};

export function ActivityNotificationList({
  items,
  loading,
  emptyLabel,
  onMarkRead,
  markReadPendingId,
}: Props) {
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
      renderItem={(activity) => {
        const title = formatActivityTitle(activity, t);
        const whatChanged = formatActivityWhatChanged(activity);
        const clock = formatClock(activity.createdAtUtc, formatLocale);

        return (
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
                  <span aria-hidden>{severityMarker(activity.severity)}</span>
                  {clock ? (
                    <span style={{ color: 'rgba(0,0,0,0.45)', fontVariantNumeric: 'tabular-nums' }}>
                      {clock}
                    </span>
                  ) : null}
                  <span style={{ fontWeight: activity.isRead ? 400 : 600 }}>{title}</span>
                  <Tag color={severityColor(activity.severity)} style={{ margin: 0 }}>
                    {activity.severity}
                  </Tag>
                </span>
              }
              description={
                <>
                  {whatChanged ? (
                    <div>
                      {isPermissionActivityType(activity.type) ? (
                        <>
                          <strong>{t('activityNotifications.whatChangedLabel')}</strong>{' '}
                          {whatChanged}
                        </>
                      ) : (
                        whatChanged
                      )}
                    </div>
                  ) : null}
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
        );
      }}
    />
  );
}

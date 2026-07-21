'use client';

import { BellOutlined } from '@ant-design/icons';
import { Badge, Button } from 'antd';
import { useState } from 'react';

import { NotificationCenterDrawer } from '@/features/activity-notifications/components/NotificationCenterDrawer';
import {
  useActivityNotificationsAccess,
  useActivityUnreadCount,
} from '@/features/activity-notifications/hooks/useActivityNotifications';
import { useI18n } from '@/i18n/I18nProvider';

/** Header bell with unread badge; opens the activity notification drawer. */
export function ActivityNotificationsBell() {
  const { t } = useI18n();
  const canSee = useActivityNotificationsAccess();
  const [notificationsOpen, setNotificationsOpen] = useState(false);

  const { data: unread, isLoading: unreadLoading } = useActivityUnreadCount(canSee);
  const unreadCount = unread?.unreadCount ?? 0;

  if (!canSee) {
    return null;
  }

  return (
    <>
      <Button
        type="default"
        size="small"
        className="admin-header-tool-btn admin-header-icon-btn"
        aria-label={t('activityNotifications.bellAria')}
        onClick={() => setNotificationsOpen(true)}
        icon={
          <Badge count={unreadCount} size="small" offset={[-2, 2]}>
            <BellOutlined style={{ fontSize: 16, color: unreadLoading ? '#bfbfbf' : undefined }} />
          </Badge>
        }
      />
      <NotificationCenterDrawer
        open={notificationsOpen}
        onClose={() => setNotificationsOpen(false)}
      />
    </>
  );
}

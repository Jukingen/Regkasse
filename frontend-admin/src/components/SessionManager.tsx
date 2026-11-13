'use client';

import { Button, Card, Space, Tag, Typography } from 'antd';

import { SimpleList as List } from '@/components/ui/SimpleList';
import type { ActiveSession } from '@/features/auth/api/sessionsApi';
import { useSessions } from '@/features/auth/hooks/useSessions';
import { useAntdApp } from '@/hooks/useAntdApp';
import { useI18n } from '@/i18n';
import { formatDateTime } from '@/lib/dateUtils';

function formatClientApp(app: string): string {
  if (app === 'admin') return 'Admin';
  if (app === 'pos') return 'POS';
  return app;
}

/** Compact session list (e.g. profile). Full table: `/settings/sessions`. */
export function SessionManager() {
  const { message } = useAntdApp();
  const { t } = useI18n();
  const { sessions, isLoading, refetch, revoke, revokeOthers, isRevoking } = useSessions();

  const handleTerminate = async (session: ActiveSession) => {
    if (session.isCurrent) {
      message.warning(t('common.auth.sessions.cannotTerminateCurrent'));
      return;
    }
    try {
      await revoke(session.id);
      message.success(t('common.auth.sessions.terminated'));
    } catch {
      message.error(t('common.auth.sessions.terminateFailed'));
    }
  };

  const handleTerminateOthers = async () => {
    try {
      const count = await revokeOthers();
      message.success(t('common.auth.sessions.terminatedOthers', { count: String(count) }));
    } catch {
      message.error(t('common.auth.sessions.terminateFailed'));
    }
  };

  return (
    <Card
      title={t('common.auth.sessions.title')}
      extra={
        <Button size="small" onClick={() => void refetch()} loading={isLoading}>
          {t('common.buttons.refresh')}
        </Button>
      }
    >
      <Space orientation="vertical" style={{ width: '100%' }} size="middle">
        <Typography.Paragraph type="secondary" style={{ marginBottom: 0 }}>
          {t('common.auth.sessions.description')}
        </Typography.Paragraph>
        <Button
          onClick={() => void handleTerminateOthers()}
          disabled={sessions.filter((s) => !s.isCurrent).length === 0}
          loading={isRevoking}
        >
          {t('common.auth.sessions.terminateAllOthers')}
        </Button>
        <List
          loading={isLoading}
          dataSource={sessions}
          locale={{ emptyText: t('common.auth.sessions.empty') }}
          renderItem={(item) => (
            <List.Item
              actions={
                item.isCurrent
                  ? undefined
                  : [
                      <Button
                        key="end"
                        type="link"
                        danger
                        size="small"
                        loading={isRevoking}
                        onClick={() => void handleTerminate(item)}
                      >
                        {t('common.auth.sessions.endSession')}
                      </Button>,
                    ]
              }
            >
              <List.Item.Meta
                title={
                  <Space wrap>
                    {item.deviceName || formatClientApp(item.clientApp)}
                    {item.isCurrent ? (
                      <Tag color="blue">{t('common.auth.sessions.thisDevice')}</Tag>
                    ) : null}
                  </Space>
                }
                description={
                  <>
                    {[item.browser, item.os, item.ipAddress, formatClientApp(item.clientApp)]
                      .filter(Boolean)
                      .join(' · ') || '—'}
                    <br />
                    {t('common.auth.sessions.lastActivity')}:{' '}
                    {formatDateTime(item.lastActivityAtUtc)}
                  </>
                }
              />
            </List.Item>
          )}
        />
      </Space>
    </Card>
  );
}

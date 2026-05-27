'use client';

import { useCallback } from 'react';
import { Button, Card, List, Space, Tag, Typography, message } from 'antd';
import dayjs from 'dayjs';
import { useQuery, useQueryClient } from '@tanstack/react-query';

import {
    fetchMySessions,
    terminateAllOtherSessions,
    terminateSession,
    type ActiveSession,
} from '@/features/auth/api/sessionsApi';
import { useI18n } from '@/i18n';

const SESSIONS_KEY = ['user', 'sessions'] as const;

function formatClientApp(app: string): string {
    if (app === 'admin') return 'Admin';
    if (app === 'pos') return 'POS';
    return app;
}

export function SessionManager() {
    const { t } = useI18n();
    const queryClient = useQueryClient();

    const { data: sessions = [], isLoading, refetch } = useQuery({
        queryKey: SESSIONS_KEY,
        queryFn: fetchMySessions,
    });

    const invalidate = useCallback(() => {
        void queryClient.invalidateQueries({ queryKey: SESSIONS_KEY });
    }, [queryClient]);

    const handleTerminate = async (session: ActiveSession) => {
        if (session.isCurrent) {
            message.warning(t('common.auth.sessions.cannotTerminateCurrent'));
            return;
        }
        try {
            await terminateSession(session.id);
            message.success(t('common.auth.sessions.terminated'));
            invalidate();
        } catch {
            message.error(t('common.auth.sessions.terminateFailed'));
        }
    };

    const handleTerminateOthers = async () => {
        try {
            const count = await terminateAllOtherSessions();
            message.success(t('common.auth.sessions.terminatedOthers', { count: String(count) }));
            invalidate();
        } catch {
            message.error(t('common.auth.sessions.terminateFailed'));
        }
    };

    return (
        <Card
            title={t('common.auth.sessions.title')}
            extra={
                <Button size="small" onClick={() => refetch()} loading={isLoading}>
                    {t('common.buttons.refresh')}
                </Button>
            }
        >
            <Space direction="vertical" style={{ width: '100%' }} size="middle">
                <Typography.Paragraph type="secondary" style={{ marginBottom: 0 }}>
                    {t('common.auth.sessions.description')}
                </Typography.Paragraph>
                <Button onClick={handleTerminateOthers} disabled={sessions.filter((s) => !s.isCurrent).length === 0}>
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
                                              onClick={() => handleTerminate(item)}
                                          >
                                              {t('common.auth.sessions.endSession')}
                                          </Button>,
                                      ]
                            }
                        >
                            <List.Item.Meta
                                title={
                                    <Space>
                                        {formatClientApp(item.clientApp)}
                                        {item.isCurrent ? (
                                            <Tag color="blue">{t('common.auth.sessions.thisDevice')}</Tag>
                                        ) : null}
                                    </Space>
                                }
                                description={
                                    <>
                                        {item.ipAddress ? `${item.ipAddress} · ` : ''}
                                        {item.deviceId ?? '—'}
                                        <br />
                                        {t('common.auth.sessions.lastActivity')}:{' '}
                                        {dayjs(item.lastActivityAtUtc).format('DD.MM.YYYY HH:mm')}
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

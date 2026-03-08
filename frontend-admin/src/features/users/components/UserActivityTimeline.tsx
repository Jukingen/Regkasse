/**
 * User activity timeline – GET api/AuditLog/user/{userId}. Backend ensures audit_logs table at startup; empty state when no logs.
 */
import React, { useState, useMemo } from 'react';
import { Table, Tag, Typography, Empty, Alert, Button } from 'antd';
import { useGetApiAuditLogUserUserId } from '@/api/generated/audit-log/audit-log';
import dayjs from 'dayjs';
import { usersCopy } from '../constants/copy';

const { Text } = Typography;

type Props = {
    userId: string;
    userName?: string;
};

const AUDIT_LOG_STALE_MS = 60 * 1000;
const PAGE_SIZE = 10;

export function UserActivityTimeline({ userId, userName }: Props) {
    const [page, setPage] = useState(1);
    const validUserId = (userId ?? '').trim();
    const params = useMemo(() => ({ page, pageSize: PAGE_SIZE }), [page]);

    const { data, isLoading, isError, refetch } = useGetApiAuditLogUserUserId(
        validUserId,
        params,
        {
            query: {
                enabled: validUserId.length > 0,
                staleTime: AUDIT_LOG_STALE_MS,
                retry: false,
                refetchOnWindowFocus: false,
                refetchOnReconnect: false,
            },
        }
    );

    const columns = [
        {
            title: usersCopy.activityTime,
            dataIndex: 'timestamp',
            key: 'timestamp',
            width: 160,
            render: (v: string) => (v ? dayjs(v).format('DD.MM.YYYY HH:mm:ss') : '—'),
        },
        {
            title: usersCopy.action,
            dataIndex: 'action',
            key: 'action',
            render: (action: string) => <Tag color="blue">{action}</Tag>,
        },
        {
            title: usersCopy.description,
            dataIndex: 'description',
            key: 'description',
            ellipsis: true,
        },
        {
            title: usersCopy.status,
            dataIndex: 'status',
            key: 'status',
            width: 100,
            render: (status: string) => (
                <Tag color={status === 'Success' ? 'green' : 'red'}>{status}</Tag>
            ),
        },
    ];

    const list = Array.isArray(data?.auditLogs) ? data.auditLogs : [];
    const total = typeof data?.totalCount === 'number' ? data.totalCount : 0;

    if (validUserId.length === 0) {
        return (
            <Alert type="info" message={usersCopy.emptyActivity} showIcon />
        );
    }

    if (isError) {
        return (
            <Alert
                type="warning"
                message={usersCopy.errorLoadActivity}
                description={usersCopy.errorLoadActivityHint}
                action={
                    <Button size="small" onClick={() => refetch()}>
                        {usersCopy.retry}
                    </Button>
                }
            />
        );
    }

    return (
        <div>
            {userName && (
                <Text type="secondary" style={{ display: 'block', marginBottom: 8 }}>
                    {usersCopy.activityFor}: {userName}
                </Text>
            )}
            <Table
                size="small"
                columns={columns}
                dataSource={list}
                loading={isLoading}
                rowKey={(r) => r.id ?? `${r.timestamp}-${r.action}`}
                pagination={{
                    current: page,
                    pageSize: PAGE_SIZE,
                    total,
                    showSizeChanger: false,
                    onChange: setPage,
                }}
                locale={{
                    emptyText: <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description={usersCopy.emptyActivity} />,
                }}
            />
        </div>
    );
}

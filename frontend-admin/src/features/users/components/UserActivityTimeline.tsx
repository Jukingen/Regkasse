/**
 * Kullanıcı aktivite zaman çizelgesi – GET api/AuditLog/user/{userId} ile denetim kayıtları.
 * RKSV/DSGVO: Who did what, when. Loading, empty, error states.
 */
import React, { useState } from 'react';
import { Table, Tag, Typography, Empty, Alert, Button } from 'antd';
import { useGetApiAuditLogUserUserId } from '@/api/generated/audit-log/audit-log';
import dayjs from 'dayjs';
import { usersCopy } from '../constants/copy';

const { Text } = Typography;

type Props = {
    userId: string;
    userName?: string;
};

export function UserActivityTimeline({ userId, userName }: Props) {
    const [page, setPage] = useState(1);
    const pageSize = 10;

    const { data, isLoading, isError, refetch } = useGetApiAuditLogUserUserId(userId, { page, pageSize });

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

    const list = data?.auditLogs ?? [];
    const total = data?.totalCount ?? 0;

    if (isError) {
        return (
            <Alert
                type="error"
                message={usersCopy.errorLoad}
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
                    pageSize,
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

'use client';

import React from 'react';
import { Card, Table, Tag, Typography, Switch, Space, Alert } from 'antd';
import { useSearchParams } from 'next/navigation';
import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { useGetApiAuditLog, useGetApiAuditLogCorrelationCorrelationId } from '@/api/generated/audit-log/audit-log';
import type { AuditLogEntryDto } from '@/api/generated/model';
import dayjs from 'dayjs';

export default function RksvVerificationsPage() {
    const searchParams = useSearchParams();
    const correlationId = searchParams?.get('correlationId') ?? undefined;

    const { data, isLoading } = useGetApiAuditLog({ page: 1, pageSize: 100 });
    const { data: correlationData, isLoading: correlationLoading } = useGetApiAuditLogCorrelationCorrelationId(
        correlationId ?? '',
        { query: { enabled: !!correlationId } }
    );

    const useCorrelation = !!correlationId;
    const list = useCorrelation ? (correlationData?.auditLogs ?? []) : (data?.auditLogs ?? []);
    const isLoadingList = useCorrelation ? correlationLoading : isLoading;

    const signatureEntries =
        (useCorrelation ? list : data?.auditLogs)?.filter(
            (e: AuditLogEntryDto) =>
                e.action?.toLowerCase().includes('signature') ||
                e.action?.toLowerCase().includes('offline') ||
                e.entityType?.toLowerCase().includes('receipt') ||
                e.entityType?.toLowerCase().includes('payment') ||
                e.entityType?.toLowerCase().includes('offlinetransaction')
        ) ?? (useCorrelation ? list : []);

    const [offlineOriginOnly, setOfflineOriginOnly] = React.useState(false);
    const [failedReplayOnly, setFailedReplayOnly] = React.useState(false);
    const [suspiciousTimingOnly, setSuspiciousTimingOnly] = React.useState(false);

    const filteredEntries = React.useMemo(() => {
        return signatureEntries.filter((e: AuditLogEntryDto) => {
            const action = String(e.action ?? '').toLowerCase();
            const entity = String(e.entityType ?? '').toLowerCase();

            if (offlineOriginOnly) {
                const isOfflineRelated =
                    action.includes('offline') || entity.includes('offlinetransaction');
                if (!isOfflineRelated) return false;
            }

            if (failedReplayOnly) {
                const isFailed =
                    action.includes('offline_replay_failed') ||
                    action.includes('offline_replay_exception') ||
                    action.includes('max_retry_limit_exceeded') ||
                    action.includes('payload_immutable_mismatch') ||
                    action.includes('sequence_duplicate');
                if (!isFailed) return false;
            }

            if (suspiciousTimingOnly) {
                if (!action.includes('clock_drift_warning')) return false;
            }

            return true;
        });
    }, [signatureEntries, offlineOriginOnly, failedReplayOnly, suspiciousTimingOnly]);

    const columns = [
        {
            title: 'Timestamp',
            dataIndex: 'timestamp',
            key: 'timestamp',
            width: 180,
            render: (ts: string) => dayjs(ts).format('DD.MM.YYYY HH:mm:ss'),
        },
        {
            title: 'User',
            key: 'userName',
            render: (_: unknown, r: AuditLogEntryDto) => r.actorDisplayName ?? r.userId ?? '—',
        },
        {
            title: 'Action',
            dataIndex: 'action',
            key: 'action',
            render: (a: string | null | undefined) => <Tag color="blue">{a ?? '—'}</Tag>,
        },
        {
            title: 'Entity',
            dataIndex: 'entityType',
            key: 'entityType',
        },
        {
            title: 'Status',
            dataIndex: 'status',
            key: 'status',
            render: (s: string) => <Tag color={s === 'Success' ? 'green' : 'red'}>{s ?? '—'}</Tag>,
        },
        {
            title: 'Details',
            dataIndex: 'description',
            key: 'description',
            ellipsis: true,
        },
    ];

    return (
        <>
            <AdminPageHeader
                title="Last 100 Verification Results"
                breadcrumbs={[
                    { title: 'Dashboard', href: '/dashboard' },
                    { title: 'RKSV', href: '/rksv' },
                    { title: 'Verifications' },
                ]}
            />

            <Card>
                {correlationId && (
                    <Alert
                        type="info"
                        message={`Log-Trace für Correlation-ID: ${correlationId}`}
                        style={{ marginBottom: 16 }}
                    />
                )}
                <Typography.Paragraph type="secondary" style={{ marginBottom: 16 }}>
                    {useCorrelation
                        ? `Audit-Logs für Replay-Batch-Correlation (${list.length} Einträge).`
                        : 'Signatur-, Zahlungs- und Offline-Replay-Audit (OFFLINE_CREATED / OFFLINE_SYNCED, max. 100).'}
                </Typography.Paragraph>

                <Space direction="horizontal" wrap style={{ marginBottom: 12 }}>
                    <Space direction="horizontal">
                        <Typography.Text>Offline-Ursprung</Typography.Text>
                        <Switch checked={offlineOriginOnly} onChange={setOfflineOriginOnly} />
                    </Space>
                    <Space direction="horizontal">
                        <Typography.Text>Fehlerhafte Replays</Typography.Text>
                        <Switch checked={failedReplayOnly} onChange={setFailedReplayOnly} />
                    </Space>
                    <Space direction="horizontal">
                        <Typography.Text>Verdächtige Offline-Zeit</Typography.Text>
                        <Switch checked={suspiciousTimingOnly} onChange={setSuspiciousTimingOnly} />
                    </Space>
                </Space>
                <Table
                    columns={columns}
                    dataSource={useCorrelation ? list : filteredEntries}
                    loading={isLoadingList}
                    rowKey={(r) => r.id ?? r.timestamp ?? r.createdAt ?? ''}
                    pagination={false}
                    size="small"
                />
            </Card>
        </>
    );
}

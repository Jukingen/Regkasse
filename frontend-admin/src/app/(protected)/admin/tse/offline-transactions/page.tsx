'use client';

import { useAntdApp } from '@/hooks/useAntdApp';
/**
 * Admin: server-side non-fiscal offline payment queue (TSE replay backlog).
 * Operator copy: de-DE (Austria).
 */

import React, { useCallback, useMemo, useState } from 'react';
import { Alert, Button, Card, Col, DatePicker, Form, Row, Select, Space, Table, Tag, Typography } from 'antd';
import type { ColumnsType, TablePaginationConfig } from 'antd/es/table';
import { CloudDownloadOutlined, RedoOutlined, SyncOutlined } from '@ant-design/icons';
import dayjs, { type Dayjs } from 'dayjs';
import utc from 'dayjs/plugin/utc';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { adminOverviewCrumb } from '@/shared/adminShellLabels';
import { AXIOS_INSTANCE } from '@/lib/axios';
import { useGetApiCashRegister } from '@/api/generated/cash-register/cash-register';
import {
    getApiAdminOfflineTransactions,
    getApiAdminOfflineTransactionsSummary,
    getGetApiAdminOfflineTransactionsQueryKey,
    getGetApiAdminOfflineTransactionsSummaryQueryKey,
    postApiAdminOfflineTransactionsIdRetry,
    postApiAdminOfflineTransactionsRetryAll,
} from '@/api/generated/admin/admin';
import type { AdminOfflineTransactionRowDto } from '@/api/generated/model/adminOfflineTransactionRowDto';
import { useI18n } from '@/i18n/I18nProvider';
import { usePermissions } from '@/shared/auth/usePermissions';
import { PERMISSIONS } from '@/shared/auth/permissions';

dayjs.extend(utc);

const REFETCH_MS = 30_000;

type StatusFilter = 'all' | 'pending' | 'completed' | 'failed';

function paymentMethodLabel(raw: string): string {
    const m = (raw || '').toLowerCase();
    if (m === 'cash') return 'Bar';
    if (m === 'card') return 'Karte';
    if (m === 'voucher') return 'Gutschein';
    return raw || '—';
}

function statusTag(record: AdminOfflineTransactionRowDto) {
    const s = record.status ?? '';
    if (s === 'Synced') return <Tag color="success">Abgeschlossen</Tag>;
    if (s === 'Failed') return <Tag color="error">Fehlgeschlagen</Tag>;
    if (s === 'Pending' || s === 'NonFiscalPending') return <Tag color="warning">Ausstehend</Tag>;
    return <Tag>{s}</Tag>;
}

export default function AdminOfflineTransactionsPage() {
  const { message } = useAntdApp();

    const { t } = useI18n();
    const { hasPermission } = usePermissions();
    const allowed = hasPermission(PERMISSIONS.PAYMENT_VIEW);
    const queryClient = useQueryClient();

    const [statusGroup, setStatusGroup] = useState<StatusFilter>('all');
    const [cashRegisterId, setCashRegisterId] = useState<string | undefined>(undefined);
    const [rangeUtc, setRangeUtc] = useState<[Dayjs, Dayjs] | null>(null);
    const [page, setPage] = useState(1);
    const [pageSize, setPageSize] = useState(50);

    const { data: registersData } = useGetApiCashRegister({
        query: { enabled: allowed, staleTime: 120_000 },
    });

    const registerOptions = useMemo(() => {
        const raw = registersData as unknown;
        const arr = Array.isArray(raw)
            ? raw
            : (raw as { registers?: { id?: string; registerNumber?: string; location?: string }[] })?.registers ??
              [];
        return (arr as { id?: string; registerNumber?: string; location?: string }[])
            .filter((r) => r?.id)
            .map((r) => ({
                value: r.id as string,
                label: `${r.registerNumber ?? ''} · ${r.location ?? ''}`,
            }));
    }, [registersData]);

    const listParams = useMemo(() => {
        const fromUtc = rangeUtc?.[0]?.utc().startOf('day').toISOString();
        const toUtc = rangeUtc?.[1]?.utc().endOf('day').toISOString();
        return {
            statusGroup: statusGroup === 'all' ? undefined : statusGroup,
            cashRegisterId,
            fromUtc,
            toUtc,
            pageNumber: page,
            pageSize,
        };
    }, [statusGroup, cashRegisterId, rangeUtc, page, pageSize]);

    const summaryQuery = useQuery({
        queryKey: getGetApiAdminOfflineTransactionsSummaryQueryKey(),
        queryFn: ({ signal }) => getApiAdminOfflineTransactionsSummary({ signal }),
        enabled: allowed,
        refetchInterval: REFETCH_MS,
    });

    const listQuery = useQuery({
        queryKey: getGetApiAdminOfflineTransactionsQueryKey(listParams),
        queryFn: ({ signal }) => getApiAdminOfflineTransactions(listParams, undefined, signal),
        enabled: allowed,
        refetchInterval: REFETCH_MS,
    });

    const invalidate = useCallback(() => {
        void queryClient.invalidateQueries({ queryKey: getGetApiAdminOfflineTransactionsSummaryQueryKey() });
        void queryClient.invalidateQueries({ queryKey: ['/api/admin/offline-transactions'] });
    }, [queryClient]);

    const [retryingId, setRetryingId] = useState<string | null>(null);
    const [retryAllBusy, setRetryAllBusy] = useState(false);

    const handleRetryOne = async (id: string) => {
        setRetryingId(id);
        try {
            const res = await postApiAdminOfflineTransactionsIdRetry(id);
            message.success(
                `Replay gestartet${res.replayBatchCorrelationId ? ` (${res.replayBatchCorrelationId})` : ''}`,
            );
            invalidate();
        } catch (e: unknown) {
            message.error(String((e as Error)?.message ?? 'Retry fehlgeschlagen'));
        } finally {
            setRetryingId(null);
        }
    };

    const handleRetryAll = async () => {
        setRetryAllBusy(true);
        try {
            const res = await postApiAdminOfflineTransactionsRetryAll();
            message.success(
                res.queuedCount === 0
                    ? 'Keine fehlgeschlagenen Einträge.'
                    : `${res.queuedCount} Einträge in Replay-Kette.`,
            );
            invalidate();
        } catch (e: unknown) {
            message.error(String((e as Error)?.message ?? 'Retry alle fehlgeschlagen'));
        } finally {
            setRetryAllBusy(false);
        }
    };

    const handleExportFailed = async () => {
        try {
            const params = {
                cashRegisterId,
                fromUtc: rangeUtc?.[0]?.utc().startOf('day').toISOString(),
                toUtc: rangeUtc?.[1]?.utc().endOf('day').toISOString(),
            };
            const res = await AXIOS_INSTANCE.get('/api/admin/offline-transactions/export-failed', {
                params,
                responseType: 'blob',
            });
            const blob = res.data as Blob;
            const url = URL.createObjectURL(blob);
            const a = document.createElement('a');
            a.href = url;
            a.download = `offline-failed-${dayjs().format('YYYYMMDDHHmmss')}.csv`;
            document.body.appendChild(a);
            a.click();
            a.remove();
            URL.revokeObjectURL(url);
            message.success('CSV exportiert.');
        } catch {
            message.error('Export fehlgeschlagen');
        }
    };

    const columns: ColumnsType<AdminOfflineTransactionRowDto> = [
        {
            title: 'Transaktions-ID',
            dataIndex: 'id',
            key: 'id',
            render: (v: string) => <Typography.Text code copyable={{ text: v }}>{v.slice(0, 8)}…</Typography.Text>,
            width: 140,
        },
        {
            title: 'Kasse',
            dataIndex: 'cashRegisterLabel',
            key: 'cashRegisterLabel',
            ellipsis: true,
        },
        {
            title: 'Empfangen (UTC)',
            dataIndex: 'serverReceivedAtUtc',
            key: 'serverReceivedAtUtc',
            width: 200,
            render: (v: string) => dayjs(v).utc().format('DD.MM.YYYY HH:mm:ss'),
        },
        {
            title: 'Betrag',
            dataIndex: 'amount',
            key: 'amount',
            align: 'right',
            width: 110,
            render: (v: number) =>
                typeof v === 'number'
                    ? v.toLocaleString('de-AT', { style: 'currency', currency: 'EUR' })
                    : '—',
        },
        {
            title: 'Zahlart',
            dataIndex: 'paymentMethod',
            key: 'paymentMethod',
            width: 110,
            render: (v: string) => paymentMethodLabel(v),
        },
        {
            title: 'Status',
            key: 'status',
            width: 140,
            render: (_, row) => statusTag(row),
        },
        {
            title: 'Retries',
            dataIndex: 'retryCount',
            key: 'retryCount',
            width: 90,
            align: 'right',
        },
        {
            title: 'Aktion',
            key: 'action',
            width: 120,
            render: (_, row) => {
                const canRetry = row.status !== 'Synced';
                return (
                    <Button
                        type="link"
                        size="small"
                        icon={<RedoOutlined />}
                        loading={retryingId === row.id}
                        disabled={!canRetry}
                        onClick={() => {
                            if (!row.id) return;
                            void handleRetryOne(row.id);
                        }}
                    >
                        Retry
                    </Button>
                );
            },
        },
    ];

    const pagination: TablePaginationConfig = {
        current: page,
        pageSize,
        total: listQuery.data?.totalCount ?? 0,
        showSizeChanger: true,
        pageSizeOptions: ['25', '50', '100'],
        onChange: (p, ps) => {
            setPage(p);
            setPageSize(ps ?? 50);
        },
    };

    const pendingHigh = (summaryQuery.data?.pendingCount ?? 0) > 10;

    if (!allowed) {
        return (
            <Alert
                type="error"
                showIcon
                title="Keine Berechtigung"
                description="Sie benötigen «payment.view», um die Offline-Warteschlange zu sehen."
            />
        );
    }

    return (
        <Space orientation="vertical" size="large" style={{ width: '100%', paddingBottom: 24 }}>
            <AdminPageHeader
                title="Offline-Transaktionen (TSE)"
                breadcrumbs={[adminOverviewCrumb(t), { title: 'Offline-Transaktionen (TSE)' }]}
            >
                <Typography.Paragraph type="secondary" style={{ marginBottom: 0 }}>
                    Serverseitige Offline-Zahlungsintents (nur Bar/Karte in der Warteschlange). Manueller Replay bei
                    Störungen.
                </Typography.Paragraph>
            </AdminPageHeader>

            <Row gutter={16}>
                <Col xs={24} md={8}>
                    <Card size="small" title="Ausstehend">
                        <Typography.Title level={3} style={{ margin: 0 }}>
                            {summaryQuery.data?.pendingCount ?? '—'}
                        </Typography.Title>
                    </Card>
                </Col>
                <Col xs={24} md={8}>
                    <Card size="small" title="Fehlgeschlagen">
                        <Typography.Title level={3} style={{ margin: 0 }}>
                            {summaryQuery.data?.failedCount ?? '—'}
                        </Typography.Title>
                    </Card>
                </Col>
                <Col xs={24} md={8}>
                    <Card size="small" title="Letzter Replay (UTC)">
                        <Typography.Text>
                            {summaryQuery.data?.lastReplayAtUtc
                                ? dayjs(summaryQuery.data.lastReplayAtUtc).utc().format('DD.MM.YYYY HH:mm:ss')
                                : '—'}
                        </Typography.Text>
                    </Card>
                </Col>
            </Row>

            {pendingHigh ? (
                <Alert
                    type="warning"
                    showIcon
                    title="Hoher Offline-Rückstau"
                    description={`Es sind ${summaryQuery.data?.pendingCount} Zahlungen noch nicht fiskal signiert (Schwelle &gt; 10).`}
                />
            ) : null}

            <Card
                title="Filter"
                extra={
                    <Space wrap>
                        <Button icon={<SyncOutlined />} onClick={() => void invalidate()}>
                            Aktualisieren
                        </Button>
                        <Button
                            type="primary"
                            icon={<RedoOutlined />}
                            loading={retryAllBusy}
                            onClick={() => void handleRetryAll()}
                        >
                            Alle fehlgeschlagenen erneut
                        </Button>
                        <Button icon={<CloudDownloadOutlined />} onClick={() => void handleExportFailed()}>
                            Fehlgeschlagene als CSV
                        </Button>
                    </Space>
                }
            >
                <Form layout="inline" style={{ flexWrap: 'wrap', gap: 12 }}>
                    <Form.Item label="Status">
                        <Select<StatusFilter>
                            style={{ minWidth: 160 }}
                            value={statusGroup}
                            onChange={(v) => {
                                setStatusGroup(v);
                                setPage(1);
                            }}
                            options={[
                                { value: 'all', label: 'Alle' },
                                { value: 'pending', label: 'Ausstehend' },
                                { value: 'completed', label: 'Abgeschlossen' },
                                { value: 'failed', label: 'Fehlgeschlagen' },
                            ]}
                        />
                    </Form.Item>
                    <Form.Item label="Kasse">
                        <Select
                            allowClear
                            placeholder="Alle Kassen"
                            style={{ minWidth: 220 }}
                            value={cashRegisterId}
                            onChange={(v) => {
                                setCashRegisterId(v);
                                setPage(1);
                            }}
                            options={registerOptions}
                        />
                    </Form.Item>
                    <Form.Item label="Zeitraum (UTC)">
                        <DatePicker.RangePicker
                            value={rangeUtc}
                            onChange={(d) => {
                                setRangeUtc(d as [Dayjs, Dayjs] | null);
                                setPage(1);
                            }}
                        />
                    </Form.Item>
                </Form>
            </Card>

            <Card title="Warteschlange">
                <Table<AdminOfflineTransactionRowDto>
                    rowKey="id"
                    loading={listQuery.isLoading}
                    columns={columns}
                    dataSource={listQuery.data?.items ?? []}
                    pagination={pagination}
                    scroll={{ x: true }}
                />
            </Card>
        </Space>
    );
}

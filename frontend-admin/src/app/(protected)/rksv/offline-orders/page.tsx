'use client';

/**
 * RKSV admin: full POS order snapshots (offline_orders).
 * NOT the TSE payment-intent queue — see /admin/tse/offline-transactions.
 * @see docs/release/OFFLINE_SYSTEMS_SEPARATION.md
 */

import React, { useCallback, useMemo, useState } from 'react';
import {
    Alert,
    Button,
    Card,
    Form,
    Select,
    Space,
    Spin,
    Table,
    Tag,
    Typography,
} from 'antd';
import type { ColumnsType, TablePaginationConfig } from 'antd/es/table';
import { RedoOutlined, SyncOutlined } from '@ant-design/icons';
import { useQueryClient } from '@tanstack/react-query';
import {
    getGetApiAdminOfflineOrdersQueryKey,
    useGetApiAdminOfflineOrders,
    usePostApiAdminOfflineOrdersIdReplay,
    usePostApiAdminOfflineOrdersReplayAll,
} from '@/api/generated/admin/admin';
import type { AdminOfflineOrderRowDto } from '@/api/generated/model/adminOfflineOrderRowDto';
import type { GetApiAdminOfflineOrdersParams } from '@/api/generated/model/getApiAdminOfflineOrdersParams';
import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { ADMIN_NAV_GROUP_LABELS, adminOverviewCrumb } from '@/shared/adminShellLabels';
import type { OfflineOrderStatus } from '@/features/offline/types';
import { useAdminCashRegisterList } from '@/features/cash-registers/hooks/useAdminCashRegisterList';
import { useAntdApp } from '@/hooks/useAntdApp';
import { useI18n } from '@/i18n/I18nProvider';
import { formatUserDateTime } from '@/lib/dateFormatter';
import { usePermissions } from '@/shared/auth/usePermissions';
import { PERMISSIONS } from '@/shared/auth/permissions';
import { ApiErrorAlertDescription } from '@/shared/errors/ApiErrorAlertDescription';

const REFETCH_MS = 30_000;

function paymentMethodKey(raw: string): string {
    const m = (raw || '').toLowerCase();
    if (m === 'cash') return 'cash';
    if (m === 'card') return 'card';
    if (m === 'voucher') return 'voucher';
    return 'other';
}

function toApiListParams(input: {
    status: OfflineOrderStatus;
    cashRegisterId?: string;
    pageNumber: number;
    pageSize: number;
}): GetApiAdminOfflineOrdersParams {
    return {
        cashRegisterId: input.cashRegisterId,
        pageNumber: input.pageNumber,
        pageSize: input.pageSize,
        status: input.status === 'all' ? undefined : input.status,
    };
}

export default function OfflineOrdersPage() {
    const { t } = useI18n();
    const { message, modal } = useAntdApp();
    const { hasPermission } = usePermissions();
    const allowed = hasPermission(PERMISSIONS.PAYMENT_VIEW);
    const queryClient = useQueryClient();

    const tp = useCallback((path: string, options?: Record<string, string | number>) => {
        return t(`rksvHub.offlineOrdersPage.${path}`, options);
    }, [t]);

    const [statusFilter, setStatusFilter] = useState<OfflineOrderStatus>('all');
    const [cashRegisterId, setCashRegisterId] = useState<string | undefined>();
    const [page, setPage] = useState(1);
    const [pageSize, setPageSize] = useState(20);
    const [replayingId, setReplayingId] = useState<string | null>(null);

    const listParams = useMemo(
        () =>
            toApiListParams({
                status: statusFilter,
                cashRegisterId,
                pageNumber: page,
                pageSize,
            }),
        [statusFilter, cashRegisterId, page, pageSize],
    );

    const invalidateList = useCallback(() => {
        void queryClient.invalidateQueries({ queryKey: getGetApiAdminOfflineOrdersQueryKey() });
    }, [queryClient]);

    const {
        data,
        isLoading,
        refetch,
        isFetching,
        isError,
        error,
    } = useGetApiAdminOfflineOrders(listParams, {
        query: { enabled: allowed, refetchInterval: REFETCH_MS },
    });

    const replayOneMutation = usePostApiAdminOfflineOrdersIdReplay({
        mutation: { onSuccess: invalidateList },
    });

    const replayAllMutation = usePostApiAdminOfflineOrdersReplayAll({
        mutation: { onSuccess: invalidateList },
    });

    const { registers } = useAdminCashRegisterList({
        allowTenantScopedDefault: true,
        excludeDecommissioned: false,
        enabled: allowed,
        pageSize: 200,
        pollIntervalMs: 120_000,
    });

    const registerOptions = useMemo(
        () =>
            registers
                .filter((r) => r?.id)
                .map((r) => ({
                    value: r.id,
                    label: `${r.registerNumber ?? ''} · ${r.location ?? ''}`,
                })),
        [registers],
    );

    const statusColor = (status: string): string => {
        if (status === 'pending') return 'warning';
        if (status === 'synced') return 'success';
        if (status === 'failed') return 'error';
        if (status === 'expired') return 'default';
        return 'default';
    };

    const hoursTagColor = (hours: number): string => {
        if (hours < 24) return 'error';
        if (hours < 48) return 'warning';
        return 'success';
    };

    const handleReplaySingle = async (orderId: string) => {
        setReplayingId(orderId);
        try {
            const result = await replayOneMutation.mutateAsync({ id: orderId });
            if (result.success) {
                message.success(
                    tp('replaySingleSuccess', {
                        invoice: result.invoiceNumber ?? '—',
                    }),
                );
            } else {
                message.error(result.errorMessage ?? tp('replayFailed'));
            }
            await refetch();
        } catch (e: unknown) {
            message.error(String((e as Error)?.message ?? tp('replayFailed')));
        } finally {
            setReplayingId(null);
        }
    };

    const handleReplayAll = () => {
        modal.confirm({
            title: tp('replayAllConfirmTitle'),
            content: tp('replayAllConfirmBody'),
            okText: tp('replayAllConfirmOk'),
            cancelText: t('common.buttons.cancel'),
            onOk: async () => {
                try {
                    const result = await replayAllMutation.mutateAsync({
                        params: cashRegisterId ? { cashRegisterId } : undefined,
                    });
                    message.success(
                        tp('replayAllSuccess', {
                            success: result.success ?? 0,
                            failed: result.failed ?? 0,
                            total: result.total ?? 0,
                        }),
                    );
                    await refetch();
                } catch (e: unknown) {
                    message.error(String((e as Error)?.message ?? tp('replayFailed')));
                }
            },
        });
    };

    const columns: ColumnsType<AdminOfflineOrderRowDto> = [
        {
            title: tp('colOrderId'),
            dataIndex: 'offlineOrderId',
            key: 'offlineOrderId',
            render: (v: string) => (
                <Typography.Text code copyable={{ text: v }}>
                    {v}
                </Typography.Text>
            ),
        },
        {
            title: tp('colCreatedAt'),
            dataIndex: 'createdAtUtc',
            key: 'createdAtUtc',
            width: 170,
            render: (v: string) => formatUserDateTime(v, { includeSeconds: true }),
        },
        {
            title: tp('colTotal'),
            dataIndex: 'orderTotal',
            key: 'orderTotal',
            align: 'right',
            width: 110,
            render: (v: number) =>
                typeof v === 'number'
                    ? v.toLocaleString('de-AT', { style: 'currency', currency: 'EUR' })
                    : '—',
        },
        {
            title: tp('colPaymentMethod'),
            dataIndex: 'paymentMethod',
            key: 'paymentMethod',
            width: 110,
            render: (v: string) => tp(`paymentMethod.${paymentMethodKey(v)}`),
        },
        {
            title: tp('colRegister'),
            dataIndex: 'cashRegisterLabel',
            key: 'cashRegisterLabel',
            ellipsis: true,
        },
        {
            title: tp('colHoursRemaining'),
            dataIndex: 'hoursRemaining',
            key: 'hoursRemaining',
            width: 130,
            render: (hours: number) => (
                <Tag color={hoursTagColor(hours)}>{tp('hoursRemaining', { hours })}</Tag>
            ),
        },
        {
            title: tp('colStatus'),
            dataIndex: 'status',
            key: 'status',
            width: 130,
            render: (status: string) => {
                const labelKey = `status.${status}`;
                const known = ['pending', 'synced', 'failed', 'expired', 'all'].includes(status);
                return (
                    <Tag color={statusColor(status)}>
                        {known ? tp(labelKey) : status}
                    </Tag>
                );
            },
        },
        {
            title: tp('colAction'),
            key: 'action',
            width: 140,
            render: (_, record) =>
                record.status === 'pending' ? (
                    <Button
                        size="small"
                        icon={<RedoOutlined />}
                        loading={replayingId === record.id}
                        onClick={() => {
                            if (!record.id) return;
                            void handleReplaySingle(record.id);
                        }}
                    >
                        {tp('syncOne')}
                    </Button>
                ) : null,
        },
    ];

    const pagination: TablePaginationConfig = {
        current: page,
        pageSize,
        total: data?.totalCount ?? 0,
        showSizeChanger: true,
        pageSizeOptions: ['20', '50', '100'],
        onChange: (p, ps) => {
            setPage(p);
            setPageSize(ps ?? 20);
        },
    };

    if (!allowed) {
        return (
            <Alert
                type="error"
                showIcon
                title={tp('forbiddenTitle')}
                description={tp('forbiddenDescription')}
            />
        );
    }

    return (
        <Space orientation="vertical" size="large" style={{ width: '100%', paddingBottom: 24 }}>
            <AdminPageHeader
                title={tp('title')}
                breadcrumbs={[
                    adminOverviewCrumb(t),
                    { title: ADMIN_NAV_GROUP_LABELS.rksv, href: '/rksv' },
                    { title: tp('title') },
                ]}
            >
                <Typography.Paragraph type="secondary" style={{ marginBottom: 0 }}>
                    {tp('subtitle')}
                </Typography.Paragraph>
            </AdminPageHeader>

            <Alert type="warning" showIcon title={tp('warningTitle')} description={tp('warningDescription')} />

            {isError ? (
                <Alert
                    type="error"
                    showIcon
                    title={tp('loadErrorTitle')}
                    description={
                        <ApiErrorAlertDescription
                            t={t}
                            error={error}
                            logContext="OfflineOrders.list"
                            fallbackKey="common.messages.unknownError"
                        />
                    }
                />
            ) : null}

            <Card
                title={tp('filtersTitle')}
                extra={
                    <Space wrap>
                        <Button icon={<SyncOutlined />} onClick={() => void refetch()} loading={isFetching}>
                            {tp('refresh')}
                        </Button>
                        <Button
                            type="primary"
                            icon={<RedoOutlined />}
                            loading={replayAllMutation.isPending}
                            onClick={handleReplayAll}
                        >
                            {tp('syncAll')}
                        </Button>
                    </Space>
                }
            >
                <Form layout="inline" style={{ flexWrap: 'wrap', gap: 12 }}>
                    <Form.Item label={tp('filterStatus')}>
                        <Select<OfflineOrderStatus>
                            style={{ minWidth: 160 }}
                            value={statusFilter}
                            onChange={(v) => {
                                setStatusFilter(v);
                                setPage(1);
                            }}
                            options={[
                                { value: 'all', label: tp('status.all') },
                                { value: 'pending', label: tp('status.pending') },
                                { value: 'synced', label: tp('status.synced') },
                                { value: 'failed', label: tp('status.failed') },
                                { value: 'expired', label: tp('status.expired') },
                            ]}
                        />
                    </Form.Item>
                    <Form.Item label={tp('filterRegister')}>
                        <Select
                            allowClear
                            placeholder={tp('filterRegisterAll')}
                            style={{ minWidth: 220 }}
                            value={cashRegisterId}
                            onChange={(v) => {
                                setCashRegisterId(v);
                                setPage(1);
                            }}
                            options={registerOptions}
                        />
                    </Form.Item>
                </Form>
            </Card>

            <Spin spinning={isLoading}>
                <Table<AdminOfflineOrderRowDto>
                    rowKey="id"
                    columns={columns}
                    dataSource={data?.items ?? []}
                    pagination={pagination}
                    scroll={{ x: true }}
                />
            </Spin>
        </Space>
    );
}

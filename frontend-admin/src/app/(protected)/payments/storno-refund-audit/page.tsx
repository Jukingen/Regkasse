'use client';

import React, { useCallback, useMemo, useState } from 'react';
import { Alert, Button, Card, DatePicker, Select, Space, Table, Tag, Typography } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { DownloadOutlined, ReloadOutlined } from '@ant-design/icons';
import dayjs from 'dayjs';
import { useGetApiAdminPayments } from '@/api/generated/admin/admin';
import { fetchAllAdminPaymentsPages } from '@/features/payments/api/fetchAllAdminPayments';
import type { AdminPaymentListItemDto } from '@/api/generated/model';
import { StornoRefundAuditDetailModal } from '@/features/payments/stornoRefundAudit/StornoRefundAuditDetailModal';
import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { AdminPageShell } from '@/components/admin-layout/AdminPageShell';
import { ADMIN_OVERVIEW_CRUMB } from '@/shared/adminShellLabels';
import { useGetApiCashRegister } from '@/api/generated/cash-register/cash-register';
import type { CashRegister } from '@/api/generated/model';
import { useI18n } from '@/i18n';
import { FORMAT_EMPTY_DISPLAY, createIntlFormatters } from '@/i18n/formatting';
import { ApiErrorAlertDescription } from '@/shared/errors/ApiErrorAlertDescription';

const { RangePicker } = DatePicker;

function normalizeRegisters(data: unknown): CashRegister[] {
    if (Array.isArray(data)) return data as CashRegister[];
    if (data && typeof data === 'object' && 'registers' in data) {
        const r = (data as { registers?: CashRegister[] }).registers;
        if (Array.isArray(r)) return r;
    }
    return [];
}

type ReversalTypeFilter = 'all' | 'storno' | 'refund';

const STORNO_REASON_QUERY_VALUES = ['FalscherBetrag', 'KundeStorniert', 'TechnischerFehler', 'Anderes'] as const;

const STORNO_REASON_QUERY_LABEL_KEY: Record<(typeof STORNO_REASON_QUERY_VALUES)[number], string> = {
    FalscherBetrag: 'payments.stornoRefundAudit.stornoReason.falscherBetrag',
    KundeStorniert: 'payments.stornoRefundAudit.stornoReason.kundeStorniert',
    TechnischerFehler: 'payments.stornoRefundAudit.stornoReason.technischerFehler',
    Anderes: 'payments.stornoRefundAudit.stornoReason.anderes',
};

function csvEscape(value: string): string {
    const v = value.replace(/"/g, '""');
    return `"${v}"`;
}

export default function StornoRefundAuditPage() {
    const { t, formatLocale } = useI18n();
    const fmt = useMemo(() => createIntlFormatters(formatLocale), [formatLocale]);

    const [dateRange, setDateRange] = useState<[dayjs.Dayjs, dayjs.Dayjs]>([
        dayjs().subtract(30, 'day'),
        dayjs(),
    ]);
    const [registerId, setRegisterId] = useState<string | undefined>(undefined);
    const [reversalType, setReversalType] = useState<ReversalTypeFilter>('all');
    const [stornoReasonFilter, setStornoReasonFilter] = useState<string | undefined>(undefined);
    const [page, setPage] = useState(1);
    const [pageSize, setPageSize] = useState(50);
    const [detailId, setDetailId] = useState<string | null>(null);
    const [exportBusy, setExportBusy] = useState(false);

    const { data: registersRaw } = useGetApiCashRegister();
    const registers = useMemo(() => normalizeRegisters(registersRaw), [registersRaw]);

    const registerOptions = useMemo(
        () =>
            registers
                .filter((r) => r.id)
                .map((r) => ({
                    value: r.id as string,
                    label: `${r.registerNumber ?? r.id}`,
                })),
        [registers],
    );

    const listParams = useMemo(() => {
        const base = {
            startDate: dateRange[0].format('YYYY-MM-DD'),
            endDate: dateRange[1].format('YYYY-MM-DD'),
            cashRegisterId: registerId,
            pageNumber: page,
            pageSize,
        };
        if (reversalType === 'all') {
            return {
                ...base,
                isStorno: true,
                isRefund: true,
                stornoReason: stornoReasonFilter,
            };
        }
        if (reversalType === 'storno') {
            return {
                ...base,
                isStorno: true,
                stornoReason: stornoReasonFilter,
            };
        }
        return {
            ...base,
            isRefund: true,
        };
    }, [dateRange, registerId, page, pageSize, reversalType, stornoReasonFilter]);

    const { data, isLoading, isError, error, refetch } = useGetApiAdminPayments(listParams);

    const items = data?.items ?? [];
    const total = data?.total ?? 0;

    const stornoReasonLabel = useCallback(
        (reason: AdminPaymentListItemDto['stornoReason']) => {
            if (reason === undefined) return FORMAT_EMPTY_DISPLAY;
            switch (reason) {
                case 0:
                    return t('payments.stornoRefundAudit.stornoReason.falscherBetrag');
                case 1:
                    return t('payments.stornoRefundAudit.stornoReason.kundeStorniert');
                case 2:
                    return t('payments.stornoRefundAudit.stornoReason.technischerFehler');
                case 3:
                    return t('payments.stornoRefundAudit.stornoReason.anderes');
                default:
                    return FORMAT_EMPTY_DISPLAY;
            }
        },
        [t],
    );

    const columns: ColumnsType<AdminPaymentListItemDto> = useMemo(
        () => [
            {
                title: t('payments.stornoRefundAudit.table.colOriginalReceipt'),
                dataIndex: 'originalReceiptNumber',
                key: 'originalReceiptNumber',
                render: (v: string | null | undefined) => v?.trim() || FORMAT_EMPTY_DISPLAY,
            },
            {
                title: t('payments.stornoRefundAudit.table.colNewReceipt'),
                dataIndex: 'receiptNumber',
                key: 'receiptNumber',
                render: (v: string | null | undefined) => v?.trim() || FORMAT_EMPTY_DISPLAY,
            },
            {
                title: t('payments.stornoRefundAudit.table.colType'),
                key: 'type',
                render: (_, row) =>
                    row.isStorno ? (
                        <Tag color="red">{t('payments.stornoRefundAudit.type.storno')}</Tag>
                    ) : row.isRefund ? (
                        <Tag color="gold">{t('payments.stornoRefundAudit.type.refund')}</Tag>
                    ) : (
                        FORMAT_EMPTY_DISPLAY
                    ),
            },
            {
                title: t('payments.stornoRefundAudit.table.colAmount'),
                dataIndex: 'totalAmount',
                key: 'totalAmount',
                align: 'right',
                render: (v: number | undefined) => fmt.formatCurrency(v ?? 0),
            },
            {
                title: t('payments.stornoRefundAudit.table.colReason'),
                key: 'reason',
                render: (_, row) => (row.isStorno ? stornoReasonLabel(row.stornoReason) : FORMAT_EMPTY_DISPLAY),
            },
            {
                title: t('payments.stornoRefundAudit.table.colCashier'),
                dataIndex: 'cashierDisplayName',
                key: 'cashierDisplayName',
                ellipsis: true,
                render: (v: string | null | undefined) => v?.trim() || FORMAT_EMPTY_DISPLAY,
            },
            {
                title: t('payments.stornoRefundAudit.table.colWhen'),
                dataIndex: 'createdAt',
                key: 'createdAt',
                render: (iso: string | undefined) => (iso ? fmt.formatDateTime(iso) : FORMAT_EMPTY_DISPLAY),
            },
            {
                title: t('payments.stornoRefundAudit.table.colStatus'),
                dataIndex: 'reversalCompletionStatus',
                key: 'reversalCompletionStatus',
                render: (s: string | null | undefined) =>
                    s === 'Failed' ? (
                        <Tag color="error">{t('payments.stornoRefundAudit.status.failed')}</Tag>
                    ) : (
                        <Tag color="success">{t('payments.stornoRefundAudit.status.completed')}</Tag>
                    ),
            },
            {
                title: t('payments.stornoRefundAudit.table.colActions'),
                key: 'actions',
                width: 120,
                render: (_, row) => (
                    <Button type="link" size="small" onClick={() => row.id && setDetailId(row.id)}>
                        {t('payments.stornoRefundAudit.table.viewDetail')}
                    </Button>
                ),
            },
        ],
        [fmt, stornoReasonLabel, t],
    );

    const onExportCsv = useCallback(async () => {
        setExportBusy(true);
        try {
            const { pageNumber: _pn, pageSize: _ps, ...exportBase } = listParams;
            const rows = await fetchAllAdminPaymentsPages(exportBase);
            const header = [
                t('payments.stornoRefundAudit.table.colOriginalReceipt'),
                t('payments.stornoRefundAudit.table.colNewReceipt'),
                t('payments.stornoRefundAudit.table.colType'),
                t('payments.stornoRefundAudit.table.colAmount'),
                t('payments.stornoRefundAudit.table.colReason'),
                t('payments.stornoRefundAudit.table.colCashier'),
                t('payments.stornoRefundAudit.table.colWhen'),
                t('payments.stornoRefundAudit.table.colStatus'),
            ];
            const lines = [header.join(';')];
            for (const row of rows) {
                const typ = row.isStorno
                    ? t('payments.stornoRefundAudit.type.storno')
                    : row.isRefund
                      ? t('payments.stornoRefundAudit.type.refund')
                      : '';
                const reason = row.isStorno ? stornoReasonLabel(row.stornoReason) : '';
                const status =
                    row.reversalCompletionStatus === 'Failed'
                        ? t('payments.stornoRefundAudit.status.failed')
                        : t('payments.stornoRefundAudit.status.completed');
                lines.push(
                    [
                        csvEscape(row.originalReceiptNumber?.trim() ?? ''),
                        csvEscape(row.receiptNumber?.trim() ?? ''),
                        csvEscape(typ),
                        csvEscape(row.totalAmount != null ? String(row.totalAmount) : ''),
                        csvEscape(reason),
                        csvEscape(row.cashierDisplayName?.trim() ?? ''),
                        csvEscape(row.createdAt ?? ''),
                        csvEscape(status),
                    ].join(';'),
                );
            }
            const blob = new Blob([`\uFEFF${lines.join('\n')}`], { type: 'text/csv;charset=utf-8;' });
            const url = URL.createObjectURL(blob);
            const a = document.createElement('a');
            a.href = url;
            a.download = `storno-refund-audit-${dayjs().format('YYYYMMDD_HHmmss')}.csv`;
            a.click();
            URL.revokeObjectURL(url);
        } finally {
            setExportBusy(false);
        }
    }, [listParams, stornoReasonLabel, t]);

    return (
        <AdminPageShell>
            <AdminPageHeader
                title={t('payments.stornoRefundAudit.pageTitle')}
                breadcrumbs={[
                    ADMIN_OVERVIEW_CRUMB,
                    { title: t('nav.payments'), href: '/payments' },
                    { title: t('payments.stornoRefundAudit.pageTitle') },
                ]}
                actions={
                    <Space>
                        <Button icon={<ReloadOutlined />} onClick={() => void refetch()}>
                            {t('payments.toolbar.refresh')}
                        </Button>
                        <Button icon={<DownloadOutlined />} loading={exportBusy} onClick={() => void onExportCsv()}>
                            {t('payments.stornoRefundAudit.exportCsv')}
                        </Button>
                    </Space>
                }
            />

            <Typography.Paragraph type="secondary">{t('payments.stornoRefundAudit.intro')}</Typography.Paragraph>

            <Card style={{ marginBottom: 16 }}>
                <Space wrap>
                    <RangePicker
                        value={dateRange}
                        onChange={(v) => {
                            if (v?.[0] && v[1]) setDateRange([v[0], v[1]]);
                        }}
                    />
                    <Select
                        allowClear
                        placeholder={t('payments.stornoRefundAudit.filterRegister')}
                        style={{ minWidth: 220 }}
                        options={registerOptions}
                        value={registerId}
                        onChange={(v) => setRegisterId(v)}
                    />
                    <Select<ReversalTypeFilter>
                        style={{ minWidth: 200 }}
                        value={reversalType}
                        onChange={(v) => {
                            setReversalType(v);
                            if (v === 'refund') setStornoReasonFilter(undefined);
                            setPage(1);
                        }}
                        options={[
                            { value: 'all', label: t('payments.stornoRefundAudit.filterTypeAll') },
                            { value: 'storno', label: t('payments.stornoRefundAudit.filterTypeStorno') },
                            { value: 'refund', label: t('payments.stornoRefundAudit.filterTypeRefund') },
                        ]}
                    />
                    <Select
                        allowClear
                        disabled={reversalType === 'refund'}
                        placeholder={t('payments.stornoRefundAudit.filterStornoReason')}
                        style={{ minWidth: 240 }}
                        value={stornoReasonFilter}
                        onChange={(v) => {
                            setStornoReasonFilter(v);
                            setPage(1);
                        }}
                        options={STORNO_REASON_QUERY_VALUES.map((v) => ({
                            value: v,
                            label: t(STORNO_REASON_QUERY_LABEL_KEY[v]),
                        }))}
                    />
                </Space>
            </Card>

            {isError ? (
                <Alert
                    type="error"
                    showIcon
                    style={{ marginBottom: 16 }}
                    title={t('payments.list.loadErrorTitle')}
                    description={
                        error ? (
                            <ApiErrorAlertDescription
                                t={t}
                                error={error}
                                logContext="StornoRefundAuditPage.list"
                                fallbackKey="payments.list.loadErrorFallback"
                            />
                        ) : (
                            t('payments.list.loadErrorFallback')
                        )
                    }
                />
            ) : null}

            <Table<AdminPaymentListItemDto>
                rowKey={(r) => r.id ?? String(r.transactionId)}
                loading={isLoading}
                columns={columns}
                dataSource={items}
                pagination={{
                    current: page,
                    pageSize,
                    total,
                    showSizeChanger: true,
                    pageSizeOptions: ['20', '50', '100'],
                    onChange: (p, ps) => {
                        setPage(p);
                        setPageSize(ps);
                    },
                }}
                scroll={{ x: true }}
            />

            <StornoRefundAuditDetailModal paymentId={detailId} open={Boolean(detailId)} onClose={() => setDetailId(null)} />
        </AdminPageShell>
    );
}

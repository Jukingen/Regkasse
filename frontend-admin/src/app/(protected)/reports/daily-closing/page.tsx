'use client';

/**
 * Admin snapshot: payment-row totals for one Vienna calendar day (GET /api/admin/reports/daily-closing).
 */

import React, { useCallback, useMemo, useState } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { Alert, Button, Card, Col, DatePicker, Row, Select, Space, Statistic, Table, Typography } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { DownloadOutlined } from '@ant-design/icons';
import dayjs from 'dayjs';
import Link from 'next/link';
import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { adminOverviewCrumb } from '@/shared/adminShellLabels';
import { useI18n } from '@/i18n';
import { formatCurrency, formatDateTime } from '@/i18n/formatting';
import { useGetApiCashRegister } from '@/api/generated/cash-register/cash-register';
import {
    getGetApiAdminReportsDailyClosingQueryKey,
    useGetApiAdminReportsDailyClosing,
} from '@/api/generated/admin/admin';
import type { GetApiAdminReportsDailyClosingParams } from '@/api/generated/model';
import type { CashRegister, DailyClosingSummaryLineDto } from '@/api/generated/model';
import { exportDailyClosingSummaryCsv } from '@/features/reporting/daily-closing/exportDailyClosingSummaryCsv';

function normalizeRegisters(data: unknown): CashRegister[] {
    if (Array.isArray(data)) return data as CashRegister[];
    const r = (data as { registers?: CashRegister[] } | undefined)?.registers;
    return Array.isArray(r) ? r : [];
}

export default function DailyClosingSummaryPage() {
    const { t, formatLocale } = useI18n();
    const queryClient = useQueryClient();
    const [day, setDay] = useState(() => dayjs());
    const [registerId, setRegisterId] = useState<string | undefined>(undefined);

    const registersQ = useGetApiCashRegister();
    const registerRows = useMemo(
        () => normalizeRegisters(registersQ.data as unknown),
        [registersQ.data],
    );

    const dateParam = day.format('YYYY-MM-DD');
    const closingFilters = useMemo<GetApiAdminReportsDailyClosingParams>(
        () => ({
            date: dateParam,
            cashRegisterId: registerId,
        }),
        [dateParam, registerId],
    );
    const closingQueryKey = useMemo(
        () => getGetApiAdminReportsDailyClosingQueryKey(closingFilters),
        [closingFilters],
    );

    const summaryQ = useGetApiAdminReportsDailyClosing(closingFilters, { query: { enabled: true } });
    const data = summaryQ.data;

    const registerSlug = useMemo(() => {
        if (!registerId) return t('reporting.dailyClosing.allRegisters');
        const row = registerRows.find((r) => r.id === registerId);
        return row?.registerNumber ?? String(registerId);
    }, [registerId, registerRows, t]);

    const csvLabels = useMemo(
        () => ({
            date: t('reporting.dailyClosing.export.colDate'),
            register: t('reporting.dailyClosing.export.colRegister'),
            openingBalance: t('reporting.dailyClosing.export.colOpeningBalance'),
            closingBalance: t('reporting.dailyClosing.export.colClosingBalance'),
            totalSales: t('reporting.dailyClosing.export.colTotalSales'),
            cashCount: t('reporting.dailyClosing.export.colCashCount'),
            difference: t('reporting.dailyClosing.export.colDifference'),
        }),
        [t],
    );

    const handleExportCsv = useCallback(() => {
        const cached = queryClient.getQueryData(closingQueryKey);
        if (!cached) return;
        exportDailyClosingSummaryCsv(cached, { dateParam, registerSlug }, csvLabels);
    }, [closingQueryKey, csvLabels, dateParam, queryClient, registerSlug]);

    const canExportCsv = Boolean(data) && !summaryQ.isLoading;

    const specialColumns: ColumnsType<DailyClosingSummaryLineDto> = useMemo(
        () => [
            {
                title: t('reporting.dailyClosing.tables.columns.time'),
                dataIndex: 'createdAtUtc',
                key: 'createdAtUtc',
                render: (v: string | undefined) => formatDateTime(v, formatLocale, { dateStyle: 'short', timeStyle: 'medium' }),
            },
            {
                title: t('reporting.dailyClosing.tables.columns.register'),
                dataIndex: 'cashRegisterId',
                key: 'cashRegisterId',
                width: 280,
                ellipsis: true,
            },
            {
                title: t('reporting.dailyClosing.tables.columns.receipt'),
                dataIndex: 'receiptNumber',
                key: 'receiptNumber',
            },
            {
                title: t('reporting.dailyClosing.tables.columns.amount'),
                dataIndex: 'totalAmount',
                key: 'totalAmount',
                align: 'right',
                render: (v: number | undefined) => formatCurrency(v ?? 0, formatLocale),
            },
            {
                title: t('reporting.dailyClosing.tables.columns.method'),
                dataIndex: 'paymentMethod',
                key: 'paymentMethod',
            },
            {
                title: t('reporting.dailyClosing.tables.columns.kind'),
                dataIndex: 'rksvSpecialReceiptKind',
                key: 'rksvSpecialReceiptKind',
            },
        ],
        [formatLocale, t],
    );

    const stornoColumns: ColumnsType<DailyClosingSummaryLineDto> = useMemo(
        () => [
            {
                title: t('reporting.dailyClosing.tables.columns.time'),
                dataIndex: 'createdAtUtc',
                key: 'createdAtUtc',
                render: (v: string | undefined) => formatDateTime(v, formatLocale, { dateStyle: 'short', timeStyle: 'medium' }),
            },
            {
                title: t('reporting.dailyClosing.tables.columns.register'),
                dataIndex: 'cashRegisterId',
                key: 'cashRegisterId',
                width: 280,
                ellipsis: true,
            },
            {
                title: t('reporting.dailyClosing.tables.columns.receipt'),
                dataIndex: 'receiptNumber',
                key: 'receiptNumber',
            },
            {
                title: t('reporting.dailyClosing.tables.columns.amount'),
                dataIndex: 'totalAmount',
                key: 'totalAmount',
                align: 'right',
                render: (v: number | undefined) => formatCurrency(v ?? 0, formatLocale),
            },
            {
                title: t('reporting.dailyClosing.tables.columns.method'),
                dataIndex: 'paymentMethod',
                key: 'paymentMethod',
            },
            {
                title: t('reporting.dailyClosing.tables.columns.stornoReason'),
                dataIndex: 'stornoReason',
                key: 'stornoReason',
            },
            {
                title: t('reporting.dailyClosing.tables.columns.originalReceipt'),
                dataIndex: 'originalReceiptId',
                key: 'originalReceiptId',
                ellipsis: true,
            },
        ],
        [formatLocale, t],
    );

    return (
        <>
            <AdminPageHeader
                title={t('reporting.dailyClosing.pageTitle')}
                breadcrumbs={[
                    adminOverviewCrumb(t),
                    { title: t('nav.reporting'), href: '/reporting' },
                    { title: t('reporting.dailyClosing.breadcrumb'), href: '/reports/daily-closing' },
                ]}
            />
            <Card>
                <Typography.Paragraph type="secondary">{t('reporting.dailyClosing.intro')}</Typography.Paragraph>
                {data?.snapshotDisclaimerDe ? (
                    <Alert type="info" showIcon title={data.snapshotDisclaimerDe} style={{ marginBottom: 16 }} />
                ) : null}
                <Space wrap align="center" style={{ marginBottom: 16 }}>
                    <span>{t('reporting.dailyClosing.dateLabel')}</span>
                    <DatePicker value={day} onChange={(d) => d && setDay(d)} format="DD.MM.YYYY" allowClear={false} />
                    <span>{t('reporting.dailyClosing.registerLabel')}</span>
                    <Select
                        allowClear
                        placeholder={t('reporting.dailyClosing.allRegisters')}
                        style={{ minWidth: 220 }}
                        value={registerId}
                        onChange={(v) => setRegisterId(v)}
                        options={registerRows.map((r) => ({
                            value: r.id,
                            label: r.registerNumber ? `${r.registerNumber} (${r.location ?? ''})` : String(r.id),
                        }))}
                        loading={registersQ.isLoading}
                    />
                    <Button
                        icon={<DownloadOutlined />}
                        onClick={handleExportCsv}
                        disabled={!canExportCsv}
                    >
                        {t('reporting.dailyClosing.exportCsv')}
                    </Button>
                    <Link href="/reporting">{t('nav.reporting')}</Link>
                </Space>
                <Row gutter={[16, 16]}>
                    <Col xs={24} sm={12} md={8} lg={6}>
                        <Statistic
                            title={t('reporting.dailyClosing.stats.totalSales')}
                            value={data?.totalSales ?? 0}
                            formatter={(val) => formatCurrency(Number(val), formatLocale)}
                            loading={summaryQ.isLoading}
                        />
                    </Col>
                    <Col xs={24} sm={12} md={8} lg={6}>
                        <Statistic
                            title={t('reporting.dailyClosing.stats.totalCash')}
                            value={data?.totalCash ?? 0}
                            formatter={(val) => formatCurrency(Number(val), formatLocale)}
                            loading={summaryQ.isLoading}
                        />
                    </Col>
                    <Col xs={24} sm={12} md={8} lg={6}>
                        <Statistic
                            title={t('reporting.dailyClosing.stats.totalCard')}
                            value={data?.totalCard ?? 0}
                            formatter={(val) => formatCurrency(Number(val), formatLocale)}
                            loading={summaryQ.isLoading}
                        />
                    </Col>
                    <Col xs={24} sm={12} md={8} lg={6}>
                        <Statistic
                            title={t('reporting.dailyClosing.stats.totalVoucher')}
                            value={data?.totalVoucherRedemptions ?? 0}
                            formatter={(val) => formatCurrency(Number(val), formatLocale)}
                            loading={summaryQ.isLoading}
                        />
                    </Col>
                    <Col xs={24} sm={12} md={8} lg={6}>
                        <Statistic
                            title={t('reporting.dailyClosing.stats.totalOther')}
                            value={data?.totalOtherPaymentMethods ?? 0}
                            formatter={(val) => formatCurrency(Number(val), formatLocale)}
                            loading={summaryQ.isLoading}
                        />
                    </Col>
                    <Col xs={24} sm={12} md={8} lg={6}>
                        <Statistic
                            title={t('reporting.dailyClosing.stats.receiptCount')}
                            value={data?.receiptCount ?? 0}
                            loading={summaryQ.isLoading}
                        />
                    </Col>
                    <Col xs={24} sm={12} md={8} lg={6}>
                        <Statistic
                            title={t('reporting.dailyClosing.stats.stornoCount')}
                            value={data?.stornoRowCount ?? 0}
                            loading={summaryQ.isLoading}
                        />
                    </Col>
                    <Col xs={24} sm={12} md={8} lg={6}>
                        <Statistic
                            title={t('reporting.dailyClosing.stats.stornoTotal')}
                            value={data?.stornoTotalAmount ?? 0}
                            formatter={(val) => formatCurrency(Number(val), formatLocale)}
                            loading={summaryQ.isLoading}
                        />
                    </Col>
                </Row>
                <Table<DailyClosingSummaryLineDto>
                    style={{ marginTop: 24 }}
                    title={() => t('reporting.dailyClosing.tables.specialTitle')}
                    rowKey={(r) => r.id ?? `${r.receiptNumber}-${r.createdAtUtc}`}
                    loading={summaryQ.isLoading}
                    dataSource={(data?.specialReceipts ?? []).filter(Boolean) as DailyClosingSummaryLineDto[]}
                    columns={specialColumns}
                    pagination={false}
                    locale={{ emptyText: t('reporting.dailyClosing.tables.empty') }}
                />
                <Table<DailyClosingSummaryLineDto>
                    style={{ marginTop: 24 }}
                    title={() => t('reporting.dailyClosing.tables.stornoTitle')}
                    rowKey={(r) => r.id ?? `${r.receiptNumber}-${r.createdAtUtc}`}
                    loading={summaryQ.isLoading}
                    dataSource={(data?.stornos ?? []).filter(Boolean) as DailyClosingSummaryLineDto[]}
                    columns={stornoColumns}
                    pagination={false}
                    locale={{ emptyText: t('reporting.dailyClosing.tables.empty') }}
                />
            </Card>
        </>
    );
}

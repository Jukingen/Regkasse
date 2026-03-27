'use client';

/**
 * Personal-/Kassenleistung: payment_details-basiert; Hinweise zu Kalendertag vs. Schicht in reliability.
 */
import React, { useMemo, useState } from 'react';
import {
    Alert,
    Card,
    Col,
    Collapse,
    DatePicker,
    Row,
    Select,
    Space,
    Spin,
    Statistic,
    Switch,
    Table,
    Typography,
} from 'antd';
import type { ColumnsType } from 'antd/es/table';
import dayjs from 'dayjs';
import utc from 'dayjs/plugin/utc';

dayjs.extend(utc);
import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { adminOverviewCrumb } from '@/shared/adminShellLabels';
import { useI18n } from '@/i18n/I18nProvider';
import { useGetApiCashRegister } from '@/api/generated/cash-register/cash-register';
import { useGetApiUserManagement } from '@/api/generated/user-management/user-management';
import { useGetApiReportsOperationalStaffPerformance } from '@/api/generated/reports/reports';
import type { StaffPerformanceStaffRowDto } from '@/api/generated/model';
import { usePermissions } from '@/shared/auth/usePermissions';
import { PERMISSIONS } from '@/shared/auth/permissions';
import Link from 'next/link';

const { RangePicker } = DatePicker;

const PAYMENT_METHOD_OPTIONS: { value: number; labelDe: string }[] = [
    { value: 0, labelDe: 'Bar' },
    { value: 1, labelDe: 'Karte' },
    { value: 2, labelDe: 'Überweisung' },
    { value: 3, labelDe: 'Scheck' },
    { value: 4, labelDe: 'Gutschein' },
    { value: 5, labelDe: 'Mobil' },
];

function formatPaymentMethodKey(key: string | undefined): string {
    if (key === undefined || key === '') return '—';
    const n = Number.parseInt(key, 10);
    const opt = PAYMENT_METHOD_OPTIONS.find((o) => o.value === n);
    if (opt) return opt.labelDe;
    return key;
}

function formatYyyyMmDd(yyyyMmDd: string | undefined): string {
    if (!yyyyMmDd || yyyyMmDd.length !== 8) return yyyyMmDd ?? '—';
    const y = yyyyMmDd.slice(0, 4);
    const m = yyyyMmDd.slice(4, 6);
    const d = yyyyMmDd.slice(6, 8);
    return `${d}.${m}.${y}`;
}

export default function StaffPerformanceReportingPage() {
    const { t, formatLocale } = useI18n();
    const { hasPermission } = usePermissions();
    const canView = hasPermission(PERMISSIONS.REPORT_VIEW);

    const [dateRange, setDateRange] = useState<[dayjs.Dayjs, dayjs.Dayjs]>([
        dayjs().startOf('month'),
        dayjs().endOf('month'),
    ]);
    const [cashRegisterId, setCashRegisterId] = useState<string | undefined>();
    const [cashierId, setCashierId] = useState<string | undefined>();
    const [paymentMethod, setPaymentMethod] = useState<number | undefined>();
    const [activeOnly, setActiveOnly] = useState(true);
    const [includePerStaffPerDay, setIncludePerStaffPerDay] = useState(false);

    const startDate = dateRange[0].format('YYYY-MM-DD');
    const endDate = dateRange[1].format('YYYY-MM-DD');

    const params = useMemo(
        () => ({
            startDate,
            endDate,
            cashRegisterId,
            cashierId,
            paymentMethod,
            activeOnly,
            includePerStaffPerDay,
        }),
        [startDate, endDate, cashRegisterId, cashierId, paymentMethod, activeOnly, includePerStaffPerDay],
    );

    const { data: registers } = useGetApiCashRegister();
    const { data: usersPage } = useGetApiUserManagement({ pageSize: 500, isActive: true });

    const reportQ = useGetApiReportsOperationalStaffPerformance(params, {
        query: { enabled: canView },
    });

    const moneyFmt = useMemo(
        () =>
            new Intl.NumberFormat(formatLocale || 'de-AT', {
                style: 'currency',
                currency: 'EUR',
            }),
        [formatLocale],
    );

    const ratioFmt = useMemo(
        () =>
            new Intl.NumberFormat(formatLocale || 'de-AT', {
                minimumFractionDigits: 2,
                maximumFractionDigits: 2,
            }),
        [formatLocale],
    );

    const pctFmt = useMemo(
        () =>
            new Intl.NumberFormat(formatLocale || 'de-AT', {
                style: 'percent',
                minimumFractionDigits: 1,
                maximumFractionDigits: 1,
            }),
        [formatLocale],
    );

    const staffColumns: ColumnsType<StaffPerformanceStaffRowDto> = [
        {
            title: t('adminShell.staffPerformance.colCashier'),
            dataIndex: 'userName',
            render: (_: unknown, row) => (
                <Space direction="vertical" size={0}>
                    <Typography.Text strong>{row.userName ?? row.cashierId}</Typography.Text>
                    <Typography.Text type="secondary" style={{ fontSize: 12, fontFamily: 'monospace' }}>
                        {row.cashierId}
                    </Typography.Text>
                </Space>
            ),
        },
        {
            title: t('adminShell.staffPerformance.colSalesCount'),
            dataIndex: 'saleTransactionCount',
            sorter: (a, b) => (a.saleTransactionCount ?? 0) - (b.saleTransactionCount ?? 0),
        },
        {
            title: t('adminShell.staffPerformance.colGross'),
            dataIndex: 'grossSalesAmount',
            render: (v: number) => moneyFmt.format(Number(v ?? 0)),
            sorter: (a, b) => Number(a.grossSalesAmount ?? 0) - Number(b.grossSalesAmount ?? 0),
        },
        {
            title: t('adminShell.staffPerformance.colRefunds'),
            dataIndex: 'refundRowCount',
        },
        {
            title: t('adminShell.staffPerformance.colStorno'),
            dataIndex: 'stornoRowCount',
        },
        {
            title: t('adminShell.staffPerformance.colRefundPerSale'),
            dataIndex: 'refundRowsPerSale',
            render: (v: number) => ratioFmt.format(Number(v ?? 0)),
        },
        {
            title: t('adminShell.staffPerformance.colStornoPerSale'),
            dataIndex: 'stornoRowsPerSale',
            render: (v: number) => ratioFmt.format(Number(v ?? 0)),
        },
        {
            title: t('adminShell.staffPerformance.colRefundAmtRatio'),
            dataIndex: 'refundAmountToGrossRatio',
            render: (v: number) => pctFmt.format(Number(v ?? 0)),
        },
    ];

    const methodSliceColumns: ColumnsType<{
        cashierId?: string;
        paymentMethodRaw?: string;
        saleCount?: number;
        grossAmount?: number;
    }> = [
        {
            title: t('adminShell.staffPerformance.colCashier'),
            dataIndex: 'cashierId',
            ellipsis: true,
        },
        {
            title: t('adminShell.reporting.methodKey'),
            dataIndex: 'paymentMethodRaw',
            render: (v: string) => formatPaymentMethodKey(v),
        },
        { title: t('adminShell.reporting.count'), dataIndex: 'saleCount' },
        {
            title: t('adminShell.reporting.amount'),
            dataIndex: 'grossAmount',
            render: (v: number) => moneyFmt.format(Number(v ?? 0)),
        },
    ];

    const dayAggColumns = [
        {
            title: t('adminShell.staffPerformance.colLocalDay'),
            dataIndex: 'localDayYyyyMmDd',
            render: (v: string) => formatYyyyMmDd(v),
        },
        { title: t('adminShell.staffPerformance.colSalesCount'), dataIndex: 'saleTransactionCount' },
        {
            title: t('adminShell.staffPerformance.colGross'),
            dataIndex: 'grossSalesAmount',
            render: (v: number) => moneyFmt.format(Number(v ?? 0)),
        },
        { title: t('adminShell.staffPerformance.colRefunds'), dataIndex: 'refundRowCount' },
        { title: t('adminShell.staffPerformance.colStorno'), dataIndex: 'stornoRowCount' },
    ];

    const totals = reportQ.data?.totals;
    const rel = reportQ.data?.reliability;

    if (!canView) {
        return (
            <Alert
                type="warning"
                showIcon
                message={t('adminShell.staffPerformance.noPermission')}
                style={{ margin: 24 }}
            />
        );
    }

    return (
        <Space direction="vertical" size="large" style={{ width: '100%' }}>
            <AdminPageHeader
                title={t('adminShell.staffPerformance.pageTitle')}
                breadcrumbs={[
                    adminOverviewCrumb(t),
                    { title: t('nav.reporting'), href: '/reporting' },
                    { title: t('adminShell.staffPerformance.pageTitle') },
                ]}
            >
                <Typography.Paragraph type="secondary" style={{ marginBottom: 0 }}>
                    {t('adminShell.staffPerformance.pageIntro')}
                </Typography.Paragraph>
                <Link href="/reporting">{t('adminShell.staffPerformance.backToOperational')}</Link>
            </AdminPageHeader>

            <Card size="small" title={t('adminShell.reporting.filtersTitle')}>
                <Space wrap size="large" align="start">
                    <Space direction="vertical" size={4}>
                        <Typography.Text type="secondary">{t('adminShell.reporting.dateRange')}</Typography.Text>
                        <RangePicker
                            value={dateRange}
                            onChange={(r) => {
                                if (r?.[0] && r[1]) setDateRange([r[0], r[1]]);
                            }}
                        />
                    </Space>
                    <Space direction="vertical" size={4}>
                        <Typography.Text type="secondary">{t('adminShell.reporting.register')}</Typography.Text>
                        <Select
                            allowClear
                            style={{ minWidth: 220 }}
                            placeholder={t('adminShell.reporting.registerAll')}
                            value={cashRegisterId}
                            onChange={(v) => setCashRegisterId(v)}
                            options={(registers ?? []).map((r) => ({
                                value: r.id,
                                label: `${r.registerNumber ?? r.id} · ${r.location ?? ''}`.trim(),
                            }))}
                        />
                    </Space>
                    <Space direction="vertical" size={4}>
                        <Typography.Text type="secondary">{t('adminShell.reporting.cashier')}</Typography.Text>
                        <Select
                            allowClear
                            showSearch
                            optionFilterProp="label"
                            style={{ minWidth: 240 }}
                            placeholder={t('adminShell.reporting.cashierAll')}
                            value={cashierId}
                            onChange={(v) => setCashierId(v)}
                            options={(usersPage?.items ?? []).map((u) => ({
                                value: u.id,
                                label: `${u.userName ?? u.email ?? u.id}`,
                            }))}
                        />
                    </Space>
                    <Space direction="vertical" size={4}>
                        <Typography.Text type="secondary">{t('adminShell.reporting.paymentMethod')}</Typography.Text>
                        <Select
                            allowClear
                            style={{ minWidth: 180 }}
                            placeholder={t('adminShell.reporting.paymentAll')}
                            value={paymentMethod}
                            onChange={(v) => setPaymentMethod(v)}
                            options={PAYMENT_METHOD_OPTIONS.map((o) => ({ value: o.value, label: o.labelDe }))}
                        />
                    </Space>
                    <Space direction="vertical" size={4}>
                        <Typography.Text type="secondary">{t('adminShell.reporting.activeOnly')}</Typography.Text>
                        <Switch checked={activeOnly} onChange={setActiveOnly} />
                    </Space>
                    <Space direction="vertical" size={4}>
                        <Typography.Text type="secondary">{t('adminShell.staffPerformance.includePerStaffPerDay')}</Typography.Text>
                        <Switch checked={includePerStaffPerDay} onChange={setIncludePerStaffPerDay} />
                    </Space>
                </Space>
            </Card>

            {rel ? (
                <Collapse
                    items={[
                        {
                            key: 'rel',
                            label: t('adminShell.staffPerformance.reliabilityTitle'),
                            children: (
                                <Space direction="vertical">
                                    <Typography.Paragraph style={{ marginBottom: 0 }}>
                                        <strong>{rel.primaryDataSource}</strong> — {rel.businessTimeZone}
                                    </Typography.Paragraph>
                                    <Typography.Paragraph type="secondary" style={{ marginBottom: 0 }}>
                                        {rel.dayBucketNote}
                                    </Typography.Paragraph>
                                    <Typography.Paragraph type="secondary" style={{ marginBottom: 0 }}>
                                        {rel.auditSeparationNote}
                                    </Typography.Paragraph>
                                    <Typography.Paragraph type="secondary" style={{ marginBottom: 0 }}>
                                        {rel.cashierIdentityNote}
                                    </Typography.Paragraph>
                                </Space>
                            ),
                        },
                    ]}
                />
            ) : null}

            <Spin spinning={reportQ.isLoading}>
                <Row gutter={[16, 16]}>
                    <Col xs={24} sm={12} md={6}>
                        <Card size="small">
                            <Statistic
                                title={t('adminShell.staffPerformance.kpiSalesCount')}
                                value={totals?.saleTransactionCount ?? 0}
                            />
                        </Card>
                    </Col>
                    <Col xs={24} sm={12} md={6}>
                        <Card size="small">
                            <Statistic
                                title={t('adminShell.staffPerformance.kpiGross')}
                                value={Number(totals?.grossSalesAmount ?? 0)}
                                precision={2}
                                formatter={(v) => moneyFmt.format(Number(v))}
                            />
                        </Card>
                    </Col>
                    <Col xs={24} sm={12} md={6}>
                        <Card size="small">
                            <Statistic
                                title={t('adminShell.staffPerformance.kpiRefunds')}
                                value={totals?.refundRowCount ?? 0}
                            />
                        </Card>
                    </Col>
                    <Col xs={24} sm={12} md={6}>
                        <Card size="small">
                            <Statistic
                                title={t('adminShell.staffPerformance.kpiStorno')}
                                value={totals?.stornoRowCount ?? 0}
                            />
                        </Card>
                    </Col>
                </Row>

                {(reportQ.data?.anomalies?.length ?? 0) > 0 ? (
                    <Alert
                        type="warning"
                        showIcon
                        message={t('adminShell.staffPerformance.anomaliesTitle')}
                        description={
                            <ul style={{ marginBottom: 0, paddingLeft: 18 }}>
                                {reportQ.data?.anomalies?.map((a, i) => (
                                    <li key={i}>
                                        <Typography.Text code>{a.cashierId}</Typography.Text> — {a.message}
                                    </li>
                                ))}
                            </ul>
                        }
                    />
                ) : null}

                <Card title={t('adminShell.staffPerformance.tableStaffTitle')} style={{ marginTop: 16 }}>
                    <Table<StaffPerformanceStaffRowDto>
                        rowKey={(r) => r.cashierId ?? ''}
                        dataSource={reportQ.data?.byStaff ?? []}
                        columns={staffColumns}
                        pagination={{ pageSize: 20 }}
                        scroll={{ x: 1100 }}
                    />
                </Card>

                <Card title={t('adminShell.staffPerformance.tableMethodTitle')} style={{ marginTop: 16 }}>
                    <Table
                        rowKey={(r) => `${r.cashierId}-${r.paymentMethodRaw}`}
                        dataSource={reportQ.data?.byStaffAndPaymentMethod ?? []}
                        columns={methodSliceColumns}
                        pagination={{ pageSize: 15 }}
                    />
                </Card>

                <Card title={t('adminShell.staffPerformance.tableDayTitle')} style={{ marginTop: 16 }}>
                    <Table
                        rowKey={(r) => r.localDayYyyyMmDd ?? ''}
                        dataSource={reportQ.data?.aggregateByLocalDay ?? []}
                        columns={dayAggColumns}
                        pagination={{ pageSize: 31 }}
                    />
                </Card>

                {includePerStaffPerDay ? (
                    <Card title={t('adminShell.staffPerformance.tableDayStaffTitle')} style={{ marginTop: 16 }}>
                        <Table
                            rowKey={(r) => `${r.localDayYyyyMmDd}-${r.cashierId}`}
                            dataSource={reportQ.data?.byLocalDayAndStaff ?? []}
                            columns={[
                                {
                                    title: t('adminShell.staffPerformance.colLocalDay'),
                                    dataIndex: 'localDayYyyyMmDd',
                                    render: (v: string) => formatYyyyMmDd(v),
                                },
                                { title: t('adminShell.staffPerformance.colCashierId'), dataIndex: 'cashierId', ellipsis: true },
                                { title: t('adminShell.staffPerformance.colSalesCount'), dataIndex: 'saleTransactionCount' },
                                {
                                    title: t('adminShell.staffPerformance.colGross'),
                                    dataIndex: 'grossSalesAmount',
                                    render: (v: number) => moneyFmt.format(Number(v ?? 0)),
                                },
                                { title: t('adminShell.staffPerformance.colRefunds'), dataIndex: 'refundRowCount' },
                                { title: t('adminShell.staffPerformance.colStorno'), dataIndex: 'stornoRowCount' },
                            ]}
                            pagination={{ pageSize: 40 }}
                            scroll={{ x: 900 }}
                        />
                    </Card>
                ) : null}
            </Spin>
        </Space>
    );
}

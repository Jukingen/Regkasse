'use client';

import { useMemo, useState } from 'react';
import { Table, Input, Select, DatePicker, Button, Space, Tooltip } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { ReloadOutlined, FilePdfOutlined, PlusOutlined } from '@ant-design/icons';
import { useRouter, useSearchParams } from 'next/navigation';
import { type Dayjs } from 'dayjs';
import { useAntdApp } from '@/hooks/useAntdApp';
import { useBillingSalesList } from '@/features/billing/hooks';
import { BillingAccessGate } from '@/features/billing/components/BillingAccessGate';
import { EmptyState } from '@/components/EmptyState';
import { StatusBadge, resolveStatusType } from '@/components/StatusBadge';
import { formatGermanDateTime } from '@/lib/dateFormatter';
import { downloadLicenseSaleInvoicePdf } from '@/features/billing/utils/downloadInvoicePdf';
import type { LicenseSaleResponse } from '@/api/generated/model';
import { openApiErrorMessage } from '@/shared/errors/openApiErrorMessage';
import { useI18n } from '@/i18n';

const { RangePicker } = DatePicker;

const STATUS_OPTIONS = [
    { value: 'all', label: 'Alle Status' },
    { value: 'active', label: 'Aktiv' },
    { value: 'cancelled', label: 'Storniert' },
    { value: 'refunded', label: 'Rückerstattet' },
] as const;

export default function BillingSalesPage() {
    const router = useRouter();
    const searchParams = useSearchParams();
    const { message } = useAntdApp();
    const { t } = useI18n();
    const [pdfLoadingId, setPdfLoadingId] = useState<string | null>(null);

    const initialTenantId = useMemo(() => searchParams.get('tenantId') ?? undefined, [searchParams]);

    const [filters, setFilters] = useState({
        page: 1,
        pageSize: 20,
        search: '',
        status: 'all',
        tenantId: initialTenantId,
        fromDate: undefined as string | undefined,
        toDate: undefined as string | undefined,
    });

    const { data, isLoading, refetch } = useBillingSalesList(filters);

    const handleSearch = (value: string) => {
        setFilters((prev) => ({ ...prev, search: value, page: 1 }));
    };

    const handleStatusChange = (value: string) => {
        setFilters((prev) => ({ ...prev, status: value, page: 1 }));
    };

    const handleDateRange = (range: [Dayjs | null, Dayjs | null] | null) => {
        setFilters((prev) => ({
            ...prev,
            fromDate: range?.[0]?.startOf('day').toISOString(),
            toDate: range?.[1]?.endOf('day').toISOString(),
            page: 1,
        }));
    };

    const handlePdfDownload = async (saleId: string, invoiceNumber?: string | null) => {
        setPdfLoadingId(saleId);
        try {
            await downloadLicenseSaleInvoicePdf(saleId, invoiceNumber ? `${invoiceNumber}.pdf` : undefined);
        } catch (err) {
            openApiErrorMessage(message.open, t, err, { logContext: 'BillingSalesPage.downloadPdf' });
        } finally {
            setPdfLoadingId(null);
        }
    };

    const columns: ColumnsType<LicenseSaleResponse> = [
        {
            title: 'Rechnungsnummer',
            dataIndex: 'invoiceNumber',
            key: 'invoiceNumber',
            render: (text: string | null | undefined, record) =>
                record.id ? (
                    <Button
                        type="link"
                        style={{ padding: 0 }}
                        onClick={() => router.push(`/admin/billing/sales/${record.id}`)}
                    >
                        {text ?? '—'}
                    </Button>
                ) : (
                    (text ?? '—')
                ),
        },
        {
            title: 'Mandant',
            dataIndex: 'tenantName',
            key: 'tenantName',
            render: (text: string | null | undefined, record) =>
                record.tenantId ? (
                    <Button
                        type="link"
                        style={{ padding: 0 }}
                        onClick={() => router.push(`/admin/tenants/${record.tenantId}`)}
                    >
                        {text ?? record.tenantSlug ?? '—'}
                    </Button>
                ) : (
                    (text ?? '—')
                ),
        },
        { title: 'Slug', dataIndex: 'tenantSlug', key: 'tenantSlug' },
        { title: 'Lizenzplan', dataIndex: 'licensePlan', key: 'licensePlan' },
        {
            title: 'Lizenzschlüssel',
            dataIndex: 'licenseKey',
            key: 'licenseKey',
            render: (text: string | null | undefined) => {
                if (!text) return '—';
                const short = text.length > 15 ? `${text.substring(0, 15)}…` : text;
                return (
                    <Tooltip title={text}>
                        <code>{short}</code>
                    </Tooltip>
                );
            },
        },
        {
            title: 'Gültig bis',
            dataIndex: 'validUntilUtc',
            key: 'validUntilUtc',
            render: (date: string | undefined) => (date ? formatGermanDateTime(date) : '—'),
        },
        {
            title: 'Betrag (Netto)',
            dataIndex: 'priceNet',
            key: 'priceNet',
            align: 'right',
            render: (value: number | undefined) => (value != null ? `€ ${value.toFixed(2)}` : '—'),
        },
        {
            title: 'Status',
            dataIndex: 'status',
            key: 'status',
            render: (status: string | null | undefined) => {
                const key = (status ?? '').toLowerCase();
                const labelMap: Record<string, string> = {
                    active: t('billing.sales.statusActive'),
                    cancelled: t('billing.sales.statusCancelled'),
                    refunded: t('billing.sales.statusRefunded'),
                };
                const resolved = resolveStatusType(key);
                if (resolved) {
                    return <StatusBadge status={resolved} label={labelMap[key]} />;
                }
                if (key === 'refunded') {
                    return <StatusBadge status="warning" label={labelMap.refunded} />;
                }
                return <StatusBadge status="info" label={status || '—'} />;
            },
        },
        {
            title: 'Verkauft am',
            dataIndex: 'soldAtUtc',
            key: 'soldAtUtc',
            render: (date: string | undefined) => (date ? formatGermanDateTime(date) : '—'),
        },
        {
            title: 'Aktionen',
            key: 'actions',
            width: 80,
            render: (_, record) =>
                record.id ? (
                    <Space>
                        <Button
                            type="link"
                            size="small"
                            icon={<FilePdfOutlined />}
                            loading={pdfLoadingId === record.id}
                            onClick={() => void handlePdfDownload(record.id!, record.invoiceNumber)}
                        />
                    </Space>
                ) : null,
        },
    ];

    return (
        <BillingAccessGate>
            <div style={{ padding: 24 }}>
                <div
                    style={{
                        display: 'flex',
                        justifyContent: 'space-between',
                        alignItems: 'center',
                        marginBottom: 16,
                        flexWrap: 'wrap',
                        gap: 16,
                    }}
                >
                    <div>
                        <h1 style={{ margin: 0 }}>Lizenzverkäufe</h1>
                        <p style={{ color: '#64748b', marginBottom: 0 }}>
                            Alle Lizenzverkäufe und -verlängerungen im Überblick.
                        </p>
                    </div>
                    <Button type="primary" icon={<PlusOutlined />} onClick={() => router.push('/admin/billing/sales/new')}>
                        Neuer Verkauf
                    </Button>
                </div>

                <div style={{ marginBottom: 16, display: 'flex', gap: 12, flexWrap: 'wrap' }}>
                    <Input.Search
                        placeholder="Suchen (Mandant, Schlüssel, Rechnung)"
                        onSearch={handleSearch}
                        style={{ width: 280 }}
                        allowClear
                    />
                    <Select
                        value={filters.status}
                        onChange={handleStatusChange}
                        style={{ width: 150 }}
                        options={STATUS_OPTIONS.map((opt) => ({ value: opt.value, label: opt.label }))}
                    />
                    <RangePicker onChange={handleDateRange} />
                    <Button icon={<ReloadOutlined />} onClick={() => void refetch()}>
                        Aktualisieren
                    </Button>
                </div>

                <Table<LicenseSaleResponse>
                    columns={columns}
                    dataSource={data?.items ?? []}
                    rowKey={(row) => row.id ?? row.licenseKey ?? row.invoiceNumber ?? 'row'}
                    loading={isLoading}
                    pagination={{
                        current: filters.page,
                        pageSize: filters.pageSize,
                        total: data?.totalCount ?? 0,
                        showSizeChanger: true,
                        onChange: (page, pageSize) => {
                            setFilters((prev) => ({ ...prev, page, pageSize: pageSize ?? prev.pageSize }));
                        },
                    }}
                    locale={{
                        emptyText: (
                            <EmptyState
                                title={t('billing.sales.noResults')}
                                actionText={t('billing.sales.newSale')}
                                onAction={() => router.push('/admin/billing/sales/new')}
                            />
                        ),
                    }}
                />
            </div>
        </BillingAccessGate>
    );
}

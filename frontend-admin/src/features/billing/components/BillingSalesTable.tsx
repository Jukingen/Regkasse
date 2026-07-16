'use client';

import React, { useMemo, useState } from 'react';
import { Button, Card, DatePicker, Descriptions, Drawer, Form, Input, Modal, Select, Space, Table, Tag } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { DownloadOutlined, PlusOutlined } from '@ant-design/icons';
import Link from 'next/link';
import { useRouter } from 'next/navigation';
import dayjs, { type Dayjs } from 'dayjs';
import { useQueryClient } from '@tanstack/react-query';
import { useAntdApp } from '@/hooks/useAntdApp';
import { useI18n, formatCurrency, formatGermanDateTime } from '@/i18n';
import { billingApi } from '@/features/billing/api/billingApi';
import type { LicenseSaleResponse } from '@/api/generated/model';
import { useBillingAccess } from '@/features/billing/hooks/useBillingAccess';
import { useBillingSalesList } from '@/features/billing/hooks/useBillingSalesList';
import { listAdminTenants } from '@/features/super-admin/api/adminTenants';
import { useQuery } from '@tanstack/react-query';
import {
    formatLicensePlanLabel,
    formatSaleStatusLabel,
    isSaleCancellable,
} from '@/features/billing/utils/billingFormatters';
import { downloadLicenseSaleInvoicePdf } from '@/features/billing/utils/downloadInvoicePdf';
import { billingQueryKeys } from '@/features/billing/constants/billingQueryKeys';
import { openApiErrorMessage } from '@/shared/errors/openApiErrorMessage';

type FilterState = {
    page: number;
    pageSize: number;
    search?: string;
    status?: string;
    tenantId?: string;
    fromDate?: string;
    toDate?: string;
};

const STATUS_OPTIONS = ['active', 'cancelled', 'refunded'] as const;

export function BillingSalesTable({ showHeaderActions = true }: { showHeaderActions?: boolean }) {
    const { t, formatLocale } = useI18n();
    const { message } = useAntdApp();
    const router = useRouter();
    const queryClient = useQueryClient();
    const canAccess = useBillingAccess();

    const [filters, setFilters] = useState<FilterState>({ page: 1, pageSize: 20 });
    const [selectedSale, setSelectedSale] = useState<LicenseSaleResponse | null>(null);
    const [cancelTarget, setCancelTarget] = useState<LicenseSaleResponse | null>(null);
    const [pdfLoadingId, setPdfLoadingId] = useState<string | null>(null);

    const tenantsQuery = useQuery({
        queryKey: ['admin-tenants', 'billing-filter'],
        queryFn: () => listAdminTenants(false),
        enabled: canAccess,
    });

    const salesQuery = useBillingSalesList(filters);

    const cancelMutation = billingApi.useCancel({
        mutation: {
            onSuccess: async () => {
                message.success(t('billing.sales.cancelSuccess'));
                setSelectedSale(null);
                await queryClient.invalidateQueries({ queryKey: billingQueryKeys.all });
            },
            onError: (err) =>
                openApiErrorMessage(message.open, t, err, { logContext: 'BillingSalesTable.cancel' }),
        },
    });

    const tenantOptions = useMemo(
        () =>
            (tenantsQuery.data ?? []).map((tenant) => ({
                value: tenant.id,
                label: `${tenant.name} (${tenant.slug})`,
            })),
        [tenantsQuery.data],
    );

    const statusColor = (status: string | null | undefined) => {
        switch (status) {
            case 'active':
                return 'green';
            case 'cancelled':
                return 'red';
            case 'refunded':
                return 'orange';
            default:
                return 'default';
        }
    };

    const handleDownloadPdf = async (sale: LicenseSaleResponse) => {
        if (!sale.id) return;
        setPdfLoadingId(sale.id);
        try {
            await downloadLicenseSaleInvoicePdf(
                sale.id,
                sale.invoiceNumber ? `${sale.invoiceNumber}.pdf` : undefined,
            );
        } catch (err) {
            openApiErrorMessage(message.open, t, err, { logContext: 'BillingSalesTable.downloadPdf' });
        } finally {
            setPdfLoadingId(null);
        }
    };

    const handleCancel = (sale: LicenseSaleResponse) => {
        if (!sale.id) return;
        setCancelTarget(sale);
    };

    const columns: ColumnsType<LicenseSaleResponse> = [
        {
            title: t('billing.sales.columns.invoiceNumber'),
            dataIndex: 'invoiceNumber',
            key: 'invoiceNumber',
            render: (value: string | null | undefined) => value ?? '—',
        },
        {
            title: t('billing.sales.columns.tenant'),
            key: 'tenant',
            render: (_, row) => (
                <Link href={`/admin/tenants/${row.tenantId}`}>
                    {row.tenantName ?? row.tenantSlug ?? row.tenantId}
                </Link>
            ),
        },
        {
            title: t('billing.sales.columns.licenseKey'),
            dataIndex: 'licenseKey',
            key: 'licenseKey',
            ellipsis: true,
        },
        {
            title: t('billing.sales.columns.plan'),
            dataIndex: 'licensePlan',
            key: 'licensePlan',
            render: (plan: string | null | undefined) => formatLicensePlanLabel(plan, t),
        },
        {
            title: t('billing.sales.columns.priceGross'),
            dataIndex: 'priceGross',
            key: 'priceGross',
            align: 'right',
            render: (value: number | undefined) =>
                value != null ? formatCurrency(value, formatLocale, { currency: 'EUR' }) : '—',
        },
        {
            title: t('billing.sales.columns.validUntil'),
            dataIndex: 'validUntilUtc',
            key: 'validUntilUtc',
            render: (value: string | undefined) =>
                value ? formatGermanDateTime(value) : '—',
        },
        {
            title: t('billing.sales.columns.status'),
            dataIndex: 'status',
            key: 'status',
            render: (status: string | null | undefined) => (
                <Tag color={statusColor(status)}>{formatSaleStatusLabel(status, t)}</Tag>
            ),
        },
        {
            title: t('billing.sales.columns.soldAt'),
            dataIndex: 'soldAtUtc',
            key: 'soldAtUtc',
            render: (value: string | undefined) =>
                value ? formatGermanDateTime(value) : '—',
        },
        {
            key: 'actions',
            width: 120,
            render: (_, row) => (
                <Space>
                    <Button type="link" size="small" onClick={() => setSelectedSale(row)}>
                        {t('billing.sales.view')}
                    </Button>
                </Space>
            ),
        },
    ];

  const onDateRangeChange = (range: [Dayjs | null, Dayjs | null] | null) => {
        setFilters((prev) => ({
            ...prev,
            page: 1,
            fromDate: range?.[0]?.startOf('day').toISOString(),
            toDate: range?.[1]?.endOf('day').toISOString(),
        }));
    };

    return (
        <>
            <Card variant="borderless">
                <Space wrap style={{ marginBottom: 16, width: '100%', justifyContent: 'space-between' }}>
                    <Space wrap>
                        <Input.Search
                            allowClear
                            placeholder={t('billing.sales.searchPlaceholder')}
                            onSearch={(value) =>
                                setFilters((prev) => ({ ...prev, page: 1, search: value.trim() || undefined }))
                            }
                            style={{ width: 280 }}
                        />
                        <Select
                            allowClear
                            placeholder={t('billing.sales.statusFilter')}
                            style={{ width: 160 }}
                            options={STATUS_OPTIONS.map((status) => ({
                                value: status,
                                label: formatSaleStatusLabel(status, t),
                            }))}
                            onChange={(status) => setFilters((prev) => ({ ...prev, page: 1, status }))}
                        />
                        <Select
                            allowClear
                            showSearch
                            optionFilterProp="label"
                            placeholder={t('billing.sales.tenantFilter')}
                            style={{ width: 240 }}
                            options={tenantOptions}
                            onChange={(tenantId) => setFilters((prev) => ({ ...prev, page: 1, tenantId }))}
                        />
                        <DatePicker.RangePicker onChange={onDateRangeChange} />
                    </Space>
                    {showHeaderActions ? (
                        <Button
                            type="primary"
                            icon={<PlusOutlined />}
                            onClick={() => router.push('/admin/billing/sales/new')}
                        >
                            {t('billing.sales.newSale')}
                        </Button>
                    ) : null}
                </Space>

                <Table<LicenseSaleResponse>
                    rowKey={(row) => row.id ?? row.licenseKey ?? row.invoiceNumber ?? 'row'}
                    columns={columns}
                    dataSource={salesQuery.data?.items ?? []}
                    loading={salesQuery.isLoading}
                    pagination={{
                        current: filters.page,
                        pageSize: filters.pageSize,
                        total: salesQuery.data?.totalCount ?? 0,
                        showSizeChanger: true,
                        onChange: (page, pageSize) =>
                            setFilters((prev) => ({ ...prev, page, pageSize: pageSize ?? prev.pageSize })),
                    }}
                />
            </Card>

            <Drawer
                title={t('billing.sales.detailTitle')}
                open={selectedSale != null}
                onClose={() => setSelectedSale(null)}
                width={520}
                destroyOnHidden
                extra={
                    selectedSale?.id ? (
                        <Space>
                            <Button
                                icon={<DownloadOutlined />}
                                loading={pdfLoadingId === selectedSale.id}
                                onClick={() => void handleDownloadPdf(selectedSale)}
                            >
                                {t('billing.sales.downloadPdf')}
                            </Button>
                            {isSaleCancellable(selectedSale) ? (
                                <Button danger onClick={() => handleCancel(selectedSale)}>
                                    {t('billing.sales.cancelSale')}
                                </Button>
                            ) : null}
                        </Space>
                    ) : null
                }
            >
                {selectedSale ? (
                    <Descriptions column={1} size="small" bordered>
                        <Descriptions.Item label={t('billing.sales.columns.invoiceNumber')}>
                            {selectedSale.invoiceNumber ?? '—'}
                        </Descriptions.Item>
                        <Descriptions.Item label={t('billing.sales.columns.tenant')}>
                            {selectedSale.tenantName ?? selectedSale.tenantSlug ?? '—'}
                        </Descriptions.Item>
                        <Descriptions.Item label={t('billing.sales.columns.licenseKey')}>
                            {selectedSale.licenseKey ?? '—'}
                        </Descriptions.Item>
                        <Descriptions.Item label={t('billing.sales.columns.plan')}>
                            {formatLicensePlanLabel(selectedSale.licensePlan, t)}
                        </Descriptions.Item>
                        <Descriptions.Item label={t('billing.sales.columns.priceGross')}>
                            {selectedSale.priceGross != null
                                ? formatCurrency(selectedSale.priceGross, formatLocale, { currency: 'EUR' })
                                : '—'}
                        </Descriptions.Item>
                        <Descriptions.Item label={t('billing.sales.columns.validUntil')}>
                            {selectedSale.validUntilUtc
                                ? formatGermanDateTime(selectedSale.validUntilUtc)
                                : '—'}
                        </Descriptions.Item>
                        <Descriptions.Item label={t('billing.sales.columns.status')}>
                            <Tag color={statusColor(selectedSale.status)}>
                                {formatSaleStatusLabel(selectedSale.status, t)}
                            </Tag>
                        </Descriptions.Item>
                    </Descriptions>
                ) : null}
            </Drawer>

            {cancelTarget ? (
                <BillingSaleCancelModal
                    sale={cancelTarget}
                    loading={cancelMutation.isPending}
                    onClose={() => setCancelTarget(null)}
                    onConfirm={async (cancellationReason) => {
                        await cancelMutation.mutateAsync({
                            id: cancelTarget.id!,
                            data: { cancellationReason },
                        });
                        setCancelTarget(null);
                    }}
                />
            ) : null}
        </>
    );
}

type BillingSaleCancelModalProps = {
    sale: LicenseSaleResponse;
    loading: boolean;
    onClose: () => void;
    onConfirm: (cancellationReason: string) => Promise<void>;
};

function BillingSaleCancelModal({ loading, onClose, onConfirm }: BillingSaleCancelModalProps) {
    const { t } = useI18n();
    const [form] = Form.useForm<{ cancellationReason: string }>();

    const handleOk = async () => {
        const values = await form.validateFields();
        await onConfirm(values.cancellationReason);
    };

    return (
        <Modal
            open
            title={t('billing.sales.cancelConfirmTitle')}
            onCancel={onClose}
            onOk={handleOk}
            confirmLoading={loading}
            okText={t('billing.sales.cancelSale')}
            okButtonProps={{ danger: true }}
            cancelText={t('common.buttons.cancel')}
            destroyOnHidden
        >
            <Form form={form} layout="vertical" style={{ marginTop: 8 }}>
                <Form.Item
                    name="cancellationReason"
                    label={t('billing.sales.cancelReasonLabel')}
                    rules={[
                        { required: true, message: t('billing.sales.cancelReasonRequired') },
                        { min: 10, message: t('billing.sales.cancelReasonRequired') },
                    ]}
                >
                    <Input.TextArea rows={3} />
                </Form.Item>
            </Form>
        </Modal>
    );
}

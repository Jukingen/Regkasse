'use client';

import React, { useState, useMemo } from 'react';
import { Table, Button, Input, Select, DatePicker, Space, Tag, Card, Row, Col, message, Tooltip, Modal, Descriptions, Alert, Empty } from 'antd';
import { SearchOutlined, DownloadOutlined, ReloadOutlined, EyeOutlined, PrinterOutlined, CloudUploadOutlined } from '@ant-design/icons';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import dayjs from 'dayjs';
import type { TablePaginationConfig, TableProps } from 'antd/es/table';
import type { SorterResult } from 'antd/es/table/interface';
import { useDebounce } from '@/hooks/useDebounce';
import { normalizeFromDate, normalizeToDate, validateDateRange } from '../utils/dateUtils';

// Orval-generated hooks and types (detail, duplicate, export — NOT list)
import {
    useGetApiInvoiceId,
    getGetApiInvoiceIdQueryKey,
    exportInvoices as orvalExportInvoices,
} from '@/api/generated/invoice/invoice';
import { usePostApiFinanzOnlineSubmitInvoice } from '@/api/generated/finanz-online/finanz-online';
import type { InvoiceListItemDto } from '@/api/generated/model/invoiceListItemDto';
import type { Invoice, PaymentMethod, InvoiceStatus } from '@/api/generated/model';

// POS-backed list — uses /api/Invoice/pos-list
import { getInvoicesList, getInvoicePdf } from '../api/invoiceService';
import type { InvoiceListParams } from '../types';

const { RangePicker } = DatePicker;

// Manual mapping because Orval generated NUMBER_0 etc. for enum
const InvoiceStatusMap: Record<number, { label: string, color: string }> = {
    0: { label: 'Draft', color: 'default' },
    1: { label: 'Sent', color: 'processing' },
    2: { label: 'Paid', color: 'success' },
    3: { label: 'Partially Paid', color: 'warning' },
    4: { label: 'Unpaid', color: 'error' },
    5: { label: 'Overdue', color: 'error' },
    6: { label: 'Cancelled', color: 'default' },
};

const getPaymentMethodLabel = (method?: PaymentMethod) => {
    switch (method) {
        case 0: return 'Cash';
        case 1: return 'Card';
        case 2: return 'BankTransfer';
        case 3: return 'Check';
        case 4: return 'Voucher';
        case 5: return 'Mobile';
        default: return '-';
    }
};


export const InvoiceList: React.FC = () => {
    const queryClient = useQueryClient();

    // State
    const [pagination, setPagination] = useState<TablePaginationConfig>({
        current: 1,
        pageSize: 50,
        showSizeChanger: true,
        pageSizeOptions: ['20', '50', '100', '200']
    });
    const [searchText, setSearchText] = useState('');
    const [statusFilter, setStatusFilter] = useState<InvoiceStatus | undefined>(undefined);
    const [dateRange, setDateRange] = useState<[dayjs.Dayjs | null, dayjs.Dayjs | null] | null>(null);
    const [sortField, setSortField] = useState<string>('invoiceDate');
    const [sortOrder, setSortOrder] = useState<'asc' | 'desc'>('desc');
    const [exportLoading, setExportLoading] = useState(false);

    // Detail Modal State
    const [detailVisible, setDetailVisible] = useState(false);
    const [selectedInvoiceId, setSelectedInvoiceId] = useState<string | null>(null);

    const debouncedSearch = useDebounce(searchText, 500);

    // Date range validation
    const dateRangeError = useMemo(() => {
        if (!dateRange) return null;
        return validateDateRange(dateRange[0], dateRange[1]);
    }, [dateRange]);

    // Query Params — manual type matching POS-list endpoint
    const queryParams: InvoiceListParams = useMemo(() => ({
        page: pagination.current,
        pageSize: pagination.pageSize,
        query: debouncedSearch || undefined,
        from: dateRange?.[0] ? normalizeFromDate(dateRange[0]) : undefined,
        to: dateRange?.[1] ? normalizeToDate(dateRange[1]) : undefined,
        sortBy: sortField as InvoiceListParams['sortBy'],
        sortDir: sortOrder as InvoiceListParams['sortDir'],
    }), [pagination.current, pagination.pageSize, debouncedSearch, dateRange, sortField, sortOrder]);

    // Data Fetching — POS-backed via invoiceService
    const { data, isLoading, isFetching, isError, error, refetch } = useQuery({
        queryKey: ['invoices', queryParams],
        queryFn: () => getInvoicesList(queryParams),
        placeholderData: (previousData: any) => previousData,
        enabled: !dateRangeError,
    });

    // Detail Fetching
    const { data: detailInvoice, isLoading: detailLoading } = useGetApiInvoiceId(
        selectedInvoiceId || '',
        {
            query: {
                enabled: !!selectedInvoiceId && detailVisible,
            },
        }
    );

    // Mutations
    const submitFinanzOnlineMutation = usePostApiFinanzOnlineSubmitInvoice();


    // Handlers
    const handleTableChange: TableProps<InvoiceListItemDto>['onChange'] = (
        newPagination,
        filters,
        sorter
    ) => {
        setPagination(newPagination);
        if (Array.isArray(sorter)) {
            // multisort not supported yet
        } else {
            const s = sorter as SorterResult<InvoiceListItemDto>;
            if (s.field) {
                setSortField(s.field as string);
                setSortOrder(s.order === 'ascend' ? 'asc' : 'desc');
            } else {
                setSortField('invoiceDate');
                setSortOrder('desc');
            }
        }
    };

    const handleExport = async () => {
        try {
            setExportLoading(true);
            const blob = await orvalExportInvoices({
                from: queryParams.from,
                to: queryParams.to,
                status: queryParams.status,
                query: queryParams.query,
                sortBy: queryParams.sortBy,
                sortDir: queryParams.sortDir,
            });
            const url = window.URL.createObjectURL(blob);
            const link = document.createElement('a');
            link.href = url;
            link.setAttribute('download', `Invoices_${dayjs().format('YYYYMMDD_HHmm')}.csv`);
            document.body.appendChild(link);
            link.click();
            link.remove();
            window.URL.revokeObjectURL(url);
            message.success('Export started successfully');
        } catch (error) {
            console.error('Export failed', error);
            message.error('Failed to export invoices');
        } finally {
            setExportLoading(false);
        }
    };

    const handlePrint = async (id: string) => {
        try {
            const blob = await getInvoicePdf(id);
            const blobUrl = URL.createObjectURL(new Blob([blob], { type: 'application/pdf' }));
            window.open(blobUrl, '_blank');
            // Clean up after a delay so the new tab can load
            setTimeout(() => URL.revokeObjectURL(blobUrl), 60_000);
        } catch (err: any) {
            const status = err?.response?.status;
            if (status === 401) {
                message.error('Session expired. Please log in again.');
            } else if (status === 404) {
                message.error('Invoice not found or has been deleted.');
            } else {
                message.error('Failed to generate PDF. Please try again.');
            }
        }
    };

    const handleSubmitFinanzOnline = (invoice: Invoice) => {
        if (!invoice.tseSignature || !invoice.taxDetails) {
            message.error('Missing TSE Signature or Tax Details');
            return;
        }

        submitFinanzOnlineMutation.mutate({
            data: {
                invoiceNumber: invoice.invoiceNumber,
                totalAmount: invoice.totalAmount,
                tseSignature: invoice.tseSignature,
                taxDetails: JSON.stringify(invoice.taxDetails),
                invoiceDate: invoice.invoiceDate,
                kassenId: invoice.kassenId,
            }
        }, {
            onSuccess: (data) => {
                if (data.success) {
                    message.success('Submitted to FinanzOnline successfully');
                    refetch();
                    if (selectedInvoiceId) {
                        // re-fetch details if open
                        queryClient.invalidateQueries({ queryKey: getGetApiInvoiceIdQueryKey(selectedInvoiceId) } as any);
                    }
                } else {
                    message.warning(`Submission failed: ${data.message}`);
                }
            },
            onError: () => {
                message.error('Error submitting to FinanzOnline');
            }
        });
    };

    // Columns
    const columns: TableProps<InvoiceListItemDto>['columns'] = [
        {
            title: 'Invoice #',
            dataIndex: 'invoiceNumber',
            key: 'invoiceNumber',
            sorter: true,
            render: (text) => <span style={{ fontWeight: 500 }}>{text}</span>
        },
        {
            title: 'Date',
            dataIndex: 'invoiceDate',
            key: 'invoiceDate',
            sorter: true,
            width: 150,
            render: (date) => dayjs(date).format('DD.MM.YYYY HH:mm'),
        },
        {
            title: 'Customer',
            dataIndex: 'customerName',
            key: 'customerName',
            render: (text) => text || '-',
        },
        {
            title: 'Total',
            dataIndex: 'totalAmount',
            key: 'totalAmount',
            sorter: true,
            align: 'right',
            render: (amount) =>
                new Intl.NumberFormat('de-AT', { style: 'currency', currency: 'EUR' }).format(amount ?? 0),
        },
        {
            title: 'Status',
            dataIndex: 'status',
            key: 'status',
            sorter: true,
            width: 120,
            render: (status: InvoiceStatus) => {
                const info = InvoiceStatusMap[status as unknown as number] || { label: 'Unknown', color: 'default' };
                return (
                    <Tag color={info.color}>
                        {info.label}
                    </Tag>
                );
            }
        },
        {
            title: 'Actions',
            key: 'actions',
            width: 120,
            render: (_, record) => (
                <Space size="small">
                    <Tooltip title="View Details">
                        <Button
                            icon={<EyeOutlined />}
                            size="small"
                            onClick={() => { setSelectedInvoiceId(record.id ?? null); setDetailVisible(true); }}
                        />
                    </Tooltip>
                    <Tooltip title="Print">
                        <Button
                            icon={<PrinterOutlined />}
                            size="small"
                            onClick={() => handlePrint(record.id ?? '')}
                        />
                    </Tooltip>
                </Space>
            ),
        },
    ];

    const statusOptions = Object.entries(InvoiceStatusMap).map(([key, val]) => ({
        label: val.label,
        value: Number(key) as InvoiceStatus
    }));

    return (
        <React.Fragment>
            <Card title="Invoices" extra={
                <Button
                    type="primary"
                    icon={<DownloadOutlined />}
                    onClick={handleExport}
                    loading={exportLoading}
                >
                    Export CSV
                </Button>
            }>
                <Space direction="vertical" style={{ width: '100%' }} size="middle">
                    {/* Filters */}
                    <Row gutter={[16, 16]}>
                        <Col xs={24} sm={8} md={6}>
                            <Input
                                placeholder="Search #, Client..."
                                prefix={<SearchOutlined />}
                                value={searchText}
                                onChange={(e) => setSearchText(e.target.value)}
                                allowClear
                            />
                        </Col>
                        <Col xs={24} sm={8} md={6}>
                            <Select
                                placeholder="Status"
                                style={{ width: '100%' }}
                                allowClear
                                onChange={(val) => setStatusFilter(val)}
                                options={statusOptions}
                            />
                        </Col>
                        <Col xs={24} sm={8} md={8}>
                            <RangePicker
                                style={{ width: '100%' }}
                                onChange={(dates) => setDateRange(dates as any)}
                                status={dateRangeError ? 'error' : undefined}
                            />
                            {dateRangeError && (
                                <div style={{ color: '#ff4d4f', fontSize: 12, marginTop: 4 }}>{dateRangeError}</div>
                            )}
                        </Col>
                        <Col xs={24} sm={24} md={4} style={{ textAlign: 'right' }}>
                            <Button icon={<ReloadOutlined />} onClick={() => refetch()} loading={isFetching} />
                        </Col>
                    </Row>

                    {/* Error State */}
                    {isError && (
                        <Alert
                            type="error"
                            message="Failed to load invoices"
                            description="An unexpected error occurred while loading invoices. Please try again."
                            showIcon
                            closable
                            action={
                                <Button size="small" onClick={() => refetch()}>
                                    Retry
                                </Button>
                            }
                        />
                    )}

                    {/* Table */}
                    <Table
                        columns={columns}
                        dataSource={data?.items || []}
                        rowKey="id"
                        pagination={{
                            ...pagination,
                            total: data?.totalCount || 0,
                        }}
                        loading={isLoading || isFetching}
                        onChange={handleTableChange}
                        size="middle"
                        scroll={{ x: 800 }}
                        locale={{
                            emptyText: isLoading ? undefined : (
                                <Empty
                                    description={
                                        dateRange
                                            ? 'No invoices found for the selected date range. Try adjusting your filters.'
                                            : 'No invoices found. Try adjusting your filters or create a new invoice.'
                                    }
                                />
                            ),
                        }}
                    />
                </Space>
            </Card>

            {/* Detail Modal */}
            <Modal
                title={detailInvoice ? `Invoice: ${detailInvoice.invoiceNumber}` : 'Invoice Details'}
                open={detailVisible}
                onCancel={() => setDetailVisible(false)}
                footer={[
                    <Button key="close" onClick={() => setDetailVisible(false)}>Close</Button>,
                    <Button
                        key="print"
                        type="primary"
                        icon={<PrinterOutlined />}
                        onClick={() => detailInvoice && handlePrint(detailInvoice.id || '')}
                    >
                        Print
                    </Button>,
                    // Add Submit Button here if pertinent
                    (detailInvoice && (detailInvoice.status === 1 || detailInvoice.status === 2)) && (
                        <Button
                            key="submit"
                            icon={<CloudUploadOutlined />}
                            onClick={() => handleSubmitFinanzOnline(detailInvoice)}
                        >
                            Submit to FinanzOnline
                        </Button>
                    )
                ]}
                width={800}
            >
                {detailLoading ? (
                    <p>Loading...</p>
                ) : detailInvoice ? (
                    <>
                        <Descriptions bordered column={2} size="small">
                            <Descriptions.Item label="Date">{dayjs(detailInvoice.invoiceDate).format('DD.MM.YYYY HH:mm')}</Descriptions.Item>
                            <Descriptions.Item label="Status">
                                <Tag color={InvoiceStatusMap[detailInvoice.status as unknown as number]?.color || 'default'}>
                                    {InvoiceStatusMap[detailInvoice.status as unknown as number]?.label || detailInvoice.status}
                                </Tag>
                            </Descriptions.Item>

                            <Descriptions.Item label="Customer" span={2}>
                                {detailInvoice.customerName} <br />
                                {detailInvoice.customerAddress} <br />
                                {detailInvoice.customerTaxNumber}
                            </Descriptions.Item>

                            <Descriptions.Item label="Company" span={2}>
                                {detailInvoice.companyName} <br />
                                {detailInvoice.companyTaxNumber}
                            </Descriptions.Item>

                            <Descriptions.Item label="Total Amount">€ {(detailInvoice.totalAmount ?? 0).toFixed(2)}</Descriptions.Item>
                            <Descriptions.Item label="Tax Amount">€ {(detailInvoice.taxAmount ?? 0).toFixed(2)}</Descriptions.Item>

                            <Descriptions.Item label="Payment Method">{getPaymentMethodLabel(detailInvoice.paymentMethod)}</Descriptions.Item>
                            <Descriptions.Item label="TSE Signature" span={2} style={{ wordBreak: 'break-all', fontFamily: 'monospace', fontSize: 10 }}>
                                {detailInvoice.tseSignature}
                            </Descriptions.Item>

                            <Descriptions.Item label="Items (JSON)" span={2}>
                                <pre style={{ maxHeight: 200, overflow: 'auto', fontSize: 11 }}>
                                    {JSON.stringify(detailInvoice.invoiceItems, null, 2)}
                                </pre>
                            </Descriptions.Item>
                        </Descriptions>
                    </>
                ) : (
                    <p>No details found.</p>
                )}
            </Modal>
        </React.Fragment>
    );
};

'use client';

import React, { useState, useMemo } from 'react';
import { Table, Button, Input, Select, DatePicker, Space, Tag, Card, Row, Col, message, Tooltip, Modal, Descriptions, Alert, Empty, Form } from 'antd';
import { SearchOutlined, DownloadOutlined, ReloadOutlined, EyeOutlined, PrinterOutlined, CloudUploadOutlined, RollbackOutlined } from '@ant-design/icons';
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
    getApiInvoiceId,
} from '@/api/generated/invoice/invoice';
import {
    usePostApiFinanzOnlineSubmitInvoice,
    postApiFinanzOnlineSubmitInvoice
} from '@/api/generated/finanz-online/finanz-online';
import type { Invoice, PaymentMethod, InvoiceStatus } from '@/api/generated/model';

// POS-backed list — uses /api/Invoice/pos-list
import { getInvoicesList, getInvoicePdf, createCreditNote } from '../api/invoiceService';
import type { ExtendedInvoiceListItem } from '../api/invoiceService';
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
    7: { label: 'Credit Note', color: 'purple' },
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
    const [cashRegisterIdFilter, setCashRegisterIdFilter] = useState<string | undefined>(undefined);
    const [sortField, setSortField] = useState<string>('invoiceDate');
    const [sortOrder, setSortOrder] = useState<'asc' | 'desc'>('desc');
    const [exportLoading, setExportLoading] = useState(false);
    const [batchLoading, setBatchLoading] = useState(false);
    const [selectedRowKeys, setSelectedRowKeys] = useState<React.Key[]>([]);

    // Detail Modal State
    const [detailVisible, setDetailVisible] = useState(false);
    const [selectedInvoiceId, setSelectedInvoiceId] = useState<string | null>(null);

    // Credit Note Modal State
    const [creditNoteVisible, setCreditNoteVisible] = useState(false);
    const [creditNoteTargetId, setCreditNoteTargetId] = useState<string | null>(null);
    const [creditNoteLoading, setCreditNoteLoading] = useState(false);
    const [creditNoteForm] = Form.useForm();

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
        status: statusFilter,
        from: dateRange?.[0] ? normalizeFromDate(dateRange[0]) : undefined,
        to: dateRange?.[1] ? normalizeToDate(dateRange[1]) : undefined,
        sortBy: sortField as InvoiceListParams['sortBy'],
        sortDir: sortOrder as InvoiceListParams['sortDir'],
        cashRegisterId: cashRegisterIdFilter,
    }), [pagination.current, pagination.pageSize, debouncedSearch, statusFilter, dateRange, sortField, sortOrder, cashRegisterIdFilter]);

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
    const handleTableChange: TableProps<ExtendedInvoiceListItem>['onChange'] = (
        newPagination,
        filters,
        sorter
    ) => {
        setPagination(newPagination);
        if (Array.isArray(sorter)) {
            // multisort not supported yet
        } else {
            const s = sorter as SorterResult<ExtendedInvoiceListItem>;
            if (s.field) {
                setSortField(s.field as string);
                setSortOrder(s.order === 'ascend' ? 'asc' : 'desc');
            } else {
                setSortField('invoiceDate');
                setSortOrder('desc');
            }
        }
    };

    const handleCreateCreditNote = async () => {
        try {
            const values = await creditNoteForm.validateFields();
            setCreditNoteLoading(true);
            await createCreditNote(creditNoteTargetId!, {
                reasonCode: values.reasonCode,
                reasonText: values.reasonText,
            });
            message.success('Credit note created successfully');
            setCreditNoteVisible(false);
            creditNoteForm.resetFields();
            refetch();
        } catch (err: any) {
            const status = err?.response?.status;
            if (status === 409) {
                message.warning('A credit note already exists for this invoice.');
            } else if (status === 400) {
                message.error(err?.response?.data || 'Invalid request.');
            } else if (err?.errorFields) {
                // form validation error — ignore, form shows inline
            } else {
                message.error('Failed to create credit note.');
            }
        } finally {
            setCreditNoteLoading(false);
        }
    };

    const handleBatchPrint = async () => {
        if (!selectedRowKeys.length) return;
        setBatchLoading(true);
        let success = 0;
        let fail = 0;

        for (const key of selectedRowKeys) {
            try {
                const blob = await getInvoicePdf(key.toString());
                const blobUrl = URL.createObjectURL(new Blob([blob], { type: 'application/pdf' }));
                const link = document.createElement('a');
                link.href = blobUrl;
                link.setAttribute('download', `Invoice_${key}.pdf`);
                document.body.appendChild(link);
                link.click();
                link.remove();
                setTimeout(() => URL.revokeObjectURL(blobUrl), 60_000);
                success++;
            } catch (err) {
                fail++;
            }
        }
        setBatchLoading(false);
        message.success(`Batch Print: ${success} successful, ${fail} failed`);
        setSelectedRowKeys([]);
    };

    const handleBatchExport = async () => {
        if (!selectedRowKeys.length) return;
        setBatchLoading(true);
        let success = 0;
        let fail = 0;
        const lines = ['InvoiceNumber;InvoiceDate;CustomerName;CompanyName;TotalAmount;Status;DocumentType;OriginalInvoiceId;KassenId;TseSignature'];

        for (const key of selectedRowKeys) {
            try {
                const i = await getApiInvoiceId(key.toString());
                const escapeCsv = (v: any) => v ? `"${String(v).replace(/"/g, '""')}"` : '';
                lines.push(`${i.invoiceNumber};${dayjs(i.invoiceDate).format('YYYY-MM-DD HH:mm')};${escapeCsv(i.customerName)};${escapeCsv(i.companyName)};${i.totalAmount};${i.status};${(i as any).documentType || ''};${(i as any).originalInvoiceId || ''};${i.kassenId || ''};${escapeCsv(i.tseSignature)}`);
                success++;
            } catch {
                fail++;
            }
        }

        if (success > 0) {
            const blob = new Blob([lines.join('\n')], { type: 'text/csv;charset=utf-8;' });
            const url = window.URL.createObjectURL(blob);
            const link = document.createElement('a');
            link.href = url;
            link.setAttribute('download', `Batch_Export_${dayjs().format('YYYYMMDD_HHmm')}.csv`);
            document.body.appendChild(link);
            link.click();
            link.remove();
            window.URL.revokeObjectURL(url);
        }
        setBatchLoading(false);
        message.success(`Batch Export: ${success} exported, ${fail} failed`);
        setSelectedRowKeys([]);
    };

    const handleBatchSubmit = () => {
        if (!selectedRowKeys.length) return;
        Modal.confirm({
            title: 'Confirm Batch Submit',
            content: `Are you sure you want to submit ${selectedRowKeys.length} invoice(s) to FinanzOnline? Ineligible statuses will be skipped.`,
            onOk: async () => {
                setBatchLoading(true);
                let success = 0;
                let fail = 0;
                let skipped = 0;
                for (const key of selectedRowKeys) {
                    try {
                        const inv = await getApiInvoiceId(key.toString());
                        if (inv.status !== 1 && inv.status !== 2) {
                            skipped++;
                            continue;
                        }
                        if (!inv.tseSignature || !inv.taxDetails) {
                            fail++;
                            continue;
                        }

                        const res = await postApiFinanzOnlineSubmitInvoice({
                            invoiceNumber: inv.invoiceNumber,
                            totalAmount: inv.totalAmount,
                            tseSignature: inv.tseSignature,
                            taxDetails: typeof inv.taxDetails === 'string' ? inv.taxDetails : JSON.stringify(inv.taxDetails),
                            invoiceDate: inv.invoiceDate,
                            kassenId: inv.kassenId,
                        });

                        if (res.success) {
                            success++;
                        } else {
                            fail++;
                        }
                    } catch {
                        fail++;
                    }
                }
                setBatchLoading(false);
                message.info(`Batch Submit: ${success} successful, ${fail} failed, ${skipped} skipped.`);
                setSelectedRowKeys([]);
                refetch();
            }
        });
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
                cashRegisterId: queryParams.cashRegisterId,
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
    const columns: TableProps<ExtendedInvoiceListItem>['columns'] = [
        {
            title: 'Invoice #',
            dataIndex: 'invoiceNumber',
            key: 'invoiceNumber',
            sorter: true,
            render: (text, record) => (
                <Space size={4}>
                    <span style={{ fontWeight: 500 }}>{text}</span>
                    {record.documentType === 1 && <Tag color="purple" style={{ fontSize: 10 }}>CN</Tag>}
                </Space>
            )
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
            width: 160,
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
                    {/* Credit note only for Paid(2) or Sent(1) invoices that are not already credit notes */}
                    {(record.status as unknown as number === 2 || record.status as unknown as number === 1) && record.documentType !== 1 && (
                        <Tooltip title="Create Credit Note">
                            <Button
                                icon={<RollbackOutlined />}
                                size="small"
                                danger
                                onClick={() => {
                                    setCreditNoteTargetId(record.id ?? null);
                                    setCreditNoteVisible(true);
                                }}
                            />
                        </Tooltip>
                    )}
                </Space>
            ),
        },
    ];

    const statusOptions = Object.entries(InvoiceStatusMap).map(([key, val]) => ({
        label: val.label,
        value: Number(key) as InvoiceStatus
    }));

    const selectedRow = data?.items?.find((item) => item.id === selectedInvoiceId);
    const displayInvoiceNumber = detailInvoice?.invoiceNumber || selectedRow?.invoiceNumber || selectedInvoiceId || 'Unknown';

    return (
        <React.Fragment>
            <Card title="Invoices" extra={
                <Space>
                    <Button
                        disabled={!selectedRowKeys.length}
                        icon={<PrinterOutlined />}
                        onClick={handleBatchPrint}
                        loading={batchLoading}
                    >
                        Batch Print
                    </Button>
                    <Button
                        disabled={!selectedRowKeys.length}
                        icon={<DownloadOutlined />}
                        onClick={handleBatchExport}
                        loading={batchLoading}
                    >
                        Batch Export
                    </Button>
                    <Button
                        disabled={!selectedRowKeys.length}
                        icon={<CloudUploadOutlined />}
                        onClick={handleBatchSubmit}
                        loading={batchLoading}
                    >
                        Batch Submit
                    </Button>
                    <Button
                        type="primary"
                        icon={<DownloadOutlined />}
                        onClick={handleExport}
                        loading={exportLoading}
                    >
                        Export CSV (All)
                    </Button>
                </Space>
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
                        <Col xs={24} sm={8} md={4}>
                            <Select
                                placeholder="Status"
                                style={{ width: '100%' }}
                                allowClear
                                value={statusFilter}
                                onChange={(val) => { setStatusFilter(val); setPagination(p => ({ ...p, current: 1 })); }}
                                options={statusOptions}
                            />
                        </Col>
                        <Col xs={24} sm={8} md={5}>
                            <Input
                                placeholder="KassenID / Register ID"
                                value={cashRegisterIdFilter}
                                onChange={(e) => { setCashRegisterIdFilter(e.target.value || undefined); setPagination(p => ({ ...p, current: 1 })); }}
                                allowClear
                            />
                        </Col>
                        <Col xs={24} sm={16} md={7}>
                            <RangePicker
                                style={{ width: '100%' }}
                                onChange={(dates) => setDateRange(dates as any)}
                                status={dateRangeError ? 'error' : undefined}
                            />
                            {dateRangeError && (
                                <div style={{ color: '#ff4d4f', fontSize: 12, marginTop: 4 }}>{dateRangeError}</div>
                            )}
                        </Col>
                        <Col xs={24} sm={8} md={2} style={{ textAlign: 'right' }}>
                            <Button icon={<ReloadOutlined />} onClick={() => refetch()} loading={isFetching} />
                        </Col>
                    </Row>

                    {/* Active Filter Tags */}
                    {cashRegisterIdFilter && (
                        <div>
                            <Tag
                                closable
                                onClose={() => {
                                    setCashRegisterIdFilter(undefined);
                                    setPagination(p => ({ ...p, current: 1 }));
                                }}
                                color="blue"
                            >
                                KassenID: {cashRegisterIdFilter}
                            </Tag>
                            <Button type="link" size="small" onClick={() => { setCashRegisterIdFilter(undefined); setPagination(p => ({ ...p, current: 1 })); }}>Clear Filter</Button>
                        </div>
                    )}

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
                        rowSelection={{
                            selectedRowKeys,
                            onChange: setSelectedRowKeys,
                        }}
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
                title={`Invoice: ${displayInvoiceNumber}`}
                open={detailVisible}
                onCancel={() => setDetailVisible(false)}
                footer={[
                    <Button key="close" onClick={() => setDetailVisible(false)}>Close</Button>,
                    <Button
                        key="print"
                        type="primary"
                        icon={<PrinterOutlined />}
                        onClick={() => handlePrint(selectedInvoiceId || '')}
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
                    <div style={{ textAlign: 'center', padding: '20px' }}>Loading details...</div>
                ) : detailInvoice ? (
                    <React.Fragment>
                        {(() => {
                            const safe = (v: any) => (v === null || v === undefined || v === '') ? "-" : v;

                            let itemsObj: any[] = [];
                            try {
                                if (typeof detailInvoice.invoiceItems === 'string') {
                                    itemsObj = JSON.parse(detailInvoice.invoiceItems);
                                } else if (Array.isArray(detailInvoice.invoiceItems)) {
                                    itemsObj = detailInvoice.invoiceItems;
                                } else if (detailInvoice.invoiceItems) {
                                    itemsObj = [detailInvoice.invoiceItems];
                                }
                            } catch (e) { }

                            return (
                                <Descriptions bordered column={2} size="small">
                                    <Descriptions.Item label="Date">{safe(dayjs(detailInvoice.invoiceDate || detailInvoice.createdAt).isValid() ? dayjs(detailInvoice.invoiceDate || detailInvoice.createdAt).format('DD.MM.YYYY HH:mm') : null)}</Descriptions.Item>
                                    <Descriptions.Item label="Status">
                                        <Tag color={InvoiceStatusMap[detailInvoice.status as unknown as number]?.color || 'default'}>
                                            {InvoiceStatusMap[detailInvoice.status as unknown as number]?.label || safe(detailInvoice.status)}
                                        </Tag>
                                    </Descriptions.Item>

                                    <Descriptions.Item label="Customer" span={2}>
                                        {safe(detailInvoice.customerName)} <br />
                                        {safe(detailInvoice.customerAddress)} <br />
                                        {safe(detailInvoice.customerTaxNumber)}
                                    </Descriptions.Item>

                                    <Descriptions.Item label="Company" span={2}>
                                        {safe(detailInvoice.companyName)} <br />
                                        {safe(detailInvoice.companyTaxNumber)}
                                    </Descriptions.Item>

                                    <Descriptions.Item label="Total Amount">€ {safe((detailInvoice.totalAmount ?? 0).toFixed(2))}</Descriptions.Item>
                                    <Descriptions.Item label="Tax Amount">€ {safe((detailInvoice.taxAmount ?? 0).toFixed(2))}</Descriptions.Item>

                                    <Descriptions.Item label="Payment Method">{safe(getPaymentMethodLabel(detailInvoice.paymentMethod))}</Descriptions.Item>
                                    <Descriptions.Item label="TSE Signature" span={2} style={{ wordBreak: 'break-all', fontFamily: 'monospace', fontSize: 10 }}>
                                        {safe(detailInvoice.tseSignature)}
                                    </Descriptions.Item>

                                    <Descriptions.Item label="Items (JSON)" span={2}>
                                        <div style={{ marginBottom: 8, fontWeight: 'bold' }}>
                                            {itemsObj.length > 0 ? `${itemsObj.length} items` : 'No items'}
                                        </div>
                                        {itemsObj.length > 0 ? (
                                            <pre style={{ maxHeight: 200, overflow: 'auto', fontSize: 11, margin: 0 }}>
                                                {JSON.stringify(itemsObj, null, 2)}
                                            </pre>
                                        ) : detailInvoice.invoiceItems ? (
                                            <pre style={{ maxHeight: 200, overflow: 'auto', fontSize: 11, margin: 0 }}>
                                                {JSON.stringify(detailInvoice.invoiceItems, null, 2)}
                                            </pre>
                                        ) : null}
                                    </Descriptions.Item>
                                </Descriptions>
                            );
                        })()}
                    </React.Fragment>
                ) : (
                    <Empty description="No details found or failed to load." />
                )}
            </Modal>

            {/* Credit Note Modal */}
            <Modal
                title="Create Credit Note (Gutschrift)"
                open={creditNoteVisible}
                onCancel={() => { setCreditNoteVisible(false); creditNoteForm.resetFields(); }}
                onOk={handleCreateCreditNote}
                confirmLoading={creditNoteLoading}
                okText="Create Credit Note"
                okButtonProps={{ danger: true }}
            >
                <Alert
                    type="warning"
                    message="This will create a reversal invoice with negative amounts."
                    showIcon
                    style={{ marginBottom: 16 }}
                />
                <Form form={creditNoteForm} layout="vertical">
                    <Form.Item
                        name="reasonCode"
                        label="Reason Code"
                        rules={[{ required: true, message: 'Please select a reason code' }]}
                    >
                        <Select placeholder="Select reason...">
                            <Select.Option value="RETURN">Return / Retoure</Select.Option>
                            <Select.Option value="ERROR">Billing Error / Fehler</Select.Option>
                            <Select.Option value="DISCOUNT">Discount / Rabatt</Select.Option>
                            <Select.Option value="CANCEL">Full Cancellation / Storno</Select.Option>
                            <Select.Option value="OTHER">Other / Sonstiges</Select.Option>
                        </Select>
                    </Form.Item>
                    <Form.Item
                        name="reasonText"
                        label="Reason Description"
                        rules={[{ required: true, message: 'Please describe the reason' }]}
                    >
                        <Input.TextArea rows={3} placeholder="Describe the reason for the credit note..." />
                    </Form.Item>
                </Form>
            </Modal>
        </React.Fragment>
    );
};

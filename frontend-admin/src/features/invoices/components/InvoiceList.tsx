'use client';

import React, { useState, useMemo } from 'react';
import { Table, Button, Input, Select, DatePicker, Space, Tag, Card, Row, Col, message, Tooltip, Modal, Descriptions, Alert, Empty, Form, Checkbox, Typography, Divider, Collapse } from 'antd';
import { SearchOutlined, DownloadOutlined, ReloadOutlined, EyeOutlined, PrinterOutlined, CloudUploadOutlined, RollbackOutlined } from '@ant-design/icons';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import dayjs from 'dayjs';
import Link from 'next/link';
import type { TablePaginationConfig, TableProps } from 'antd/es/table';
import type { SorterResult } from 'antd/es/table/interface';
import { useDebounce } from '@/hooks/useDebounce';
import { normalizeFromDate, normalizeToDate, validateDateRange } from '../utils/dateUtils';

// Orval-generated hooks and types
import {
    useGetApiInvoiceId,
    getGetApiInvoiceIdQueryKey,
    getApiInvoiceId,
} from '@/api/generated/invoice/invoice';
import { postApiAdminFinanzonlineReconciliationRetryPaymentId } from '@/api/generated/admin/admin';
import type { Invoice, InvoiceListItemDto, InvoiceListItemDtoPagedResult, PaymentMethod } from '@/api/generated/model';
import { DocumentType, InvoiceStatus } from '@/api/generated/model';
import { rksvAdminQueryKeys } from '@/api/admin-rksv/query-keys';

import { getInvoicesList, getInvoicePdf, createCreditNote, exportInvoices as orvalExportInvoices } from '../api/invoiceService';
import { coerceInvoiceListSortField, type InvoiceListParams, type InvoiceListSortBy } from '../types';
import { normalizeInvoiceItemsForDisplay } from '@/shared/contract/invoiceInvoiceItemsDisplay';
import {
    getAxiosResponseDataString,
    getAxiosResponseStatus,
    isAntdFormValidateError,
} from '@/shared/contract/httpErrorShape';
import {
    analyzeRegisterFkField,
    formatRegisterDisplayLabel,
    parseAuthoritativeRegisterGuid,
} from '@/shared/utils/registerIdentity';
import {
    buildFinanzOnlineQueueInvestigationHref,
    buildIncidentInvestigationHref,
    buildReplayBatchDetailHref,
} from '@/shared/investigationNavigation';
import { RKSv_ADMIN_CONTRACT_GAPS, viewInvoiceListRegister } from '@/shared/rksvAdminTruth';
import { AdminTruthBadge } from '@/shared/adminTruthBadges';
import {
    OperatorBusinessSection,
    OperatorSummaryStrip,
    OperatorTechnicalSection,
} from '@/shared/operatorTriageLayout';
import {
    OPERATOR_INVOICE_COPY,
    OPERATOR_LINK_LABELS,
    OPERATOR_REGISTER_LINK_COPY,
    OPERATOR_SHARED_COPY,
} from '@/shared/operatorTruthCopy';

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

/** Display-only: does not coerce API values into fake DTOs. */
function displayScalar(v: unknown): string {
    if (v === null || v === undefined || v === '') {
        return '-';
    }
    if (typeof v === 'string' || typeof v === 'number' || typeof v === 'boolean') {
        return String(v);
    }
    return '-';
}

function escapeCsvScalar(v: unknown): string {
    if (v === null || v === undefined || v === '') {
        return '';
    }
    return `"${String(v).replace(/"/g, '""')}"`;
}

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
    const [invalidRegisterOnly, setInvalidRegisterOnly] = useState(false);
    const [sortField, setSortField] = useState<InvoiceListSortBy>('invoiceDate');
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

    const registerListFilterAnalysis = useMemo(
        () => analyzeRegisterFkField(cashRegisterIdFilter),
        [cashRegisterIdFilter],
    );

    // Query Params — manual type matching POS-list endpoint
    const queryParams: InvoiceListParams = useMemo(() => ({
        page: pagination.current,
        pageSize: pagination.pageSize,
        query: debouncedSearch || undefined,
        status: statusFilter,
        from: dateRange?.[0] ? normalizeFromDate(dateRange[0]) : undefined,
        to: dateRange?.[1] ? normalizeToDate(dateRange[1]) : undefined,
        sortBy: sortField,
        sortDir: sortOrder,
        cashRegisterId: cashRegisterIdFilter,
    }), [pagination.current, pagination.pageSize, debouncedSearch, statusFilter, dateRange, sortField, sortOrder, cashRegisterIdFilter]);

    const { data, isLoading, isFetching, isError, error: listQueryError, refetch } = useQuery<
        InvoiceListItemDtoPagedResult,
        Error
    >({
        queryKey: ['invoices', queryParams],
        queryFn: () => getInvoicesList(queryParams),
        placeholderData: (previousData) => previousData,
        enabled: !dateRangeError,
    });

    const displayedItems = useMemo(() => {
        const items = data?.items || [];
        if (invalidRegisterOnly) {
            return items.filter((item) => !viewInvoiceListRegister(item).finanzQueueRegisterRowId);
        }
        return items;
    }, [data?.items, invalidRegisterOnly]);

    // Detail Fetching
    const { data: detailInvoice, isLoading: detailLoading } = useGetApiInvoiceId(
        selectedInvoiceId || '',
        {
            query: {
                enabled: !!selectedInvoiceId && detailVisible,
            },
        }
    );

    const invalidateReconciliationViews = async () => {
        await queryClient.invalidateQueries({ queryKey: rksvAdminQueryKeys.finanzOnline.base });
    };

    const openReconciliationHandoffModal = (args: {
        title: string;
        messageText: string;
        submissionId?: string | null;
        submittedAt?: string | null;
        cashRegisterId?: string;
        focusPaymentId?: string | null;
        investigationBatchCorrelationId?: string | null;
        fromUtc?: string;
        toUtc?: string;
        footerHint?: string;
    }) => {
        const link = buildFinanzOnlineQueueInvestigationHref({
            registerRowId: args.cashRegisterId,
            focusPaymentId: args.focusPaymentId,
            investigationBatchCorrelationId: args.investigationBatchCorrelationId,
            fromUtc: args.fromUtc,
            toUtc: args.toUtc,
        });
        Modal.success({
            title: args.title,
            okText: 'Zur Abgleichsseite',
            onOk: () => {
                window.open(link, '_blank', 'noopener,noreferrer');
            },
            content: (
                <Space direction="vertical" size={8}>
                    <Typography.Text>{args.messageText}</Typography.Text>
                    {args.submissionId ? (
                        <Typography.Text copyable={{ text: args.submissionId }}>
                            SubmissionId: {args.submissionId}
                        </Typography.Text>
                    ) : null}
                    {args.submittedAt ? (
                        <Typography.Text>
                            SubmittedAt: {dayjs(args.submittedAt).isValid() ? dayjs(args.submittedAt).format('DD.MM.YYYY HH:mm:ss') : args.submittedAt}
                        </Typography.Text>
                    ) : null}
                    <Typography.Text type="secondary">
                        {args.footerHint || OPERATOR_INVOICE_COPY.reconciliationHandoffFooter}
                    </Typography.Text>
                </Space>
            ),
        });
    };


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
                setSortField(coerceInvoiceListSortField(s.field));
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
        } catch (err: unknown) {
            const status = getAxiosResponseStatus(err);
            if (status === 409) {
                message.warning('A credit note already exists for this invoice.');
            } else if (status === 400) {
                message.error(getAxiosResponseDataString(err) ?? 'Invalid request.');
            } else if (isAntdFormValidateError(err)) {
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
        const lines = [
            'InvoiceNumber;InvoiceDate;CustomerName;CompanyName;TotalAmount;Status;DocumentType;OriginalInvoiceId;KassenIdDisplay;CashRegisterId;TseSignature',
        ];

        for (const key of selectedRowKeys) {
            try {
                const i = await getApiInvoiceId(key.toString());
                lines.push(
                    `${i.invoiceNumber};${dayjs(i.invoiceDate).format('YYYY-MM-DD HH:mm')};${escapeCsvScalar(i.customerName)};${escapeCsvScalar(i.companyName)};${i.totalAmount};${i.status};${i.documentType ?? ''};${i.originalInvoiceId ?? ''};${i.kassenId ?? ''};${i.cashRegisterId ?? ''};${escapeCsvScalar(i.tseSignature)}`
                );
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
            title: 'Confirm Batch Reconciliation Retry',
            content: `Retry FinanzOnline reconciliation for ${selectedRowKeys.length} invoice/payment row(s)?`,
            onOk: async () => {
                setBatchLoading(true);
                let success = 0;
                let fail = 0;
                let skipped = 0;
                let alreadySubmitted = 0;
                const attemptedRows: Array<{ cashRegisterId?: string; invoiceDate?: string }> = [];
                let handoffFocusPaymentId: string | undefined;
                let handoffBatchCorrelation: string | undefined;
                for (const key of selectedRowKeys) {
                    try {
                        const inv = await getApiInvoiceId(key.toString());
                        const paymentId = inv.sourcePaymentId ?? inv.id;
                        if (!paymentId) {
                            skipped++;
                            continue;
                        }
                        const res = await postApiAdminFinanzonlineReconciliationRetryPaymentId(paymentId);
                        if (res.success) {
                            success++;
                            if (res.message === 'Submitted' && res.referenceId) {
                                // Fresh submit with reference id.
                            } else if (!res.referenceId && res.message === 'Submitted') {
                                alreadySubmitted++;
                            }
                            attemptedRows.push({
                                cashRegisterId:
                                    parseAuthoritativeRegisterGuid(inv.cashRegisterId) ?? undefined,
                                invoiceDate: inv.invoiceDate,
                            });
                            if (!handoffFocusPaymentId) {
                                const fp = parseAuthoritativeRegisterGuid(inv.sourcePaymentId ?? inv.id);
                                if (fp) handoffFocusPaymentId = fp;
                            }
                            if (!handoffBatchCorrelation && inv.correlationId?.trim()) {
                                handoffBatchCorrelation = inv.correlationId.trim();
                            }
                        } else {
                            fail++;
                        }
                    } catch {
                        fail++;
                    }
                }
                setBatchLoading(false);
                message.info(`Batch reconciliation: ${success} successful, ${fail} failed, ${skipped} skipped, ${alreadySubmitted} already submitted.`);
                if (success > 0) {
                    const firstRegister = attemptedRows.find((x) => !!x.cashRegisterId)?.cashRegisterId;
                    const dates = attemptedRows
                        .map((x) => x.invoiceDate)
                        .filter((x): x is string => !!x && dayjs(x).isValid());
                    const minDate = dates.length
                        ? dates.map((d) => dayjs(d)).reduce((a, b) => (b.isBefore(a) ? b : a))
                        : dayjs();
                    const maxDate = dates.length
                        ? dates.map((d) => dayjs(d)).reduce((a, b) => (b.isAfter(a) ? b : a))
                        : dayjs();
                    openReconciliationHandoffModal({
                        title: 'Batch-Reconciliation abgeschlossen',
                        messageText: `${success} Rechnung/Payment-Zeilen verarbeitet. Bitte den aktuellen Abgleichsstatus prüfen.`,
                        cashRegisterId: firstRegister,
                        focusPaymentId: handoffFocusPaymentId,
                        investigationBatchCorrelationId: handoffBatchCorrelation,
                        fromUtc: minDate.startOf('day').toISOString(),
                        toUtc: maxDate.endOf('day').toISOString(),
                        footerHint: `Erfolg: ${success}, Fehlgeschlagen: ${fail}, Übersprungen: ${skipped}, Bereits submitted: ${alreadySubmitted}`,
                    });
                }
                setSelectedRowKeys([]);
                refetch();
                void invalidateReconciliationViews();
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
        } catch (err: unknown) {
            const status = getAxiosResponseStatus(err);
            if (status === 401) {
                message.error('Session expired. Please log in again.');
            } else if (status === 404) {
                message.error('Invoice not found or has been deleted.');
            } else {
                message.error('Failed to generate PDF. Please try again.');
            }
        }
    };

    const handleSubmitFinanzOnline = async (invoice: Invoice) => {
        const paymentId = invoice.sourcePaymentId ?? invoice.id;
        if (!paymentId) {
            message.error('Kein verknüpftes Payment für Reconciliation gefunden');
            return;
        }
        try {
            const data = await postApiAdminFinanzonlineReconciliationRetryPaymentId(paymentId);
            const invoiceDate = dayjs(invoice.invoiceDate).isValid() ? dayjs(invoice.invoiceDate) : dayjs();
            if (data.success) {
                const uiMessage = data.referenceId
                    ? 'FinanzOnline-Übermittlung erfolgreich abgeschlossen.'
                    : 'Bereits als Submitted markiert.';
                message.success(uiMessage);
                openReconciliationHandoffModal({
                    title: data.referenceId ? 'Reconciliation erfolgreich' : 'Bereits Submitted',
                    messageText: `${uiMessage} Status jetzt im Abgleich prüfen.`,
                    submissionId: data.referenceId || null,
                    submittedAt: data.submittedAt || null,
                    cashRegisterId: invoice.cashRegisterId,
                    focusPaymentId: invoice.sourcePaymentId ?? invoice.id,
                    investigationBatchCorrelationId: invoice.correlationId ?? undefined,
                    fromUtc: invoiceDate.startOf('day').toISOString(),
                    toUtc: invoiceDate.endOf('day').toISOString(),
                });
            } else {
                message.warning(`Reconciliation fehlgeschlagen: ${data.message}`);
                Modal.warning({
                    title: 'Reconciliation fehlgeschlagen',
                    content: (
                        <Space direction="vertical" size={8}>
                            <Typography.Text>{data.message || 'Unbekannter Fehler beim Retry.'}</Typography.Text>
                            <Typography.Text type="secondary">
                                Öffne den FinanzOnline-Abgleich, um Status/Retry-Zustand zu prüfen.
                            </Typography.Text>
                        </Space>
                    ),
                    okText: 'Zum Abgleich',
                    onOk: () => {
                        const link = buildFinanzOnlineQueueInvestigationHref({
                            registerRowId: invoice.cashRegisterId,
                            focusPaymentId: invoice.sourcePaymentId ?? invoice.id,
                            investigationBatchCorrelationId: invoice.correlationId ?? undefined,
                            fromUtc: invoiceDate.startOf('day').toISOString(),
                            toUtc: invoiceDate.endOf('day').toISOString(),
                        });
                        window.open(link, '_blank', 'noopener,noreferrer');
                    },
                });
            }
            refetch();
            if (selectedInvoiceId) {
                void queryClient.invalidateQueries({ queryKey: getGetApiInvoiceIdQueryKey(selectedInvoiceId) });
            }
            void invalidateReconciliationViews();
        } catch (err: unknown) {
            message.error('Error running reconciliation retry');
            Modal.error({
                title: 'Reconciliation Fehler',
                content: (
                    <Space direction="vertical" size={8}>
                        <Typography.Text>
                            {getAxiosResponseDataString(err) ??
                                'Fehler beim Auslösen des Reconciliation-Retry.'}
                        </Typography.Text>
                        <Typography.Text type="secondary">
                            Du kannst direkt zur Abgleichsansicht wechseln, um den aktuellen Zustand zu prüfen.
                        </Typography.Text>
                    </Space>
                ),
                okText: 'Zum Abgleich',
                onOk: () => {
                    const invoiceDate = dayjs(invoice.invoiceDate).isValid() ? dayjs(invoice.invoiceDate) : dayjs();
                    const link = buildFinanzOnlineQueueInvestigationHref({
                        registerRowId: invoice.cashRegisterId,
                        focusPaymentId: invoice.sourcePaymentId ?? invoice.id,
                        investigationBatchCorrelationId: invoice.correlationId ?? undefined,
                        fromUtc: invoiceDate.startOf('day').toISOString(),
                        toUtc: invoiceDate.endOf('day').toISOString(),
                    });
                    window.open(link, '_blank', 'noopener,noreferrer');
                },
            });
        }
    };

    // Columns
    const columns: TableProps<InvoiceListItemDto>['columns'] = [
        {
            title: 'Invoice #',
            dataIndex: 'invoiceNumber',
            key: 'invoiceNumber',
            sorter: true,
            render: (text, record) => {
                const reg = viewInvoiceListRegister(record);
                const missingFk = !reg.finanzQueueRegisterRowId;
                return (
                    <Space size={4} wrap>
                        <span style={{ fontWeight: 500 }}>{text}</span>
                        {record.documentType === DocumentType.NUMBER_1 && <Tag color="purple" style={{ fontSize: 10 }}>CN</Tag>}
                        {missingFk ? (
                            <AdminTruthBadge kind="link_incomplete" />
                        ) : (
                            <AdminTruthBadge kind="authoritative_api" />
                        )}
                    </Space>
                );
            }
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
            title: 'Kunde (Snapshot)',
            dataIndex: 'customerName',
            key: 'customerName',
            render: (text) => text || '-',
        },
        {
            title: 'Kassen-ID (Anzeige)',
            dataIndex: 'kassenId',
            key: 'kassenId',
            width: 120,
            ellipsis: true,
            render: (text: string | null | undefined, record) => {
                const apiFk = viewInvoiceListRegister(record).apiCashRegisterId;
                return (
                    <Space size={4} wrap align="center">
                        <Tooltip
                            title={
                                apiFk
                                    ? `Register-FK (Rohwert aus API-Liste): ${apiFk}`
                                    : 'Kein cashRegisterId im List-DTO — Spalte zeigt nur Anzeigetext.'
                            }
                        >
                            <span>{text?.trim() || '—'}</span>
                        </Tooltip>
                        <AdminTruthBadge kind="display_only_label" />
                    </Space>
                );
            },
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
            render: (status: InvoiceStatus | undefined) => {
                const code = status ?? InvoiceStatus.NUMBER_0;
                const info = InvoiceStatusMap[code] || { label: 'Unknown', color: 'default' };
                return (
                    <Tag color={info.color}>
                        {info.label}
                    </Tag>
                );
            }
        },
        {
            title: (
                <Tooltip title="Vorschau der TSE-Signatur; vollständiger Wert im Detaildialog.">
                    <span>TSE (Präfix)</span>
                </Tooltip>
            ),
            key: 'tsePrefix',
            width: 100,
            ellipsis: true,
            render: (_: unknown, record: InvoiceListItemDto) => {
                const t = record.tseSignature?.trim();
                if (!t) return <Typography.Text type="secondary">—</Typography.Text>;
                const shortT = t.length > 28 ? `${t.slice(0, 28)}…` : t;
                return (
                    <Tooltip title={t}>
                        <Typography.Text code style={{ fontSize: 10 }} ellipsis>
                            {shortT}
                        </Typography.Text>
                    </Tooltip>
                );
            },
        },
        {
            title: (
                <Tooltip title="originalInvoiceId laut API-Liste (Gutschrift/Storno).">
                    <span>Storno-Ref</span>
                </Tooltip>
            ),
            dataIndex: 'originalInvoiceId',
            key: 'originalInvoiceId',
            width: 108,
            ellipsis: true,
            render: (v: string | null | undefined) =>
                v?.trim() ? (
                    <Typography.Text code copyable ellipsis style={{ maxWidth: 100 }}>
                        {v}
                    </Typography.Text>
                ) : (
                    <Typography.Text type="secondary">—</Typography.Text>
                ),
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
                    {(record.status === InvoiceStatus.NUMBER_2 || record.status === InvoiceStatus.NUMBER_1) &&
                        record.documentType !== DocumentType.NUMBER_1 && (
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

    const statusOptions = (
        [
            InvoiceStatus.NUMBER_0,
            InvoiceStatus.NUMBER_1,
            InvoiceStatus.NUMBER_2,
            InvoiceStatus.NUMBER_3,
            InvoiceStatus.NUMBER_4,
            InvoiceStatus.NUMBER_5,
            InvoiceStatus.NUMBER_6,
            InvoiceStatus.NUMBER_7,
        ] as const
    ).map((value) => ({
        label: InvoiceStatusMap[value]?.label ?? String(value),
        value,
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
                        Batch Reconcile
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
                                placeholder="Register-UUID (cash_registers.Id)"
                                value={cashRegisterIdFilter}
                                onChange={(e) => { setCashRegisterIdFilter(e.target.value || undefined); setPagination(p => ({ ...p, current: 1 })); }}
                                allowClear
                            />
                            <Typography.Text type="secondary" style={{ display: 'block', fontSize: 11, marginTop: 4 }}>
                                {OPERATOR_INVOICE_COPY.registerListFilterApiFootnote}
                            </Typography.Text>
                            <div style={{ marginTop: 8 }}>
                                <Checkbox
                                    checked={invalidRegisterOnly}
                                    onChange={e => setInvalidRegisterOnly(e.target.checked)}
                                >
                                    {OPERATOR_INVOICE_COPY.invalidRegisterOnlyCheckboxLabel}
                                </Checkbox>
                            </div>
                        </Col>
                        <Col xs={24} sm={16} md={7}>
                            <RangePicker
                                style={{ width: '100%' }}
                                onChange={(dates) =>
                                    setDateRange(
                                        dates && dates[0] && dates[1] ? [dates[0], dates[1]] : null
                                    )
                                }
                                status={dateRangeError ? 'error' : undefined}
                            />
                            {dateRangeError && (
                                <div style={{ color: '#ff4d4f', fontSize: 12, marginTop: 4 }}>{dateRangeError}</div>
                            )}
                        </Col>
                        <Col xs={24} sm={8} md={2} style={{ textAlign: 'right' }}>
                            <Tooltip title={OPERATOR_SHARED_COPY.refetchHintToolbar}>
                                <Button icon={<ReloadOutlined />} onClick={() => refetch()} loading={isFetching} />
                            </Tooltip>
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
                                color={registerListFilterAnalysis.linkSafeUuid ? 'geekblue' : 'orange'}
                            >
                                {registerListFilterAnalysis.linkSafeUuid
                                    ? 'Register (UUID): '
                                    : 'Register (API-Rohfilter): '}
                                {cashRegisterIdFilter}
                            </Tag>
                            <Button type="link" size="small" onClick={() => { setCashRegisterIdFilter(undefined); setPagination(p => ({ ...p, current: 1 })); }}>Clear Filter</Button>
                        </div>
                    )}

                    {registerListFilterAnalysis.isRawPresentButNotLinkSafe && (
                        <Alert
                            type="warning"
                            showIcon
                            message={OPERATOR_INVOICE_COPY.registerFilterInvalidTitle}
                            description={OPERATOR_INVOICE_COPY.registerFilterInvalidDescription}
                        />
                    )}

                    {/* Error State */}
                    {isError && (
                        <Alert
                            type="error"
                            message={OPERATOR_SHARED_COPY.loadFailedList}
                            description={
                                listQueryError instanceof Error
                                    ? listQueryError.message
                                    : OPERATOR_SHARED_COPY.unknownErrorDetail
                            }
                            showIcon
                            closable
                            action={
                                <Button size="small" onClick={() => refetch()}>
                                    {OPERATOR_SHARED_COPY.retryLoadShort}
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
                        dataSource={displayedItems}
                        rowKey="id"
                        pagination={{
                            ...pagination,
                            total: data?.totalCount || 0,
                        }}
                        loading={isLoading || isFetching}
                        onChange={handleTableChange}
                        size="middle"
                        scroll={{ x: 1020 }}
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
                            onClick={() => void handleSubmitFinanzOnline(detailInvoice)}
                        >
                            Reconciliation Retry
                        </Button>
                    )
                ]}
                width={880}
            >
                {detailLoading ? (
                    <div style={{ textAlign: 'center', padding: '20px' }}>
                        {OPERATOR_SHARED_COPY.loadingInvoiceDetail}
                    </div>
                ) : detailInvoice ? (
                    <React.Fragment>
                        {(() => {
                            const detailRegFk = analyzeRegisterFkField(detailInvoice.cashRegisterId);
                            const itemsDisplay = normalizeInvoiceItemsForDisplay(detailInvoice.invoiceItems);

                            const detailDate = dayjs(detailInvoice.invoiceDate || detailInvoice.createdAt).isValid()
                                ? dayjs(detailInvoice.invoiceDate || detailInvoice.createdAt).format('DD.MM.YYYY HH:mm')
                                : null;

                            const positionsLabel =
                                itemsDisplay.kind === 'parse_error'
                                    ? 'JSON-Fehler'
                                    : itemsDisplay.kind === 'unsupported_primitive'
                                      ? `Typ ${itemsDisplay.primitive}`
                                      : itemsDisplay.rows.length > 0
                                        ? `${itemsDisplay.rows.length} Zeilen`
                                        : 'leer / Rohpayload';

                            return (
                                <Space direction="vertical" size={0} style={{ width: '100%' }}>
                                    <OperatorSummaryStrip>
                                        <Space wrap size={[16, 12]} align="start">
                                            <div>
                                                <Typography.Text type="secondary" style={{ fontSize: 11, display: 'block' }}>
                                                    Rechnung
                                                </Typography.Text>
                                                <Space size={6}>
                                                    <Typography.Text strong>{displayScalar(detailInvoice.invoiceNumber)}</Typography.Text>
                                                    {detailInvoice.documentType === DocumentType.NUMBER_1 ? (
                                                        <Tag color="purple">Gutschrift</Tag>
                                                    ) : null}
                                                </Space>
                                            </div>
                                            <div>
                                                <Typography.Text type="secondary" style={{ fontSize: 11, display: 'block' }}>
                                                    Status
                                                </Typography.Text>
                                                <Tag color={InvoiceStatusMap[detailInvoice.status]?.color || 'default'}>
                                                    {InvoiceStatusMap[detailInvoice.status]?.label || displayScalar(detailInvoice.status)}
                                                </Tag>
                                            </div>
                                            <div>
                                                <Typography.Text type="secondary" style={{ fontSize: 11, display: 'block' }}>
                                                    Datum
                                                </Typography.Text>
                                                <Typography.Text>{displayScalar(detailDate)}</Typography.Text>
                                            </div>
                                            <div>
                                                <Typography.Text type="secondary" style={{ fontSize: 11, display: 'block' }}>
                                                    Brutto
                                                </Typography.Text>
                                                <Typography.Text strong>
                                                    € {displayScalar((detailInvoice.totalAmount ?? 0).toFixed(2))}
                                                </Typography.Text>
                                            </div>
                                        </Space>
                                        <Divider style={{ margin: '12px 0' }} />
                                        <Space wrap align="center" size={8}>
                                            <Typography.Text type="secondary">
                                                {OPERATOR_INVOICE_COPY.detailRegisterMachineLabel}:
                                            </Typography.Text>
                                            {detailRegFk.linkSafeUuid ? (
                                                <AdminTruthBadge kind="authoritative_api" />
                                            ) : (
                                                <AdminTruthBadge kind="link_incomplete" />
                                            )}
                                            <Typography.Text code copyable style={{ maxWidth: 320 }} ellipsis>
                                                {displayScalar(detailInvoice.cashRegisterId)}
                                            </Typography.Text>
                                            {detailRegFk.linkSafeUuid ? (
                                                <Link
                                                    href={buildFinanzOnlineQueueInvestigationHref({
                                                        registerRowId: detailInvoice.cashRegisterId,
                                                        focusPaymentId: detailInvoice.sourcePaymentId,
                                                        investigationBatchCorrelationId:
                                                            detailInvoice.correlationId ?? undefined,
                                                    })}
                                                    target="_blank"
                                                    rel="noopener noreferrer"
                                                >
                                                    {OPERATOR_INVOICE_COPY.detailFoLinkWithContext}
                                                </Link>
                                            ) : null}
                                        </Space>
                                        <Divider style={{ margin: '12px 0' }} />
                                        <Space direction="vertical" size={6} style={{ width: '100%' }}>
                                            <Space wrap align="center">
                                                <Typography.Text type="secondary">Zahlung (Reconciliation):</Typography.Text>
                                                {detailInvoice.sourcePaymentId ? (
                                                    <>
                                                        <Typography.Text code copyable>
                                                            {detailInvoice.sourcePaymentId}
                                                        </Typography.Text>
                                                        <Link
                                                            href={`/payments?paymentId=${encodeURIComponent(detailInvoice.sourcePaymentId)}`}
                                                            target="_blank"
                                                            rel="noopener noreferrer"
                                                        >
                                                            Öffnen
                                                        </Link>
                                                    </>
                                                ) : (
                                                    <Typography.Text type="secondary">— kein Payment verknüpft</Typography.Text>
                                                )}
                                            </Space>
                                            <Space wrap align="center">
                                                <Typography.Text type="secondary">
                                                    {OPERATOR_INVOICE_COPY.correlationPathsLabel}:
                                                </Typography.Text>
                                                {detailInvoice.correlationId ? (
                                                    <>
                                                        <Typography.Text code copyable>
                                                            {detailInvoice.correlationId}
                                                        </Typography.Text>
                                                        <Link
                                                            href={buildIncidentInvestigationHref(detailInvoice.correlationId)}
                                                            target="_blank"
                                                            rel="noopener noreferrer"
                                                        >
                                                            {OPERATOR_LINK_LABELS.incidentAggregate}
                                                        </Link>
                                                        <Typography.Text type="secondary">·</Typography.Text>
                                                        <Link
                                                            href={buildReplayBatchDetailHref(detailInvoice.correlationId)}
                                                            target="_blank"
                                                            rel="noopener noreferrer"
                                                        >
                                                            {OPERATOR_LINK_LABELS.replayBatchDetail}
                                                        </Link>
                                                    </>
                                                ) : (
                                                    <Typography.Text type="secondary">—</Typography.Text>
                                                )}
                                            </Space>
                                        </Space>
                                        <Typography.Paragraph type="secondary" style={{ marginTop: 12, marginBottom: 0, fontSize: 12 }}>
                                            <strong>Herkunft:</strong> {OPERATOR_INVOICE_COPY.detailProvenanceFooter}
                                        </Typography.Paragraph>
                                        {(detailInvoice.status === InvoiceStatus.NUMBER_1 || detailInvoice.status === InvoiceStatus.NUMBER_2) && (
                                            <Typography.Paragraph type="secondary" style={{ marginTop: 8, marginBottom: 0, fontSize: 12 }}>
                                                <strong>Retry:</strong> FinanzOnline-Reconciliation über Schaltfläche unten im Dialog
                                                (Footer).
                                            </Typography.Paragraph>
                                        )}
                                    </OperatorSummaryStrip>

                                    <OperatorBusinessSection>
                                        <Descriptions bordered column={2} size="small">
                                            <Descriptions.Item label="Kunde (Snapshot)" span={2}>
                                                {displayScalar(detailInvoice.customerName)} <br />
                                                {displayScalar(detailInvoice.customerAddress)} <br />
                                                {displayScalar(detailInvoice.customerTaxNumber)}
                                            </Descriptions.Item>
                                            <Descriptions.Item label="Company" span={2}>
                                                {displayScalar(detailInvoice.companyName)} <br />
                                                {displayScalar(detailInvoice.companyTaxNumber)}
                                            </Descriptions.Item>
                                            <Descriptions.Item label="Total Amount">
                                                € {displayScalar((detailInvoice.totalAmount ?? 0).toFixed(2))}
                                            </Descriptions.Item>
                                            <Descriptions.Item label="Tax Amount">
                                                € {displayScalar((detailInvoice.taxAmount ?? 0).toFixed(2))}
                                            </Descriptions.Item>
                                            <Descriptions.Item label="Register (FK, nur Maschine)" span={2}>
                                                <Space direction="vertical" size={8} style={{ width: '100%' }}>
                                                    {detailRegFk.isRawPresentButNotLinkSafe ? (
                                                        <Alert
                                                            type="warning"
                                                            showIcon
                                                            message={OPERATOR_REGISTER_LINK_COPY.uuidNotLinkSafeTitle}
                                                            description={OPERATOR_REGISTER_LINK_COPY.uuidNotLinkSafeDescription}
                                                        />
                                                    ) : null}
                                                    <Space wrap>
                                                        {detailRegFk.linkSafeUuid ? (
                                                            <AdminTruthBadge kind="authoritative_api" />
                                                        ) : (
                                                            <AdminTruthBadge kind="link_incomplete" />
                                                        )}
                                                    </Space>
                                                    <Typography.Text code copyable>
                                                        {displayScalar(detailInvoice.cashRegisterId)}
                                                    </Typography.Text>
                                                    {detailRegFk.linkSafeUuid ? (
                                                        <div>
                                                            <Link
                                                                href={buildFinanzOnlineQueueInvestigationHref({
                                                                    registerRowId: detailInvoice.cashRegisterId,
                                                                    focusPaymentId: detailInvoice.sourcePaymentId,
                                                                    investigationBatchCorrelationId:
                                                                        detailInvoice.correlationId ?? undefined,
                                                                })}
                                                                target="_blank"
                                                                rel="noopener noreferrer"
                                                            >
                                                                {OPERATOR_INVOICE_COPY.detailFoLinkRegisterOnly}
                                                            </Link>
                                                        </div>
                                                    ) : (
                                                        <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                                                            {detailRegFk.rawTrimmed
                                                                ? OPERATOR_REGISTER_LINK_COPY.noMachineUuidHint
                                                                : OPERATOR_REGISTER_LINK_COPY.missingRegisterFkInApiHint}
                                                        </Typography.Text>
                                                    )}
                                                </Space>
                                            </Descriptions.Item>
                                            <Descriptions.Item label="Kassen-ID / Nummer (Anzeige)" span={2}>
                                                <Space wrap align="center">
                                                    <AdminTruthBadge kind="display_only_label" />
                                                    <span>{formatRegisterDisplayLabel(detailInvoice.kassenId)}</span>
                                                </Space>
                                            </Descriptions.Item>
                                            <Descriptions.Item label="Payment Method" span={2}>
                                                {displayScalar(getPaymentMethodLabel(detailInvoice.paymentMethod))}
                                            </Descriptions.Item>
                                        </Descriptions>
                                    </OperatorBusinessSection>

                                    <OperatorTechnicalSection>
                                        <Collapse
                                            bordered={false}
                                            items={[
                                                {
                                                    key: 'tse',
                                                    label: 'TSE-Signatur (Rohfeld)',
                                                    children: (
                                                        <Typography.Paragraph
                                                            code
                                                            copyable
                                                            style={{
                                                                wordBreak: 'break-all',
                                                                fontFamily: 'monospace',
                                                                fontSize: 11,
                                                                marginBottom: 0,
                                                            }}
                                                        >
                                                            {displayScalar(detailInvoice.tseSignature)}
                                                        </Typography.Paragraph>
                                                    ),
                                                },
                                                {
                                                    key: 'items',
                                                    label: `Positionen / Artikel (OpenAPI: unknown) — ${positionsLabel}`,
                                                    children: (
                                                        <Space direction="vertical" size={8} style={{ width: '100%' }}>
                                                            <Alert
                                                                type="info"
                                                                showIcon
                                                                message={OPERATOR_INVOICE_COPY.contractInvoiceItemsTitle}
                                                                description={RKSv_ADMIN_CONTRACT_GAPS.invoiceDetailInvoiceItems}
                                                            />
                                                            {itemsDisplay.kind === 'parse_error' ? (
                                                                <Alert
                                                                    type="warning"
                                                                    showIcon
                                                                    message="invoiceItems konnte nicht als JSON gelesen werden"
                                                                    description={itemsDisplay.message}
                                                                />
                                                            ) : null}
                                                            {itemsDisplay.kind === 'unsupported_primitive' ? (
                                                                <Alert
                                                                    type="warning"
                                                                    showIcon
                                                                    message="Unerwarteter Laufzeit-Typ"
                                                                    description={`Erhalten: ${itemsDisplay.primitive}`}
                                                                />
                                                            ) : null}
                                                            {itemsDisplay.kind === 'rows' && itemsDisplay.rows.length > 0 ? (
                                                                <pre
                                                                    style={{
                                                                        maxHeight: 240,
                                                                        overflow: 'auto',
                                                                        fontSize: 11,
                                                                        margin: 0,
                                                                        background: '#f5f5f5',
                                                                        padding: 12,
                                                                    }}
                                                                >
                                                                    {JSON.stringify(itemsDisplay.rows, null, 2)}
                                                                </pre>
                                                            ) : itemsDisplay.kind === 'rows' ? (
                                                                <Typography.Text type="secondary">
                                                                    Keine Positionsdaten (leer oder null laut Normalisierung).
                                                                </Typography.Text>
                                                            ) : null}
                                                        </Space>
                                                    ),
                                                },
                                            ]}
                                        />
                                    </OperatorTechnicalSection>
                                </Space>
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

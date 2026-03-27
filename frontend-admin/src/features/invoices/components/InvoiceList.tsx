'use client';

import React, { useState, useMemo } from 'react';
import { Table, Button, Input, Select, DatePicker, Space, Tag, Row, Col, message, Tooltip, Modal, Descriptions, Alert, Empty, Form, Checkbox, Typography, Divider, Collapse, theme } from 'antd';
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
import { invoiceProvenanceUiFacet, registerDeepLinkEligibleBadgeKind } from '@/shared/adminTruthFacets';
import {
    getAxiosResponseDataString,
    getAxiosResponseStatus,
    isAntdFormValidateError,
} from '@/shared/contract/httpErrorShape';
import {
    analyzeRegisterFkField,
    formatRegisterDisplayLabel,
    parseAuthoritativeRegisterGuid,
    toLinkSafeRegisterRowId,
} from '@/shared/utils/registerIdentity';
import {
    buildFinanzOnlineQueueInvestigationHref,
    buildIncidentInvestigationHref,
    buildReplayBatchDetailHref,
} from '@/shared/investigationNavigation';
import { RKSv_ADMIN_CONTRACT_GAPS, viewInvoiceListRegister } from '@/shared/rksvAdminTruth';
import { technicalConsole } from '@/shared/dev/technicalConsole';
import { AdminTruthBadge } from '@/shared/adminTruthBadges';
import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { ADMIN_OVERVIEW_CRUMB } from '@/shared/adminShellLabels';
import {
    OperatorBusinessSection,
    OperatorSummaryStrip,
    OperatorTechnicalSection,
} from '@/shared/operatorTriageLayout';
import {
    OPERATOR_FO_OPERATIONS_PAGE_COPY,
    OPERATOR_FO_QUEUE_COPY,
    OPERATOR_INVOICE_COPY,
    OPERATOR_LINK_LABELS,
    OPERATOR_REGISTER_LINK_COPY,
    OPERATOR_SHARED_COPY,
} from '@/shared/operatorTruthCopy';
import { useI18n } from '@/i18n';
import { createIntlFormatters } from '@/i18n/formatting';

const { RangePicker } = DatePicker;

// Manual mapping because Orval generated NUMBER_0 etc. for enum (labels: de-DE operator surface)
const InvoiceStatusMap: Record<number, { label: string; color: string }> = {
    0: { label: OPERATOR_INVOICE_COPY.invoiceStatusDraft, color: 'default' },
    1: { label: OPERATOR_INVOICE_COPY.invoiceStatusSent, color: 'processing' },
    2: { label: OPERATOR_INVOICE_COPY.invoiceStatusPaid, color: 'success' },
    3: { label: OPERATOR_INVOICE_COPY.invoiceStatusPartiallyPaid, color: 'warning' },
    4: { label: OPERATOR_INVOICE_COPY.invoiceStatusUnpaid, color: 'error' },
    5: { label: OPERATOR_INVOICE_COPY.invoiceStatusOverdue, color: 'error' },
    6: { label: OPERATOR_INVOICE_COPY.invoiceStatusCancelled, color: 'default' },
    7: { label: OPERATOR_INVOICE_COPY.invoiceStatusCreditNote, color: 'purple' },
};

const getPaymentMethodLabel = (method?: PaymentMethod) => {
    switch (method) {
        case 0: return OPERATOR_INVOICE_COPY.paymentMethodBar;
        case 1: return OPERATOR_INVOICE_COPY.paymentMethodCard;
        case 2: return OPERATOR_INVOICE_COPY.paymentMethodTransfer;
        case 3: return OPERATOR_INVOICE_COPY.paymentMethodCheck;
        case 4: return OPERATOR_INVOICE_COPY.paymentMethodVoucher;
        case 5: return OPERATOR_INVOICE_COPY.paymentMethodMobile;
        default: return '—';
    }
};

/** User-facing list error line: API message when short/safe; else HTTP status or generic German copy (no raw stack traces). */
function invoiceListErrorUserMessage(err: unknown): string {
    const api = getAxiosResponseDataString(err)?.trim();
    if (api && api.length > 0 && api.length < 400) {
        return api;
    }
    const status = getAxiosResponseStatus(err);
    if (status === 401) return OPERATOR_INVOICE_COPY.listErrorUnauthorized;
    if (status === 403) return OPERATOR_INVOICE_COPY.listErrorForbidden;
    if (status === 404) return OPERATOR_INVOICE_COPY.listErrorNotFound;
    if (status != null && status >= 500) return OPERATOR_INVOICE_COPY.listErrorServer;
    if (err instanceof Error && /network|failed to fetch|load failed|econnrefused|timeout/i.test(err.message)) {
        return OPERATOR_INVOICE_COPY.listErrorNetwork;
    }
    return OPERATOR_INVOICE_COPY.listErrorGeneric;
}

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
    const { token } = theme.useToken();
    const queryClient = useQueryClient();
    const { formatLocale } = useI18n();
    const fmt = useMemo(() => createIntlFormatters(formatLocale), [formatLocale]);

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

    const isInitialListLoading = isLoading && !data;
    const isListRefreshing = isFetching && !isInitialListLoading;

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

    /** `cashRegisterId`: raw API register FK from Invoice (may be non-UUID); only link-safe subset is written to the URL. */
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
        const registerForUrl = toLinkSafeRegisterRowId(args.cashRegisterId);
        const registerRawTrimmed = args.cashRegisterId?.trim();
        const registerFilterOmitted = Boolean(registerRawTrimmed) && !registerForUrl;
        const link = buildFinanzOnlineQueueInvestigationHref({
            registerRowId: registerForUrl,
            focusPaymentId: args.focusPaymentId,
            investigationBatchCorrelationId: args.investigationBatchCorrelationId,
            fromUtc: args.fromUtc,
            toUtc: args.toUtc,
        });
        Modal.success({
            title: args.title,
            okText: OPERATOR_INVOICE_COPY.modalOpenFinanzOnlineQueue,
            onOk: () => {
                window.open(link, '_blank', 'noopener,noreferrer');
            },
            content: (
                <Space direction="vertical" size={8}>
                    <Typography.Text>{args.messageText}</Typography.Text>
                    {args.submissionId ? (
                        <Typography.Text copyable={{ text: args.submissionId }}>
                            {OPERATOR_INVOICE_COPY.handoffLabelSubmissionId}: {args.submissionId}
                        </Typography.Text>
                    ) : null}
                    {args.submittedAt ? (
                        <Typography.Text>
                            {OPERATOR_INVOICE_COPY.handoffLabelSubmittedAt}:{' '}
                            {dayjs(args.submittedAt).isValid() ? dayjs(args.submittedAt).format('DD.MM.YYYY HH:mm:ss') : args.submittedAt}
                        </Typography.Text>
                    ) : null}
                    {registerFilterOmitted ? (
                        <Typography.Paragraph type="warning" style={{ marginBottom: 0, fontSize: 12 }}>
                            {OPERATOR_INVOICE_COPY.reconciliationHandoffRegisterFilterOmitted}
                        </Typography.Paragraph>
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
            message.success(OPERATOR_INVOICE_COPY.toastCreditNoteCreated);
            setCreditNoteVisible(false);
            creditNoteForm.resetFields();
            refetch();
        } catch (err: unknown) {
            const status = getAxiosResponseStatus(err);
            if (status === 409) {
                message.warning(OPERATOR_INVOICE_COPY.toastCreditNoteExists);
            } else if (status === 400) {
                message.error(getAxiosResponseDataString(err) ?? OPERATOR_INVOICE_COPY.toastCreditNoteBadRequest);
            } else if (isAntdFormValidateError(err)) {
                // form validation error — ignore, form shows inline
            } else {
                message.error(OPERATOR_INVOICE_COPY.toastCreditNoteFailed);
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
                link.setAttribute('download', `Rechnung_${key}.pdf`);
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
        message.success(OPERATOR_INVOICE_COPY.toastBatchPrint(success, fail));
        setSelectedRowKeys([]);
    };

    const handleBatchExport = async () => {
        if (!selectedRowKeys.length) return;
        setBatchLoading(true);
        let success = 0;
        let fail = 0;
        const lines: string[] = [OPERATOR_INVOICE_COPY.csvExportHeaderRow];

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
            link.setAttribute('download', `Stapel_Export_${dayjs().format('YYYYMMDD_HHmm')}.csv`);
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
            title: OPERATOR_INVOICE_COPY.batchReconcileModalTitle,
            content: OPERATOR_INVOICE_COPY.batchReconcileModalBody(selectedRowKeys.length),
            okText: OPERATOR_INVOICE_COPY.batchReconcileModalOk,
            cancelText: OPERATOR_INVOICE_COPY.batchReconcileModalCancel,
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
                message.info(OPERATOR_INVOICE_COPY.toastBatchReconcileSummary(success, fail, skipped, alreadySubmitted));
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
                        title: OPERATOR_INVOICE_COPY.batchReconcileFinishedTitle,
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
            link.setAttribute('download', `Rechnungen_${dayjs().format('YYYYMMDD_HHmm')}.csv`);
            document.body.appendChild(link);
            link.click();
            link.remove();
            window.URL.revokeObjectURL(url);
            message.success(OPERATOR_INVOICE_COPY.toastExportCsvOk);
        } catch (error) {
            technicalConsole.error('[InvoiceList] CSV export failed', error);
            message.error(OPERATOR_INVOICE_COPY.toastExportCsvFailed);
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
                message.error(OPERATOR_INVOICE_COPY.toastPdfSessionExpired);
            } else if (status === 404) {
                message.error(OPERATOR_INVOICE_COPY.toastPdfNotFound);
            } else {
                message.error(OPERATOR_INVOICE_COPY.toastPdfFailed);
            }
        }
    };

    const handleSubmitFinanzOnline = async (invoice: Invoice) => {
        const paymentId = invoice.sourcePaymentId ?? invoice.id;
        if (!paymentId) {
            message.error(OPERATOR_INVOICE_COPY.toastNoPaymentLinkedForReconcile);
            return;
        }
        try {
            const data = await postApiAdminFinanzonlineReconciliationRetryPaymentId(paymentId);
            const invoiceDate = dayjs(invoice.invoiceDate).isValid() ? dayjs(invoice.invoiceDate) : dayjs();
            if (data.success) {
                const uiMessage = data.referenceId
                    ? OPERATOR_INVOICE_COPY.finanzOnlineToastSubmitOk
                    : OPERATOR_INVOICE_COPY.finanzOnlineToastAlreadySubmitted;
                message.success(uiMessage);
                openReconciliationHandoffModal({
                    title: data.referenceId
                        ? OPERATOR_INVOICE_COPY.modalReconciliationSuccessTitle
                        : OPERATOR_INVOICE_COPY.modalAlreadySubmittedTitle,
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
                message.warning(`${OPERATOR_INVOICE_COPY.modalReconciliationFailedTitle}: ${data.message}`);
                Modal.warning({
                    title: OPERATOR_INVOICE_COPY.modalReconciliationFailedTitle,
                    content: (
                        <Space direction="vertical" size={8}>
                            <Typography.Text>{data.message || 'Unbekannter Fehler beim Retry.'}</Typography.Text>
                            <Typography.Text type="secondary">
                                Öffne den FinanzOnline-Abgleich, um Status/Retry-Zustand zu prüfen.
                            </Typography.Text>
                        </Space>
                    ),
                    okText: OPERATOR_INVOICE_COPY.modalOpenFinanzOnlineQueue,
                    onOk: () => {
                        const link = buildFinanzOnlineQueueInvestigationHref({
                            registerRowId: toLinkSafeRegisterRowId(invoice.cashRegisterId),
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
            message.error(OPERATOR_INVOICE_COPY.toastReconciliationRetryTriggerFailed);
            Modal.error({
                title: OPERATOR_INVOICE_COPY.modalReconciliationErrorTitle,
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
                okText: OPERATOR_INVOICE_COPY.modalOpenFinanzOnlineQueue,
                onOk: () => {
                    const invoiceDate = dayjs(invoice.invoiceDate).isValid() ? dayjs(invoice.invoiceDate) : dayjs();
                    const link = buildFinanzOnlineQueueInvestigationHref({
                        registerRowId: toLinkSafeRegisterRowId(invoice.cashRegisterId),
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

    const clearAllFilters = () => {
        setSearchText('');
        setStatusFilter(undefined);
        setDateRange(null);
        setCashRegisterIdFilter(undefined);
        setInvalidRegisterOnly(false);
        setPagination((p) => ({ ...p, current: 1 }));
    };

    const hasActiveFilters =
        !!(debouncedSearch && debouncedSearch.trim()) ||
        statusFilter !== undefined ||
        !!(dateRange?.[0] && dateRange?.[1]) ||
        !!(cashRegisterIdFilter && cashRegisterIdFilter.trim()) ||
        invalidRegisterOnly;

    // Columns: business-first order; technical columns de-emphasized; Storno-Ref → detail view only
    const columns: TableProps<InvoiceListItemDto>['columns'] = [
        {
            title: OPERATOR_INVOICE_COPY.listColumnInvoiceNumber,
            dataIndex: 'invoiceNumber',
            key: 'invoiceNumber',
            sorter: true,
            fixed: 'left',
            width: 200,
            render: (text, record) => {
                const reg = viewInvoiceListRegister(record);
                return (
                    <Space size={4} wrap>
                        <span style={{ fontWeight: 600 }}>{text}</span>
                        {record.documentType === DocumentType.NUMBER_1 && (
                            <Tag color="purple" style={{ fontSize: 10 }}>{OPERATOR_INVOICE_COPY.creditNoteTagShort}</Tag>
                        )}
                        <AdminTruthBadge
                            kind={registerDeepLinkEligibleBadgeKind({
                                linkSafeUuid: reg.finanzQueueRegisterRowId,
                            })}
                        />
                    </Space>
                );
            },
        },
        {
            title: OPERATOR_INVOICE_COPY.listColumnDate,
            dataIndex: 'invoiceDate',
            key: 'invoiceDate',
            sorter: true,
            width: 132,
            render: (date) => dayjs(date).format('DD.MM.YYYY HH:mm'),
        },
        {
            title: OPERATOR_INVOICE_COPY.listColumnCustomer,
            dataIndex: 'customerName',
            key: 'customerName',
            width: 168,
            ellipsis: { showTitle: true },
            render: (text) => text || '—',
        },
        {
            title: OPERATOR_INVOICE_COPY.listColumnTotal,
            dataIndex: 'totalAmount',
            key: 'totalAmount',
            sorter: true,
            width: 104,
            align: 'right',
            render: (amount) => fmt.formatCurrency(Number(amount ?? 0)),
        },
        {
            title: OPERATOR_INVOICE_COPY.listColumnStatus,
            dataIndex: 'status',
            key: 'status',
            sorter: true,
            width: 112,
            render: (status: InvoiceStatus | undefined) => {
                const code = status ?? InvoiceStatus.NUMBER_0;
                const info = InvoiceStatusMap[code] || { label: OPERATOR_INVOICE_COPY.invoiceStatusUnknown, color: 'default' };
                return <Tag color={info.color}>{info.label}</Tag>;
            },
        },
        {
            title: (
                <Tooltip title="Anzeige-Kassen-ID; Register-FK (Rohwert) im Tooltip der Zelle.">
                    <Typography.Text type="secondary">{OPERATOR_INVOICE_COPY.listColumnKassenShort}</Typography.Text>
                </Tooltip>
            ),
            dataIndex: 'kassenId',
            key: 'kassenId',
            width: 116,
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
                            <Typography.Text type="secondary">{text?.trim() || '—'}</Typography.Text>
                        </Tooltip>
                        <AdminTruthBadge kind="display_only_label" />
                    </Space>
                );
            },
        },
        {
            title: (
                <Tooltip title="Vorschau der TSE-Signatur; vollständiger Wert im Detaildialog.">
                    <Typography.Text type="secondary">{OPERATOR_INVOICE_COPY.listColumnTseShort}</Typography.Text>
                </Tooltip>
            ),
            key: 'tsePrefix',
            width: 92,
            ellipsis: true,
            render: (_: unknown, record: InvoiceListItemDto) => {
                const t = record.tseSignature?.trim();
                if (!t) return <Typography.Text type="secondary">—</Typography.Text>;
                const shortT = t.length > 22 ? `${t.slice(0, 22)}…` : t;
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
            title: OPERATOR_INVOICE_COPY.listColumnActions,
            key: 'actions',
            width: 268,
            fixed: 'right',
            render: (_, record) => (
                <Space size={4} wrap align="center">
                    <Tooltip title={OPERATOR_INVOICE_COPY.rowActionDetailExtendedTooltip}>
                        <Button
                            type="link"
                            size="small"
                            style={{ paddingInline: 0 }}
                            aria-label={OPERATOR_INVOICE_COPY.rowActionDetailExtendedTooltip}
                            onClick={() => {
                                setSelectedInvoiceId(record.id ?? null);
                                setDetailVisible(true);
                            }}
                        >
                            {OPERATOR_INVOICE_COPY.rowActionDetailTooltip}
                        </Button>
                    </Tooltip>
                    <Tooltip title={OPERATOR_INVOICE_COPY.rowActionPrintTooltip}>
                        <Button
                            icon={<PrinterOutlined />}
                            size="small"
                            aria-label={OPERATOR_INVOICE_COPY.rowActionPrintTooltip}
                            onClick={() => handlePrint(record.id ?? '')}
                        >
                            {OPERATOR_INVOICE_COPY.rowActionPrintCompact}
                        </Button>
                    </Tooltip>
                    {(record.status === InvoiceStatus.NUMBER_2 || record.status === InvoiceStatus.NUMBER_1) &&
                        record.documentType !== DocumentType.NUMBER_1 && (
                        <Tooltip title={OPERATOR_INVOICE_COPY.rowActionCreditNoteTooltip}>
                            <Button
                                icon={<RollbackOutlined />}
                                size="small"
                                danger
                                aria-label={OPERATOR_INVOICE_COPY.rowActionCreditNoteTooltip}
                                onClick={() => {
                                    setCreditNoteTargetId(record.id ?? null);
                                    setCreditNoteVisible(true);
                                }}
                            >
                                {OPERATOR_INVOICE_COPY.rowActionCreditCompact}
                            </Button>
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
    const displayInvoiceNumber =
        detailInvoice?.invoiceNumber || selectedRow?.invoiceNumber || selectedInvoiceId || OPERATOR_INVOICE_COPY.displayUnknownInvoice;

    const tableEmptyText =
        isInitialListLoading ? undefined : dateRangeError ? (
            <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description={OPERATOR_INVOICE_COPY.emptyListInvalidDateRange}>
                <Typography.Paragraph type="secondary" style={{ fontSize: 12, marginTop: 8, marginBottom: 0 }}>
                    {dateRangeError}
                </Typography.Paragraph>
                <Typography.Paragraph type="secondary" style={{ fontSize: 12, marginTop: 8, marginBottom: 0 }}>
                    {OPERATOR_INVOICE_COPY.dateRangeBlocksQuerySuffix}
                </Typography.Paragraph>
            </Empty>
        ) : isError && !data ? (
            <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description={OPERATOR_INVOICE_COPY.emptyListLoadFailedTitle}>
                <Typography.Paragraph type="secondary" style={{ fontSize: 12, marginTop: 8 }}>
                    {invoiceListErrorUserMessage(listQueryError)}
                </Typography.Paragraph>
                <Space wrap size={[12, 12]} style={{ marginTop: 12 }}>
                    <Button type="primary" onClick={() => refetch()}>
                        {OPERATOR_SHARED_COPY.retryLoadShort}
                    </Button>
                    <Button onClick={clearAllFilters}>{OPERATOR_INVOICE_COPY.clearAllFilters}</Button>
                </Space>
                <Typography.Paragraph type="secondary" style={{ fontSize: 12, marginTop: 12, marginBottom: 0 }}>
                    {OPERATOR_INVOICE_COPY.emptyListLoadFailedHint}
                </Typography.Paragraph>
            </Empty>
        ) : (
            <Empty
                image={Empty.PRESENTED_IMAGE_SIMPLE}
                description={
                    dateRange?.[0] && dateRange?.[1]
                        ? OPERATOR_INVOICE_COPY.emptyListDateRange
                        : OPERATOR_INVOICE_COPY.emptyListDefault
                }
            >
                <Space wrap size={[12, 12]} style={{ marginTop: 12 }}>
                    <Button type="primary" onClick={() => refetch()}>
                        {OPERATOR_SHARED_COPY.retryLoadShort}
                    </Button>
                    <Button onClick={clearAllFilters}>{OPERATOR_INVOICE_COPY.clearAllFilters}</Button>
                </Space>
                <Typography.Paragraph type="secondary" style={{ fontSize: 12, marginBottom: 0, marginTop: 8 }}>
                    {OPERATOR_INVOICE_COPY.emptyListMoreHint}
                </Typography.Paragraph>
            </Empty>
        );

    return (
        <React.Fragment>
            <Space direction="vertical" size="large" style={{ width: '100%' }}>
                <AdminPageHeader
                    title={OPERATOR_INVOICE_COPY.pageTitle}
                    breadcrumbs={[ADMIN_OVERVIEW_CRUMB, { title: OPERATOR_INVOICE_COPY.pageTitle }]}
                    actions={
                        <Space wrap>
                            <Button
                                disabled={!selectedRowKeys.length}
                                icon={<PrinterOutlined />}
                                onClick={handleBatchPrint}
                                loading={batchLoading}
                            >
                                {OPERATOR_INVOICE_COPY.actionBatchPrint}
                            </Button>
                            <Button
                                disabled={!selectedRowKeys.length}
                                icon={<DownloadOutlined />}
                                onClick={handleBatchExport}
                                loading={batchLoading}
                            >
                                {OPERATOR_INVOICE_COPY.actionBatchExport}
                            </Button>
                            <Button
                                disabled={!selectedRowKeys.length}
                                icon={<CloudUploadOutlined />}
                                onClick={handleBatchSubmit}
                                loading={batchLoading}
                            >
                                {OPERATOR_INVOICE_COPY.actionBatchReconcile}
                            </Button>
                            <Button
                                type="primary"
                                icon={<DownloadOutlined />}
                                onClick={handleExport}
                                loading={exportLoading}
                            >
                                {OPERATOR_INVOICE_COPY.actionExportCsvAll}
                            </Button>
                        </Space>
                    }
                >
                    <Typography.Paragraph type="secondary" style={{ marginBottom: 8 }}>
                        {OPERATOR_INVOICE_COPY.listPageLead}
                    </Typography.Paragraph>
                    <Typography.Paragraph type="secondary" style={{ marginBottom: 0 }}>
                        {OPERATOR_FO_QUEUE_COPY.relatedSupportingLabel}:{' '}
                        <Link href="/rksv/finanz-online-queue">{OPERATOR_FO_OPERATIONS_PAGE_COPY.introAbgleichLinkLabel}</Link>
                        {' · '}
                        <Link href="/rksv/incident">{OPERATOR_LINK_LABELS.incidentAggregate}</Link>
                        {' · '}
                        <Link href="/rksv/replay-batch">{OPERATOR_LINK_LABELS.replayBatch}</Link>
                    </Typography.Paragraph>
                </AdminPageHeader>
                <Divider style={{ margin: '4px 0 4px' }} />
                <Space direction="vertical" style={{ width: '100%' }} size="middle">
                    {/* Filters */}
                    <Row gutter={[16, 16]}>
                        <Col xs={24} sm={8} md={6}>
                            <Input
                                placeholder={OPERATOR_INVOICE_COPY.listSearchPlaceholder}
                                prefix={<SearchOutlined />}
                                value={searchText}
                                onChange={(e) => setSearchText(e.target.value)}
                                allowClear
                            />
                        </Col>
                        <Col xs={24} sm={8} md={4}>
                            <Select
                                placeholder={OPERATOR_INVOICE_COPY.listStatusPlaceholder}
                                style={{ width: '100%' }}
                                allowClear
                                value={statusFilter}
                                onChange={(val) => { setStatusFilter(val); setPagination(p => ({ ...p, current: 1 })); }}
                                options={statusOptions}
                            />
                        </Col>
                        <Col xs={24} sm={8} md={5}>
                            <Space direction="vertical" size={4} style={{ width: '100%' }}>
                                <Input
                                    placeholder={OPERATOR_INVOICE_COPY.listRegisterPlaceholder}
                                    value={cashRegisterIdFilter}
                                    onChange={(e) => { setCashRegisterIdFilter(e.target.value || undefined); setPagination(p => ({ ...p, current: 1 })); }}
                                    allowClear
                                />
                                <Typography.Text type="secondary" style={{ display: 'block', fontSize: 11 }}>
                                    {OPERATOR_INVOICE_COPY.registerListFilterApiFootnote}
                                </Typography.Text>
                                <Checkbox
                                    checked={invalidRegisterOnly}
                                    onChange={e => setInvalidRegisterOnly(e.target.checked)}
                                >
                                    {OPERATOR_INVOICE_COPY.invalidRegisterOnlyCheckboxLabel}
                                </Checkbox>
                            </Space>
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
                        </Col>
                        <Col
                            xs={24}
                            sm={8}
                            md={24}
                            lg={2}
                            style={{ textAlign: 'right' }}
                        >
                            <Tooltip title={OPERATOR_SHARED_COPY.refetchHintToolbar}>
                                <Button
                                    icon={<ReloadOutlined />}
                                    onClick={() => refetch()}
                                    loading={isFetching}
                                >
                                    {OPERATOR_SHARED_COPY.toolbarRefresh}
                                </Button>
                            </Tooltip>
                        </Col>
                    </Row>

                    {dateRangeError ? (
                        <Alert
                            type="warning"
                            showIcon
                            message={OPERATOR_INVOICE_COPY.dateRangeBlocksQueryTitle}
                            description={
                                <Space direction="vertical" size={4}>
                                    <Typography.Text>{dateRangeError}</Typography.Text>
                                    <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                                        {OPERATOR_INVOICE_COPY.dateRangeBlocksQuerySuffix}
                                    </Typography.Text>
                                </Space>
                            }
                        />
                    ) : null}

                    {/* Active filters — single scan row */}
                    {hasActiveFilters ? (
                        <div>
                            <Space wrap size={[8, 8]} align="center">
                                <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                                    {OPERATOR_INVOICE_COPY.activeFiltersLabel}:
                                </Typography.Text>
                                {debouncedSearch.trim() ? (
                                    <Tag
                                        closable
                                        onClose={() => {
                                            setSearchText('');
                                            setPagination((p) => ({ ...p, current: 1 }));
                                        }}
                                    >
                                        {OPERATOR_INVOICE_COPY.filterTagSearchPrefix}: {debouncedSearch.trim()}
                                    </Tag>
                                ) : null}
                                {statusFilter !== undefined ? (
                                    <Tag
                                        closable
                                        onClose={() => {
                                            setStatusFilter(undefined);
                                            setPagination((p) => ({ ...p, current: 1 }));
                                        }}
                                    >
                                        {OPERATOR_INVOICE_COPY.filterTagStatusPrefix}:{' '}
                                        {InvoiceStatusMap[statusFilter]?.label ?? String(statusFilter)}
                                    </Tag>
                                ) : null}
                                {dateRange?.[0] && dateRange?.[1] ? (
                                    <Tag
                                        closable
                                        onClose={() => {
                                            setDateRange(null);
                                            setPagination((p) => ({ ...p, current: 1 }));
                                        }}
                                    >
                                        {OPERATOR_INVOICE_COPY.filterTagDateRangePrefix}:{' '}
                                        {dayjs(dateRange[0]).format('DD.MM.YYYY')} –{' '}
                                        {dayjs(dateRange[1]).format('DD.MM.YYYY')}
                                    </Tag>
                                ) : null}
                                {cashRegisterIdFilter?.trim() ? (
                                    <Tag
                                        closable
                                        onClose={() => {
                                            setCashRegisterIdFilter(undefined);
                                            setPagination((p) => ({ ...p, current: 1 }));
                                        }}
                                        color={registerListFilterAnalysis.linkSafeUuid ? 'geekblue' : 'orange'}
                                    >
                                        {registerListFilterAnalysis.linkSafeUuid
                                            ? `${OPERATOR_INVOICE_COPY.filterTagRegisterUuid}: `
                                            : `${OPERATOR_INVOICE_COPY.filterTagRegisterApi}: `}
                                        {cashRegisterIdFilter.trim()}
                                    </Tag>
                                ) : null}
                                {invalidRegisterOnly ? (
                                    <Tag
                                        closable
                                        onClose={() => setInvalidRegisterOnly(false)}
                                        color="purple"
                                    >
                                        {OPERATOR_INVOICE_COPY.filterTagInvalidRegisterShort}
                                    </Tag>
                                ) : null}
                                <Button type="link" size="small" onClick={clearAllFilters}>
                                    {OPERATOR_INVOICE_COPY.clearAllFilters}
                                </Button>
                            </Space>
                        </div>
                    ) : null}

                    {registerListFilterAnalysis.isRawPresentButNotLinkSafe && (
                        <Alert
                            type="warning"
                            showIcon
                            message={OPERATOR_INVOICE_COPY.registerFilterInvalidTitle}
                            description={OPERATOR_INVOICE_COPY.registerFilterInvalidDescription}
                        />
                    )}

                    {/* Error with cached/partial data — keep filters + table usable */}
                    {isError && data ? (
                        <Alert
                            type="error"
                            message={OPERATOR_SHARED_COPY.loadFailedList}
                            description={
                                <Space direction="vertical" size={4}>
                                    <Typography.Text>{invoiceListErrorUserMessage(listQueryError)}</Typography.Text>
                                    <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                                        {OPERATOR_INVOICE_COPY.listStaleAfterErrorNote}
                                    </Typography.Text>
                                </Space>
                            }
                            showIcon
                            closable={false}
                            action={
                                <Space wrap>
                                    <Button size="small" type="primary" onClick={() => refetch()}>
                                        {OPERATOR_SHARED_COPY.retryLoadShort}
                                    </Button>
                                    <Button size="small" onClick={clearAllFilters}>
                                        {OPERATOR_INVOICE_COPY.clearAllFilters}
                                    </Button>
                                </Space>
                            }
                        />
                    ) : null}

                    {data && !dateRangeError ? (
                        <div
                            style={{
                                padding: '10px 12px',
                                borderRadius: token.borderRadiusLG,
                                background: token.colorFillQuaternary,
                                border: `1px solid ${token.colorBorderSecondary}`,
                            }}
                        >
                            <Typography.Text type="secondary" style={{ fontSize: 12, display: 'block' }}>
                                <strong>{OPERATOR_INVOICE_COPY.listSummaryApiTotal}:</strong>{' '}
                                {fmt.formatNumber(data.totalCount ?? 0, { maximumFractionDigits: 0 })}
                                {' · '}
                                <strong>{OPERATOR_INVOICE_COPY.listSummaryRowsThisPage}:</strong>{' '}
                                {fmt.formatNumber(displayedItems.length, { maximumFractionDigits: 0 })}
                                {invalidRegisterOnly ? (
                                    <>
                                        {' — '}
                                        {OPERATOR_INVOICE_COPY.listSummaryClientFilterNote}
                                    </>
                                ) : null}
                                {isListRefreshing && !isError ? (
                                    <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                                        {' '}
                                        ({OPERATOR_INVOICE_COPY.listRefreshingHint})
                                    </Typography.Text>
                                ) : null}
                            </Typography.Text>
                        </div>
                    ) : null}

                    {/* Table */}
                    <Table<InvoiceListItemDto>
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
                            showTotal: (total, range) => {
                                if (total <= 0) {
                                    return OPERATOR_INVOICE_COPY.paginationZeroResults;
                                }
                                const from = range[0] ?? 0;
                                const to = range[1] ?? 0;
                                return `${fmt.formatNumber(from, { maximumFractionDigits: 0 })}–${fmt.formatNumber(to, { maximumFractionDigits: 0 })} von ${fmt.formatNumber(total, { maximumFractionDigits: 0 })}`;
                            },
                        }}
                        loading={
                            isInitialListLoading
                                ? { tip: OPERATOR_INVOICE_COPY.listLoadingTip }
                                : false
                        }
                        onChange={handleTableChange}
                        size="middle"
                        scroll={{ x: 1180 }}
                        locale={{
                            emptyText: tableEmptyText,
                        }}
                    />
                </Space>
            </Space>

            {/* Detail Modal */}
            <Modal
                title={`${OPERATOR_INVOICE_COPY.detailModalTitlePrefix} ${displayInvoiceNumber}`}
                open={detailVisible}
                onCancel={() => setDetailVisible(false)}
                footer={[
                    <Button key="close" onClick={() => setDetailVisible(false)}>
                        {OPERATOR_INVOICE_COPY.detailModalClose}
                    </Button>,
                    <Button
                        key="print"
                        type="primary"
                        icon={<PrinterOutlined />}
                        onClick={() => handlePrint(selectedInvoiceId || '')}
                    >
                        {OPERATOR_INVOICE_COPY.detailModalPrint}
                    </Button>,
                    (detailInvoice && (detailInvoice.status === 1 || detailInvoice.status === 2)) && (
                        <Button
                            key="submit"
                            icon={<CloudUploadOutlined />}
                            onClick={() => void handleSubmitFinanzOnline(detailInvoice)}
                        >
                            {OPERATOR_INVOICE_COPY.detailModalReconciliationRetry}
                        </Button>
                    ),
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
                            const provenanceFacet = invoiceProvenanceUiFacet(detailInvoice);
                            const detailCorrelationTrimmed = detailInvoice.correlationId?.trim() ?? '';

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
                                                    {fmt.formatCurrency(detailInvoice.totalAmount ?? 0)}
                                                </Typography.Text>
                                            </div>
                                        </Space>
                                        <Divider style={{ margin: '12px 0' }} />
                                        <Space wrap align="center" size={8}>
                                            <Typography.Text type="secondary">
                                                {OPERATOR_INVOICE_COPY.detailRegisterMachineLabel}:
                                            </Typography.Text>
                                            <AdminTruthBadge
                                                kind={registerDeepLinkEligibleBadgeKind(detailRegFk)}
                                            />
                                            <Typography.Text code copyable style={{ maxWidth: 320 }} ellipsis>
                                                {displayScalar(detailInvoice.cashRegisterId)}
                                            </Typography.Text>
                                            {detailRegFk.linkSafeUuid ? (
                                                <Link
                                                    href={buildFinanzOnlineQueueInvestigationHref({
                                                        registerRowId: detailRegFk.linkSafeUuid,
                                                        focusPaymentId: detailInvoice.sourcePaymentId,
                                                        investigationBatchCorrelationId:
                                                            detailCorrelationTrimmed || undefined,
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
                                                {detailCorrelationTrimmed ? (
                                                    <>
                                                        <Typography.Text code copyable>
                                                            {detailCorrelationTrimmed}
                                                        </Typography.Text>
                                                        <Link
                                                            href={buildIncidentInvestigationHref(detailCorrelationTrimmed)}
                                                            target="_blank"
                                                            rel="noopener noreferrer"
                                                        >
                                                            {OPERATOR_LINK_LABELS.incidentAggregate}
                                                        </Link>
                                                        <Typography.Text type="secondary">·</Typography.Text>
                                                        <Link
                                                            href={buildReplayBatchDetailHref(detailCorrelationTrimmed)}
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
                                            {provenanceFacet.kind === 'explicit_backend_string' ? (
                                                <>
                                                    <strong>Herkunft (Antwort):</strong>{' '}
                                                    {provenanceFacet.operatorLabel}
                                                    <Typography.Text type="secondary" style={{ display: 'block', marginTop: 4, fontSize: 11 }}>
                                                        {OPERATOR_INVOICE_COPY.detailProvenanceUntypedApiNote}
                                                    </Typography.Text>
                                                </>
                                            ) : (
                                                <>
                                                    <strong>Herkunft:</strong> {OPERATOR_INVOICE_COPY.detailProvenanceFooter}
                                                </>
                                            )}
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
                                            <Descriptions.Item label={OPERATOR_INVOICE_COPY.listColumnCustomer} span={2}>
                                                {displayScalar(detailInvoice.customerName)} <br />
                                                {displayScalar(detailInvoice.customerAddress)} <br />
                                                {displayScalar(detailInvoice.customerTaxNumber)}
                                            </Descriptions.Item>
                                            <Descriptions.Item label={OPERATOR_INVOICE_COPY.descLabelCompany} span={2}>
                                                {displayScalar(detailInvoice.companyName)} <br />
                                                {displayScalar(detailInvoice.companyTaxNumber)}
                                            </Descriptions.Item>
                                            <Descriptions.Item label={OPERATOR_INVOICE_COPY.descLabelTotalAmount}>
                                                {fmt.formatCurrency(detailInvoice.totalAmount ?? 0)}
                                            </Descriptions.Item>
                                            <Descriptions.Item label={OPERATOR_INVOICE_COPY.descLabelTaxAmount}>
                                                {fmt.formatCurrency(detailInvoice.taxAmount ?? 0)}
                                            </Descriptions.Item>
                                            <Descriptions.Item label={OPERATOR_INVOICE_COPY.descRegisterFkMachine} span={2}>
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
                                                        <AdminTruthBadge
                                                            kind={registerDeepLinkEligibleBadgeKind(detailRegFk)}
                                                        />
                                                    </Space>
                                                    <Typography.Text code copyable>
                                                        {displayScalar(detailInvoice.cashRegisterId)}
                                                    </Typography.Text>
                                                    {detailRegFk.linkSafeUuid ? (
                                                        <div>
                                                            <Link
                                                                href={buildFinanzOnlineQueueInvestigationHref({
                                                                    registerRowId: detailRegFk.linkSafeUuid,
                                                                    focusPaymentId: detailInvoice.sourcePaymentId,
                                                                    investigationBatchCorrelationId:
                                                                        detailCorrelationTrimmed || undefined,
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
                                            <Descriptions.Item label={OPERATOR_INVOICE_COPY.descKassenIdDisplay} span={2}>
                                                <Space wrap align="center">
                                                    <AdminTruthBadge kind="display_only_label" />
                                                    <span>{formatRegisterDisplayLabel(detailInvoice.kassenId)}</span>
                                                </Space>
                                            </Descriptions.Item>
                                            <Descriptions.Item label={OPERATOR_INVOICE_COPY.descLabelPaymentMethod} span={2}>
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
                    <Empty description={OPERATOR_INVOICE_COPY.detailEmptyLoadFailed} />
                )}
            </Modal>

            {/* Credit Note Modal */}
            <Modal
                title={OPERATOR_INVOICE_COPY.creditNoteModalTitle}
                open={creditNoteVisible}
                onCancel={() => { setCreditNoteVisible(false); creditNoteForm.resetFields(); }}
                onOk={handleCreateCreditNote}
                confirmLoading={creditNoteLoading}
                okText={OPERATOR_INVOICE_COPY.creditNoteModalOk}
                cancelText={OPERATOR_INVOICE_COPY.creditNoteModalCancel}
                okButtonProps={{ danger: true }}
            >
                <Alert
                    type="warning"
                    message={OPERATOR_INVOICE_COPY.creditNoteAlertMessage}
                    description={OPERATOR_INVOICE_COPY.creditNoteAlertDescription}
                    showIcon
                    style={{ marginBottom: 16 }}
                />
                <Form form={creditNoteForm} layout="vertical">
                    <Form.Item
                        name="reasonCode"
                        label={OPERATOR_INVOICE_COPY.formReasonCodeLabel}
                        rules={[{ required: true, message: OPERATOR_INVOICE_COPY.formReasonCodeRequired }]}
                    >
                        <Select placeholder={OPERATOR_INVOICE_COPY.formReasonPlaceholder}>
                            <Select.Option value="RETURN">{OPERATOR_INVOICE_COPY.creditReasonReturn}</Select.Option>
                            <Select.Option value="ERROR">{OPERATOR_INVOICE_COPY.creditReasonError}</Select.Option>
                            <Select.Option value="DISCOUNT">{OPERATOR_INVOICE_COPY.creditReasonDiscount}</Select.Option>
                            <Select.Option value="CANCEL">{OPERATOR_INVOICE_COPY.creditReasonCancel}</Select.Option>
                            <Select.Option value="OTHER">{OPERATOR_INVOICE_COPY.creditReasonOther}</Select.Option>
                        </Select>
                    </Form.Item>
                    <Form.Item
                        name="reasonText"
                        label={OPERATOR_INVOICE_COPY.formReasonTextLabel}
                        rules={[{ required: true, message: OPERATOR_INVOICE_COPY.formReasonTextRequired }]}
                    >
                        <Input.TextArea rows={3} placeholder={OPERATOR_INVOICE_COPY.formReasonTextAreaPlaceholder} />
                    </Form.Item>
                </Form>
            </Modal>
        </React.Fragment>
    );
};

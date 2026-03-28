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
import { BackendRawTextBlock } from '@/components/admin-layout/BackendRawTextBlock';
import { extractRawApiErrorMessage } from '@/shared/errors/extractRawApiErrorMessage';
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
    OPERATOR_LINK_LABELS,
    OPERATOR_REGISTER_LINK_COPY,
} from '@/shared/operatorTruthCopy';
import { useI18n } from '@/i18n';
import { FORMAT_EMPTY_DISPLAY, createIntlFormatters } from '@/i18n/formatting';

const { RangePicker } = DatePicker;

type InvoiceTranslateFn = (key: string, options?: Record<string, string | number>) => string;

// Manual mapping because Orval generated NUMBER_0 etc. for enum
function buildInvoiceStatusMap(t: InvoiceTranslateFn): Record<number, { label: string; color: string }> {
    return {
        0: { label: t('invoices.status.draft'), color: 'default' },
        1: { label: t('invoices.status.sent'), color: 'processing' },
        2: { label: t('invoices.status.paid'), color: 'success' },
        3: { label: t('invoices.status.partiallyPaid'), color: 'warning' },
        4: { label: t('invoices.status.unpaid'), color: 'error' },
        5: { label: t('invoices.status.overdue'), color: 'error' },
        6: { label: t('invoices.status.cancelled'), color: 'default' },
        7: { label: t('invoices.status.creditNote'), color: 'purple' },
    };
}

function getPaymentMethodLabel(method: PaymentMethod | undefined, t: InvoiceTranslateFn): string {
    switch (method) {
        case 0: return t('invoices.paymentMethod.bar');
        case 1: return t('invoices.paymentMethod.card');
        case 2: return t('invoices.paymentMethod.transfer');
        case 3: return t('invoices.paymentMethod.check');
        case 4: return t('invoices.paymentMethod.voucher');
        case 5: return t('invoices.paymentMethod.mobile');
        default: return FORMAT_EMPTY_DISPLAY;
    }
}

/** Short user-facing hint; raw API text is shown separately via `BackendRawTextBlock`. */
function invoiceListLocalizedErrorHint(err: unknown, t: InvoiceTranslateFn): string {
    const status = getAxiosResponseStatus(err);
    if (status === 401) return t('invoices.errors.listUnauthorized');
    if (status === 403) return t('invoices.errors.listForbidden');
    if (status === 404) return t('invoices.errors.listNotFound');
    if (status != null && status >= 500) return t('invoices.errors.listServer');
    if (err instanceof Error && /network|failed to fetch|load failed|econnrefused|timeout/i.test(err.message)) {
        return t('invoices.errors.listNetwork');
    }
    return t('invoices.errors.listGeneric');
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
    const { formatLocale, t } = useI18n();
    const fmt = useMemo(() => createIntlFormatters(formatLocale), [formatLocale]);
    const invoiceStatusMap = useMemo(() => buildInvoiceStatusMap(t), [t]);

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
            okText: t('invoices.modals.openFinanzOnlineQueue'),
            onOk: () => {
                window.open(link, '_blank', 'noopener,noreferrer');
            },
            content: (
                <Space direction="vertical" size={8}>
                    <Typography.Text>{args.messageText}</Typography.Text>
                    {args.submissionId ? (
                        <Typography.Text copyable={{ text: args.submissionId }}>
                            {t('invoices.handoff.submissionIdLabel')}: {args.submissionId}
                        </Typography.Text>
                    ) : null}
                    {args.submittedAt ? (
                        <Typography.Text>
                            {t('invoices.handoff.submittedAtLabel')}:{' '}
                            {dayjs(args.submittedAt).isValid() ? dayjs(args.submittedAt).format('DD.MM.YYYY HH:mm:ss') : args.submittedAt}
                        </Typography.Text>
                    ) : null}
                    {registerFilterOmitted ? (
                        <Typography.Paragraph type="warning" style={{ marginBottom: 0, fontSize: 12 }}>
                            {t('invoices.handoff.registerFilterOmitted')}
                        </Typography.Paragraph>
                    ) : null}
                    <Typography.Text type="secondary">
                        {args.footerHint || t('invoices.handoff.defaultFooter')}
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
            message.success(t('invoices.messages.creditNoteCreated'));
            setCreditNoteVisible(false);
            creditNoteForm.resetFields();
            refetch();
        } catch (err: unknown) {
            const status = getAxiosResponseStatus(err);
            if (status === 409) {
                message.warning(t('invoices.messages.creditNoteExists'));
            } else if (status === 400) {
                message.error(getAxiosResponseDataString(err) ?? t('invoices.messages.creditNoteBadRequest'));
            } else if (isAntdFormValidateError(err)) {
                // form validation error — ignore, form shows inline
            } else {
                message.error(t('invoices.messages.creditNoteFailed'));
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
        message.success(t('invoices.messages.batchPrint', { ok: success, fail }));
        setSelectedRowKeys([]);
    };

    const handleBatchExport = async () => {
        if (!selectedRowKeys.length) return;
        setBatchLoading(true);
        let success = 0;
        let fail = 0;
        const lines: string[] = [t('invoices.export.csvHeaderRow')];

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
        message.success(t('invoices.batch.exportSuccess', { success, fail }));
        setSelectedRowKeys([]);
    };

    const handleBatchSubmit = () => {
        if (!selectedRowKeys.length) return;
        Modal.confirm({
            title: t('invoices.modals.batchReconcileTitle'),
            content: t('invoices.modals.batchReconcileBody', { count: selectedRowKeys.length }),
            okText: t('invoices.modals.batchReconcileOk'),
            cancelText: t('invoices.modals.batchReconcileCancel'),
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
                message.info(
                    t('invoices.messages.batchReconcileSummary', {
                        ok: success,
                        fail,
                        skipped,
                        already: alreadySubmitted,
                    }),
                );
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
                        title: t('invoices.modals.batchReconcileFinishedTitle'),
                        messageText: t('invoices.batch.reconcileHandoffBody', { success }),
                        cashRegisterId: firstRegister,
                        focusPaymentId: handoffFocusPaymentId,
                        investigationBatchCorrelationId: handoffBatchCorrelation,
                        fromUtc: minDate.startOf('day').toISOString(),
                        toUtc: maxDate.endOf('day').toISOString(),
                        footerHint: t('invoices.batch.reconcileFooterHint', {
                            success,
                            fail,
                            skipped,
                            alreadySubmitted,
                        }),
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
            message.success(t('invoices.messages.exportCsvOk'));
        } catch (error) {
            technicalConsole.error('[InvoiceList] CSV export failed', error);
            message.error(t('invoices.messages.exportCsvFailed'));
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
                message.error(t('invoices.messages.pdfSessionExpired'));
            } else if (status === 404) {
                message.error(t('invoices.messages.pdfNotFound'));
            } else {
                message.error(t('invoices.messages.pdfFailed'));
            }
        }
    };

    const handleSubmitFinanzOnline = async (invoice: Invoice) => {
        const paymentId = invoice.sourcePaymentId ?? invoice.id;
        if (!paymentId) {
            message.error(t('invoices.messages.noPaymentLinkedForReconcile'));
            return;
        }
        try {
            const data = await postApiAdminFinanzonlineReconciliationRetryPaymentId(paymentId);
            const invoiceDate = dayjs(invoice.invoiceDate).isValid() ? dayjs(invoice.invoiceDate) : dayjs();
            if (data.success) {
                const uiMessage = data.referenceId
                    ? t('invoices.messages.finanzOnlineSubmitOk')
                    : t('invoices.messages.finanzOnlineAlreadySubmitted');
                message.success(uiMessage);
                openReconciliationHandoffModal({
                    title: data.referenceId
                        ? t('invoices.modals.reconciliationSuccessTitle')
                        : t('invoices.modals.alreadySubmittedTitle'),
                    messageText: `${uiMessage}${t('invoices.reconciliation.checkStatusAfter')}`,
                    submissionId: data.referenceId || null,
                    submittedAt: data.submittedAt || null,
                    cashRegisterId: invoice.cashRegisterId,
                    focusPaymentId: invoice.sourcePaymentId ?? invoice.id,
                    investigationBatchCorrelationId: invoice.correlationId ?? undefined,
                    fromUtc: invoiceDate.startOf('day').toISOString(),
                    toUtc: invoiceDate.endOf('day').toISOString(),
                });
            } else {
                message.warning(`${t('invoices.modals.reconciliationFailedTitle')}: ${data.message}`);
                Modal.warning({
                    title: t('invoices.modals.reconciliationFailedTitle'),
                    content: (
                        <Space direction="vertical" size={8}>
                            <Typography.Text>{data.message || t('invoices.reconciliation.retryUnknownError')}</Typography.Text>
                            <Typography.Text type="secondary">
                                {t('invoices.reconciliation.failedOpenQueueHint')}
                            </Typography.Text>
                        </Space>
                    ),
                    okText: t('invoices.modals.openFinanzOnlineQueue'),
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
            message.error(t('invoices.messages.reconciliationRetryTriggerFailed'));
            Modal.error({
                title: t('invoices.modals.reconciliationErrorTitle'),
                content: (
                    <Space direction="vertical" size={8}>
                        <Typography.Text>
                            {getAxiosResponseDataString(err) ??
                                t('invoices.reconciliation.catchFallbackMessage')}
                        </Typography.Text>
                        <Typography.Text type="secondary">
                            {t('invoices.reconciliation.errorOpenQueueHint')}
                        </Typography.Text>
                    </Space>
                ),
                okText: t('invoices.modals.openFinanzOnlineQueue'),
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
            title: t('invoices.columns.invoiceNumber'),
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
                            <Tag color="purple" style={{ fontSize: 10 }}>{t('invoices.creditNoteTagShort')}</Tag>
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
            title: t('invoices.columns.date'),
            dataIndex: 'invoiceDate',
            key: 'invoiceDate',
            sorter: true,
            width: 132,
            render: (date) => dayjs(date).format('DD.MM.YYYY HH:mm'),
        },
        {
            title: t('invoices.columns.customer'),
            dataIndex: 'customerName',
            key: 'customerName',
            width: 168,
            ellipsis: { showTitle: true },
            render: (text) => text || FORMAT_EMPTY_DISPLAY,
        },
        {
            title: t('invoices.columns.total'),
            dataIndex: 'totalAmount',
            key: 'totalAmount',
            sorter: true,
            width: 104,
            align: 'right',
            render: (amount) => fmt.formatCurrency(Number(amount ?? 0)),
        },
        {
            title: t('invoices.columns.status'),
            dataIndex: 'status',
            key: 'status',
            sorter: true,
            width: 112,
            render: (status: InvoiceStatus | undefined) => {
                const code = status ?? InvoiceStatus.NUMBER_0;
                const info = invoiceStatusMap[code] || { label: t('invoices.status.unknown'), color: 'default' };
                return <Tag color={info.color}>{info.label}</Tag>;
            },
        },
        {
            title: (
                <Tooltip title={t('invoices.list.kassenHeaderTooltip')}>
                    <Typography.Text type="secondary">{t('invoices.columns.kassenShort')}</Typography.Text>
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
                                    ? t('invoices.list.kassenCellTooltipWithFk', { fk: apiFk })
                                    : t('invoices.list.kassenCellTooltipNoFk')
                            }
                        >
                            <Typography.Text type="secondary">{text?.trim() || FORMAT_EMPTY_DISPLAY}</Typography.Text>
                        </Tooltip>
                        <AdminTruthBadge kind="display_only_label" />
                    </Space>
                );
            },
        },
        {
            title: (
                <Tooltip title={t('invoices.list.tseHeaderTooltip')}>
                    <Typography.Text type="secondary">{t('invoices.columns.tseShort')}</Typography.Text>
                </Tooltip>
            ),
            key: 'tsePrefix',
            width: 92,
            ellipsis: true,
            render: (_: unknown, record: InvoiceListItemDto) => {
                const tseSig = record.tseSignature?.trim();
                if (!tseSig) return <Typography.Text type="secondary">{FORMAT_EMPTY_DISPLAY}</Typography.Text>;
                const shortT = tseSig.length > 22 ? `${tseSig.slice(0, 22)}…` : tseSig;
                return (
                    <Tooltip title={tseSig}>
                        <Typography.Text code style={{ fontSize: 10 }} ellipsis>
                            {shortT}
                        </Typography.Text>
                    </Tooltip>
                );
            },
        },
        {
            title: t('invoices.columns.actions'),
            key: 'actions',
            width: 268,
            fixed: 'right',
            render: (_, record) => (
                <Space size={4} wrap align="center">
                    <Tooltip title={t('invoices.rowActions.detailExtendedTooltip')}>
                        <Button
                            type="link"
                            size="small"
                            style={{ paddingInline: 0 }}
                            aria-label={t('invoices.rowActions.detailExtendedTooltip')}
                            onClick={() => {
                                setSelectedInvoiceId(record.id ?? null);
                                setDetailVisible(true);
                            }}
                        >
                            {t('invoices.rowActions.detailTooltip')}
                        </Button>
                    </Tooltip>
                    <Tooltip title={t('invoices.rowActions.printTooltip')}>
                        <Button
                            icon={<PrinterOutlined />}
                            size="small"
                            aria-label={t('invoices.rowActions.printTooltip')}
                            onClick={() => handlePrint(record.id ?? '')}
                        >
                            {t('invoices.rowActions.printCompact')}
                        </Button>
                    </Tooltip>
                    {(record.status === InvoiceStatus.NUMBER_2 || record.status === InvoiceStatus.NUMBER_1) &&
                        record.documentType !== DocumentType.NUMBER_1 && (
                        <Tooltip title={t('invoices.rowActions.creditNoteTooltip')}>
                            <Button
                                icon={<RollbackOutlined />}
                                size="small"
                                danger
                                aria-label={t('invoices.rowActions.creditNoteTooltip')}
                                onClick={() => {
                                    setCreditNoteTargetId(record.id ?? null);
                                    setCreditNoteVisible(true);
                                }}
                            >
                                {t('invoices.rowActions.creditCompact')}
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
        label: invoiceStatusMap[value]?.label ?? String(value),
        value,
    }));

    const selectedRow = data?.items?.find((item) => item.id === selectedInvoiceId);
    const displayInvoiceNumber =
        detailInvoice?.invoiceNumber || selectedRow?.invoiceNumber || selectedInvoiceId || t('invoices.display.unknownInvoice');

    const tableEmptyText =
        isInitialListLoading ? undefined : dateRangeError ? (
            <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description={t('invoices.empty.invalidDateRange')}>
                <Typography.Paragraph type="secondary" style={{ fontSize: 12, marginTop: 8, marginBottom: 0 }}>
                    {dateRangeError}
                </Typography.Paragraph>
                <Typography.Paragraph type="secondary" style={{ fontSize: 12, marginTop: 8, marginBottom: 0 }}>
                    {t('invoices.dateRange.blocksQuerySuffix')}
                </Typography.Paragraph>
            </Empty>
        ) : isError && !data ? (
            <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description={t('invoices.empty.loadFailedTitle')}>
                <Typography.Paragraph type="secondary" style={{ fontSize: 12, marginTop: 8 }}>
                    {invoiceListLocalizedErrorHint(listQueryError, t)}
                </Typography.Paragraph>
                <BackendRawTextBlock introKey="common.backend.serverHintIntro" body={extractRawApiErrorMessage(listQueryError)} />
                <Space wrap size={[12, 12]} style={{ marginTop: 12 }}>
                    <Button type="primary" onClick={() => refetch()}>
                        {t('common.buttons.reload')}
                    </Button>
                    <Button onClick={clearAllFilters}>{t('invoices.filters.clearAllFilters')}</Button>
                </Space>
                <Typography.Paragraph type="secondary" style={{ fontSize: 12, marginTop: 12, marginBottom: 0 }}>
                    {t('invoices.empty.loadFailedHint')}
                </Typography.Paragraph>
            </Empty>
        ) : (
            <Empty
                image={Empty.PRESENTED_IMAGE_SIMPLE}
                description={
                    dateRange?.[0] && dateRange?.[1]
                        ? t('invoices.empty.dateRange')
                        : t('invoices.empty.default')
                }
            >
                <Space wrap size={[12, 12]} style={{ marginTop: 12 }}>
                    <Button type="primary" onClick={() => refetch()}>
                        {t('common.buttons.reload')}
                    </Button>
                    <Button onClick={clearAllFilters}>{t('invoices.filters.clearAllFilters')}</Button>
                </Space>
                <Typography.Paragraph type="secondary" style={{ fontSize: 12, marginBottom: 0, marginTop: 8 }}>
                    {t('invoices.empty.moreHint')}
                </Typography.Paragraph>
            </Empty>
        );

    return (
        <React.Fragment>
            <Space direction="vertical" size="large" style={{ width: '100%' }}>
                <AdminPageHeader
                    title={t('invoices.page.title')}
                    breadcrumbs={[ADMIN_OVERVIEW_CRUMB, { title: t('invoices.page.title') }]}
                    actions={
                        <Space wrap>
                            <Button
                                disabled={!selectedRowKeys.length}
                                icon={<PrinterOutlined />}
                                onClick={handleBatchPrint}
                                loading={batchLoading}
                            >
                                {t('invoices.actions.batchPrint')}
                            </Button>
                            <Button
                                disabled={!selectedRowKeys.length}
                                icon={<DownloadOutlined />}
                                onClick={handleBatchExport}
                                loading={batchLoading}
                            >
                                {t('invoices.actions.batchExport')}
                            </Button>
                            <Button
                                disabled={!selectedRowKeys.length}
                                icon={<CloudUploadOutlined />}
                                onClick={handleBatchSubmit}
                                loading={batchLoading}
                            >
                                {t('invoices.actions.batchReconcile')}
                            </Button>
                            <Button
                                type="primary"
                                icon={<DownloadOutlined />}
                                onClick={handleExport}
                                loading={exportLoading}
                            >
                                {t('invoices.actions.exportCsvAll')}
                            </Button>
                        </Space>
                    }
                >
                    <Typography.Paragraph type="secondary" style={{ marginBottom: 8 }}>
                        {t('invoices.page.listLead')}
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
                                placeholder={t('invoices.filters.searchPlaceholder')}
                                prefix={<SearchOutlined />}
                                value={searchText}
                                onChange={(e) => setSearchText(e.target.value)}
                                allowClear
                            />
                        </Col>
                        <Col xs={24} sm={8} md={4}>
                            <Select
                                placeholder={t('invoices.filters.statusPlaceholder')}
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
                                    placeholder={t('invoices.filters.registerPlaceholder')}
                                    value={cashRegisterIdFilter}
                                    onChange={(e) => { setCashRegisterIdFilter(e.target.value || undefined); setPagination(p => ({ ...p, current: 1 })); }}
                                    allowClear
                                />
                                <Typography.Text type="secondary" style={{ display: 'block', fontSize: 11 }}>
                                    {t('invoices.filters.registerListFilterApiFootnote')}
                                </Typography.Text>
                                <Checkbox
                                    checked={invalidRegisterOnly}
                                    onChange={e => setInvalidRegisterOnly(e.target.checked)}
                                >
                                    {t('invoices.filters.invalidRegisterOnlyCheckboxLabel')}
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
                            <Tooltip title={t('common.toolbar.refetchHint')}>
                                <Button
                                    icon={<ReloadOutlined />}
                                    onClick={() => refetch()}
                                    loading={isFetching}
                                >
                                    {t('common.buttons.refresh')}
                                </Button>
                            </Tooltip>
                        </Col>
                    </Row>

                    {dateRangeError ? (
                        <Alert
                            type="warning"
                            showIcon
                            message={t('invoices.dateRange.blocksQueryTitle')}
                            description={
                                <Space direction="vertical" size={4}>
                                    <Typography.Text>{dateRangeError}</Typography.Text>
                                    <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                                        {t('invoices.dateRange.blocksQuerySuffix')}
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
                                    {t('invoices.filters.activeFiltersLabel')}:
                                </Typography.Text>
                                {debouncedSearch.trim() ? (
                                    <Tag
                                        closable
                                        onClose={() => {
                                            setSearchText('');
                                            setPagination((p) => ({ ...p, current: 1 }));
                                        }}
                                    >
                                        {t('invoices.filterTags.searchPrefix')}: {debouncedSearch.trim()}
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
                                        {t('invoices.filterTags.statusPrefix')}:{' '}
                                        {invoiceStatusMap[statusFilter]?.label ?? String(statusFilter)}
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
                                        {t('invoices.filterTags.dateRangePrefix')}:{' '}
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
                                            ? `${t('invoices.filterTags.registerUuid')}: `
                                            : `${t('invoices.filterTags.registerApi')}: `}
                                        {cashRegisterIdFilter.trim()}
                                    </Tag>
                                ) : null}
                                {invalidRegisterOnly ? (
                                    <Tag
                                        closable
                                        onClose={() => setInvalidRegisterOnly(false)}
                                        color="purple"
                                    >
                                        {t('invoices.filterTags.invalidRegisterShort')}
                                    </Tag>
                                ) : null}
                                <Button type="link" size="small" onClick={clearAllFilters}>
                                    {t('invoices.filters.clearAllFilters')}
                                </Button>
                            </Space>
                        </div>
                    ) : null}

                    {registerListFilterAnalysis.isRawPresentButNotLinkSafe && (
                        <Alert
                            type="warning"
                            showIcon
                            message={t('invoices.registerFilter.invalidTitle')}
                            description={t('invoices.registerFilter.invalidDescription')}
                        />
                    )}

                    {/* Error with cached/partial data — keep filters + table usable */}
                    {isError && data ? (
                        <Alert
                            type="error"
                            message={t('common.loadErrors.list')}
                            description={
                                <Space direction="vertical" size={4}>
                                    <Typography.Text>{invoiceListLocalizedErrorHint(listQueryError, t)}</Typography.Text>
                                    <BackendRawTextBlock introKey="common.backend.serverHintIntro" body={extractRawApiErrorMessage(listQueryError)} />
                                    <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                                        {t('invoices.listSummary.staleAfterErrorNote')}
                                    </Typography.Text>
                                </Space>
                            }
                            showIcon
                            closable={false}
                            action={
                                <Space wrap>
                                    <Button size="small" type="primary" onClick={() => refetch()}>
                                        {t('common.buttons.reload')}
                                    </Button>
                                    <Button size="small" onClick={clearAllFilters}>
                                        {t('invoices.filters.clearAllFilters')}
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
                                <strong>{t('invoices.listSummary.apiTotal')}:</strong>{' '}
                                {fmt.formatNumber(data.totalCount ?? 0, { maximumFractionDigits: 0 })}
                                {' · '}
                                <strong>{t('invoices.listSummary.rowsThisPage')}:</strong>{' '}
                                {fmt.formatNumber(displayedItems.length, { maximumFractionDigits: 0 })}
                                {invalidRegisterOnly ? (
                                    <>
                                        {' — '}
                                        {t('invoices.listSummary.clientFilterNote')}
                                    </>
                                ) : null}
                                {isListRefreshing && !isError ? (
                                    <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                                        {' '}
                                        ({t('invoices.listSummary.refreshingHint')})
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
                                    return t('invoices.pagination.zeroResults');
                                }
                                const from = range[0] ?? 0;
                                const to = range[1] ?? 0;
                                return t('invoices.pagination.rangeOfTotal', {
                                    from: fmt.formatNumber(from, { maximumFractionDigits: 0 }),
                                    to: fmt.formatNumber(to, { maximumFractionDigits: 0 }),
                                    total: fmt.formatNumber(total, { maximumFractionDigits: 0 }),
                                });
                            },
                        }}
                        loading={
                            isInitialListLoading
                                ? { tip: t('invoices.list.loadingTip') }
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
                title={`${t('invoices.detail.modalTitlePrefix')} ${displayInvoiceNumber}`}
                open={detailVisible}
                onCancel={() => setDetailVisible(false)}
                footer={[
                    <Button key="close" onClick={() => setDetailVisible(false)}>
                        {t('invoices.detail.modalClose')}
                    </Button>,
                    <Button
                        key="print"
                        type="primary"
                        icon={<PrinterOutlined />}
                        onClick={() => handlePrint(selectedInvoiceId || '')}
                    >
                        {t('invoices.detail.modalPrint')}
                    </Button>,
                    (detailInvoice && (detailInvoice.status === 1 || detailInvoice.status === 2)) && (
                        <Button
                            key="submit"
                            icon={<CloudUploadOutlined />}
                            onClick={() => void handleSubmitFinanzOnline(detailInvoice)}
                        >
                            {t('invoices.detail.modalReconciliationRetry')}
                        </Button>
                    ),
                ]}
                width={880}
            >
                {detailLoading ? (
                    <div style={{ textAlign: 'center', padding: '20px' }}>
                        {t('common.loading.invoiceDetail')}
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
                                    ? t('invoices.detail.positionsJsonError')
                                    : itemsDisplay.kind === 'unsupported_primitive'
                                      ? t('invoices.detail.positionsTypeLabel', { primitive: itemsDisplay.primitive })
                                      : itemsDisplay.rows.length > 0
                                        ? t('invoices.detail.positionsRowCount', { count: itemsDisplay.rows.length })
                                        : t('invoices.detail.positionsEmptyRaw');

                            return (
                                <Space direction="vertical" size={0} style={{ width: '100%' }}>
                                    <OperatorSummaryStrip>
                                        <Space wrap size={[16, 12]} align="start">
                                            <div>
                                                <Typography.Text type="secondary" style={{ fontSize: 11, display: 'block' }}>
                                                    {t('invoices.detail.invoiceLabel')}
                                                </Typography.Text>
                                                <Space size={6}>
                                                    <Typography.Text strong>{displayScalar(detailInvoice.invoiceNumber)}</Typography.Text>
                                                    {detailInvoice.documentType === DocumentType.NUMBER_1 ? (
                                                        <Tag color="purple">{t('invoices.detail.creditNoteTag')}</Tag>
                                                    ) : null}
                                                </Space>
                                            </div>
                                            <div>
                                                <Typography.Text type="secondary" style={{ fontSize: 11, display: 'block' }}>
                                                    {t('invoices.detail.statusLabel')}
                                                </Typography.Text>
                                                <Tag color={invoiceStatusMap[detailInvoice.status]?.color || 'default'}>
                                                    {invoiceStatusMap[detailInvoice.status]?.label || displayScalar(detailInvoice.status)}
                                                </Tag>
                                            </div>
                                            <div>
                                                <Typography.Text type="secondary" style={{ fontSize: 11, display: 'block' }}>
                                                    {t('invoices.detail.dateLabel')}
                                                </Typography.Text>
                                                <Typography.Text>{displayScalar(detailDate)}</Typography.Text>
                                            </div>
                                            <div>
                                                <Typography.Text type="secondary" style={{ fontSize: 11, display: 'block' }}>
                                                    {t('invoices.detail.grossLabel')}
                                                </Typography.Text>
                                                <Typography.Text strong>
                                                    {fmt.formatCurrency(detailInvoice.totalAmount ?? 0)}
                                                </Typography.Text>
                                            </div>
                                        </Space>
                                        <Divider style={{ margin: '12px 0' }} />
                                        <Space wrap align="center" size={8}>
                                            <Typography.Text type="secondary">
                                                {t('invoices.detail.registerMachineLabel')}:
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
                                                    {t('invoices.detail.foLinkWithContext')}
                                                </Link>
                                            ) : null}
                                        </Space>
                                        <Divider style={{ margin: '12px 0' }} />
                                        <Space direction="vertical" size={6} style={{ width: '100%' }}>
                                            <Space wrap align="center">
                                                <Typography.Text type="secondary">{t('invoices.detail.paymentReconciliation')}</Typography.Text>
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
                                                            {t('invoices.detail.openPayment')}
                                                        </Link>
                                                    </>
                                                ) : (
                                                    <Typography.Text type="secondary">{t('invoices.detail.noPaymentLinked')}</Typography.Text>
                                                )}
                                            </Space>
                                            <Space wrap align="center">
                                                <Typography.Text type="secondary">
                                                    {t('invoices.detail.correlationPathsLabel')}:
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
                                                    <strong>{t('invoices.detail.provenanceFromResponse')}</strong>{' '}
                                                    {provenanceFacet.operatorLabel}
                                                    <Typography.Text type="secondary" style={{ display: 'block', marginTop: 4, fontSize: 11 }}>
                                                        {t('invoices.detail.provenanceUntypedApiNote')}
                                                    </Typography.Text>
                                                </>
                                            ) : (
                                                <>
                                                    <strong>{t('invoices.detail.provenancePlain')}</strong> {t('invoices.detail.provenanceOperatorFooter')}
                                                </>
                                            )}
                                        </Typography.Paragraph>
                                        {(detailInvoice.status === InvoiceStatus.NUMBER_1 || detailInvoice.status === InvoiceStatus.NUMBER_2) && (
                                            <Typography.Paragraph type="secondary" style={{ marginTop: 8, marginBottom: 0, fontSize: 12 }}>
                                                {t('invoices.detail.retryFooterHint')}
                                            </Typography.Paragraph>
                                        )}
                                    </OperatorSummaryStrip>

                                    <OperatorBusinessSection>
                                        <Descriptions bordered column={2} size="small">
                                            <Descriptions.Item label={t('invoices.columns.customer')} span={2}>
                                                {displayScalar(detailInvoice.customerName)} <br />
                                                {displayScalar(detailInvoice.customerAddress)} <br />
                                                {displayScalar(detailInvoice.customerTaxNumber)}
                                            </Descriptions.Item>
                                            <Descriptions.Item label={t('invoices.detail.descLabelCompany')} span={2}>
                                                {displayScalar(detailInvoice.companyName)} <br />
                                                {displayScalar(detailInvoice.companyTaxNumber)}
                                            </Descriptions.Item>
                                            <Descriptions.Item label={t('invoices.detail.descLabelTotalAmount')}>
                                                {fmt.formatCurrency(detailInvoice.totalAmount ?? 0)}
                                            </Descriptions.Item>
                                            <Descriptions.Item label={t('invoices.detail.descLabelTaxAmount')}>
                                                {fmt.formatCurrency(detailInvoice.taxAmount ?? 0)}
                                            </Descriptions.Item>
                                            <Descriptions.Item label={t('invoices.detail.descRegisterFkMachine')} span={2}>
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
                                                                {t('invoices.detail.foLinkRegisterOnly')}
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
                                            <Descriptions.Item label={t('invoices.detail.descKassenIdDisplay')} span={2}>
                                                <Space wrap align="center">
                                                    <AdminTruthBadge kind="display_only_label" />
                                                    <span>{formatRegisterDisplayLabel(detailInvoice.kassenId)}</span>
                                                </Space>
                                            </Descriptions.Item>
                                            <Descriptions.Item label={t('invoices.detail.descLabelPaymentMethod')} span={2}>
                                                {displayScalar(getPaymentMethodLabel(detailInvoice.paymentMethod, t))}
                                            </Descriptions.Item>
                                        </Descriptions>
                                    </OperatorBusinessSection>

                                    <OperatorTechnicalSection>
                                        <Collapse
                                            bordered={false}
                                            items={[
                                                {
                                                    key: 'tse',
                                                    label: t('invoices.detail.collapseTseRaw'),
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
                                                    label: t('invoices.detail.positionsCollapseLabel', { label: positionsLabel }),
                                                    children: (
                                                        <Space direction="vertical" size={8} style={{ width: '100%' }}>
                                                            <Alert
                                                                type="info"
                                                                showIcon
                                                                message={t('invoices.detail.contractInvoiceItemsTitle')}
                                                                description={RKSv_ADMIN_CONTRACT_GAPS.invoiceDetailInvoiceItems}
                                                            />
                                                            {itemsDisplay.kind === 'parse_error' ? (
                                                                <Alert
                                                                    type="warning"
                                                                    showIcon
                                                                    message={t('invoices.detail.itemsParseErrorTitle')}
                                                                    description={itemsDisplay.message}
                                                                />
                                                            ) : null}
                                                            {itemsDisplay.kind === 'unsupported_primitive' ? (
                                                                <Alert
                                                                    type="warning"
                                                                    showIcon
                                                                    message={t('invoices.detail.itemsUnexpectedTypeTitle')}
                                                                    description={t('invoices.detail.itemsUnexpectedTypeDesc', {
                                                                        primitive: itemsDisplay.primitive,
                                                                    })}
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
                                                                    {t('invoices.detail.itemsNoRows')}
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
                    <Empty description={t('invoices.detail.emptyLoadFailed')} />
                )}
            </Modal>

            {/* Credit Note Modal */}
            <Modal
                title={t('invoices.creditNote.modalTitle')}
                open={creditNoteVisible}
                onCancel={() => { setCreditNoteVisible(false); creditNoteForm.resetFields(); }}
                onOk={handleCreateCreditNote}
                confirmLoading={creditNoteLoading}
                okText={t('invoices.creditNote.modalOk')}
                cancelText={t('invoices.creditNote.modalCancel')}
                okButtonProps={{ danger: true }}
            >
                <Alert
                    type="warning"
                    message={t('invoices.creditNote.alertMessage')}
                    description={t('invoices.creditNote.alertDescription')}
                    showIcon
                    style={{ marginBottom: 16 }}
                />
                <Form form={creditNoteForm} layout="vertical">
                    <Form.Item
                        name="reasonCode"
                        label={t('invoices.creditNote.formReasonCodeLabel')}
                        rules={[{ required: true, message: t('invoices.creditNote.formReasonCodeRequired') }]}
                    >
                        <Select placeholder={t('invoices.creditNote.formReasonPlaceholder')}>
                            <Select.Option value="RETURN">{t('invoices.creditNote.reasonReturn')}</Select.Option>
                            <Select.Option value="ERROR">{t('invoices.creditNote.reasonError')}</Select.Option>
                            <Select.Option value="DISCOUNT">{t('invoices.creditNote.reasonDiscount')}</Select.Option>
                            <Select.Option value="CANCEL">{t('invoices.creditNote.reasonCancel')}</Select.Option>
                            <Select.Option value="OTHER">{t('invoices.creditNote.reasonOther')}</Select.Option>
                        </Select>
                    </Form.Item>
                    <Form.Item
                        name="reasonText"
                        label={t('invoices.creditNote.formReasonTextLabel')}
                        rules={[{ required: true, message: t('invoices.creditNote.formReasonTextRequired') }]}
                    >
                        <Input.TextArea rows={3} placeholder={t('invoices.creditNote.formReasonTextAreaPlaceholder')} />
                    </Form.Item>
                </Form>
            </Modal>
        </React.Fragment>
    );
};

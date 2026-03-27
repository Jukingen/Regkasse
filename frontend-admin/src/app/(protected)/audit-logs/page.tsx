'use client';

import React, { useState, useCallback, useEffect, useMemo } from 'react';
import { Table, Card, Typography, Tag, Space, Button, Select, DatePicker, message, Alert, Empty, Flex, Tooltip } from 'antd';
import { ClearOutlined, ReloadOutlined } from '@ant-design/icons';
import type { Dayjs } from 'dayjs';
import Link from 'next/link';
import { useGetApiAuditLog } from '@/api/generated/audit-log/audit-log';
import type { AuditLogEntryDto } from '@/api/generated/model';
import { AXIOS_INSTANCE } from '@/lib/axios';
import dayjs from 'dayjs';
import { viewAuditLogStatusPresentation } from '@/shared/verificationsAuditView';
import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { AdminPageShell, AdminPageScopeSummary } from '@/components/admin-layout/AdminPageShell';
import { ADMIN_NAV_LABEL_KEYS, adminOverviewCrumb } from '@/shared/adminShellLabels';
import { OPERATOR_SHARED_COPY } from '@/shared/operatorTruthCopy';
import { useI18n } from '@/i18n';
import { formatNumber } from '@/i18n/formatting';

const { RangePicker } = DatePicker;

/** de-DE operator copy for this page (aligned with invoice / FO list contract). */
const AUDIT_PAGE_COPY = {
    filterCardTitle: 'Filter',
    activeFiltersLabel: 'Aktive Filter:',
    clearAllFilters: 'Alle Filter zurücksetzen',
    forensicsHint:
        'Für RKSV-/Offline-Stichproben (Correlation, Signatur-Stichworte) nutzen Sie die Audit-Spur — dieses Protokoll ist die allgemeine GET-/api/AuditLog-Liste.',
    forensicsLinkVerifications: 'RKSV Audit-Spur öffnen',
    dateRangeIncompleteTitle: 'Zeitraum unvollständig',
    dateRangeIncompleteDescription:
        'Es ist nur ein Datum gewählt. Für einen klaren Zeitraum bitte Start- und Enddatum setzen oder den Bereich leeren (API erhält sonst nur ein Datum).',
    emptyFiltered:
        'Keine Einträge für diese Abfrage. Zeitraum erweitern, Aktion löschen oder Filter zurücksetzen — die Tabelle zeigt nur die aktuelle API-Seite.',
    emptyNoRows: 'Keine Daten in dieser API-Antwort.',
    errorTitle: 'Audit-Protokoll konnte nicht geladen werden',
    errorFallbackDetail: 'Keine technische Detailmeldung verfügbar.',
    paginationZero: '0 Treffer',
    scopeApiPageNote: 'Anzeige = nur diese API-Seite (nicht das gesamte Protokoll).',
} as const;

function getAuditListErrorMessage(error: unknown): string {
    if (error instanceof Error) return error.message;
    return AUDIT_PAGE_COPY.errorFallbackDetail;
}

const ACTION_OPTIONS = [
    { value: 'Login', label: 'Login' },
    { value: 'CreateInvoice', label: 'Rechnung erstellen' },
    { value: 'Payment', label: 'Zahlung' },
];

export default function AuditLogsPage() {
    const { t, formatLocale } = useI18n();
    const [page, setPage] = useState(1);
    const [pageSize, setPageSize] = useState(10);
    const [dateRange, setDateRange] = useState<[Dayjs | null, Dayjs | null] | null>(null);
    const [actionFilter, setActionFilter] = useState<string | undefined>(undefined);

    const queryParams = {
        page,
        pageSize,
        startDate: dateRange?.[0] ? dateRange[0].toISOString() : undefined,
        endDate: dateRange?.[1] ? dateRange[1].toISOString() : undefined,
        action: actionFilter,
    };

    const { data, isLoading, isFetching, isError, error, refetch } = useGetApiAuditLog(queryParams);

    useEffect(() => {
        setPage(1);
    }, [dateRange, actionFilter]);

    const resetFilters = useCallback(() => {
        setDateRange(null);
        setActionFilter(undefined);
        setPage(1);
    }, []);

    const dateRangeIncomplete = Boolean(
        dateRange && ((dateRange[0] && !dateRange[1]) || (!dateRange[0] && dateRange[1])),
    );

    const hasActiveFilters = Boolean(actionFilter || (dateRange?.[0] && dateRange[1]));

    const scopeSummary = useMemo(() => {
        const parts: string[] = [
            `Seite ${page}`,
            `${pageSize} Zeilen pro API-Anfrage`,
            data?.totalCount != null
                ? `${formatNumber(data.totalCount, formatLocale, { maximumFractionDigits: 0 })} Einträge gesamt laut API`
                : 'Gesamtanzahl wird geladen …',
        ];
        if (actionFilter) parts.push(`Aktion = ${actionFilter}`);
        if (dateRange?.[0] && dateRange[1]) {
            parts.push(
                `${dateRange[0].format('DD.MM.YYYY')}–${dateRange[1].format('DD.MM.YYYY')}`,
            );
        } else {
            parts.push('kein Datumsfilter');
        }
        parts.push(AUDIT_PAGE_COPY.scopeApiPageNote);
        return parts.join(' · ');
    }, [page, pageSize, data?.totalCount, actionFilter, dateRange, formatLocale]);

    const actionOptionLabel = useCallback((value: string) => {
        const opt = ACTION_OPTIONS.find((o) => o.value === value);
        return opt?.label ?? value;
    }, []);

    const handleExport = useCallback(async (format: 'json' | 'csv') => {
        try {
            const params: Record<string, string> = { format };
            if (dateRange?.[0]) params.startDate = dateRange[0].toISOString();
            if (dateRange?.[1]) params.endDate = dateRange[1].toISOString();
            if (actionFilter) params.action = actionFilter;

            const res = await AXIOS_INSTANCE.get<Blob>('/api/AuditLog/export', {
                params,
                responseType: 'blob',
            });

            const blob = res.data;
            const contentType = String(res.headers?.['content-type'] ?? '').toLowerCase();

            // When backend returns an error JSON, axios still gives us a Blob (responseType='blob').
            // We must detect that case and avoid downloading "error payloads" as valid exports.
            const maybeJsonText = await blob.text();

            if (format === 'json' && contentType.includes('application/json')) {
                try {
                    const parsed = JSON.parse(maybeJsonText);
                    if (Array.isArray(parsed)) {
                        const url = URL.createObjectURL(blob as Blob);
                        const a = document.createElement('a');
                        a.href = url;
                        a.download = `audit_logs_${dayjs().format('YYYYMMDD_HHmmss')}.${format}`;
                        a.click();
                        URL.revokeObjectURL(url);
                        message.success('Export gestartet');
                        return;
                    }

                    // Object-shaped JSON: treat as an error envelope.
                    const msg =
                        typeof parsed?.message === 'string'
                            ? parsed.message
                            : 'Export fehlgeschlagen';
                    message.error(msg);
                    return;
                } catch {
                    // Not valid JSON: fall through and attempt download.
                }
            }

            if (format === 'csv' && !contentType.includes('text/csv')) {
                try {
                    const parsed = JSON.parse(maybeJsonText);
                    const msg =
                        typeof parsed?.message === 'string'
                            ? parsed.message
                            : 'Export fehlgeschlagen';
                    message.error(msg);
                    return;
                } catch {
                    message.error('Export fehlgeschlagen');
                    return;
                }
            }

            // Expected success formats.
            if (format === 'csv' && !contentType.includes('text/csv')) {
                message.error('Export fehlgeschlagen');
                return;
            }

            const url = URL.createObjectURL(blob as Blob);
            const a = document.createElement('a');
            a.href = url;
            a.download = `audit_logs_${dayjs().format('YYYYMMDD_HHmmss')}.${format}`;
            a.click();
            URL.revokeObjectURL(url);
            message.success('Export gestartet');
        } catch (e) {
            message.error('Export fehlgeschlagen');
        }
    }, [dateRange, actionFilter]);

    const columns = [
        {
            title: 'Zeit',
            dataIndex: 'timestamp',
            key: 'timestamp',
            width: 168,
            render: (ts: string | undefined) => (
                <Typography.Text strong style={{ fontVariantNumeric: 'tabular-nums' }}>
                    {ts ? dayjs(ts).format('DD.MM.YYYY HH:mm:ss') : '—'}
                </Typography.Text>
            ),
        },
        {
            title: 'Correlation',
            key: 'correlationId',
            width: 112,
            ellipsis: true,
            render: (_: unknown, record: AuditLogEntryDto) => {
                const c = record.correlationId?.trim();
                if (!c) return <Typography.Text type="secondary">—</Typography.Text>;
                return (
                    <Typography.Text code copyable={{ text: c }} ellipsis style={{ fontSize: 11, maxWidth: 104 }}>
                        {c.length > 14 ? `${c.slice(0, 12)}…` : c}
                    </Typography.Text>
                );
            },
        },
        {
            title: 'Benutzer',
            key: 'userName',
            width: 140,
            ellipsis: true,
            render: (_: unknown, record: AuditLogEntryDto) => (
                <Typography.Text type="secondary" ellipsis={{ tooltip: true }}>
                    {record.actorDisplayName ?? record.createdBy ?? record.userId ?? '—'}
                </Typography.Text>
            ),
        },
        {
            title: 'Aktion',
            dataIndex: 'action',
            key: 'action',
            width: 200,
            ellipsis: true,
            render: (action: string | null | undefined) => <Tag color="blue">{action ?? '—'}</Tag>,
        },
        {
            title: 'Entität',
            key: 'entity',
            width: 200,
            render: (_: unknown, record: AuditLogEntryDto) => {
                const type = record.entityType?.trim() || '—';
                const id = record.entityId?.trim();
                if (!id) {
                    return <Typography.Text strong>{type}</Typography.Text>;
                }
                return (
                    <Space direction="vertical" size={0} style={{ maxWidth: 220 }}>
                        <Typography.Text strong ellipsis style={{ display: 'block' }}>
                            {type}
                        </Typography.Text>
                        <Typography.Text
                            type="secondary"
                            ellipsis={{ tooltip: true }}
                            copyable={{ text: id }}
                            style={{ display: 'block', fontSize: 12, fontFamily: 'monospace' }}
                        >
                            {id}
                        </Typography.Text>
                    </Space>
                );
            },
        },
        {
            title: 'Details',
            dataIndex: 'description',
            key: 'description',
            ellipsis: true,
            render: (text: string | null | undefined) => {
                const t = text?.trim();
                if (!t) return <Typography.Text type="secondary">—</Typography.Text>;
                return (
                    <Tooltip title={t}>
                        <Typography.Text ellipsis style={{ maxWidth: 320, display: 'block' }}>
                            {t}
                        </Typography.Text>
                    </Tooltip>
                );
            },
        },
        {
            title: 'Status',
            dataIndex: 'status',
            key: 'status',
            width: 120,
            align: 'center' as const,
            render: (_: unknown, record: AuditLogEntryDto) => {
                const p = viewAuditLogStatusPresentation(record.status);
                return <Tag color={p.antColor}>{p.label}</Tag>;
            },
        },
    ];

    const rows = data?.auditLogs ?? [];
    const emptyList = !isLoading && !isError && rows.length === 0;

    return (
        <AdminPageShell>
            <AdminPageHeader
                title={t(ADMIN_NAV_LABEL_KEYS.auditLogs)}
                breadcrumbs={[adminOverviewCrumb(t), { title: t(ADMIN_NAV_LABEL_KEYS.auditLogs) }]}
                actions={
                    <Space wrap>
                        <Tooltip title={t('common.operator.refetchHintToolbar')}>
                            <Button icon={<ReloadOutlined />} onClick={() => refetch()} loading={isFetching}>
                                {OPERATOR_SHARED_COPY.toolbarRefresh}
                            </Button>
                        </Tooltip>
                        <Button onClick={() => handleExport('json')} disabled={isLoading}>
                            Export JSON
                        </Button>
                        <Button onClick={() => handleExport('csv')} disabled={isLoading}>
                            Export CSV
                        </Button>
                    </Space>
                }
            >
                <Typography.Paragraph type="secondary" style={{ marginBottom: 8, maxWidth: 720 }}>
                    Nach Aktion und Zeitraum filtern; Export nutzt dieselben Filter.{' '}
                    <strong>Die Tabelle zeigt nur die aktuelle API-Seite</strong> — weiterblättern ändert die
                    serverseitige Seite, nicht einen clientseitigen Ausschnitt.
                </Typography.Paragraph>
                <Typography.Paragraph type="secondary" style={{ marginBottom: 0, fontSize: 12, maxWidth: 720 }}>
                    {AUDIT_PAGE_COPY.forensicsHint}{' '}
                    <Link href="/rksv/verifications">{AUDIT_PAGE_COPY.forensicsLinkVerifications}</Link>
                </Typography.Paragraph>
            </AdminPageHeader>

            <Card size="small" title={AUDIT_PAGE_COPY.filterCardTitle}>
                <Flex wrap="wrap" gap="small" align="center">
                    <Typography.Text type="secondary">Kriterien</Typography.Text>
                    <Select
                        placeholder="Aktion"
                        style={{ width: 168 }}
                        allowClear
                        value={actionFilter}
                        onChange={setActionFilter}
                        options={ACTION_OPTIONS}
                    />
                    <RangePicker
                        value={dateRange ?? undefined}
                        onChange={(dates) => setDateRange(dates as [Dayjs | null, Dayjs | null] | null)}
                        format="DD.MM.YYYY"
                    />
                    <Button icon={<ClearOutlined />} onClick={resetFilters}>
                        {AUDIT_PAGE_COPY.clearAllFilters}
                    </Button>
                </Flex>
            </Card>

            {dateRangeIncomplete ? (
                <Alert
                    type="warning"
                    showIcon
                    style={{ marginTop: 8 }}
                    message={AUDIT_PAGE_COPY.dateRangeIncompleteTitle}
                    description={AUDIT_PAGE_COPY.dateRangeIncompleteDescription}
                />
            ) : null}

            {hasActiveFilters ? (
                <div style={{ marginTop: 8 }}>
                    <Space wrap size={[8, 8]} align="center">
                        <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                            {AUDIT_PAGE_COPY.activeFiltersLabel}
                        </Typography.Text>
                        {actionFilter ? (
                            <Tag closable onClose={() => setActionFilter(undefined)}>
                                Aktion: {actionOptionLabel(actionFilter)}
                            </Tag>
                        ) : null}
                        {dateRange?.[0] && dateRange[1] ? (
                            <Tag
                                closable
                                onClose={() => setDateRange(null)}
                            >
                                Zeitraum: {dateRange[0].format('DD.MM.YYYY')} – {dateRange[1].format('DD.MM.YYYY')}
                            </Tag>
                        ) : null}
                        <Button type="link" size="small" onClick={resetFilters}>
                            {AUDIT_PAGE_COPY.clearAllFilters}
                        </Button>
                    </Space>
                </div>
            ) : null}

            <AdminPageScopeSummary label="Aktive Ansicht:">
                {scopeSummary}
                {isFetching && !isLoading && !isError ? (
                    <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                        {' '}
                        (Aktualisiert …)
                    </Typography.Text>
                ) : null}
            </AdminPageScopeSummary>

            {isError ? (
                <Alert
                    type="error"
                    message={AUDIT_PAGE_COPY.errorTitle}
                    description={getAuditListErrorMessage(error)}
                    showIcon
                    action={
                        <Space direction="vertical" size="small">
                            <Button size="small" onClick={() => refetch()}>
                                {OPERATOR_SHARED_COPY.retryAfterError}
                            </Button>
                            <Button size="small" type="link" onClick={resetFilters} style={{ padding: 0, height: 'auto' }}>
                                {AUDIT_PAGE_COPY.clearAllFilters}
                            </Button>
                        </Space>
                    }
                />
            ) : null}

            {!isError ? (
                <Table<AuditLogEntryDto>
                    columns={columns}
                    dataSource={rows}
                    loading={isLoading}
                    rowKey={(r) => r.id ?? `${r.timestamp ?? ''}-${r.action ?? ''}`}
                    size="middle"
                    scroll={{ x: 1240 }}
                    pagination={{
                        current: page,
                        pageSize,
                        total: data?.totalCount ?? 0,
                        showSizeChanger: true,
                        pageSizeOptions: ['10', '25', '50', '100'],
                        showTotal: (total, range) => {
                            if (total <= 0) return AUDIT_PAGE_COPY.paginationZero;
                            return `${range[0]}–${range[1]} von ${formatNumber(total, formatLocale, { maximumFractionDigits: 0 })} Einträgen`;
                        },
                        hideOnSinglePage: false,
                        onChange: (p, s) => {
                            setPage(p);
                            setPageSize(s ?? pageSize);
                        },
                    }}
                    locale={{
                        emptyText: emptyList ? (
                            <Empty
                                description={
                                    hasActiveFilters ? AUDIT_PAGE_COPY.emptyFiltered : AUDIT_PAGE_COPY.emptyNoRows
                                }
                                image={Empty.PRESENTED_IMAGE_SIMPLE}
                            />
                        ) : (
                            <Empty description={AUDIT_PAGE_COPY.emptyNoRows} image={Empty.PRESENTED_IMAGE_SIMPLE} />
                        ),
                    }}
                />
            ) : null}
        </AdminPageShell>
    );
}
